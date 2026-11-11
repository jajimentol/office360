using Microsoft.EntityFrameworkCore;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;
using Ofis360.Infrastructure.Persistence;
using static Ofis360.Seed.SeedSupport;

namespace Ofis360.Seed;

public sealed record ActivityOptions(string? BuildingCode = null, IReadOnlyList<string>? DepartmentCodes = null, int HistoryDays = 365, int? Seed = null, string? Only = null);

/// <summary>
/// Hibrit calisma aktivitesi (kaynak: prisma/seed-activity.ts). Mevcut rezervasyonlari silmez; aktif (masa+gun) ve
/// (kullanici+gun) anahtarlari bellege alinir, yalnizca bos anahtarlar icin uretim yapilir. Deterministiktir.
/// </summary>
public sealed class ActivitySeeder(Ofis360DbContext db, string today, ActivityOptions options, TextWriter log)
{
    private const int FutureDays = 14;
    private const int WeeklyLimit = 3;
    private const int PopularStreakLimit = 2;
    private static readonly Dictionary<int, double> DailyOccupancyTarget = new() { [1] = 0.42, [2] = 0.80, [3] = 0.91, [4] = 0.72, [5] = 0.33 };
    private static readonly string[] CancelReasons =
    [
        "Planlarım değişti, uzaktan çalışacağım.", "Toplantılarım iptal oldu.", "Sağlık nedeniyle ofise gelemiyorum.", "Müşteri ziyaretine gideceğim.", "Ekip planı bu haftaya alındı.",
    ];

    private sealed class ActivityDesk
    {
        public required string Id; public required string Code; public required bool Popular; public required bool AccessibleOnly;
        public required bool Quiet; public required int FloorNumber; public required double Weight;
    }

    private sealed record ActivityUser(string Id, string DepartmentCode, bool AccessibilityApproved, int WeeklyDays, bool PrefersQuietZone);

