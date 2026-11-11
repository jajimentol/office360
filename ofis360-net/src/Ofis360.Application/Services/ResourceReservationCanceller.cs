using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Ofis360.Application.Common;
using Ofis360.Application.Persistence;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;

namespace Ofis360.Application.Services;

/// <summary>
/// Kullanima kapanan masa, oda, bolge, kat veya binadaki rezervasyonlarin iptali. Kapatma ve alan yonetimi ayni yolu kullanir.
/// Yalnizca henuz gerceklesmemis kayitlar iptal edilir: giris yapilmis, sonuclanmis, baslamis veya bitmis kayitlar tarihtir, degismez.
/// Cagiran taraf islemi transaction icinde yurutur.
/// </summary>
public sealed class ResourceReservationCanceller(IOfis360Db db, NotificationService notifications, IClock clock)
{
    private const int MaxCancelReasonLength = 240;

    /// <param name="target">Hangi masalarin kapandigi.</param>
    /// <param name="from">Baslangic; bos veya gecmisse bugun.</param>
    /// <param name="to">Bitis; bossa suresiz.</param>
    /// <param name="notice">Bildirimde tirnak icinde gosterilen kisa gerekce.</param>
    public async Task<int> CancelDesksAsync(
        Expression<Func<DeskReservation, bool>> target, DateOnly? from, DateOnly? to, string cancelReason, string notice, AuditActor actor, CancellationToken ct)
    {
        var start = Start(from);
        var q = db.DeskReservations.Include(r => r.Desk).Where(target).Where(r => r.Date >= start && r.Status == DeskReservationStatus.Booked);
        if (to is DateOnly end) q = q.Where(r => r.Date <= end);
        var reservations = await q.ToListAsync(ct);

        foreach (var reservation in reservations)
        {
            reservation.Status = DeskReservationStatus.Cancelled;
            reservation.ActiveDeskKey = null;
            reservation.ActiveUserDayKey = null;
            reservation.CancelledAt = clock.UtcNow;
            reservation.CancelledById = actor.Id;
            reservation.CancelReason = Trim(cancelReason);
            reservation.UpdatedAt = clock.UtcNow;
            await db.SaveChangesAsync(ct);
            await notifications.NotifyAsync(new NotificationPayload(reservation.UserId, NotificationTypes.ResourceClosed,
                "Rezervasyonunuz kapatma nedeniyle iptal edildi",
                $"{DayKeys.FormatLong(reservation.DayKey)} tarihli {reservation.Desk.Code} masa rezervasyonunuz \"{notice}\" gerekçesiyle iptal edildi.",
                "/panel/masa", NotificationSeverities.Warning), ct);
        }
        return reservations.Count;
    }

    /// <inheritdoc cref="CancelDesksAsync"/>
    public async Task<int> CancelRoomsAsync(
        Expression<Func<RoomReservation, bool>> target, DateOnly? from, DateOnly? to, string cancelReason, string notice, AuditActor actor, CancellationToken ct)
    {
        var start = Start(from);
        var today = clock.Today;
        var nowMinutes = clock.CurrentMinutesOfDay;
        var q = db.RoomReservations.Include(r => r.Room).Where(target)
            .Where(r => r.Date >= start && r.Status == RoomReservationStatus.Booked && r.StartedAt == null
                && (r.Date > today || r.EndMinutes > nowMinutes));
        if (to is DateOnly end) q = q.Where(r => r.Date <= end);
        var reservations = await q.ToListAsync(ct);

        foreach (var reservation in reservations)
        {
            reservation.Status = RoomReservationStatus.Cancelled;
            reservation.CancelledAt = clock.UtcNow;
            reservation.CancelledById = actor.Id;
            reservation.CancelReason = Trim(cancelReason);
            reservation.UpdatedAt = clock.UtcNow;
            await db.SaveChangesAsync(ct);
            await notifications.NotifyAsync(new NotificationPayload(reservation.UserId, NotificationTypes.ResourceClosed,
                "Toplantı rezervasyonunuz kapatma nedeniyle iptal edildi",
                $"{DayKeys.FormatLong(reservation.DayKey)} tarihli {reservation.Room.Name} rezervasyonunuz \"{notice}\" gerekçesiyle iptal edildi.",
                "/panel/toplanti", NotificationSeverities.Warning), ct);
        }
        return reservations.Count;
    }

    private DateOnly Start(DateOnly? from) => from is DateOnly value && value > clock.Today ? value : clock.Today;

    private static string Trim(string reason) => reason.Length > MaxCancelReasonLength ? reason[..MaxCancelReasonLength] : reason;
}
