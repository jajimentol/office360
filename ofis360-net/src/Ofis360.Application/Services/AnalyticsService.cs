using Microsoft.EntityFrameworkCore;
using Ofis360.Application.Common;
using Ofis360.Application.Persistence;
using Ofis360.Domain.Common;

namespace Ofis360.Application.Services;

public sealed record AnalyticsFilters(string FromDayKey, string ToDayKey, string? FloorId = null, string? DepartmentId = null, string ResourceType = "ALL");

public sealed record AnalyticsFilterInfo(string FromDayKey, string ToDayKey, string? FloorId, string? DepartmentId, string ResourceType, int DayCount, int WorkdayCount);

public sealed record PeakDay(string DayKey, string Label, string Weekday, double OccupancyRate);

public sealed record AnalyticsKpis(
    int TodayReservations, double TodayOccupancyRate, double WeeklyAverageOccupancy, double NoShowRate, double RoomUsageRate, double CancellationRate,
    int TotalDeskReservations, int TotalRoomReservations, int ActiveDeskCount, int TotalEmployees, int ActiveEmployees,
    // K6: "Bugun ofiste olan" etiketiyle bugunun aktif rezervasyon sayisi (giris yapilmamis BOOKED dahil). Davranis kaynakla ayni.
    int InOfficeToday, int AvailableDesksToday, int TotalUsableDesks, int UnmetRequests, int UnmetRequestDays, int NoShowLostSeats, PeakDay? PeakDay);

public sealed record FloorPressureRow(string FloorId, int FloorNumber, string Name, int Capacity, double PeakOccupancy, double AverageOccupancy, int SaturatedDays);
public sealed record OccupancyTrendRow(string DayKey, string Label, string Weekday, int Reservations, double OccupancyRate);
public sealed record FloorComparisonRow(string FloorId, int FloorNumber, string Name, int Reservations, int Capacity, double OccupancyRate);
public sealed record HourlyDensityRow(string Hour, int Meetings, int Arrivals);
public sealed record DepartmentDistributionRow(string DepartmentId, string Name, string Color, int Reservations, int EmployeeCount, double AverageOfficeDays, double AttendanceRate);
public sealed record DeskHeatmapRow(string DeskId, string Code, int FloorNumber, string ZoneName, bool Popular, int Reservations, double UsageRate);
public sealed record RoomUsageRow(string RoomId, string Code, string Name, int FloorNumber, int Capacity, int Reservations, int BookedMinutes, double UsageRate, double AverageAttendees);
public sealed record BookedVsUsedRow(string DayKey, string Label, int Booked, int Used, int NoShow);
public sealed record BucketRow(string Bucket, int Employees);
public sealed record FairnessInfo(IReadOnlyList<BucketRow> Distribution, IReadOnlyList<BucketRow> PopularShare, double GiniIndex, double TopDeskShare, int ActiveEmployees, double AverageReservationsPerEmployee);
public sealed record StatusCount(string Status, int Count);

public sealed record AnalyticsResult(
    AnalyticsFilterInfo Filters, AnalyticsKpis Kpis, IReadOnlyList<FloorPressureRow> FloorPressure, IReadOnlyList<OccupancyTrendRow> OccupancyTrend,
    IReadOnlyList<FloorComparisonRow> FloorComparison, IReadOnlyList<HourlyDensityRow> HourlyDensity, IReadOnlyList<DepartmentDistributionRow> DepartmentDistribution,
    IReadOnlyList<DeskHeatmapRow> DeskHeatmap, IReadOnlyList<RoomUsageRow> RoomUsage, IReadOnlyList<BookedVsUsedRow> BookedVsUsed,
    FairnessInfo Fairness, IReadOnlyList<StatusCount> StatusBreakdown);

/// <summary>
/// Yonetici raporlama servisi (kaynak: analytics.service.ts). Raporlar kisisel performans takibi amaci tasimaz:
/// adil kullanim gostergeleri kisi adi icermez, yalnizca anonim kovalar dondurulur. Tum toplamlar DB'de gruplanir.
/// </summary>
public sealed class AnalyticsService(IOfis360Db db, IClock clock)
{
    private static readonly System.Linq.Expressions.Expression<Func<Ofis360.Domain.Entities.DeskReservation, bool>> OnCapacityDesk = r =>
        r.Desk.Placed && r.Desk.Active && r.Desk.Zone.Reservable && r.Desk.Floor.Reservable && r.Desk.Floor.Active && r.Desk.Floor.Building.Active;

