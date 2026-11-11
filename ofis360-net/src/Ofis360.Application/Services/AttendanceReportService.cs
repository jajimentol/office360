using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Ofis360.Application.Common;
using Ofis360.Application.Persistence;
using Ofis360.Domain.Common;

namespace Ofis360.Application.Services;

public sealed record AttendanceTotals(int DeskAttended, int DeskNoShow, double DeskNoShowRate, int RoomHeld, int RoomEmpty, double RoomEmptyRate, int LateCancels);

public sealed record AttendanceDepartmentRow(string Name, int DeskAttended, int DeskNoShow, double DeskNoShowRate, int RoomHeld, int RoomEmpty, int LateCancels);

public sealed record AttendanceRoomRow(string Code, string Name, int Held, int Empty, double EmptyRate, int LateCancels);

public sealed record AttendanceWeekRow(string WeekStart, int DeskNoShow, int RoomEmpty, int LateCancels);

public sealed record AttendancePersonRow(string FullName, string Department, int DeskNoShow, int RoomEmpty, int LateCancels);

public sealed record AttendanceReport(
    string From, string To, bool PersonDetailEnabled, int LateCancelMinutes, AttendanceTotals Totals,
    IReadOnlyList<AttendanceDepartmentRow> Departments, IReadOnlyList<AttendanceRoomRow> Rooms, IReadOnlyList<AttendanceWeekRow> Weeks,
    IReadOnlyList<AttendancePersonRow>? People);

/// <summary>
/// Gelmeme, bos toplanti ve gec iptal raporu. Kisi adlari KVKK/IK onayi olmadan gosterilmez: ayar kapaliyken
/// kisi listesi hic uretilmez. Gec iptal yalnizca rezervasyon sahibinin kendi iptalini sayar.
/// </summary>
public sealed class AttendanceReportService(IOfis360Db db, IClock clock)
{
    public const int MaxRangeDays = 366;
    public const int DefaultLateCancelMinutes = 120;
    private const int MaxPeople = 200;

    public async Task<AttendanceReport> BuildAsync(string? fromKey, string? toKey, string? departmentId = null, CancellationToken ct = default)
    {
        var toDay = toKey ?? clock.TodayKey();
        var fromDay = fromKey ?? DayKeys.AddDays(toDay, -29);
        if (!DayKeys.IsValid(fromDay) || !DayKeys.IsValid(toDay)) throw AppErrors.Validation("Geçerli bir tarih aralığı seçin.");
        if (string.CompareOrdinal(fromDay, toDay) > 0) throw AppErrors.Validation("Başlangıç tarihi bitişten sonra olamaz.");
        if (DayKeys.DiffInDays(fromDay, toDay) > MaxRangeDays) throw AppErrors.Validation($"Rapor en fazla {MaxRangeDays} günlük olabilir.");

        var from = DayKeys.ToDateOnly(fromDay);
        var to = DayKeys.ToDateOnly(toDay);

        var settings = await db.SystemSettings.AsNoTracking()
            .Where(s => s.Key == SettingKeys.ReportPersonDetail || s.Key == SettingKeys.LateCancelMinutes)
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);
        var personDetail = string.Equals(settings.GetValueOrDefault(SettingKeys.ReportPersonDetail), "true", StringComparison.OrdinalIgnoreCase);
        var lateMinutes = int.TryParse(settings.GetValueOrDefault(SettingKeys.LateCancelMinutes), out var parsed) ? parsed : DefaultLateCancelMinutes;

