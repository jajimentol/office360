using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Ofis360.Application.Common;
using Ofis360.Application.Persistence;
using Ofis360.Application.Validation;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;

namespace Ofis360.Application.Services;

public sealed record CreateClosureResult(Closure Closure, int CancelledReservations);

/// <summary>
/// Bakim / etkinlik / ozel kullanim nedeniyle masa, oda veya kat kapatma yonetimi. Kapatma araligina denk gelen
/// rezervasyonlar otomatik iptal edilir ve calisanlar bilgilendirilir. K7: iptaller tek transaction'da.
/// </summary>
public sealed class ClosureService(IOfis360Db db, NotificationService notifications, AuditService audit, IClock clock)
{
    private readonly ResourceReservationCanceller canceller = new(db, notifications, clock);

    public Task<List<Closure>> ListAsync(bool? active = null, string? scope = null, CancellationToken ct = default)
    {
        // Pasif binanin kapatmalari yonetim ekranlarinda da yer almaz; "yururlukte" suresi gecmemis kayit demektir.
        var q = db.Closures.AsNoTracking().Where(c =>
            (c.DeskId != null && c.Desk!.Floor.Building.Active)
            || (c.RoomId != null && c.Room!.Floor.Building.Active)
            || (c.FloorId != null && c.Floor!.Building.Active));
        if (active is bool a) q = q.Where(c => c.Active == a);
        if (active == true)
        {
            var today = clock.Today;
            q = q.Where(c => c.EndDate >= today);
        }
        if (!string.IsNullOrEmpty(scope)) q = q.Where(c => c.Scope == scope);
        return q.Include(c => c.Desk).Include(c => c.Room).Include(c => c.Floor)
            .OrderByDescending(c => c.Active).ThenByDescending(c => c.StartDate)
            .ToListAsync(ct);
    }

    public async Task<CreateClosureResult> CreateAsync(ClosureInput input, AuditActor actor, CancellationToken ct = default)
    {
        if (string.CompareOrdinal(input.EndDayKey, input.StartDayKey) < 0)
        {
            throw AppErrors.Validation("Bitiş tarihi başlangıç tarihinden önce olamaz.");
        }
        if (string.CompareOrdinal(input.StartDayKey, clock.TodayKey()) < 0)
        {
            throw AppErrors.Validation("Kapatma geçmiş bir tarihten başlatılamaz.");
        }

        var closure = new Closure
        {
            Scope = input.Scope,
            StartDate = DayKeys.ToDateOnly(input.StartDayKey),
            EndDate = DayKeys.ToDateOnly(input.EndDayKey),
            Reason = input.Reason,
            Note = input.Note,
            CreatedById = actor.Id ?? string.Empty,
            CreatedAt = clock.UtcNow,
            UpdatedAt = clock.UtcNow,
        };

        string targetLabel;
        if (input.Scope == ClosureScopes.Desk)
        {
            var desk = await db.Desks.AsNoTracking().Include(d => d.Floor).FirstOrDefaultAsync(d => d.Id == input.TargetId, ct)
                ?? throw AppErrors.NotFound("Masa bulunamadı.");
            closure.DeskId = desk.Id;
            targetLabel = $"{desk.Code} ({desk.Floor.Name})";
        }
        else if (input.Scope == ClosureScopes.Room)
        {
            var room = await db.MeetingRooms.AsNoTracking().Include(r => r.Floor).FirstOrDefaultAsync(r => r.Id == input.TargetId, ct)
                ?? throw AppErrors.NotFound("Toplantı odası bulunamadı.");
            closure.RoomId = room.Id;
            targetLabel = $"{room.Name} ({room.Code})";
        }
        else
        {
            var floor = await db.Floors.AsNoTracking().FirstOrDefaultAsync(f => f.Id == input.TargetId, ct)
                ?? throw AppErrors.NotFound("Kat bulunamadı.");
            closure.FloorId = floor.Id;
            targetLabel = floor.Name;
        }

        var affected = await db.InTransactionAsync(async token =>
        {
            db.Closures.Add(closure);
            await db.SaveChangesAsync(token);
            return await CancelAffectedReservationsAsync(closure, actor, targetLabel, token);
        }, ct);

        await audit.RecordAsync(actor, AuditActions.ClosureCreate, "Closure", closure.Id,
            $"{targetLabel} {ClosureReasons.Labels[input.Reason]} nedeniyle {DayKeys.FormatLong(input.StartDayKey, withWeekday: false)} - {DayKeys.FormatLong(input.EndDayKey, withWeekday: false)} arasında kapatıldı.",
            new { scope = input.Scope, reason = input.Reason, cancelledReservations = affected }, ct);

        return new CreateClosureResult(closure, affected);
    }

