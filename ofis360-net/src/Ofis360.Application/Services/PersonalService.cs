using Microsoft.EntityFrameworkCore;
using Ofis360.Application.Common;
using Ofis360.Application.Persistence;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;
using Ofis360.Domain.Rules;

namespace Ofis360.Application.Services;

public sealed record PersonalTotals(
    int DeskReservations, int RoomReservations, int Cancelled, int NoShow, int Completed, int Last30Days, double NoShowRate, double PopularShareRate);

public sealed record WeeklyTrendPoint(string Label, string WeekStart, int Days);
public sealed record WeekdayCount(string Label, int Count);
public sealed record FloorPreference(string Name, int Count);
public sealed record FavoriteDesk(string Code, string ZoneName, int Count, bool Popular);

public sealed record PersonalSummary(
    PersonalTotals Totals, IReadOnlyList<WeeklyTrendPoint> WeeklyTrend, IReadOnlyList<WeekdayCount> WeekdayDistribution,
    IReadOnlyList<FloorPreference> FloorPreference, IReadOnlyList<FavoriteDesk> FavoriteDesks, FairUseSummary FairUse, int UpcomingCount);

public sealed record EmployeeHomeSummary(
    string Today, DeskReservation? TodayDesk, DeskReservation? NextDesk, IReadOnlyList<RoomReservation> TodayRooms, RoomReservation? NextRoom,
    FairUseSummary FairUse, string? Announcement, int AdvanceDays);

/// <summary>Calisanin kendi kullanim istatistikleri ve adil kullanim hakki ozeti (kaynak: personal.service.ts).</summary>
public sealed class PersonalService(IOfis360Db db, RuleService rules, AvailabilityService availability, IClock clock)
{
    private static readonly string[] OccupyingDesk = DeskReservationStatus.Occupying.ToArray();
    private static readonly string[] OccupyingRoom = RoomReservationStatus.Occupying.ToArray();

    public async Task<PersonalSummary> GetPersonalSummaryAsync(string userId, CancellationToken ct = default)
    {
        var today = clock.TodayKey();
        var settings = await rules.GetRuleSettingsForUserAsync(userId, null, ct);
        var context = await availability.BuildUserDeskContextAsync(userId, today, ct);

        var deskReservations = await db.DeskReservations.AsNoTracking().Where(r => r.UserId == userId && r.Desk.Floor.Building.Active)
            .Include(r => r.Desk).ThenInclude(d => d.Floor).Include(r => r.Desk).ThenInclude(d => d.Zone)
            .OrderByDescending(r => r.Date).ToListAsync(ct);
        var roomReservations = await db.RoomReservations.AsNoTracking().Where(r => r.UserId == userId && r.Room.Floor.Building.Active)
            .Include(r => r.Room).ThenInclude(m => m.Floor)
            .OrderByDescending(r => r.Date).ToListAsync(ct);

        var activeDesk = deskReservations.Where(r => OccupyingDesk.Contains(r.Status)).ToList();
        var activeRoom = roomReservations.Where(r => OccupyingRoom.Contains(r.Status)).ToList();
        // Ofis gunu istatistikleri "gelmedi" kayitlarini saymaz; hak ve pay hesaplari activeDesk ile surer.
        var officeDesk = activeDesk.Where(r => DeskReservationStatus.Holding.Contains(r.Status)).ToList();

        var cancelled = deskReservations.Count(r => r.Status == DeskReservationStatus.Cancelled);
        var noShow = deskReservations.Count(r => r.Status == DeskReservationStatus.NoShow);
        var completed = deskReservations.Count(r => r.Status is DeskReservationStatus.Completed or DeskReservationStatus.CheckedIn);

        // Son 12 hafta ofis gunu trendi
        var weeklyTrend = new List<WeeklyTrendPoint>();
        for (var index = 11; index >= 0; index--)
        {
            var reference = DayKeys.AddDays(today, -7 * index);
            var weekStart = DayKeys.StartOfWeek(reference);
            var weekEnd = DayKeys.EndOfWeek(reference);
            var days = officeDesk.Count(r => string.CompareOrdinal(r.DayKey, weekStart) >= 0 && string.CompareOrdinal(r.DayKey, weekEnd) <= 0);
            var month = DayKeys.MonthLabel(int.Parse(weekStart.Substring(5, 2)) - 1);
            weeklyTrend.Add(new WeeklyTrendPoint($"{weekStart.Substring(8, 2)} {(month.Length > 3 ? month[..3] : month)}", weekStart, days));
        }

        // Gunlere gore dagilim
        var weekdayCounts = officeDesk.GroupBy(r => DayKeys.WeekdayLabel(r.DayKey, shortForm: true)).ToDictionary(g => g.Key, g => g.Count());
        var weekdayDistribution = new[] { "Pzt", "Sal", "Çar", "Per", "Cum" }
            .Select(label => new WeekdayCount(label, weekdayCounts.GetValueOrDefault(label))).ToList();

        // Kat tercihi (ilk gorulme sirasi korunur, sayiya gore azalan)
        var floorPreference = officeDesk.GroupBy(r => r.Desk.FloorId)
            .Select(g => new FloorPreference(g.First().Desk.Floor.Name, g.Count()))
            .OrderByDescending(f => f.Count).ToList();

        // En cok kullanilan masalar
        var favoriteDesks = officeDesk.GroupBy(r => r.DeskId)
            .Select(g => new FavoriteDesk(g.First().Desk.Code, g.First().Desk.Zone.Name, g.Count(), g.First().Desk.Popular))
            .OrderByDescending(d => d.Count).Take(5).ToList();

        var popularCount = activeDesk.Count(r => r.Desk.Popular);
        var fairUse = DeskRules.BuildFairUseSummary(settings, context.History, context.WeeklyTotal, today);

        var thirtyAgo = DayKeys.AddDays(today, -30);
        var last30 = officeDesk.Count(r => string.CompareOrdinal(r.DayKey, thirtyAgo) >= 0 && string.CompareOrdinal(r.DayKey, today) <= 0);

        return new PersonalSummary(
            new PersonalTotals(activeDesk.Count, activeRoom.Count, cancelled, noShow, completed, last30,
                TextUtils.Ratio(noShow, Math.Max(completed + noShow, 1)),
                TextUtils.Ratio(popularCount, Math.Max(activeDesk.Count, 1))),
            weeklyTrend, weekdayDistribution, floorPreference, favoriteDesks, fairUse,
            activeDesk.Count(r => string.CompareOrdinal(r.DayKey, today) >= 0));
    }