        var deskQuery = db.DeskReservations.AsNoTracking().Where(r => r.Date >= from && r.Date <= to && r.Desk.Floor.Building.Active
            && (r.Status == DeskReservationStatus.NoShow || r.Status == DeskReservationStatus.CheckedIn || r.Status == DeskReservationStatus.Completed));
        var roomQuery = db.RoomReservations.AsNoTracking().Where(r => r.Date >= from && r.Date <= to && r.Room.Floor.Building.Active
            && (r.Status == RoomReservationStatus.NoShow || r.Status == RoomReservationStatus.Completed
                || (r.Status == RoomReservationStatus.Cancelled && r.CancelledById == r.UserId)));
        if (departmentId is not null)
        {
            deskQuery = deskQuery.Where(r => r.User.DepartmentId == departmentId);
            roomQuery = roomQuery.Where(r => r.User.DepartmentId == departmentId);
        }

        var desks = await deskQuery
            .Select(r => new { r.Date, r.Status, r.UserId, r.User.FullName, Department = r.User.Department.Name })
            .ToListAsync(ct);
        var roomRows = await roomQuery
            .Select(r => new { r.Date, r.Status, r.StartMinutes, r.CancelledAt, r.UserId, r.User.FullName, Department = r.User.Department.Name, r.Room.Code, RoomName = r.Room.Name })
            .ToListAsync(ct);

        var rooms = roomRows.Select(r => new
        {
            r.Date, r.UserId, r.FullName, r.Department, r.Code, r.RoomName,
            Empty = r.Status == RoomReservationStatus.NoShow,
            Held = r.Status != RoomReservationStatus.Cancelled,
            Late = r.Status == RoomReservationStatus.Cancelled && IsLate(r.Date, r.StartMinutes, r.CancelledAt, lateMinutes),
        }).ToList();

        var deskNoShow = desks.Count(d => d.Status == DeskReservationStatus.NoShow);
        var roomHeld = rooms.Count(r => r.Held);
        var roomEmpty = rooms.Count(r => r.Empty);
        var totals = new AttendanceTotals(desks.Count - deskNoShow, deskNoShow, TextUtils.Ratio(deskNoShow, desks.Count),
            roomHeld, roomEmpty, TextUtils.Ratio(roomEmpty, roomHeld), rooms.Count(r => r.Late));

        var departments = desks.Select(d => d.Department).Concat(rooms.Select(r => r.Department)).Distinct()
            .Select(name =>
            {
                var deskRows = desks.Where(d => d.Department == name).ToList();
                var noShow = deskRows.Count(d => d.Status == DeskReservationStatus.NoShow);
                var roomOfDepartment = rooms.Where(r => r.Department == name).ToList();
                return new AttendanceDepartmentRow(name, deskRows.Count - noShow, noShow, TextUtils.Ratio(noShow, deskRows.Count),
                    roomOfDepartment.Count(r => r.Held), roomOfDepartment.Count(r => r.Empty), roomOfDepartment.Count(r => r.Late));
            })
            .OrderByDescending(d => d.DeskNoShow + d.RoomEmpty + d.LateCancels).ThenBy(d => d.Name, StringComparer.Ordinal)
            .ToList();

        var roomTable = rooms.GroupBy(r => new { r.Code, r.RoomName })
            .Select(g => new AttendanceRoomRow(g.Key.Code, g.Key.RoomName, g.Count(r => r.Held), g.Count(r => r.Empty),
                TextUtils.Ratio(g.Count(r => r.Empty), g.Count(r => r.Held)), g.Count(r => r.Late)))
            .OrderByDescending(r => r.Empty + r.LateCancels).ThenBy(r => r.Code, StringComparer.Ordinal)
            .ToList();

        string Week(DateOnly date) => DayKeys.StartOfWeek(DayKeys.FromDateOnly(date));
        var weeks = desks.Select(d => Week(d.Date)).Concat(rooms.Select(r => Week(r.Date))).Distinct().OrderBy(w => w, StringComparer.Ordinal)
            .Select(week => new AttendanceWeekRow(week,
                desks.Count(d => Week(d.Date) == week && d.Status == DeskReservationStatus.NoShow),
                rooms.Count(r => Week(r.Date) == week && r.Empty),
                rooms.Count(r => Week(r.Date) == week && r.Late)))
            .ToList();

