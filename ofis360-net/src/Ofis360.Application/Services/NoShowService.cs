using Microsoft.EntityFrameworkCore;
using Ofis360.Application.Common;
using Ofis360.Application.Persistence;
using Ofis360.Domain.Common;

namespace Ofis360.Application.Services;

/// <param name="Healthy">False ise veri eksik olabilir; o gun kimse "gelmedi" sayilmaz.</param>
/// <param name="FirstEntries">Kucuk harf kurumsal e-posta ve gunun ilk giris ani.</param>
public sealed record AttendanceSnapshot(bool Healthy, IReadOnlyDictionary<string, DateTimeOffset> FirstEntries);

/// <summary>Turnike/kart gecisleri. Gercek baglanti, veri kaynagi netlesince bu arayuzle eklenir.</summary>
public interface IAttendanceFeed
{
    bool IsConfigured { get; }

    Task<AttendanceSnapshot> GetDayAsync(string dayKey, CancellationToken ct = default);
}

public sealed class NotConfiguredAttendanceFeed : IAttendanceFeed
{
    public bool IsConfigured => false;

    public Task<AttendanceSnapshot> GetDayAsync(string dayKey, CancellationToken ct = default) =>
        Task.FromResult(new AttendanceSnapshot(false, new Dictionary<string, DateTimeOffset>()));
}

public static class AttendanceFeedStatus
{
    public const string NotConfigured = "NOT_CONFIGURED";
    public const string Healthy = "HEALTHY";
    public const string Unavailable = "UNAVAILABLE";
}

public sealed record NoShowRunResult(int CheckedIn, int DesksReleased, int RoomsNoShow, int RoomsCompleted, string FeedStatus)
{
    public bool HasChanges => CheckedIn + DesksReleased + RoomsNoShow + RoomsCompleted > 0;
}