    /// <summary>Ana sayfa ozeti: bugunku durum, yaklasan rezervasyon, adil kullanim hakki.</summary>
    public async Task<EmployeeHomeSummary> GetEmployeeHomeSummaryAsync(string userId, CancellationToken ct = default)
    {
        var today = clock.TodayKey();
        var day = clock.Today;
        var settings = await rules.GetRuleSettingsForUserAsync(userId, null, ct);
        var context = await availability.BuildUserDeskContextAsync(userId, today, ct);

        var todayDesk = await db.DeskReservations.AsNoTracking()
            .Where(r => r.UserId == userId && r.Date == day && DeskReservationStatus.Holding.Contains(r.Status) && r.Desk.Floor.Building.Active)
            .Include(r => r.Desk).ThenInclude(d => d.Floor).Include(r => r.Desk).ThenInclude(d => d.Zone)
            .FirstOrDefaultAsync(ct);
        var nextDesk = await db.DeskReservations.AsNoTracking()
            .Where(r => r.UserId == userId && r.Date > day && OccupyingDesk.Contains(r.Status) && r.Desk.Floor.Building.Active)
            .Include(r => r.Desk).ThenInclude(d => d.Floor).Include(r => r.Desk).ThenInclude(d => d.Zone)
            .OrderBy(r => r.Date).FirstOrDefaultAsync(ct);
        var todayRooms = await db.RoomReservations.AsNoTracking()
            .Where(r => r.UserId == userId && r.Date == day && OccupyingRoom.Contains(r.Status) && r.Room.Floor.Building.Active)
            .Include(r => r.Room).ThenInclude(m => m.Floor)
            .OrderBy(r => r.StartMinutes).ToListAsync(ct);
        var nextRoom = await db.RoomReservations.AsNoTracking()
            .Where(r => r.UserId == userId && r.Date > day && OccupyingRoom.Contains(r.Status) && r.Room.Floor.Building.Active)
            .Include(r => r.Room).ThenInclude(m => m.Floor)
            .OrderBy(r => r.Date).ThenBy(r => r.StartMinutes).FirstOrDefaultAsync(ct);
        var announcement = await db.SystemSettings.AsNoTracking().Where(s => s.Key == SettingKeys.Announcement).Select(s => s.Value).FirstOrDefaultAsync(ct);

        var fairUse = DeskRules.BuildFairUseSummary(settings, context.History, context.WeeklyTotal, today);

        return new EmployeeHomeSummary(today, todayDesk, nextDesk, todayRooms, nextRoom, fairUse, announcement, settings.AdvanceBookingDays.Days);
    }
}
