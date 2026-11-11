using Microsoft.EntityFrameworkCore;
using Ofis360.Application.Common;
using Ofis360.Application.Identity;
using Ofis360.Application.Services;
using Ofis360.Application.Tests.Fixtures;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;
using Ofis360.Seed;

namespace Ofis360.Application.Tests.Integration;

public sealed class AttendanceWorld : IAsyncLifetime
{
    public static readonly DateOnly Monday = new(2031, 3, 3);

    public WorldFixture Fx { get; } = new();
    public SessionUser Ali { get; private set; } = null!;
    public SessionUser Ayse { get; private set; } = null!;
    public string OtherDepartmentId { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await Fx.InitializeAsync();
        Ali = await Fx.CreateTestUserAsync(fullName: "Ali Rapor");
        Ayse = await Fx.CreateTestUserAsync(fullName: "Ayşe Rapor");
        var admin = await Fx.CreateTestUserAsync(Roles.Admin);

        await using var db = Fx.Db();
        var other = new Department { Code = "RAPOR2", Name = "İkinci Birim" };
        db.Departments.Add(other);
        await db.SaveChangesAsync();
        OtherDepartmentId = other.Id;
        (await db.Users.SingleAsync(u => u.Id == Ayse.Id)).DepartmentId = other.Id;
        await db.EkleSettings();

        DeskReservation Desk(string code, SessionUser user, DateOnly day, string status) => new()
        {
            DeskId = Fx.World.Desks[code], UserId = user.Id, Date = day, Status = status,
            ActiveDeskKey = status == DeskReservationStatus.CheckedIn || status == DeskReservationStatus.Booked ? $"{code}#{day}" : null,
            ActiveUserDayKey = status == DeskReservationStatus.CheckedIn || status == DeskReservationStatus.Booked ? $"{user.Id}#{day}" : null,
        };
        RoomReservation Room(SessionUser user, DateOnly day, int start, string status, DateTimeOffset? cancelledAt = null, string? cancelledBy = null)
        {
            var key = DayKeys.FromDateOnly(day);
            return new RoomReservation
            {
                RoomId = Fx.World.RoomId, UserId = user.Id, Date = day, StartMinutes = start, EndMinutes = start + 30, Title = "Toplantı", Purpose = "Ekip", AttendeeCount = 2, Status = status,
                StartAt = new DateTimeOffset(DayKeys.WithMinutes(key, start), TimeSpan.Zero), EndAt = new DateTimeOffset(DayKeys.WithMinutes(key, start + 30), TimeSpan.Zero),
                CancelledAt = cancelledAt, CancelledById = cancelledBy,
            };
        }
        DateTimeOffset Local(DateOnly day, int hour, int minute) => new(day.ToDateTime(new TimeOnly(hour, minute)), TimeSpan.FromHours(3));

        var mon = Monday;
        var nextWeek = Monday.AddDays(7);
        db.AddRange(
            Desk("T-A01", Ali, mon, DeskReservationStatus.NoShow),
            Desk("T-A02", Ali, mon.AddDays(1), DeskReservationStatus.NoShow),
            Desk("T-A03", Ali, mon.AddDays(2), DeskReservationStatus.Completed),
            Desk("T-A01", Ayse, mon.AddDays(1), DeskReservationStatus.CheckedIn),
            Desk("T-A02", Ayse, nextWeek, DeskReservationStatus.NoShow),
            Desk("T-A03", Ayse, nextWeek, DeskReservationStatus.Booked),
            Desk("T-A04", Ali, nextWeek, DeskReservationStatus.Cancelled),
            Desk("T-A04", Ali, mon.AddDays(-30), DeskReservationStatus.NoShow));
        db.AddRange(
            Room(Ali, mon, 600, RoomReservationStatus.NoShow),
            Room(Ali, mon, 660, RoomReservationStatus.Completed),
            Room(Ayse, mon, 720, RoomReservationStatus.Completed),
            Room(Ayse, nextWeek, 600, RoomReservationStatus.Booked),
            // 10:00 toplantisi: tam 120 dk kala gec, 121 dk kala degil, baslangictan sonra gec.
            Room(Ali, mon.AddDays(1), 600, RoomReservationStatus.Cancelled, Local(mon.AddDays(1), 8, 0), Ali.Id),
            Room(Ali, mon.AddDays(1), 660, RoomReservationStatus.Cancelled, Local(mon.AddDays(1), 8, 59), Ali.Id),
            Room(Ayse, mon.AddDays(1), 720, RoomReservationStatus.Cancelled, Local(mon.AddDays(1), 12, 10), Ayse.Id),
            // Yonetim iptali sayilmaz.
            Room(Ali, mon.AddDays(2), 600, RoomReservationStatus.Cancelled, Local(mon.AddDays(2), 9, 30), admin.Id));
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Fx.DisposeAsync();
}

internal static class AttendanceSeedExtensions
{
    public static async Task EkleSettings(this Ofis360.Infrastructure.Persistence.Ofis360DbContext db)
    {
        db.SystemSettings.Add(new SystemSetting { Key = SettingKeys.LateCancelMinutes, Value = "120", Label = "Geç iptal", Description = "x", Type = SettingTypes.Number, Group = "OFFICE" });
        db.SystemSettings.Add(new SystemSetting { Key = SettingKeys.ReportPersonDetail, Value = "false", Label = "Kişi raporu", Description = "x", Type = SettingTypes.Boolean, Group = "OFFICE" });
        await db.SaveChangesAsync();
    }
}

[Collection(OracleCollection.Name)]
[TestCaseOrderer(PriorityOrderer.TypeName, PriorityOrderer.AssemblyName)]
public class AttendanceReportTests(AttendanceWorld w) : IClassFixture<AttendanceWorld>
{
    private WorldFixture Fx => w.Fx;
    private AttendanceReportService Service => Fx.Svc<AttendanceReportService>();
    private static readonly string From = DayKeys.FromDateOnly(AttendanceWorld.Monday);
    private static readonly string To = DayKeys.FromDateOnly(AttendanceWorld.Monday.AddDays(13));

