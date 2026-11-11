using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Ofis360.Application.Common;
using Ofis360.Application.Identity;
using Ofis360.Application.Persistence;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;
using Ofis360.Domain.Rules;

namespace Ofis360.Application.Services;

/// <summary>Baskasinin toplantisinda yalnizca saat paylasilir; baslik, duzenleyen ve kisi sayisi gonderilmez.</summary>
public sealed record RoomBusySlot(string Id, string Title, int StartMinutes, int EndMinutes, string? OwnerName, [property: JsonIgnore] string OwnerId, int AttendeeCount)
{
    public const string HiddenTitle = "Dolu";

    public RoomBusySlot Hidden() => new($"dolu-{StartMinutes}", HiddenTitle, StartMinutes, EndMinutes, null, string.Empty, 0);
}

public sealed record RoomAvailabilityView(
    string Id, string Code, string Name, int Capacity, string FloorId, string FloorName, int FloorNumber, bool Active, bool Closed,
    string? ClosureNote, string? Notes, IReadOnlyList<FeatureView> Features, IReadOnlyList<RoomBusySlot> BusySlots,
    IReadOnlyList<TimeRange> FreeSlots, bool? AvailableForRequest, double UsageRate, string? AccessReason = null, int MinAttendees = 1);

public sealed record RoomAvailabilityResult(IReadOnlyList<RoomAvailabilityView> Rooms, int WorkStartMinutes, int WorkEndMinutes, int SlotMinutes = 15);

public sealed record RoomAvailabilityQuery(
    string DayKey, int? StartMinutes = null, int? EndMinutes = null, int? MinCapacity = null, string? FloorId = null, IReadOnlyList<string>? FeatureKeys = null,
    string? UserId = null, bool ActorIsSecretary = false, bool ViewerIsManaging = false);

public sealed record CreateRoomReservationCommand(
    string RoomId, string DayKey, int StartMinutes, int EndMinutes, string Title, string Purpose, int AttendeeCount, string? TargetUserId = null);

public sealed record RoomAlternative(string Id, string Code, string Name, int Capacity, string FloorName);

public sealed record RoomSuggestion(IReadOnlyList<TimeRange> AlternativeSlots, IReadOnlyList<RoomAlternative> AlternativeRooms);

public sealed record ListRoomReservationsQuery(
    string? DayKey = null, string? FromDayKey = null, string? ToDayKey = null, string? RoomId = null, string? UserId = null,
    string? Status = null, string? Search = null, int? Take = null, int? Skip = null, bool ActiveBuildingsOnly = false);