/// <summary>
/// Gelmeyen rezervasyonlarin isaretlenmesi. Her durum gecisi kosullu UPDATE ile yapilir: ayni anda gelen check-in ya da iptal ezilmez,
/// is birden fazla sunucuda calissa da ayni kayit iki kez islenmez.
/// </summary>
public sealed class NoShowService(
    IOfis360Db db, RuleService rules, IAttendanceFeed feed, NotificationService notifications, AuditService audit, IClock clock)
{
    /// <summary>Uygulama kapali kaldiysa son gunler de tamamlanir; daha eski kayitlara dokunulmaz.</summary>
    private const int CatchUpDays = 7;

    private static readonly AuditActor SystemActor = new(null, "Sistem", "SYSTEM");

    public async Task<NoShowRunResult> RunAsync(CancellationToken ct = default)
    {
        var settings = await rules.GetRuleSettingsAsync(ct);
        var today = clock.Today;
        var todayKey = DayKeys.FromDateOnly(today);
        var nowMinutes = clock.CurrentMinutesOfDay;
        var earliest = today.AddDays(-CatchUpDays);

        var checkedIn = 0;
        var released = 0;
        var feedStatus = feed.IsConfigured ? AttendanceFeedStatus.Healthy : AttendanceFeedStatus.NotConfigured;

        // Turnike bagli degilken "gelmedi" hukmu verilemez: kanit yoklugu gelmedigi anlamina gelmez, kayit BOOKED kalir.
        if (settings.NoShowRelease.Enabled && feed.IsConfigured)
        {
            var candidates = await db.DeskReservations.AsNoTracking()
                .Where(r => r.Date >= earliest && r.Date <= today && r.Status == DeskReservationStatus.Booked && r.Desk.Floor.Building.Active)
                .Select(r => new { r.Id, r.UserId, r.Date, r.CreatedAt, r.Desk.Code, Email = r.User.Email })
                .ToListAsync(ct);

            // Her gun kendi verisiyle degerlendirilir; ariza gunu sonradan telafi edilirken o gunun kayitlari islenmez.
            var snapshots = new Dictionary<DateOnly, AttendanceSnapshot>();
            foreach (var row in candidates)
            {
                if (!snapshots.TryGetValue(row.Date, out var snapshot))
                {
                    snapshot = await feed.GetDayAsync(DayKeys.FromDateOnly(row.Date), ct);
                    snapshots[row.Date] = snapshot;
                }
                if (!snapshot.Healthy)
                {
                    feedStatus = AttendanceFeedStatus.Unavailable;
                    continue;
                }

                if (snapshot.FirstEntries.TryGetValue(row.Email.ToLowerInvariant(), out var enteredAt))
                {
                    checkedIn += await db.DeskReservations
                        .Where(r => r.Id == row.Id && r.Status == DeskReservationStatus.Booked)
                        .ExecuteUpdateAsync(u => u
                            .SetProperty(r => r.Status, DeskReservationStatus.CheckedIn)
                            .SetProperty(r => r.CheckedInAt, enteredAt)
                            .SetProperty(r => r.UpdatedAt, clock.UtcNow), ct);
                    continue;
                }

                if (!IsDue(row.Date, row.CreatedAt, today, nowMinutes, settings.NoShowRelease.StartMinutes, settings.NoShowRelease.ToleranceMinutes)) continue;

                var changed = await db.DeskReservations
                    .Where(r => r.Id == row.Id && r.Status == DeskReservationStatus.Booked)
                    .ExecuteUpdateAsync(u => u
                        .SetProperty(r => r.Status, DeskReservationStatus.NoShow)
                        .SetProperty(r => r.ActiveDeskKey, (string?)null)
                        .SetProperty(r => r.ActiveUserDayKey, (string?)null)
                        .SetProperty(r => r.UpdatedAt, clock.UtcNow), ct);
                if (changed == 0) continue;

                released++;
                await notifications.NotifyAsync(new NotificationPayload(row.UserId, NotificationTypes.DeskNoShowReleased,
                    "Masa rezervasyonunuz serbest bırakıldı",
                    $"{DayKeys.FormatLong(DayKeys.FromDateOnly(row.Date))} tarihli {row.Code} masası için giriş kaydı olmadığından rezervasyonunuz \"gelmedi\" olarak işaretlendi.",
                    "/panel/rezervasyonlarim", NotificationSeverities.Warning), ct);
            }
        }

        var roomsNoShow = 0;
        var roomsCompleted = 0;
        if (settings.RoomStartTracking.Enabled)
        {
            var ended = await db.RoomReservations.AsNoTracking()
                .Where(r => r.Date >= earliest && r.Status == RoomReservationStatus.Booked && r.Room.Floor.Building.Active
                    && (r.Date < today || (r.Date == today && r.EndMinutes <= nowMinutes)))
                .Select(r => new { r.Id, r.StartedAt })
                .ToListAsync(ct);

            foreach (var row in ended)
            {
                var target = row.StartedAt is null ? RoomReservationStatus.NoShow : RoomReservationStatus.Completed;
                var changed = await db.RoomReservations
                    .Where(r => r.Id == row.Id && r.Status == RoomReservationStatus.Booked)
                    .ExecuteUpdateAsync(u => u.SetProperty(r => r.Status, target).SetProperty(r => r.UpdatedAt, clock.UtcNow), ct);
                if (changed == 0) continue;
                if (row.StartedAt is null) roomsNoShow++;
                else roomsCompleted++;
            }
        }

        var result = new NoShowRunResult(checkedIn, released, roomsNoShow, roomsCompleted, feedStatus);
        if (result.HasChanges)
        {
            await audit.RecordAsync(SystemActor, AuditActions.NoShowRun, "Reservation", null,
                $"Otomatik işlem: {released} masa serbest bırakıldı, {checkedIn} turnike girişi işlendi, {roomsNoShow} toplantı boş kaldı, {roomsCompleted} toplantı tamamlandı.",
                result, ct);
        }
        return result;
    }

    /// <summary>Son an: gunun baslangici + tolerans. O gun gec acilan rezervasyonda sure acilis anindan baslar.</summary>
    private bool IsDue(DateOnly date, DateTimeOffset createdAt, DateOnly today, int nowMinutes, int startMinutes, int toleranceMinutes)
    {
        if (date < today) return true;

        var created = TimeZoneInfo.ConvertTime(createdAt, clock.TimeZone);
        var baseline = DateOnly.FromDateTime(created.DateTime) == today
            ? Math.Max(startMinutes, created.Hour * 60 + created.Minute)
            : startMinutes;
        return nowMinutes >= baseline + toleranceMinutes;
    }
}