    private async Task SetPersonDetail(string value)
    {
        await using var db = Fx.Db();
        (await db.SystemSettings.SingleAsync(s => s.Key == SettingKeys.ReportPersonDetail)).Value = value;
        await db.SaveChangesAsync();
    }

    [Fact, Order(1)]
    public async Task Toplamlar_yalniz_sonuclanan_kayitlari_sayar()
    {
        var r = await Service.BuildAsync(From, To);
        Assert.Equal(3, r.Totals.DeskNoShow);
        Assert.Equal(2, r.Totals.DeskAttended);
        Assert.Equal(60, r.Totals.DeskNoShowRate);
        Assert.Equal(3, r.Totals.RoomHeld);
        Assert.Equal(1, r.Totals.RoomEmpty);
    }

    [Fact, Order(2)]
    public async Task Gec_iptal_sinirda_dahil_yonetim_iptali_haric()
    {
        var r = await Service.BuildAsync(From, To);
        Assert.Equal(2, r.Totals.LateCancels);
    }

    [Fact, Order(3)]
    public async Task Departman_oda_ve_hafta_kirilimi()
    {
        var r = await Service.BuildAsync(From, To);
        var second = r.Departments.Single(d => d.Name == "İkinci Birim");
        Assert.Equal(1, second.DeskNoShow);
        Assert.Equal(1, second.DeskAttended);
        Assert.Equal(1, second.LateCancels);

        var room = Assert.Single(r.Rooms);
        Assert.Equal(3, room.Held);
        Assert.Equal(1, room.Empty);

        Assert.Equal(2, r.Weeks.Count);
        Assert.Equal(From, r.Weeks[0].WeekStart);
        Assert.Equal(2, r.Weeks[0].DeskNoShow);
        Assert.Equal(1, r.Weeks[1].DeskNoShow);
    }

    [Fact, Order(4)]
    public async Task Departman_filtresi_uygulanir()
    {
        var r = await Service.BuildAsync(From, To, w.OtherDepartmentId);
        Assert.Equal(1, r.Totals.DeskNoShow);
        Assert.Equal("İkinci Birim", Assert.Single(r.Departments).Name);
    }

    [Fact, Order(5)]
    public async Task Kisi_adlari_ayar_kapaliyken_uretilmez()
    {
        var r = await Service.BuildAsync(From, To);
        Assert.False(r.PersonDetailEnabled);
        Assert.Null(r.People);
        var error = Assert.Throws<AppException>(() => AttendanceReportService.BuildCsv(r, "kisi"));
        Assert.Equal(403, error.Status);
    }