/// <summary>Toplanti odasi rezervasyon servisi (kaynak: room-reservation.service.ts).</summary>
public sealed class RoomReservationService(
    IOfis360Db db, AvailabilityService availability, RuleService rules, NotificationService notifications, AuditService audit, IClock clock)
{
    private static readonly string[] OccupyingRoom = RoomReservationStatus.Occupying.ToArray();

    public async Task<RoomAvailabilityResult> GetAvailabilityAsync(RoomAvailabilityQuery query, CancellationToken ct = default)
    {
        var buildingId = query.FloorId is null
            ? null
            : await db.Floors.AsNoTracking().Where(f => f.Id == query.FloorId).Select(f => f.BuildingId).FirstOrDefaultAsync(ct);
        var settings = query.UserId is null
            ? await rules.GetRuleSettingsAsync(ct)
            : await rules.GetRuleSettingsForUserAsync(query.UserId, buildingId, ct);
        var workStart = settings.RoomWorkingHours.StartMinutes;
        var workEnd = settings.RoomWorkingHours.EndMinutes;
        var slotMinutes = settings.RoomSlotMinutes.Enabled ? settings.RoomSlotMinutes.Minutes : 15;
        var calendar = await rules.GetCalendarAsync(ct);
        var today = clock.TodayKey();
        RoomRequester? requester = null;
        if (query.UserId is not null)
        {
            var grade = await db.Users.AsNoTracking().Where(u => u.Id == query.UserId).Select(u => u.Grade).FirstOrDefaultAsync(ct);
            requester = new RoomRequester(query.UserId, grade, query.ActorIsSecretary);
        }
        var day = DayKeys.ToDateOnly(query.DayKey);

        // Hem pasif hem krokiden kaldirilmis oda (gecmisi icin saklanir) calisan listesinde gorunmez.
        var roomsQuery = db.MeetingRooms.AsNoTracking().Where(r => r.Floor.Active && r.Floor.Building.Active && (r.Placed || r.Active));
        if (query.FloorId is not null) roomsQuery = roomsQuery.Where(r => r.FloorId == query.FloorId);
        if (query.MinCapacity is int minCapacity && minCapacity > 0) roomsQuery = roomsQuery.Where(r => r.Capacity >= minCapacity);
        var rooms = await roomsQuery
            .Include(r => r.Floor).ThenInclude(f => f.Building)
            .Include(r => r.Features).ThenInclude(l => l.Feature)
            .Include(r => r.AccessRules)
            .Include(r => r.PriorityUsers)
            .OrderBy(r => r.Floor.Number).ThenBy(r => r.Code)
            .ToListAsync(ct);

        var reservations = await db.RoomReservations.AsNoTracking()
            .Where(r => r.Date == day && OccupyingRoom.Contains(r.Status))
            .OrderBy(r => r.StartMinutes)
            .Select(r => new { r.Id, r.RoomId, r.Title, r.StartMinutes, r.EndMinutes, r.AttendeeCount, OwnerId = r.User.Id, OwnerName = r.User.FullName })
            .ToListAsync(ct);
        var closures = await availability.GetClosureMapsAsync(query.DayKey, ct);

        var byRoom = reservations
            .GroupBy(r => r.RoomId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<RoomBusySlot>)g
                .Select(r => new RoomBusySlot(r.Id, r.Title, r.StartMinutes, r.EndMinutes, r.OwnerName, r.OwnerId, r.AttendeeCount))
                .Select(slot => query.ViewerIsManaging || slot.OwnerId == query.UserId ? slot : slot.Hidden()).ToList());

        var requestedFeatures = query.FeatureKeys ?? [];
        var notBefore = clock.CurrentMinutesForDay(query.DayKey);

        var views = rooms
            .Where(room => requestedFeatures.All(key => room.Features.Any(l => l.Feature.Key == key)))
            .Select(room =>
            {
                var busy = byRoom.GetValueOrDefault(room.Id) ?? [];
                var closureNote = closures.RoomClosures.GetValueOrDefault(room.Id) ?? closures.FloorClosures.GetValueOrDefault(room.FloorId);
                var closed = closureNote is not null || !room.Active || !room.Floor.Reservable || !room.Floor.Building.Active;
                var policy = RoomPolicyService.ToPolicy(room);
                IReadOnlyList<RuleViolation> access = requester is null ? [] : RoomAccess.Evaluate(policy, requester, query.DayKey, today, calendar, null);
                var minAttendees = requester is null ? 1 : RoomAccess.MinAttendees(policy, requester);

                var freeSlots = closed
                    ? []
                    : RoomRules.ComputeFreeSlots(workStart, workEnd, busy.Select(b => new TimeRange(b.StartMinutes, b.EndMinutes)).ToList(), Math.Max(30, slotMinutes),
                        notBefore >= 0 ? notBefore : null, slotMinutes);

                var busyMinutes = busy.Sum(b => b.EndMinutes - b.StartMinutes);

                bool? availableForRequest = null;
                if (query.StartMinutes is int start && query.EndMinutes is int end)
                {
                    availableForRequest = !closed && access.Count == 0
                        && (query.MinCapacity is not int requested || requested <= 0 || requested >= minAttendees)
                        && start < end && start >= workStart && end <= workEnd && (notBefore < 0 || start >= notBefore)
                        && !busy.Any(slot => RoomRules.Overlaps(start, end, slot.StartMinutes, slot.EndMinutes));
                }

                return new RoomAvailabilityView(room.Id, room.Code, room.Name, room.Capacity, room.FloorId, room.Floor.Name, room.Floor.Number,
                    room.Active, closed, closureNote, room.Notes,
                    room.Features.Select(l => AvailabilityService.ToFeatureView(l.Feature)).ToList(),
                    busy, freeSlots, availableForRequest,
                    workEnd > workStart ? (double)busyMinutes / (workEnd - workStart) * 100 : 0,
                    access.FirstOrDefault()?.Message, minAttendees);
            })
            .ToList();

        return new RoomAvailabilityResult(views, workStart, workEnd, slotMinutes);
    }

    public async Task<RoomReservation> CreateAsync(CreateRoomReservationCommand input, SessionUser session, CancellationToken ct = default)
    {
        var targetUserId = input.TargetUserId ?? session.Id;
        if (targetUserId != session.Id && session.Role != Roles.Admin && !session.IsSecretary)
        {
            throw AppErrors.Forbidden("Başka bir kullanıcı adına rezervasyon oluşturamazsınız.");
        }

        var target = await db.Users.AsNoTracking().Where(u => u.Id == targetUserId && u.Active).Select(u => new { u.Grade, u.IsSecretary }).FirstOrDefaultAsync(ct)
            ?? throw AppErrors.NotFound("Kullanıcı bulunamadı.");

        var room = await db.MeetingRooms.AsNoTracking()
            .Include(r => r.Floor).ThenInclude(f => f.Building)
            .Include(r => r.AccessRules)
            .Include(r => r.PriorityUsers)
            .FirstOrDefaultAsync(r => r.Id == input.RoomId, ct)
            ?? throw AppErrors.NotFound("Toplantı odası bulunamadı.");
        // Kurallar rezervasyonun sahibine gore uygulanir (admin baskasi adina isliyorsa da).
        var settings = await rules.GetRuleSettingsForUserAsync(targetUserId, room.Floor.BuildingId, ct);
        var secretaryExempt = session.IsSecretary || target.IsSecretary;
        if (secretaryExempt) settings = RuleService.WithSecretaryExemptions(settings);
        var calendar = await rules.GetCalendarAsync(ct);

        var day = DayKeys.ToDateOnly(input.DayKey);
        var conflicts = await db.RoomReservations.AsNoTracking()
            .Where(r => r.RoomId == room.Id && r.Date == day && OccupyingRoom.Contains(r.Status))
            .Select(r => new RoomConflict(r.Id, r.Title, r.StartMinutes, r.EndMinutes))
            .ToListAsync(ct);
        var closures = await availability.GetClosureMapsAsync(input.DayKey, ct);
        var closureNote = closures.RoomClosures.GetValueOrDefault(room.Id) ?? closures.FloorClosures.GetValueOrDefault(room.FloorId);

        var evaluation = RoomRules.Evaluate(new RoomRuleContext(
            clock.TodayKey(), input.DayKey, clock.CurrentMinutesForDay(input.DayKey),
            new RoomRuleRoom(room.Id, room.Code, room.Name, room.Capacity, room.Active, room.Floor.Name, room.Floor.Reservable,
                room.Floor.Building.Active, room.Floor.Building.Name),
            new ClosureInfo(closureNote is not null, closureNote),
            input.StartMinutes, input.EndMinutes, input.AttendeeCount, conflicts, calendar,
            RoomPolicyService.ToPolicy(room), new RoomRequester(targetUserId, target.Grade, secretaryExempt)), settings);

        if (!evaluation.Allowed)
        {
            var suggestions = await SuggestAlternativesAsync(input.DayKey, room.Id, input.StartMinutes, input.EndMinutes, input.AttendeeCount, ct, targetUserId, secretaryExempt);
            throw new AppException(422, "RULE_VIOLATION",
                evaluation.Violations.Count > 0 ? evaluation.Violations[0].Message : "Rezervasyon oluşturulamadı.",
                evaluation.Violations, suggestions);
        }

        var now = clock.UtcNow;
        var reservation = new RoomReservation
        {
            RoomId = room.Id,
            UserId = targetUserId,
            Date = day,
            StartAt = new DateTimeOffset(DayKeys.WithMinutes(input.DayKey, input.StartMinutes), TimeSpan.Zero),
            EndAt = new DateTimeOffset(DayKeys.WithMinutes(input.DayKey, input.EndMinutes), TimeSpan.Zero),
            StartMinutes = input.StartMinutes,
            EndMinutes = input.EndMinutes,
            Title = input.Title,
            Purpose = input.Purpose,
            AttendeeCount = input.AttendeeCount,
            Status = RoomReservationStatus.Booked,
            Source = targetUserId == session.Id ? ReservationSources.Employee : ReservationSources.Admin,
            CreatedAt = now,
            UpdatedAt = now,
        };

        // K5: PL/SQL BOOK_ROOM oda satirini kilitler; es zamanli cakisma DB duzeyinde engellenir.
        var booked = await db.InTransactionAsync(token => db.BookRoomAsync(reservation, token), ct);
        if (!booked)
        {
            throw AppErrors.Conflict("Bu saat aralığı az önce başka bir çalışan tarafından rezerve edildi. Lütfen başka bir aralık seçin.");
        }
        reservation.Room = room;

        await notifications.NotifyAsync(new NotificationPayload(targetUserId, NotificationTypes.RoomReservationCreated,
            "Toplantı odası rezervasyonunuz oluşturuldu",
            $"{room.Name} ({room.Code}) · {DayKeys.FormatLong(input.DayKey)} {DayKeys.MinutesToTime(input.StartMinutes)} - {DayKeys.MinutesToTime(input.EndMinutes)} aralığı için rezervasyonunuz onaylandı.",
            "/panel/rezervasyonlarim", NotificationSeverities.Success), ct);

        await audit.RecordAsync(AuditActor.FromSession(session), AuditActions.RoomReservationCreate, "RoomReservation", reservation.Id,
            $"{room.Code} toplantı odası {DayKeys.FormatLong(input.DayKey, withWeekday: false)} {DayKeys.MinutesToTime(input.StartMinutes)} - {DayKeys.MinutesToTime(input.EndMinutes)} için rezerve edildi.",
            new { roomCode = room.Code, date = input.DayKey, targetUserId }, ct);

        return reservation;
    }

    /// <summary>Uygun olmayan bir talep icin alternatif saat ve oda onerileri uretir.</summary>
    public async Task<RoomSuggestion> SuggestAlternativesAsync(string dayKey, string roomId, int startMinutes, int endMinutes, int attendeeCount, CancellationToken ct = default, string? userId = null, bool actorIsSecretary = false)
    {
        var duration = Math.Max(30, endMinutes - startMinutes);
        var availabilityResult = await GetAvailabilityAsync(new RoomAvailabilityQuery(dayKey, startMinutes, endMinutes, attendeeCount, UserId: userId, ActorIsSecretary: actorIsSecretary), ct);

        var sameRoom = availabilityResult.Rooms.FirstOrDefault(r => r.Id == roomId);
        var alternativeSlots = (sameRoom?.FreeSlots ?? [])
            .Where(slot => slot.EndMinutes - slot.StartMinutes >= duration)
            .Take(4)
            .Select(slot => new TimeRange(slot.StartMinutes, Math.Min(slot.StartMinutes + duration, slot.EndMinutes)))
            .ToList();

        var alternativeRooms = availabilityResult.Rooms
            .Where(r => r.Id != roomId && r.AvailableForRequest == true)
            .Take(4)
            .Select(r => new RoomAlternative(r.Id, r.Code, r.Name, r.Capacity, r.FloorName))
            .ToList();

        return new RoomSuggestion(alternativeSlots, alternativeRooms);
    }

    /// <summary>Toplanti basladi: baslangictan 15 dakika once ile bitis arasinda sahibi, Sekreter veya Admin isaretler.</summary>
    public async Task<RoomReservation> StartAsync(string reservationId, SessionUser session, CancellationToken ct = default)
    {
        const int EarlyMinutes = 15;

        var reservation = await db.RoomReservations.AsNoTracking().FirstOrDefaultAsync(r => r.Id == reservationId, ct)
            ?? throw AppErrors.NotFound("Rezervasyon bulunamadı.");
        if (reservation.UserId != session.Id && session.Role != Roles.Admin && !session.IsSecretary) throw AppErrors.Forbidden();
        if (reservation.Status != RoomReservationStatus.Booked) throw AppErrors.Conflict("Bu toplantı başlatılamaz; durumu değişmiş olabilir.");
        if (reservation.StartedAt is not null) return reservation;

        var nowMinutes = clock.CurrentMinutesOfDay;
        if (reservation.DayKey != clock.TodayKey() || nowMinutes < reservation.StartMinutes - EarlyMinutes || nowMinutes >= reservation.EndMinutes)
        {
            throw AppErrors.Validation($"Toplantı en erken {EarlyMinutes} dakika kala ve bitene kadar başlatılabilir.");
        }

        var now = clock.UtcNow;
        await db.RoomReservations
            .Where(r => r.Id == reservationId && r.Status == RoomReservationStatus.Booked && r.StartedAt == null)
            .ExecuteUpdateAsync(u => u.SetProperty(r => r.StartedAt, now).SetProperty(r => r.UpdatedAt, now), ct);
        reservation.StartedAt = now;
        return reservation;
    }

    public async Task<RoomReservation> CancelAsync(string reservationId, string? reason, SessionUser session, CancellationToken ct = default)
    {
        var reservation = await db.RoomReservations
            .Include(r => r.Room).Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == reservationId, ct)
            ?? throw AppErrors.NotFound("Rezervasyon bulunamadı.");

        var isOwner = reservation.UserId == session.Id;
        if (!isOwner && session.Role != Roles.Admin && !session.IsSecretary)
        {
            throw AppErrors.Forbidden("Bu rezervasyonu iptal etme yetkiniz yok.");
        }
        if (reservation.Status == RoomReservationStatus.Cancelled)
        {
            throw AppErrors.Conflict("Bu rezervasyon zaten iptal edilmiş.");
        }

        var dayKey = reservation.DayKey;
        if (string.CompareOrdinal(dayKey, clock.TodayKey()) < 0)
        {
            throw AppErrors.Validation("Geçmiş tarihli rezervasyonlar iptal edilemez.");
        }
        if (reservation.Status != RoomReservationStatus.Booked)
        {
            throw AppErrors.Conflict("Sonuçlanmış rezervasyon iptal edilemez.");
        }
        if (dayKey == clock.TodayKey() && reservation.EndMinutes <= clock.CurrentMinutesOfDay)
        {
            throw AppErrors.Validation("Bitmiş toplantı iptal edilemez.");
        }

        reservation.Status = RoomReservationStatus.Cancelled;
        reservation.CancelledAt = clock.UtcNow;
        reservation.CancelledById = session.Id;
        reservation.CancelReason = reason;
        await db.SaveChangesAsync(ct);

        await notifications.NotifyAsync(new NotificationPayload(reservation.UserId,
            isOwner ? NotificationTypes.RoomReservationCancelled : NotificationTypes.RoomReservationCancelledByAdmin,
            isOwner ? "Toplantı rezervasyonunuz iptal edildi" : "Toplantı rezervasyonunuz yönetim tarafından iptal edildi",
            $"{reservation.Room.Name} · {DayKeys.FormatLong(dayKey)} {DayKeys.MinutesToTime(reservation.StartMinutes)} - {DayKeys.MinutesToTime(reservation.EndMinutes)} rezervasyonu iptal edildi.{(string.IsNullOrEmpty(reason) ? "" : $" Gerekçe: {reason}")}",
            "/panel/toplanti", isOwner ? NotificationSeverities.Info : NotificationSeverities.Warning), ct);

        await audit.RecordAsync(AuditActor.FromSession(session), AuditActions.RoomReservationCancel, "RoomReservation", reservation.Id,
            $"{reservation.Room.Code} toplantı odası rezervasyonu ({DayKeys.FormatLong(dayKey, withWeekday: false)}) iptal edildi.",
            new { roomCode = reservation.Room.Code, date = dayKey, byAdmin = !isOwner }, ct);

        return reservation;
    }

    public async Task<PagedResult<RoomReservation>> ListAsync(ListRoomReservationsQuery query, CancellationToken ct = default)
    {
        var q = db.RoomReservations.AsNoTracking().AsQueryable();
        if (query.ActiveBuildingsOnly) q = q.Where(r => r.Room.Floor.Building.Active);
        if (query.RoomId is not null) q = q.Where(r => r.RoomId == query.RoomId);
        if (query.UserId is not null) q = q.Where(r => r.UserId == query.UserId);
        if (query.Status is not null) q = q.Where(r => r.Status == query.Status);
        if (!string.IsNullOrEmpty(query.Search))
        {
            var pattern = Search.Pattern(query.Search);
            q = q.Where(r => EF.Functions.Like(r.Title.ToUpper().Replace("İ", "I"), pattern, Search.Escape)
                             || EF.Functions.Like(r.User.FullName.ToUpper().Replace("İ", "I"), pattern, Search.Escape)
                             || EF.Functions.Like(r.Room.Code.ToUpper().Replace("İ", "I"), pattern, Search.Escape));
        }
        if (query.DayKey is not null)
        {
            var day = DayKeys.ToDateOnly(query.DayKey);
            q = q.Where(r => r.Date == day);
        }
        else
        {
            if (query.FromDayKey is not null) { var from = DayKeys.ToDateOnly(query.FromDayKey); q = q.Where(r => r.Date >= from); }
            if (query.ToDayKey is not null) { var to = DayKeys.ToDateOnly(query.ToDayKey); q = q.Where(r => r.Date <= to); }
        }

        var take = Paging.Take(query.Take, 50);
        var skip = Paging.Skip(query.Skip);
        var total = await q.CountAsync(ct);
        var items = await q
            .Include(r => r.Room).ThenInclude(m => m.Floor)
            .Include(r => r.User).ThenInclude(u => u.Department)
            .OrderByDescending(r => r.Date).ThenBy(r => r.StartMinutes)
            .Skip(skip).Take(take)
            .ToListAsync(ct);
        return new PagedResult<RoomReservation>(items, total, take, skip);
    }
}