        IReadOnlyList<AttendancePersonRow>? people = null;
        if (personDetail)
        {
            people = desks.Select(d => d.UserId).Concat(rooms.Select(r => r.UserId)).Distinct()
                .Select(id =>
                {
                    var deskRows = desks.Where(d => d.UserId == id).ToList();
                    var roomOfPerson = rooms.Where(r => r.UserId == id).ToList();
                    var name = deskRows.FirstOrDefault()?.FullName ?? roomOfPerson[0].FullName;
                    var department = deskRows.FirstOrDefault()?.Department ?? roomOfPerson[0].Department;
                    return new AttendancePersonRow(name, department, deskRows.Count(d => d.Status == DeskReservationStatus.NoShow),
                        roomOfPerson.Count(r => r.Empty), roomOfPerson.Count(r => r.Late));
                })
                .Where(p => p.DeskNoShow + p.RoomEmpty + p.LateCancels > 0)
                .OrderByDescending(p => p.DeskNoShow + p.RoomEmpty + p.LateCancels).ThenBy(p => p.FullName, StringComparer.Ordinal)
                .Take(MaxPeople)
                .ToList();
        }

        return new AttendanceReport(fromDay, toDay, personDetail, lateMinutes, totals, departments, roomTable, weeks, people);
    }

    /// <summary>Iptal, toplanti baslangicina belirtilen sureden az kala (veya sonra) yapildiysa gectir.</summary>
    private bool IsLate(DateOnly date, int startMinutes, DateTimeOffset? cancelledAt, int lateMinutes)
    {
        if (cancelledAt is not DateTimeOffset cancelled) return false;
        var localCancelled = TimeZoneInfo.ConvertTime(cancelled, clock.TimeZone).DateTime;
        var start = date.ToDateTime(TimeOnly.MinValue).AddMinutes(startMinutes);
        return (start - localCancelled).TotalMinutes <= lateMinutes;
    }

    private static string Tr(double value) => value.ToString("F1", CultureInfo.InvariantCulture).Replace('.', ',');

    public static CsvReport BuildCsv(AttendanceReport r, string type)
    {
        var name = $"katilim-{type}-{r.From}-{r.To}.csv";
        return type switch
        {
            "departman" => new CsvReport(TextUtils.ToCsv(
                ["Departman", "Gelinen masa", "Gelinmeyen masa", "Gelmeme oranı (%)", "Toplantı", "Boş toplantı", "Geç iptal"],
                r.Departments.Select(d => new object?[] { d.Name, d.DeskAttended, d.DeskNoShow, Tr(d.DeskNoShowRate), d.RoomHeld, d.RoomEmpty, d.LateCancels })), name),
            "oda" => new CsvReport(TextUtils.ToCsv(
                ["Kod", "Oda", "Toplantı", "Boş toplantı", "Boş oranı (%)", "Geç iptal"],
                r.Rooms.Select(o => new object?[] { o.Code, o.Name, o.Held, o.Empty, Tr(o.EmptyRate), o.LateCancels })), name),
            "hafta" => new CsvReport(TextUtils.ToCsv(
                ["Hafta başlangıcı", "Gelinmeyen masa", "Boş toplantı", "Geç iptal"],
                r.Weeks.Select(w => new object?[] { w.WeekStart, w.DeskNoShow, w.RoomEmpty, w.LateCancels })), name),
            "kisi" when r.People is not null => new CsvReport(TextUtils.ToCsv(
                ["Ad soyad", "Departman", "Gelinmeyen masa", "Boş toplantı", "Geç iptal"],
                r.People.Select(p => new object?[] { p.FullName, p.Department, p.DeskNoShow, p.RoomEmpty, p.LateCancels })), name),
            "kisi" => throw AppErrors.Forbidden("Kişi bazlı rapor kapalı."),
            _ => throw AppErrors.Validation("Geçersiz rapor türü."),
        };
    }
}