    /// <summary>Kapatma araligina denk gelen gelecekteki rezervasyonlari iptal eder.</summary>
    private async Task<int> CancelAffectedReservationsAsync(Closure closure, AuditActor actor, string targetLabel, CancellationToken ct)
    {
        var cancelReason = $"{targetLabel} geçici olarak kullanıma kapatıldı: {closure.Note}";
        var cancelled = 0;

        if (closure.Scope is ClosureScopes.Desk or ClosureScopes.Floor)
        {
            Expression<Func<DeskReservation, bool>> target = closure.Scope == ClosureScopes.Desk
                ? r => r.DeskId == closure.DeskId
                : r => r.Desk.FloorId == closure.FloorId;
            cancelled += await canceller.CancelDesksAsync(target, closure.StartDate, closure.EndDate, cancelReason, closure.Note, actor, ct);
        }

        if (closure.Scope is ClosureScopes.Room or ClosureScopes.Floor)
        {
            Expression<Func<RoomReservation, bool>> target = closure.Scope == ClosureScopes.Room
                ? r => r.RoomId == closure.RoomId
                : r => r.Room.FloorId == closure.FloorId;
            cancelled += await canceller.CancelRoomsAsync(target, closure.StartDate, closure.EndDate, cancelReason, closure.Note, actor, ct);
        }

        return cancelled;
    }

    private async Task<string> TargetLabelAsync(Closure closure, CancellationToken ct)
    {
        if (closure.DeskId is not null)
        {
            var desk = await db.Desks.AsNoTracking().Include(d => d.Floor).FirstAsync(d => d.Id == closure.DeskId, ct);
            return $"{desk.Code} ({desk.Floor.Name})";
        }
        if (closure.RoomId is not null)
        {
            var room = await db.MeetingRooms.AsNoTracking().FirstAsync(r => r.Id == closure.RoomId, ct);
            return $"{room.Name} ({room.Code})";
        }
        return (await db.Floors.AsNoTracking().FirstAsync(f => f.Id == closure.FloorId, ct)).Name;
    }

    public async Task<Closure> UpdateAsync(string closureId, ClosureUpdateInput input, AuditActor actor, CancellationToken ct = default)
    {
        var closure = await db.Closures.FirstOrDefaultAsync(c => c.Id == closureId, ct)
            ?? throw AppErrors.NotFound("Kapatma kaydı bulunamadı.");

        var previousEnd = closure.EndDate;
        var wasActive = closure.Active;
        if (input.EndDayKey is not null)
        {
            var end = DayKeys.ToDateOnly(input.EndDayKey);
            if (end < closure.StartDate) throw AppErrors.Validation("Bitiş tarihi başlangıçtan önce olamaz.");
            if (end < clock.Today && end != closure.EndDate) throw AppErrors.Validation("Bitiş tarihi geçmiş bir gün olamaz.");
            closure.EndDate = end;
        }
        if (input.Active is bool active) closure.Active = active;
        if (input.Note is not null) closure.Note = input.Note;

        // Kapsam genisledi ya da kapatma yeniden etkinlesti: yeni kapsanan rezervasyonlar da iptal edilir.
        var widened = closure.Active && (!wasActive || closure.EndDate > previousEnd);
        var targetLabel = widened ? await TargetLabelAsync(closure, ct) : string.Empty;
        await db.InTransactionAsync(async token =>
        {
            await db.SaveChangesAsync(token);
            return widened ? await CancelAffectedReservationsAsync(closure, actor, targetLabel, token) : 0;
        }, ct);

        await audit.RecordAsync(actor, AuditActions.ClosureUpdate, "Closure", closureId,
            input.Active == false ? "Kapatma kaydı sonlandırıldı, alan yeniden kullanıma açıldı." : "Kapatma kaydı güncellendi.",
            new { active = input.Active, note = input.Note, endDayKey = input.EndDayKey }, ct);

        return closure;
    }
}