    private static readonly string[] OccupyingDesk = DeskReservationStatus.Occupying.ToArray();
    private static readonly string[] OccupyingRoom = RoomReservationStatus.Occupying.ToArray();
    private static readonly string[] BucketOrder = ["Hiç", "1-2", "3-5", "6-10", "11-20", "21+"];
    private static readonly string[] ShareOrder = ["%0", "%1-25", "%26-50", "%51-75", "%76-100"];

    private static string BucketLabel(int count) => count switch
    {
        0 => "Hiç", <= 2 => "1-2", <= 5 => "3-5", <= 10 => "6-10", <= 20 => "11-20", _ => "21+",
    };

    /// <summary>JS Number(x.toFixed(n)) karsiligi.</summary>
    private static double ToFixed(double value, int digits) => Math.Round(value, digits, MidpointRounding.AwayFromZero);

    public async Task<AnalyticsResult> GetAnalyticsAsync(AnalyticsFilters filters, CancellationToken ct = default)
    {
        var from = DayKeys.ToDateOnly(filters.FromDayKey);
        var to = DayKeys.ToDateOnly(filters.ToDayKey);
        var floorId = filters.FloorId;
        var departmentId = filters.DepartmentId;

        // Pasif binalarin rezervasyonlari raporlara girmez.
        var deskWhere = db.DeskReservations.AsNoTracking()
            .Where(r => r.Date >= from && r.Date <= to && r.Desk.Floor.Building.Active);
        if (floorId is not null) deskWhere = deskWhere.Where(r => r.Desk.FloorId == floorId);
        if (departmentId is not null) deskWhere = deskWhere.Where(r => r.User.DepartmentId == departmentId);
        var roomWhere = db.RoomReservations.AsNoTracking()
            .Where(r => r.Date >= from && r.Date <= to && r.Room.Floor.Building.Active);
        if (floorId is not null) roomWhere = roomWhere.Where(r => r.Room.FloorId == floorId);
        if (departmentId is not null) roomWhere = roomWhere.Where(r => r.User.DepartmentId == departmentId);
        var activeDeskWhere = deskWhere.Where(r => OccupyingDesk.Contains(r.Status));
        var activeRoomWhere = roomWhere.Where(r => OccupyingRoom.Contains(r.Status));

        var desksQuery = db.Desks.AsNoTracking().Where(d => d.Placed && d.Floor.Reservable && d.Floor.Active && d.Floor.Building.Active && d.Zone.Reservable);
        if (floorId is not null) desksQuery = desksQuery.Where(d => d.FloorId == floorId);
        var desks = await desksQuery.Include(d => d.Floor).Include(d => d.Zone).ToListAsync(ct);

        var roomsQuery = db.MeetingRooms.AsNoTracking().Where(r => r.Floor.Active && r.Floor.Building.Active && r.Placed);
        if (floorId is not null) roomsQuery = roomsQuery.Where(r => r.FloorId == floorId);
        var rooms = await roomsQuery.Include(r => r.Floor).ToListAsync(ct);

        var departments = await db.Departments.AsNoTracking().Select(d => new { d.Id, d.Name, d.Color, UserCount = d.Users.Count }).ToListAsync(ct);
        var deskStatusGroups = await deskWhere.GroupBy(r => r.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(ct);
        var roomStatusGroups = await roomWhere.GroupBy(r => r.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(ct);
        var employeeCount = await db.Users.CountAsync(u => u.Active && (departmentId == null || u.DepartmentId == departmentId), ct);

        var totalEmployees = await db.Users.CountAsync(u => departmentId == null || u.DepartmentId == departmentId, ct);
        var shortfallRows = db.CapacityShortfalls.AsNoTracking().Where(s => s.Date >= from && s.Date <= to);
        var unmetSum = await shortfallRows.SumAsync(s => (int?)s.Unmet, ct) ?? 0;
        var unmetDayCount = await shortfallRows.CountAsync(s => s.Unmet > 0, ct);
        var usableDeskQuery = db.Desks.Where(d => d.Placed && d.Active && d.Zone.Reservable && d.Floor.Reservable && d.Floor.Active && d.Floor.Building.Active);
        if (floorId is not null) usableDeskQuery = usableDeskQuery.Where(d => d.FloorId == floorId);
        var usableDeskCount = await usableDeskQuery.CountAsync(ct);

        var activeDesks = desks.Where(d => d.Active).ToList();
        var deskCapacity = activeDesks.Count;

        // --- Tum toplamlar veritabaninda gruplanir ---
        var deskDayStatusGroups = await deskWhere.GroupBy(r => new { r.Date, r.Status }).Select(g => new { g.Key.Date, g.Key.Status, Count = g.Count() }).ToListAsync(ct);
        var deskByDeskGroups = await activeDeskWhere.GroupBy(r => r.DeskId).Select(g => new { DeskId = g.Key, Count = g.Count() }).ToListAsync(ct);
        var deskByUserGroups = await activeDeskWhere.GroupBy(r => r.UserId).Select(g => new { UserId = g.Key, Count = g.Count() }).ToListAsync(ct);
        var deskPopularByUserGroups = await activeDeskWhere.Where(r => r.Desk.Popular).GroupBy(r => r.UserId).Select(g => new { UserId = g.Key, Count = g.Count() }).ToListAsync(ct);
        var roomByRoomGroups = await activeRoomWhere.GroupBy(r => r.RoomId)
            .Select(g => new { RoomId = g.Key, Count = g.Count(), StartSum = g.Sum(r => r.StartMinutes), EndSum = g.Sum(r => r.EndMinutes), Attendees = g.Sum(r => r.AttendeeCount) })
            .ToListAsync(ct);
        var roomSlotGroups = await activeRoomWhere.GroupBy(r => new { r.StartMinutes, r.EndMinutes })
            .Select(g => new { g.Key.StartMinutes, g.Key.EndMinutes, Count = g.Count() }).ToListAsync(ct);
        // Ofise giris saatleri ve kat x gun dolulugu: Oracle view'lari (V_CHECKIN_HOURLY, V_FLOOR_DAY_OCCUPANCY).
        var arrivalRows = await db.CheckInHourly.Where(x => x.Day >= from && x.Day <= to)
            .GroupBy(x => x.Hour).Select(g => new { Hour = g.Key, Count = g.Sum(x => x.Count) }).ToListAsync(ct);
        // Doluluk fiziksel kapasiteyle olculur: yalnizca bugun kullanimda olan masalar ve masa basina gunde bir kayit.
        // Boylece kaldirilmis masalarin gecmisi ve "gelmedi" sonrasi yeni kayit orani %100'un ustune tasimaz.
        var floorDayRows = await db.DeskReservations.AsNoTracking()
            .Where(r => r.Date >= from && r.Date <= to && OccupyingDesk.Contains(r.Status)).Where(OnCapacityDesk)
            .GroupBy(r => new { r.Date, r.Desk.FloorId })
            .Select(g => new { g.Key.FloorId, Day = g.Key.Date, Count = g.Select(r => r.DeskId).Distinct().Count() })
            .ToListAsync(ct);
        var occupiedByDay = (await activeDeskWhere.Where(OnCapacityDesk)
            .GroupBy(r => r.Date)
            .Select(g => new { Day = g.Key, Count = g.Select(r => r.DeskId).Distinct().Count() })
            .ToListAsync(ct)).ToDictionary(row => DayKeys.FromDateOnly(row.Day), row => row.Count);
        var userDepartments = await db.Users.AsNoTracking().Select(u => new { u.Id, u.DepartmentId }).ToDictionaryAsync(u => u.Id, u => u.DepartmentId, ct);

        int CountOfStatus(IEnumerable<string> statuses) => deskStatusGroups.Where(row => statuses.Contains(row.Status)).Sum(row => row.Count);

        var totalDeskReservationCount = deskStatusGroups.Sum(row => row.Count);
        var activeDeskReservationCount = CountOfStatus(OccupyingDesk);
        var activeRoomReservationCount = roomStatusGroups.Where(row => OccupyingRoom.Contains(row.Status)).Sum(row => row.Count);

        var days = DayKeys.Range(filters.FromDayKey, filters.ToDayKey);
        var workdays = days.Where(d => !DayKeys.IsWeekend(d)).ToList();

        // --- Gunluk doluluk trendi ---
        var bookedByDay = new Dictionary<string, int>();
        var usedByDay = new Dictionary<string, int>();
        var noShowByDay = new Dictionary<string, int>();
        foreach (var row in deskDayStatusGroups)
        {
            var key = DayKeys.FromDateOnly(row.Date);
            if (row.Status != DeskReservationStatus.Cancelled) bookedByDay[key] = bookedByDay.GetValueOrDefault(key) + row.Count;
            if (row.Status is DeskReservationStatus.CheckedIn or DeskReservationStatus.Completed) usedByDay[key] = usedByDay.GetValueOrDefault(key) + row.Count;
            if (row.Status == DeskReservationStatus.NoShow) noShowByDay[key] = noShowByDay.GetValueOrDefault(key) + row.Count;
        }

        var trendDays = days.Count > 45 ? workdays : days.ToList();
        var occupancyTrend = trendDays.Select(dayKey =>
        {
            var reservations = occupiedByDay.GetValueOrDefault(dayKey);
            return new OccupancyTrendRow(dayKey, DayKeys.FormatShort(dayKey), DayKeys.WeekdayLabel(dayKey, shortForm: true), reservations,
                TextUtils.Ratio(reservations, deskCapacity));
        }).ToList();

        // --- Kat karsilastirmasi ---
        var floorMap = new Dictionary<string, (string FloorId, int FloorNumber, string Name, int Reservations, int Capacity)>();
        foreach (var desk in activeDesks)
        {
            var entry = floorMap.GetValueOrDefault(desk.FloorId, (FloorId: desk.FloorId, FloorNumber: desk.Floor.Number, Name: desk.Floor.Name, Reservations: 0, Capacity: 0));
            entry.Capacity += 1;
            floorMap[desk.FloorId] = entry;
        }
        var deskById = activeDesks.ToDictionary(d => d.Id);
        foreach (var row in deskByDeskGroups)
        {
            if (!deskById.TryGetValue(row.DeskId, out var desk)) continue;
            if (floorMap.TryGetValue(desk.FloorId, out var entry))
            {
                entry.Reservations += row.Count;
                floorMap[desk.FloorId] = entry;
            }
        }
        var floorComparison = floorMap.Values
            .Select(e => new FloorComparisonRow(e.FloorId, e.FloorNumber, e.Name, e.Reservations, e.Capacity, TextUtils.Ratio(e.Reservations, e.Capacity * workdays.Count)))
            .OrderBy(e => e.FloorNumber).ToList();

        // --- Saatlik yogunluk ---
        var arrivalsByHour = arrivalRows.ToDictionary(r => r.Hour, r => r.Count);
        var hourlyDensity = Enumerable.Range(8, 12).Select(hour =>
        {
            var start = hour * 60;
            var end = start + 60;
            var meetings = roomSlotGroups.Where(row => row.StartMinutes < end && start < row.EndMinutes).Sum(row => row.Count);
            return new HourlyDensityRow(DayKeys.MinutesToTime(start), meetings, arrivalsByHour.GetValueOrDefault(hour));
        }).ToList();

        // --- Departman dagilimi ---
        var departmentCounts = new Dictionary<string, int>();
        foreach (var row in deskByUserGroups)
        {
            if (!userDepartments.TryGetValue(row.UserId, out var deptId)) continue;
            departmentCounts[deptId] = departmentCounts.GetValueOrDefault(deptId) + row.Count;
        }
        var departmentDistribution = departments
            .Where(d => departmentId is null || d.Id == departmentId)
            .Select(d =>
            {
                var reservations = departmentCounts.GetValueOrDefault(d.Id);
                var employees = d.UserCount == 0 ? 1 : d.UserCount;
                return new DepartmentDistributionRow(d.Id, d.Name, d.Color, reservations, d.UserCount,
                    workdays.Count > 0 ? ToFixed((double)reservations / employees / (workdays.Count / 5.0), 2) : 0,
                    TextUtils.Ratio(reservations, Math.Max(employees * workdays.Count, 1)));
            })
            .OrderByDescending(d => d.Reservations).ToList();

        // --- Masa isi haritasi ---
        var deskCounts = deskByDeskGroups.ToDictionary(r => r.DeskId, r => r.Count);
        var deskHeatmap = activeDesks
            .Select(desk =>
            {
                var reservations = deskCounts.GetValueOrDefault(desk.Id);
                return new DeskHeatmapRow(desk.Id, desk.Code, desk.Floor.Number, desk.Zone.Name, desk.Popular, reservations, TextUtils.Ratio(reservations, workdays.Count));
            })
            .OrderByDescending(d => d.Reservations).ToList();

        // --- Toplanti odasi kullanimi ---
        var roomStats = roomByRoomGroups.ToDictionary(r => r.RoomId, r => (Reservations: r.Count, Minutes: r.EndSum - r.StartSum, Attendees: r.Attendees));
        const int availableMinutesPerDay = 11 * 60;
        var roomUsage = rooms
            .Select(room =>
            {
                var entry = roomStats.GetValueOrDefault(room.Id, (Reservations: 0, Minutes: 0, Attendees: 0));
                return new RoomUsageRow(room.Id, room.Code, room.Name, room.Floor.Number, room.Capacity, entry.Reservations, entry.Minutes,
                    TextUtils.Ratio(entry.Minutes, availableMinutesPerDay * Math.Max(workdays.Count, 1)),
                    entry.Reservations > 0 ? ToFixed((double)entry.Attendees / entry.Reservations, 1) : 0);
            })
            .OrderByDescending(r => r.UsageRate).ToList();

        // --- Rezervasyon vs gerceklesen kullanim ---
        var bookedVsUsed = trendDays.Select(dayKey => new BookedVsUsedRow(dayKey, DayKeys.FormatShort(dayKey),
            bookedByDay.GetValueOrDefault(dayKey), usedByDay.GetValueOrDefault(dayKey), noShowByDay.GetValueOrDefault(dayKey))).ToList();

        // --- Adil kullanim (anonim) ---
        var popularByUser = deskPopularByUserGroups.ToDictionary(r => r.UserId, r => r.Count);
        var perUser = deskByUserGroups.ToDictionary(r => r.UserId, r => (Total: r.Count, Popular: popularByUser.GetValueOrDefault(r.UserId)));

        var distributionCounts = new Dictionary<string, int>();
        var popularCounts = new Dictionary<string, int>();
        var inactiveEmployees = Math.Max(0, employeeCount - perUser.Count);
        distributionCounts["Hiç"] = inactiveEmployees;

        foreach (var entry in perUser.Values)
        {
            var label = BucketLabel(entry.Total);
            distributionCounts[label] = distributionCounts.GetValueOrDefault(label) + 1;
            var share = entry.Total > 0 ? (int)PlanGeometryRound((double)entry.Popular / entry.Total * 100) : 0;
            var shareLabel = share == 0 ? "%0" : share <= 25 ? "%1-25" : share <= 50 ? "%26-50" : share <= 75 ? "%51-75" : "%76-100";
            popularCounts[shareLabel] = popularCounts.GetValueOrDefault(shareLabel) + 1;
        }

        var distribution = BucketOrder.Where(distributionCounts.ContainsKey).Select(b => new BucketRow(b, distributionCounts[b])).ToList();
        var popularShare = ShareOrder.Select(b => new BucketRow(b, popularCounts.GetValueOrDefault(b))).ToList();

        var usageValues = perUser.Values.Select(e => (double)e.Total).Concat(Enumerable.Repeat(0.0, inactiveEmployees)).ToList();
        var totalDeskUsage = activeDeskReservationCount;
        var topDeskUsage = deskHeatmap.Take(5).Sum(d => d.Reservations);

        // --- KPI'lar ---
        var today = clock.TodayKey();
        var todayReservations = occupiedByDay.GetValueOrDefault(today);

        var lastSevenDays = workdays.TakeLast(7).ToList();
        var weeklyAverageOccupancy = lastSevenDays.Count > 0
            ? lastSevenDays.Sum(dayKey => TextUtils.Ratio(occupiedByDay.GetValueOrDefault(dayKey), deskCapacity)) / lastSevenDays.Count
            : 0;

        var noShowCount = CountOfStatus([DeskReservationStatus.NoShow]);
        var cancelledCount = CountOfStatus([DeskReservationStatus.Cancelled]);
        var completedOrNoShow = CountOfStatus([DeskReservationStatus.Completed, DeskReservationStatus.NoShow, DeskReservationStatus.CheckedIn]);
        var totalRoomMinutes = roomStats.Values.Sum(e => e.Minutes);

        // --- Kat bazinda kapasite baskisi ---
        var perFloorDay = new Dictionary<string, Dictionary<string, int>>();
        foreach (var row in floorDayRows)
        {
            var dayKey = DayKeys.FromDateOnly(row.Day);
            var byDay = perFloorDay.GetValueOrDefault(row.FloorId) ?? new Dictionary<string, int>();
            byDay[dayKey] = byDay.GetValueOrDefault(dayKey) + row.Count;
            perFloorDay[row.FloorId] = byDay;
        }

        var floorPressure = floorMap.Values
            .Select(floor =>
            {
                var byDay = perFloorDay.GetValueOrDefault(floor.FloorId) ?? new Dictionary<string, int>();
                var rates = workdays.Select(dayKey => TextUtils.Ratio(byDay.GetValueOrDefault(dayKey), floor.Capacity)).ToList();
                var peak = rates.Count > 0 ? rates.Max() : 0;
                var average = rates.Count > 0 ? rates.Average() : 0;
                return new FloorPressureRow(floor.FloorId, floor.FloorNumber, floor.Name, floor.Capacity, peak, average, rates.Count(v => v >= 90));
            })
            .OrderBy(f => f.FloorNumber).ToList();

        // --- Aralikaki en yogun gun ---
        OccupancyTrendRow? peakEntry = null;
        foreach (var item in occupancyTrend)
        {
            if (peakEntry is null || item.OccupancyRate > peakEntry.OccupancyRate) peakEntry = item;
        }

        var todayUsedDesks = todayReservations;

        return new AnalyticsResult(
            new AnalyticsFilterInfo(filters.FromDayKey, filters.ToDayKey, filters.FloorId, filters.DepartmentId, filters.ResourceType, days.Count, workdays.Count),
            new AnalyticsKpis(
                todayReservations,
                TextUtils.Ratio(todayReservations, deskCapacity),
                weeklyAverageOccupancy,
                TextUtils.Ratio(noShowCount, Math.Max(completedOrNoShow, 1)),
                TextUtils.Ratio(totalRoomMinutes, availableMinutesPerDay * Math.Max(workdays.Count, 1) * Math.Max(rooms.Count, 1)),
                TextUtils.Ratio(cancelledCount, Math.Max(totalDeskReservationCount, 1)),
                activeDeskReservationCount, activeRoomReservationCount, deskCapacity, totalEmployees, employeeCount,
                todayUsedDesks, Math.Max(0, usableDeskCount - todayUsedDesks), usableDeskCount, unmetSum, unmetDayCount, noShowCount,
                peakEntry is null ? null : new PeakDay(peakEntry.DayKey, peakEntry.Label, peakEntry.Weekday, peakEntry.OccupancyRate)),
            floorPressure, occupancyTrend, floorComparison, hourlyDensity, departmentDistribution, deskHeatmap, roomUsage, bookedVsUsed,
            new FairnessInfo(distribution, popularShare, ToFixed(Statistics.ComputeGini(usageValues), 3),
                TextUtils.Ratio(topDeskUsage, Math.Max(totalDeskUsage, 1)), perUser.Count,
                perUser.Count > 0 ? ToFixed((double)totalDeskUsage / perUser.Count, 1) : 0),
            deskStatusGroups.Select(row => new StatusCount(row.Status, row.Count)).ToList());
    }

    private static double PlanGeometryRound(double value) => Math.Floor(value + 0.5);

    /// <summary>Varsayilan tarih araligi: son 30 gun.</summary>
    public (string FromDayKey, string ToDayKey) DefaultRange()
    {
        var today = clock.TodayKey();
        return (DayKeys.AddDays(today, -29), today);
    }

    public static bool ValidateRange(string fromDayKey, string toDayKey)
    {
        var diff = DayKeys.DiffInDays(fromDayKey, toDayKey);
        return diff >= 0 && diff <= 400;
    }
}
