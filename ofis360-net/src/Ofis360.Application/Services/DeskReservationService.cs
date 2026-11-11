using Microsoft.EntityFrameworkCore;
using Ofis360.Application.Common;
using Ofis360.Application.Identity;
using Ofis360.Application.Persistence;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;
using Ofis360.Domain.Rules;

namespace Ofis360.Application.Services;

public sealed record AlternativeDesk(string Id, string Code, string FloorName, int FloorNumber, string ZoneName, bool Popular, string Reason);

public sealed record CreateDeskReservationCommand(string DeskId, string DayKey, string? Note = null, string? TargetUserId = null);

public sealed record ListDeskReservationsQuery(
    string? UserId = null, string? FromDayKey = null, string? ToDayKey = null, string? Status = null, string? FloorId = null,
    string? Search = null, int? Take = null, int? Skip = null, bool ActiveBuildingsOnly = false);

public sealed record UserReservations(
    IReadOnlyList<DeskReservation> UpcomingDesks, IReadOnlyList<DeskReservation> PastDesks,
    IReadOnlyList<RoomReservation> UpcomingRooms, IReadOnlyList<RoomReservation> PastRooms);

/// <summary>Masa rezervasyon servisi. Tum is kurallari burada ve Domain'de uygulanir.</summary>
public sealed class DeskReservationService(
    IOfis360Db db, AvailabilityService availability, RuleService rules, NotificationService notifications, AuditService audit, IClock clock)
{
    private static readonly string[] OccupyingDesk = DeskReservationStatus.Holding.ToArray();

    /// <summary>Rezervasyon yapilamadiginda sunulacak alternatifler: ayni kat/bolge oncelikli, populer olmayan tercih edilir.</summary>
    public async Task<List<AlternativeDesk>> SuggestAlternativeDesksAsync(
        DeskRuleUser user, string dayKey, string? preferredFloorId = null, string? preferredZoneId = null, int limit = 3, CancellationToken ct = default)
    {
        var settings = await rules.GetRuleSettingsForUserAsync(user.Id, null, ct);
        if (user.ActorIsSecretary) settings = RuleService.WithSecretaryExemptions(settings);
        var calendar = await rules.GetCalendarAsync(ct);
        var day = DayKeys.ToDateOnly(dayKey);

        var desks = await db.Desks.AsNoTracking()
            .Where(d => d.Active && d.Floor.Reservable && d.Floor.Active && d.Floor.Building.Active && d.Zone.Reservable)
            .Include(d => d.Floor).ThenInclude(f => f.Building)
            .Include(d => d.Zone)
            .OrderBy(d => d.Code)
            .ToListAsync(ct);
        var occupied = (await db.DeskReservations.AsNoTracking()
            .Where(r => r.Date == day && OccupyingDesk.Contains(r.Status))
            .Select(r => r.DeskId).ToListAsync(ct)).ToHashSet();
        var closures = await availability.GetClosureMapsAsync(dayKey, ct);
        var context = await availability.BuildUserDeskContextAsync(user.Id, dayKey, ct);

        var today = clock.TodayKey();
        var candidates = new List<(Desk Desk, double Score)>();
        foreach (var desk in desks)
        {
            if (occupied.Contains(desk.Id)) continue;
            if (closures.DeskClosures.ContainsKey(desk.Id) || closures.FloorClosures.ContainsKey(desk.FloorId)) continue;

            var evaluation = DeskRules.Evaluate(new DeskRuleContext(
                today, dayKey, user,
                new DeskRuleDesk(desk.Id, desk.Code, desk.Active, desk.Placed, desk.AccessibleOnly, desk.Popular,
                    desk.Floor.Name, desk.Floor.Reservable, desk.Zone.Name, desk.Zone.Reservable, desk.Floor.Building.Active, desk.Floor.Building.Name),
                ClosureInfo.None, null, context.SameDayReservation, context.WeeklyReservationCount, context.History, calendar), settings);
            if (!evaluation.Allowed) continue;

            double score = 1;
            if (preferredFloorId is not null && desk.FloorId == preferredFloorId) score += 3;
            if (preferredZoneId is not null && desk.ZoneId == preferredZoneId) score += 2;
            if (!desk.Popular) score += 1;
            if (desk.Zone.WindowSide) score += 0.5;
            candidates.Add((desk, score));
        }

        // JS sort kararli: esit skorda kod sirasi (ToListAsync sirasi) korunur.
        return candidates
            .OrderByDescending(c => c.Score)
            .Take(limit)
            .Select(c => new AlternativeDesk(c.Desk.Id, c.Desk.Code, c.Desk.Floor.Name, c.Desk.Floor.Number, c.Desk.Zone.Name, c.Desk.Popular,
                preferredZoneId is not null && c.Desk.ZoneId == preferredZoneId ? "Aynı bölgede uygun"
                : preferredFloorId is not null && c.Desk.FloorId == preferredFloorId ? "Aynı katta uygun"
                : "Uygun alternatif"))
            .ToList();
    }

    public async Task<DeskReservation> CreateAsync(CreateDeskReservationCommand input, SessionUser session, CancellationToken ct = default)
    {
        var targetUserId = input.TargetUserId ?? session.Id;
        if (targetUserId != session.Id && session.Role != Roles.Admin && !session.IsSecretary)
        {
            throw AppErrors.Forbidden("Başka bir kullanıcı adına rezervasyon oluşturamazsınız.");
        }

        var targetUser = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == targetUserId && u.Active, ct);
        // Izlenen sorgu: reservation.Desk atamasi izlenmeyen bir grafigi "Added" olarak isaretlemesin (sahte ORA-00001).
        var desk = await db.Desks
            .Include(d => d.Floor).ThenInclude(f => f.Building)
            .Include(d => d.Zone)
            .FirstOrDefaultAsync(d => d.Id == input.DeskId, ct);

        if (targetUser is null) throw AppErrors.NotFound("Kullanıcı bulunamadı.");
        if (desk is null) throw AppErrors.NotFound("Masa bulunamadı.");

        // Kurallar rezervasyonun sahibine gore uygulanir (admin baskasi adina isliyorsa da).
        var settings = await rules.GetRuleSettingsForUserAsync(targetUserId, desk.Floor.BuildingId, ct);
        // Sekreter muafiyeti: isi Sekreter yapiyorsa ya da kayit bir Sekreter adina aciliyorsa.
        var secretaryExempt = session.IsSecretary || targetUser.IsSecretary;
        if (secretaryExempt) settings = RuleService.WithSecretaryExemptions(settings);
        var calendar = await rules.GetCalendarAsync(ct);

        var day = DayKeys.ToDateOnly(input.DayKey);
        // Haftalik limit sayimi ile INSERT arasinda ayni kisinin baska istegi araya girmesin.
        var reservation = await db.InTransactionAsync(async token =>
        {
            await db.LockUserAsync(targetUserId, token);

            var occupant = await db.DeskReservations.AsNoTracking()
                .Where(r => r.DeskId == desk.Id && r.Date == day && OccupyingDesk.Contains(r.Status))
                .Select(r => new { r.UserId, r.User.FullName })
                .FirstOrDefaultAsync(token);
            var closures = await availability.GetClosureMapsAsync(input.DayKey, token);
            var context = await availability.BuildUserDeskContextAsync(targetUserId, input.DayKey, token);

            var closureNote = closures.DeskClosures.GetValueOrDefault(desk.Id) ?? closures.FloorClosures.GetValueOrDefault(desk.FloorId);
            var ruleUser = new DeskRuleUser(targetUser.Id, targetUser.FullName, targetUser.AccessibilityApproved, secretaryExempt);

            var evaluation = DeskRules.Evaluate(new DeskRuleContext(
                clock.TodayKey(), input.DayKey, ruleUser,
                new DeskRuleDesk(desk.Id, desk.Code, desk.Active, desk.Placed, desk.AccessibleOnly, desk.Popular,
                    desk.Floor.Name, desk.Floor.Reservable, desk.Zone.Name, desk.Zone.Reservable, desk.Floor.Building.Active, desk.Floor.Building.Name),
                new ClosureInfo(closureNote is not null, closureNote),
                occupant is null ? null : new OccupiedBy(occupant.UserId, occupant.FullName),
                context.SameDayReservation, context.WeeklyReservationCount, context.History, calendar), settings);

            if (!evaluation.Allowed)
            {
                var alternatives = await SuggestAlternativeDesksAsync(ruleUser, input.DayKey, desk.FloorId, desk.ZoneId, ct: token);
                throw new AppException(422, "RULE_VIOLATION",
                    evaluation.Violations.Count > 0 ? evaluation.Violations[0].Message : "Rezervasyon oluşturulamadı.",
                    evaluation.Violations, new { alternatives });
            }

            var row = new DeskReservation
            {
                DeskId = desk.Id,
                UserId = targetUserId,
                Date = day,
                Status = DeskReservationStatus.Booked,
                ActiveDeskKey = DeskReservation.BuildActiveDeskKey(desk.Id, input.DayKey),
                ActiveUserDayKey = DeskReservation.BuildActiveUserDayKey(targetUserId, input.DayKey),
                Note = input.Note,
                Source = targetUserId == session.Id ? ReservationSources.Employee : ReservationSources.Admin,
                CreatedAt = clock.UtcNow,
                UpdatedAt = clock.UtcNow,
            };
            db.DeskReservations.Add(row);
            try
            {
                await db.SaveChangesAsync(token);
            }
            catch (DbUpdateException ex) when (db.IsUniqueViolation(ex))
            {
                // K4: yalnizca veritabani benzersizlik korumasi (es zamanli istekler) CONFLICT'e cevrilir.
                db.DeskReservations.Entry(row).State = EntityState.Detached;
                throw AppErrors.Conflict("Bu masa az önce başka bir çalışan tarafından rezerve edildi. Lütfen başka bir masa seçin.");
            }
            return row;
        }, ct);
        reservation.Desk = desk;

        await notifications.NotifyAsync(new NotificationPayload(targetUserId, NotificationTypes.DeskReservationCreated,
            "Masa rezervasyonunuz oluşturuldu",
            $"{DayKeys.FormatLong(input.DayKey)} tarihi için {desk.Code} kodlu masa ({desk.Floor.Name} · {desk.Zone.Name}) sizin adınıza ayrıldı.",
            "/panel/rezervasyonlarim", NotificationSeverities.Success), ct);

        await audit.RecordAsync(AuditActor.FromSession(session), AuditActions.DeskReservationCreate, "DeskReservation", reservation.Id,
            $"{targetUser.FullName} için {desk.Code} masası {DayKeys.FormatLong(input.DayKey, withWeekday: false)} tarihinde rezerve edildi.",
            new { deskCode = desk.Code, date = input.DayKey, targetUserId }, ct);

        return reservation;
    }

    public async Task<DeskReservation> CancelAsync(string reservationId, string? reason, SessionUser session, CancellationToken ct = default)
    {
        // Izlenmeyen okuma: durum degisikligi asagida tek koşullu UPDATE ile yapilir.
        var reservation = await db.DeskReservations.AsNoTracking()
            .Include(r => r.Desk).ThenInclude(d => d.Floor)
            .Include(r => r.Desk).ThenInclude(d => d.Zone)
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == reservationId, ct)
            ?? throw AppErrors.NotFound("Rezervasyon bulunamadı.");

        var isOwner = reservation.UserId == session.Id;
        var canManage = session.Role == Roles.Admin || session.IsSecretary;
        if (!isOwner && !canManage) throw AppErrors.Forbidden("Bu rezervasyonu iptal etme yetkiniz yok.");

        if (reservation.Status == DeskReservationStatus.Cancelled)
        {
            throw AppErrors.Conflict("Bu rezervasyon zaten iptal edilmiş.");
        }

        var dayKey = reservation.DayKey;
        if (string.CompareOrdinal(dayKey, clock.TodayKey()) < 0)
        {
            throw AppErrors.Validation("Geçmiş tarihli rezervasyonlar iptal edilemez.");
        }
        if (reservation.Status != DeskReservationStatus.Booked && reservation.Status != DeskReservationStatus.CheckedIn)
        {
            throw AppErrors.Conflict("Sonuçlanmış rezervasyon iptal edilemez.");
        }

        // Tek atomik UPDATE: es zamanli check-in durumu ezemez, anahtarlar durumla birlikte degisir (QA-003).
        var now = clock.UtcNow;
        var changed = await db.DeskReservations
            .Where(r => r.Id == reservationId && (r.Status == DeskReservationStatus.Booked || r.Status == DeskReservationStatus.CheckedIn))
            .ExecuteUpdateAsync(u => u
                .SetProperty(r => r.Status, DeskReservationStatus.Cancelled)
                .SetProperty(r => r.ActiveDeskKey, (string?)null)
                .SetProperty(r => r.ActiveUserDayKey, (string?)null)
                .SetProperty(r => r.CancelledAt, now)
                .SetProperty(r => r.CancelledById, session.Id)
                .SetProperty(r => r.CancelReason, reason)
                .SetProperty(r => r.UpdatedAt, now), ct);
        if (changed == 0) throw AppErrors.Conflict("Bu rezervasyonun durumu değişti; iptal edilemedi.");

        reservation.Status = DeskReservationStatus.Cancelled;
        reservation.ActiveDeskKey = null;
        reservation.ActiveUserDayKey = null;
        reservation.CancelledAt = now;
        reservation.CancelledById = session.Id;
        reservation.CancelReason = reason;
        reservation.UpdatedAt = now;

        if (!isOwner)
        {
            await notifications.NotifyAsync(new NotificationPayload(reservation.UserId, NotificationTypes.DeskReservationCancelledByAdmin,
                "Rezervasyonunuz yönetim tarafından iptal edildi",
                $"{DayKeys.FormatLong(dayKey)} tarihli {reservation.Desk.Code} masa rezervasyonunuz iptal edildi.{(string.IsNullOrEmpty(reason) ? "" : $" Gerekçe: {reason}")}",
                "/panel/masa", NotificationSeverities.Warning), ct);

            var alternatives = await SuggestAlternativeDesksAsync(
                new DeskRuleUser(reservation.UserId, reservation.User.FullName, reservation.User.AccessibilityApproved),
                dayKey, reservation.Desk.FloorId, reservation.Desk.ZoneId, ct: ct);

            if (alternatives.Count > 0)
            {
                await notifications.NotifyAsync(new NotificationPayload(reservation.UserId, NotificationTypes.AlternativeDeskSuggestion,
                    "Alternatif masa önerisi",
                    $"{DayKeys.FormatLong(dayKey, withWeekday: false)} için {string.Join(", ", alternatives.Select(a => a.Code))} masaları uygun görünüyor.",
                    "/panel/masa", NotificationSeverities.Info), ct);
            }
        }
        else
        {
            await notifications.NotifyAsync(new NotificationPayload(reservation.UserId, NotificationTypes.DeskReservationCancelled,
                "Rezervasyonunuz iptal edildi",
                $"{DayKeys.FormatLong(dayKey)} tarihli {reservation.Desk.Code} masa rezervasyonunuz iptal edildi.",
                "/panel/masa", NotificationSeverities.Info), ct);
        }

        await audit.RecordAsync(AuditActor.FromSession(session), AuditActions.DeskReservationCancel, "DeskReservation", reservation.Id,
            $"{reservation.User.FullName} adlı çalışanın {reservation.Desk.Code} masa rezervasyonu ({DayKeys.FormatLong(dayKey, withWeekday: false)}) iptal edildi.",
            new { deskCode = reservation.Desk.Code, date = dayKey, byAdmin = !isOwner }, ct);

        return reservation;
    }

    /// <summary>Ofise gelis kaydi - "Bugün ofiste misin?" akisinda kullanilir.</summary>
    public async Task<DeskReservation> CheckInAsync(string reservationId, SessionUser session, CancellationToken ct = default)
    {
        var reservation = await db.DeskReservations.AsNoTracking().FirstOrDefaultAsync(r => r.Id == reservationId, ct)
            ?? throw AppErrors.NotFound("Rezervasyon bulunamadı.");
        if (reservation.UserId != session.Id) throw AppErrors.Forbidden();

        if (reservation.DayKey != clock.TodayKey())
        {
            throw AppErrors.Validation("Giriş kaydı yalnızca rezervasyon günü yapılabilir.");
        }
        if (reservation.Status == DeskReservationStatus.Cancelled)
        {
            throw AppErrors.Conflict("İptal edilmiş rezervasyon için giriş kaydı yapılamaz.");
        }
        if (reservation.Status == DeskReservationStatus.CheckedIn) return reservation;

        // Yalnizca hala BOOKED ise gecer; arada iptal edildiyse 0 satir etkilenir (QA-003).
        var now = clock.UtcNow;
        var changed = await db.DeskReservations
            .Where(r => r.Id == reservationId && r.Status == DeskReservationStatus.Booked)
            .ExecuteUpdateAsync(u => u
                .SetProperty(r => r.Status, DeskReservationStatus.CheckedIn)
                .SetProperty(r => r.CheckedInAt, now)
                .SetProperty(r => r.UpdatedAt, now), ct);
        if (changed == 0)
        {
            throw AppErrors.Conflict("Bu rezervasyon için giriş kaydı yapılamaz; durumu değişmiş olabilir.");
        }

        reservation.Status = DeskReservationStatus.CheckedIn;
        reservation.CheckedInAt = now;
        reservation.UpdatedAt = now;
        return reservation;
    }

    public async Task<PagedResult<DeskReservation>> ListAsync(ListDeskReservationsQuery query, CancellationToken ct = default)
    {
        var q = db.DeskReservations.AsNoTracking().AsQueryable();
        if (query.ActiveBuildingsOnly) q = q.Where(r => r.Desk.Floor.Building.Active);
        if (query.UserId is not null) q = q.Where(r => r.UserId == query.UserId);
        if (query.Status is not null) q = q.Where(r => r.Status == query.Status);
        if (query.FloorId is not null) q = q.Where(r => r.Desk.FloorId == query.FloorId);
        if (!string.IsNullOrEmpty(query.Search))
        {
            var pattern = Search.Pattern(query.Search);
            q = q.Where(r => EF.Functions.Like(r.User.FullName.ToUpper().Replace("İ", "I"), pattern, Search.Escape) || EF.Functions.Like(r.Desk.Code.ToUpper().Replace("İ", "I"), pattern, Search.Escape));
        }
        if (query.FromDayKey is not null) { var from = DayKeys.ToDateOnly(query.FromDayKey); q = q.Where(r => r.Date >= from); }
        if (query.ToDayKey is not null) { var to = DayKeys.ToDateOnly(query.ToDayKey); q = q.Where(r => r.Date <= to); }

        var take = Paging.Take(query.Take, 50);
        var skip = Paging.Skip(query.Skip);
        var total = await q.CountAsync(ct);
        var items = await q
            .Include(r => r.Desk).ThenInclude(d => d.Floor)
            .Include(r => r.Desk).ThenInclude(d => d.Zone)
            .Include(r => r.User).ThenInclude(u => u.Department)
            .OrderByDescending(r => r.Date).ThenByDescending(r => r.CreatedAt)
            .Skip(skip).Take(take)
            .ToListAsync(ct);
        return new PagedResult<DeskReservation>(items, total, take, skip);
    }

    /// <summary>Calisanin yaklasan ve gecmis rezervasyonlari.</summary>
    public async Task<UserReservations> GetUserReservationsAsync(string userId, CancellationToken ct = default)
    {
        var today = clock.Today;
        var upcoming = await db.DeskReservations.AsNoTracking()
            .Where(r => r.UserId == userId && r.Date >= today && (r.Status == DeskReservationStatus.Booked || r.Status == DeskReservationStatus.CheckedIn) && r.Desk.Floor.Building.Active)
            .Include(r => r.Desk).ThenInclude(d => d.Floor).Include(r => r.Desk).ThenInclude(d => d.Zone)
            .OrderBy(r => r.Date).ToListAsync(ct);
        var past = await db.DeskReservations.AsNoTracking()
            .Where(r => r.UserId == userId && (r.Date < today || (r.Status != DeskReservationStatus.Booked && r.Status != DeskReservationStatus.CheckedIn)) && r.Desk.Floor.Building.Active)
            .Include(r => r.Desk).ThenInclude(d => d.Floor).Include(r => r.Desk).ThenInclude(d => d.Zone)
            .OrderByDescending(r => r.Date).Take(30).ToListAsync(ct);
        var rooms = await db.RoomReservations.AsNoTracking()
            .Where(r => r.UserId == userId && r.Date >= today && r.Status == RoomReservationStatus.Booked && r.Room.Floor.Building.Active)
            .Include(r => r.Room).ThenInclude(m => m.Floor)
            .OrderBy(r => r.Date).ThenBy(r => r.StartMinutes).ToListAsync(ct);
        var pastRooms = await db.RoomReservations.AsNoTracking()
            .Where(r => r.UserId == userId && (r.Date < today || r.Status != RoomReservationStatus.Booked) && r.Room.Floor.Building.Active)
            .Include(r => r.Room).ThenInclude(m => m.Floor)
            .OrderByDescending(r => r.Date).ThenByDescending(r => r.StartMinutes).Take(30).ToListAsync(ct);
        return new UserReservations(upcoming, past, rooms, pastRooms);
    }

    /// <summary>Kural ihlallerini kullaniciya gosterilecek sade metne cevirir.</summary>
    public static string DescribeViolations(IEnumerable<RuleViolation> violations) => string.Join(" ", violations.Select(v => v.Message));
}