    [Fact, Order(6)]
    public async Task Ayar_acilinca_kisiler_siralanir_ve_csv_uretilir()
    {
        await SetPersonDetail("true");
        try
        {
            var r = await Service.BuildAsync(From, To);
            Assert.True(r.PersonDetailEnabled);
            Assert.Equal(2, r.People!.Count);
            Assert.Equal("Ali Rapor", r.People[0].FullName);
            Assert.Equal(2, r.People[0].DeskNoShow);
            Assert.Equal(1, r.People[0].RoomEmpty);
            Assert.Equal(1, r.People[0].LateCancels);

            var csv = AttendanceReportService.BuildCsv(r, "kisi");
            Assert.Contains("Ali Rapor", csv.Csv);
            Assert.EndsWith(".csv", csv.FileName);
        }
        finally
        {
            await SetPersonDetail("false");
        }
    }

    [Fact, Order(7)]
    public async Task Csv_turleri_ve_gecersiz_tur()
    {
        var r = await Service.BuildAsync(From, To);
        Assert.Contains("İkinci Birim", AttendanceReportService.BuildCsv(r, "departman").Csv);
        Assert.Contains("T-R01", AttendanceReportService.BuildCsv(r, "oda").Csv);
        Assert.Contains(From, AttendanceReportService.BuildCsv(r, "hafta").Csv);
        Assert.Equal(400, Assert.Throws<AppException>(() => AttendanceReportService.BuildCsv(r, "yok")).Status);
    }

    [Fact, Order(8)]
    public async Task Gecersiz_tarih_araligi_reddedilir()
    {
        await Assert.ThrowsAsync<AppException>(() => Service.BuildAsync(To, From));
        await Assert.ThrowsAsync<AppException>(() => Service.BuildAsync("2030-01-01", "2031-06-01"));
        await Assert.ThrowsAsync<AppException>(() => Service.BuildAsync("bozuk", To));
    }

    [Fact, Order(9)]
    public async Task Form_komutu_ayarlari_ekler_eski_ayari_kaldirir_degeri_ezmez()
    {
        await using (var db = Fx.Db())
        {
            await db.SystemSettings.Where(s => s.Key == SettingKeys.LateCancelMinutes || s.Key == SettingKeys.ReportPersonDetail).ExecuteDeleteAsync();
            db.SystemSettings.Add(new SystemSetting { Key = "office.no_show_grace_minutes", Value = "90", Label = "Eski", Description = "x", Type = SettingTypes.Number, Group = "OFFICE" });
            await db.SaveChangesAsync();
            await EkBinaRuleSeeder.ApplyAsync(db);
        }

        await using (var db = Fx.Db())
        {
            Assert.False(await db.SystemSettings.AnyAsync(s => s.Key == "office.no_show_grace_minutes"));
            Assert.Equal("false", (await db.SystemSettings.SingleAsync(s => s.Key == SettingKeys.ReportPersonDetail)).Value);
            (await db.SystemSettings.SingleAsync(s => s.Key == SettingKeys.LateCancelMinutes)).Value = "45";
            await db.SaveChangesAsync();
            await EkBinaRuleSeeder.ApplyAsync(db);
        }

        await using (var db = Fx.Db())
        {
            Assert.Equal("45", (await db.SystemSettings.SingleAsync(s => s.Key == SettingKeys.LateCancelMinutes)).Value);
        }
    }

    [Fact, Order(10)]
    public async Task Evet_hayir_ayarina_gecersiz_deger_yazilamaz()
    {
        var admin = await Fx.CreateTestUserAsync(Roles.Admin);
        var actor = new AuditActor(admin.Id, admin.FullName, admin.Role);
        await Assert.ThrowsAsync<AppException>(() => Fx.Svc<SettingService>().UpdateAsync(SettingKeys.ReportPersonDetail, "belki", actor));
        Assert.Equal("true", (await Fx.Svc<SettingService>().UpdateAsync(SettingKeys.ReportPersonDetail, "true", actor)).Value);
        await Fx.Svc<SettingService>().UpdateAsync(SettingKeys.ReportPersonDetail, "false", actor);
    }
}