    public async Task RunAsync()
    {
        var seedValue = options.Seed ?? (options.BuildingCode is not null ? 20261105 + options.BuildingCode.Sum(c => (int)c) : 20260823);
        var random = new SeededRandom((uint)seedValue);
        var historyDays = Math.Max(7, options.HistoryDays);
        var runDesks = options.Only is null or "desks";
        var runRooms = options.Only is null or "rooms";

        log.WriteLine("Ofis360 - 12 aylik hibrit calisma verisi uretiliyor.");
        log.WriteLine("Mevcut rezervasyonlar korunur; yalnizca bos gun/masa kombinasyonlari doldurulur.\n");

        // 1) Kaynaklar
        var buildingCode = options.BuildingCode;
        var deskQuery = db.Desks.AsNoTracking().Include(d => d.Zone).Include(d => d.Floor)
            .Where(d => d.Placed && d.Active && d.Zone.Reservable && d.Floor.Reservable && d.Floor.Active && d.Floor.Building.Active);
        if (buildingCode is not null) deskQuery = deskQuery.Where(d => d.Floor.Building.Code == buildingCode);
        if (buildingCode is not null)
        {
            log.WriteLine($"- Kapsam: yalnizca {buildingCode} binasi{(options.DepartmentCodes is not null ? $", departmanlar {string.Join(", ", options.DepartmentCodes)}" : "")}, gecmis {historyDays} gun, tohum {seedValue}.");
        }
        var deskRows = await deskQuery.ToListAsync();
        if (deskRows.Count == 0)
        {
            log.WriteLine("Krokiye yerlestirilmis rezerve edilebilir masa bulunamadi.");
            log.WriteLine("Once admin ekraninda \"Havuzdaki nesneleri otomatik yerlestir\" adimini calistirin.");
            return;
        }

        var desks = deskRows.Select(d => new ActivityDesk
        {
            Id = d.Id, Code = d.Code, Popular = d.Popular, AccessibleOnly = d.AccessibleOnly, Quiet = d.Zone.Quiet, FloorNumber = d.Floor.Number,
            Weight = d.Popular ? 2.6 : d.Zone.Quiet ? 1.2 : 1.6,
        }).ToList();
        for (var index = 0; index < desks.Count; index += 11) desks[index].Weight = 0.7;
        var bookableDesks = desks.Where(d => !d.AccessibleOnly).ToList();
        var accessibleDesks = desks.Where(d => d.AccessibleOnly).ToList();

        var userQuery = db.Users.AsNoTracking().Include(u => u.Department).Where(u => u.Active);
        if (options.DepartmentCodes is { Count: > 0 } codes) userQuery = userQuery.Where(u => codes.Contains(u.Department.Code));
        var userRows = await userQuery.ToListAsync();
        if (userRows.Count == 0)
        {
            log.WriteLine("Kapsama uyan aktif calisan bulunamadi (departman kodlarini kontrol edin).");
            return;
        }

        var users = userRows.Select(u =>
        {
            var baseDays = u.Role == "EMPLOYEE" ? 2 : 3;
            var jitter = random.Next();
            var weeklyDays = Math.Max(1, Math.Min(WeeklyLimit, jitter < 0.18 ? baseDays - 1 : jitter > 0.82 ? baseDays + 1 : baseDays));
            return new ActivityUser(u.Id, u.Department.Code, u.AccessibilityApproved, weeklyDays, u.PrefersQuietZone);
        }).ToList();

        var roomQuery = db.MeetingRooms.AsNoTracking().Where(r => r.Placed && r.Active && r.Floor.Reservable && r.Floor.Active && r.Floor.Building.Active);
        if (buildingCode is not null) roomQuery = roomQuery.Where(r => r.Floor.Building.Code == buildingCode);
        var rooms = await roomQuery.Select(r => new { r.Id, r.Code, r.Capacity }).ToListAsync();
        log.WriteLine($"- Kaynak: {users.Count} aktif calisan, {desks.Count} rezerve edilebilir masa, {rooms.Count} toplanti odasi.");

        // 2) Mevcut rezervasyonlar
        var existingDesk = await db.DeskReservations.AsNoTracking().Where(r => r.ActiveDeskKey != null).Select(r => new { r.ActiveDeskKey, r.ActiveUserDayKey }).ToListAsync();
        var takenDeskDay = existingDesk.Select(r => r.ActiveDeskKey!).ToHashSet();
        var takenUserDay = existingDesk.Where(r => r.ActiveUserDayKey is not null).Select(r => r.ActiveUserDayKey!).ToHashSet();

        var weeklyUsage = new Dictionary<string, int>();
        foreach (var row in await db.DeskReservations.AsNoTracking().Where(r => r.Status != "CANCELLED").Select(r => new { r.UserId, r.Date }).ToListAsync())
        {
            var weekKey = $"{row.UserId}#{DayKeys.StartOfWeek(DayKeys.FromDateOnly(row.Date))}";
            weeklyUsage[weekKey] = weeklyUsage.GetValueOrDefault(weekKey) + 1;
        }

        var existingRoom = await db.RoomReservations.AsNoTracking().Where(r => r.Status != "CANCELLED").Select(r => new { r.RoomId, r.Date, r.StartMinutes, r.EndMinutes }).ToListAsync();
        var roomBusy = new Dictionary<string, List<(int Start, int End)>>();
        foreach (var row in existingRoom)
        {
            var key = $"{row.RoomId}#{DayKeys.FromDateOnly(row.Date)}";
            if (!roomBusy.TryGetValue(key, out var list)) roomBusy[key] = list = [];
            list.Add((row.StartMinutes, row.EndMinutes));
        }
        log.WriteLine($"- Mevcut: {existingDesk.Count} aktif masa rezervasyonu ve {existingRoom.Count} oda rezervasyonu korunuyor.\n");

        // 3) Masa rezervasyonlari
        var allDays = DayKeys.Range(DayKeys.AddDays(today, -historyDays), DayKeys.AddDays(today, FutureDays)).Where(d => !DayKeys.IsWeekend(d)).ToList();
        var deskRows2 = new List<DeskReservation>();
        var userStreak = new Dictionary<string, (string DeskId, int Streak)>();
        var shortfallByDay = new Dictionary<string, (int Requests, int Fulfilled, int Unmet)>();

        (string Status, bool Cancelled) StatusFor(string dayKey)
        {
            var value = random.Next();
            if (string.CompareOrdinal(dayKey, today) < 0)
            {
                if (value < 0.08) return ("CANCELLED", true);
                if (value < 0.135) return ("NO_SHOW", false);
                return ("COMPLETED", false);
            }
            if (dayKey == today)
            {
                if (value < 0.05) return ("CANCELLED", true);
                if (value < 0.64) return ("CHECKED_IN", false);
                return ("BOOKED", false);
            }
            if (value < 0.07) return ("CANCELLED", true);
            return ("BOOKED", false);
        }

        var weekStarts = allDays.Select(DayKeys.StartOfWeek).Distinct().ToList();
        var attendanceByDay = new Dictionary<string, List<string>>();
        foreach (var weekStart in weekStarts)
        {
            var weekDays = allDays.Where(d => DayKeys.StartOfWeek(d) == weekStart).ToList();
            foreach (var user in users)
            {
                var teamDays = ScaleData.DepartmentOfficeDays.GetValueOrDefault(user.DepartmentCode) ?? [2, 3];
                var ranked = weekDays
                    .Select(day =>
                    {
                        var weekday = DayKeys.WeekdayIndex(day);
                        var dayWeight = ScaleData.WeekdayAttendance.GetValueOrDefault(weekday, 0.3);
                        var teamBonus = teamDays.Contains(weekday) ? 2.4 : 1;
                        return (Day: day, Score: Math.Pow(random.Next(), 1 / (dayWeight * teamBonus)));
                    })
                    .OrderByDescending(x => x.Score)
                    .Take(user.WeeklyDays);
                foreach (var item in ranked)
                {
                    if (!attendanceByDay.TryGetValue(item.Day, out var list)) attendanceByDay[item.Day] = list = [];
                    list.Add(user.Id);
                }
            }
        }

        var userById = users.ToDictionary(u => u.Id);
        var unmetDemand = 0;

        foreach (var dayKey in runDesks ? allDays : [])
        {
            string WeekKeyOf(string userId) => $"{userId}#{DayKeys.StartOfWeek(dayKey)}";
            var usedDesks = new HashSet<string>();
            foreach (var desk in desks) if (takenDeskDay.Contains($"{desk.Id}#{dayKey}")) usedDesks.Add(desk.Id);

            var willingUsers = (attendanceByDay.GetValueOrDefault(dayKey) ?? [])
                .Select(id => userById.GetValueOrDefault(id))
                .Where(u => u is not null).Select(u => u!)
                .Where(u => !takenUserDay.Contains($"{u.Id}#{dayKey}"))
                .Where(u => weeklyUsage.GetValueOrDefault(WeekKeyOf(u.Id)) < WeeklyLimit)
                .Select(u => (User: u, Score: random.Next()))
                .OrderByDescending(x => x.Score)
                .Select(x => x.User)
                .ToList();

            var weekdayIndex = DayKeys.WeekdayIndex(dayKey);
            var seatTarget = JsRound(desks.Count * DailyOccupancyTarget.GetValueOrDefault(weekdayIndex, 0.4));
            var attendees = willingUsers.Take(Math.Max(0, seatTarget - usedDesks.Count)).ToList();
            var turnedAway = willingUsers.Count - attendees.Count;
            unmetDemand += turnedAway;
            shortfallByDay[dayKey] = (willingUsers.Count, attendees.Count, turnedAway);

            var candidateDesks = bookableDesks
                .Select(d => (Desk: d, Score: d.Weight * (0.35 + random.Next() * 1.3)))
                .OrderByDescending(x => x.Score)
                .Select(x => x.Desk)
                .ToList();

            var cursor = 0;
            foreach (var user in attendees)
            {
                ActivityDesk? assigned = null;
                if (user.AccessibilityApproved)
                {
                    var free = accessibleDesks.Where(d => !usedDesks.Contains(d.Id)).ToList();
                    if (free.Count > 0 && random.Next() < 0.7) assigned = free[random.RandomInt(0, free.Count - 1)];
                }

                if (assigned is null)
                {
                    while (cursor < candidateDesks.Count && usedDesks.Contains(candidateDesks[cursor].Id)) cursor++;
                    var probe = cursor;
                    while (probe < candidateDesks.Count)
                    {
                        var desk = candidateDesks[probe];
                        if (usedDesks.Contains(desk.Id)) { probe++; continue; }
                        var streakBlocked = desk.Popular && userStreak.TryGetValue(user.Id, out var s) && s.DeskId == desk.Id && s.Streak >= PopularStreakLimit;
                        var quietSkip = user.PrefersQuietZone && !desk.Quiet && probe < cursor + 10 && random.Next() < 0.5;
                        if (streakBlocked || quietSkip) { probe++; continue; }
                        assigned = desk;
                        break;
                    }
                }

                if (assigned is null) { unmetDemand++; continue; }

                usedDesks.Add(assigned.Id);
                takenUserDay.Add($"{user.Id}#{dayKey}");
                weeklyUsage[WeekKeyOf(user.Id)] = weeklyUsage.GetValueOrDefault(WeekKeyOf(user.Id)) + 1;
                userStreak[user.Id] = userStreak.TryGetValue(user.Id, out var streak) && streak.DeskId == assigned.Id ? (assigned.Id, streak.Streak + 1) : (assigned.Id, 1);

                var outcome = StatusFor(dayKey);
                var createdAt = At(dayKey, -random.RandomInt(1, 6) * 24 + random.RandomInt(8, 17));
                deskRows2.Add(new DeskReservation
                {
                    DeskId = assigned.Id, UserId = user.Id, Date = DayKeys.ToDateOnly(dayKey), Status = outcome.Status,
                    ActiveDeskKey = outcome.Cancelled ? null : $"{assigned.Id}#{dayKey}",
                    ActiveUserDayKey = outcome.Cancelled ? null : $"{user.Id}#{dayKey}",
                    Source = "SEED",
                    CheckedInAt = outcome.Status is "CHECKED_IN" or "COMPLETED" ? At(dayKey, random.RandomInt(8, 10)).AddHours(-3) : null,
                    CancelledAt = outcome.Cancelled ? createdAt.AddDays(1) : null,
                    CancelledById = outcome.Cancelled ? user.Id : null,
                    CancelReason = outcome.Cancelled ? random.Pick(CancelReasons) : null,
                    CreatedAt = createdAt, UpdatedAt = createdAt,
                });

                if (outcome.Cancelled)
                {
                    usedDesks.Remove(assigned.Id);
                    takenUserDay.Remove($"{user.Id}#{dayKey}");
                    weeklyUsage[WeekKeyOf(user.Id)] = weeklyUsage.GetValueOrDefault(WeekKeyOf(user.Id), 1) - 1;
                }
            }
        }

        log.WriteLine($"- {deskRows2.Count} masa rezervasyonu hazirlandi, yaziliyor...");
        await BulkInsertAsync(db, deskRows2);

        if (runDesks && shortfallByDay.Count > 0)
        {
            var from = DayKeys.ToDateOnly(allDays[0]);
            var to = DayKeys.ToDateOnly(allDays[^1]);
            await db.CapacityShortfalls.Where(s => s.Source == "SEED" && s.Date >= from && s.Date <= to).ExecuteDeleteAsync();
            var shortfallRows = shortfallByDay.Select(kv => new CapacityShortfall
            {
                Date = DayKeys.ToDateOnly(kv.Key), Requests = kv.Value.Requests, Fulfilled = kv.Value.Fulfilled, Unmet = kv.Value.Unmet, Source = "SEED",
            }).ToList();
            await BulkInsertAsync(db, shortfallRows);
            log.WriteLine($"- {shortfallRows.Count} günlük kapasite açığı kaydı yazıldı.");
        }

        // 4) Toplanti odasi rezervasyonlari
        var roomRows = new List<RoomReservation>();
        int[] durations = [30, 60, 60, 90, 120];
        foreach (var dayKey in runRooms ? allDays : [])
        {
            var weekday = DayKeys.WeekdayIndex(dayKey);
            var dayWeight = ScaleData.WeekdayAttendance.GetValueOrDefault(weekday, 0.3);
            foreach (var room in rooms)
            {
                var key = $"{room.Id}#{dayKey}";
                if (!roomBusy.TryGetValue(key, out var busy)) roomBusy[key] = busy = [];
                var meetingCount = Math.Max(2, JsRound(dayWeight * random.RandomInt(4, 8)));
                var cursorMinutes = DefaultWorkingHours.StartMinutes + random.RandomInt(0, 2) * 30;

                for (var i = 0; i < meetingCount; i++)
                {
                    var duration = random.Pick(durations);
                    if (cursorMinutes + duration > DefaultWorkingHours.EndMinutes) break;
                    var startMinutes = cursorMinutes;
                    var endMinutes = cursorMinutes + duration;
                    cursorMinutes = endMinutes + random.RandomInt(0, 1) * 30;

                    if (busy.Any(slot => startMinutes < slot.End && slot.Start < endMinutes)) continue;
                    busy.Add((startMinutes, endMinutes));

                    var organizer = users[random.RandomInt(0, users.Count - 1)];
                    var value = random.Next();
                    var status = "BOOKED";
                    if (string.CompareOrdinal(dayKey, today) < 0) status = value < 0.07 ? "CANCELLED" : value < 0.13 ? "NO_SHOW" : "COMPLETED";
                    else if (value < 0.06) status = "CANCELLED";

                    var createdAt = At(dayKey, -random.RandomInt(1, 5) * 24);
                    roomRows.Add(new RoomReservation
                    {
                        RoomId = room.Id, UserId = organizer.Id, Date = DayKeys.ToDateOnly(dayKey),
                        StartAt = AtMinutes(dayKey, startMinutes), EndAt = AtMinutes(dayKey, endMinutes), StartMinutes = startMinutes, EndMinutes = endMinutes,
                        Title = random.Pick(SeedData.MeetingTitles), Purpose = random.Pick(SeedData.MeetingPurposes),
                        AttendeeCount = Math.Max(2, Math.Min(room.Capacity, random.RandomInt(2, room.Capacity))),
                        Status = status, Source = "SEED",
                        CancelledAt = status == "CANCELLED" ? createdAt.AddHours(12) : null,
                        CancelledById = status == "CANCELLED" ? organizer.Id : null,
                        CancelReason = status == "CANCELLED" ? "Toplantı çevrimiçi yapılacak şekilde güncellendi." : null,
                        CreatedAt = createdAt, UpdatedAt = createdAt,
                    });
                }
            }
        }
        log.WriteLine($"- {roomRows.Count} toplanti odasi rezervasyonu hazirlandi, yaziliyor...");
        await BulkInsertAsync(db, roomRows);

        // 5) Donemsel bakim kapatmalari
        var admin = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Role == "ADMIN");
        if (admin is not null)
        {
            var maintenanceTargets = desks.Where((_, index) => index % 37 == 3).Take(4).ToList();
            for (var index = 0; index < maintenanceTargets.Count; index++)
            {
                var desk = maintenanceTargets[index];
                var start = DayKeys.ToDateOnly(DayKeys.AddDays(today, -60 - index * 45));
                if (await db.Closures.AnyAsync(c => c.DeskId == desk.Id && c.StartDate == start)) continue;
                db.Closures.Add(new Closure
                {
                    Scope = "DESK", DeskId = desk.Id, StartDate = start, EndDate = start.AddDays(6), Reason = "MAINTENANCE",
                    Note = "Dönemsel donanım bakımı yapıldı.", Active = false, CreatedById = admin.Id,
                });
            }
            await db.SaveChangesAsync();
        }

        var deskTotal = await db.DeskReservations.CountAsync();
        var roomTotal = await db.RoomReservations.CountAsync();
        var cancelled = await db.DeskReservations.CountAsync(r => r.Status == "CANCELLED");
        var noShow = await db.DeskReservations.CountAsync(r => r.Status == "NO_SHOW");
        log.WriteLine("\nSONUC");
        log.WriteLine($"  Masa rezervasyonu : {deskTotal} (iptal %{(deskTotal > 0 ? cancelled * 100.0 / deskTotal : 0):F1}, gelmedi %{(deskTotal > 0 ? noShow * 100.0 / deskTotal : 0):F1})");
        log.WriteLine($"  Oda rezervasyonu  : {roomTotal}");
        log.WriteLine($"  Karsilanamayan talep ({historyDays} gun): {unmetDemand}");
    }
}
