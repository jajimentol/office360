using Microsoft.EntityFrameworkCore;
using Ofis360.Application.Common;
using Ofis360.Application.Identity;
using Ofis360.Application.Services;
using Ofis360.Application.Tests.Fixtures;
using Ofis360.Application.Validation;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;
using Ofis360.Domain.Rules;
using Ofis360.Seed;
using static Ofis360.Application.Tests.Fixtures.TestHelpers;

namespace Ofis360.Application.Tests.Integration;

public sealed class RuleMatrixWorld : IAsyncLifetime
{
    public WorldFixture Fx { get; } = new();
    public SessionUser Admin { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Fx.InitializeAsync();
        Admin = await Fx.CreateTestUserAsync(Roles.Admin);
    }

    public Task DisposeAsync() => Fx.DisposeAsync();
}

[Collection(OracleCollection.Name)]
public class RuleMatrixTests(RuleMatrixWorld w) : IClassFixture<RuleMatrixWorld>
{
    private WorldFixture Fx => w.Fx;
    private RuleService Rules => Fx.Svc<RuleService>();
    private string Today => Fx.Clock.TodayKey();
    private string Desk(int index) => Fx.World.Desks[$"T-A0{index % 4 + 1}"];

    private async Task Reset()
    {
        await using var db = Fx.Db();
        await db.ReservationRuleOverrides.ExecuteDeleteAsync();
        await db.Holidays.ExecuteDeleteAsync();
        await db.ReservationRules.ExecuteDeleteAsync();
        foreach (var definition in RuleDefinitions.All) db.ReservationRules.Add(RuleService.ToEntity(definition));
        await db.SaveChangesAsync();
    }

    private async Task<SessionUser> User(string? grade, string role = Roles.Employee)
    {
        var user = await Fx.CreateTestUserAsync(role);
        await using var db = Fx.Db();
        (await db.Users.SingleAsync(u => u.Id == user.Id)).Grade = grade;
        await db.SaveChangesAsync();
        return user;
    }

    private Task SetBase(string key, int value) =>
        Rules.UpdateAsync(new UpdateRuleCommand(key, IntValue: Optional<int?>.Of(value)), Actor(w.Admin));

    private Task Matrix(string? buildingId, params RuleMatrixCell[] cells) => Rules.SaveMatrixAsync(buildingId, cells, Actor(w.Admin));

    private Task<DeskReservation> Book(int desk, string day, SessionUser session, string? target = null) =>
        Fx.Svc<DeskReservationService>().CreateAsync(new CreateDeskReservationCommand(Desk(desk), day, null, target), session);

    private static async Task<AppException> Blocked(Func<Task> act, string code)
    {
        var error = await Assert.ThrowsAsync<AppException>(act);
        Assert.Contains(error.Violations ?? [], v => v.Code == code);
        return error;
    }

    private string WorkDay(int n) => WorkCalendar.Default.LatestBookableDay(Today, n);

    private string NextSaturday()
    {
        var day = DayKeys.AddDays(Today, 1);
        while (DayKeys.WeekdayIndex(day) != 6) day = DayKeys.AddDays(day, 1);
        return day;
    }

    [Fact]
    public async Task Pencere_rezervasyon_sahibinin_kademesine_gore_uygulanir()
    {
        await Reset();
        await SetBase(RuleKeys.AdvanceBookingDays, 2);
        await Matrix(null, new RuleMatrixCell(RuleKeys.AdvanceBookingDays, Grades.Director, IntValue: 5));

        var specialist = await User(Grades.Specialist);
        await Blocked(() => Book(0, WorkDay(3), specialist), "ADVANCE_WINDOW_EXCEEDED");
        var director = await User(Grades.Director);
        Assert.Equal("BOOKED", (await Book(1, WorkDay(3), director)).Status);
        await Blocked(() => Book(2, WorkDay(6), director), "ADVANCE_WINDOW_EXCEEDED");
    }

    [Fact]
    public async Task Kademesi_atanmamis_kisi_varsayilana_tabidir()
    {
        await Reset();
        await SetBase(RuleKeys.AdvanceBookingDays, 2);
        await Matrix(null, new RuleMatrixCell(RuleKeys.AdvanceBookingDays, Grades.Gm, IntValue: 15));
        var unassigned = await User(null);
        await Blocked(() => Book(3, WorkDay(3), unassigned), "ADVANCE_WINDOW_EXCEEDED");
    }

    [Fact]
    public async Task Admin_baskasi_adina_islerken_hedefin_kurali_gecerlidir()
    {
        await Reset();
        await SetBase(RuleKeys.AdvanceBookingDays, 2);
        await Matrix(null, new RuleMatrixCell(RuleKeys.AdvanceBookingDays, Grades.Gm, IntValue: 15));
        var admin = await User(Grades.Gm, Roles.Admin);
        var target = await User(Grades.Specialist);
        await Blocked(() => Book(4, WorkDay(3), admin, target.Id), "ADVANCE_WINDOW_EXCEEDED");
    }

    [Fact]
    public async Task Bina_degeri_genel_kademe_degerini_ezer()
    {
        await Reset();
        await SetBase(RuleKeys.AdvanceBookingDays, 2);
        await Matrix(null, new RuleMatrixCell(RuleKeys.AdvanceBookingDays, Grades.Director, IntValue: 5));
        await Matrix(Fx.World.BuildingId, new RuleMatrixCell(RuleKeys.AdvanceBookingDays, Grades.Director, IntValue: 1));
        var director = await User(Grades.Director);
        await Blocked(() => Book(5, WorkDay(2), director), "ADVANCE_WINDOW_EXCEEDED");
    }

    [Fact]
    public async Task Tatil_penceresi_ve_calisma_gunu_kurali_birlikte_calisir()
    {
        await Reset();
        await SetBase(RuleKeys.AdvanceBookingDays, 1);
        var first = WorkDay(1);
        var second = WorkDay(2);
        var user = await User(Grades.Specialist);
        await Blocked(() => Book(6, second, user), "ADVANCE_WINDOW_EXCEEDED");

        await Fx.Svc<HolidayService>().CreateAsync(first, "Test Tatili", Actor(w.Admin));
        Assert.Equal("BOOKED", (await Book(7, second, user)).Status);
    }

    [Fact]
    public async Task Calisma_gunu_kurali_kademeye_gore_degisir()
    {
        await Reset();
        await Rules.UpdateAsync(new UpdateRuleCommand(RuleKeys.WorkingDaysOnly, Enabled: true), Actor(w.Admin));
        await Matrix(null, new RuleMatrixCell(RuleKeys.WorkingDaysOnly, Grades.Manager, Enabled: false));
        var saturday = NextSaturday();

        var specialist = await User(Grades.Specialist);
        await Blocked(() => Book(8, saturday, specialist), "NON_WORKING_DAY");
        await Blocked(() => Fx.Svc<RoomReservationService>().CreateAsync(
            new CreateRoomReservationCommand(Fx.World.RoomId, saturday, 600, 660, "Hafta sonu", "Ekip", 3), specialist), "NON_WORKING_DAY");

        var manager = await User(Grades.Manager);
        Assert.Equal("BOOKED", (await Book(9, saturday, manager)).Status);
    }

    [Fact]
    public async Task Slot_kurali_rezervasyonda_ve_uygunlukta_gecerlidir()
    {
        await Reset();
        await SetBase(RuleKeys.RoomSlotMinutes, 30);
        var user = await User(Grades.Specialist);
        var day = WorkDay(1);
        var rooms = Fx.Svc<RoomReservationService>();

        await Blocked(() => rooms.CreateAsync(new CreateRoomReservationCommand(Fx.World.RoomId, day, 615, 675, "Yanlış", "Ekip", 3), user), "INVALID_SLOT");
        Assert.Equal("BOOKED", (await rooms.CreateAsync(new CreateRoomReservationCommand(Fx.World.RoomId, day, 630, 690, "Doğru", "Ekip", 3), user)).Status);

        var availability = await rooms.GetAvailabilityAsync(new RoomAvailabilityQuery(day, FloorId: Fx.World.FloorId, UserId: user.Id));
        Assert.Equal(30, availability.SlotMinutes);
        Assert.All(availability.Rooms.SelectMany(r => r.FreeSlots), s => Assert.Equal(0, s.StartMinutes % 30));
    }

    [Fact]
    public async Task Matris_gecersiz_degerleri_reddeder()
    {
        await Reset();
        async Task Rejected(params RuleMatrixCell[] cells) => await Assert.ThrowsAsync<AppException>(() => Matrix(null, cells));

        await Rejected(new RuleMatrixCell(RuleKeys.AdvanceBookingDays, Grades.Gm, IntValue: 0));
        await Rejected(new RuleMatrixCell(RuleKeys.AdvanceBookingDays, Grades.Gm, IntValue: 91));
        await Rejected(new RuleMatrixCell(RuleKeys.RoomSlotMinutes, Grades.Gm, IntValue: 20));
        await Rejected(new RuleMatrixCell(RuleKeys.AdvanceBookingDays, "STAJYER", IntValue: 5));
        await Rejected(new RuleMatrixCell(RuleKeys.NoPastDate, Grades.Gm, Enabled: false));
        await Rejected(new RuleMatrixCell(RuleKeys.WorkingDaysOnly, Grades.Gm, IntValue: 5));
        await Rejected(new RuleMatrixCell(RuleKeys.AdvanceBookingDays, Grades.Gm));
        await Rejected(new RuleMatrixCell(RuleKeys.AdvanceBookingDays, Grades.Gm, IntValue: 5), new RuleMatrixCell(RuleKeys.AdvanceBookingDays, Grades.Gm, IntValue: 6));
        await Assert.ThrowsAsync<AppException>(() => Rules.SaveMatrixAsync("yok", [], Actor(w.Admin)));

        await using var db = Fx.Db();
        Assert.False(await db.ReservationRuleOverrides.AnyAsync());
    }

    [Fact]
    public async Task Matris_kapsami_degistirir_denetim_yazar_degismeyende_yazmaz()
    {
        await Reset();
        await Matrix(null, new RuleMatrixCell(RuleKeys.AdvanceBookingDays, Grades.Gm, IntValue: 15), new RuleMatrixCell(RuleKeys.WeeklyDeskLimit, Grades.Gm, IntValue: 5));
        await Matrix(Fx.World.BuildingId, new RuleMatrixCell(RuleKeys.AdvanceBookingDays, Grades.Gm, IntValue: 3));

        async Task<int> Audits()
        {
            await using var db = Fx.Db();
            return await db.AuditLogs.CountAsync(a => a.Action == AuditActions.RuleMatrixUpdate);
        }

        var before = await Audits();
        await Matrix(null, new RuleMatrixCell(RuleKeys.AdvanceBookingDays, Grades.Gm, IntValue: 15), new RuleMatrixCell(RuleKeys.WeeklyDeskLimit, Grades.Gm, IntValue: 5));
        Assert.Equal(before, await Audits());

        await Matrix(null, new RuleMatrixCell(RuleKeys.AdvanceBookingDays, Grades.Gm, IntValue: 10));
        Assert.Equal(before + 1, await Audits());

        var general = await Rules.GetMatrixAsync(null);
        var cell = Assert.Single(general.Cells);
        Assert.Equal(10, cell.IntValue);
        Assert.Equal(3, Assert.Single((await Rules.GetMatrixAsync(Fx.World.BuildingId)).Cells).IntValue);
    }

    [Fact]
    public async Task Tek_alanli_guncelleme_oda_kurallarini_kilitleyemez()
    {
        await Reset();
        var actor = Actor(w.Admin);
        await Assert.ThrowsAsync<AppException>(() => Rules.UpdateAsync(new UpdateRuleCommand(RuleKeys.RoomWorkingHours, IntValue: Optional<int?>.Of(1200)), actor));
        await Assert.ThrowsAsync<AppException>(() => Rules.UpdateAsync(new UpdateRuleCommand(RuleKeys.RoomWorkingHours, SecondValue: Optional<int?>.Of(300)), actor));
        await Assert.ThrowsAsync<AppException>(() => Rules.UpdateAsync(new UpdateRuleCommand(RuleKeys.RoomMaxDurationMinutes, IntValue: Optional<int?>.Of(null)), actor));
        await Assert.ThrowsAsync<AppException>(() => Rules.UpdateAsync(new UpdateRuleCommand(RuleKeys.RoomSlotMinutes, IntValue: Optional<int?>.Of(45)), actor));
    }

    [Fact]
    public async Task Tatil_mukerrer_eklenemez_ve_silinebilir()
    {
        await Reset();
        var holidays = Fx.Svc<HolidayService>();
        var holiday = await holidays.CreateAsync("2027-01-01", "Yılbaşı", Actor(w.Admin));
        await Assert.ThrowsAsync<AppException>(() => holidays.CreateAsync("2027-01-01", "Tekrar", Actor(w.Admin)));
        await holidays.DeleteAsync(holiday.Id, Actor(w.Admin));
        Assert.Empty(await holidays.ListAsync());
    }

    [Fact]
    public async Task Form_komutu_degerleri_yukler_ve_admin_degisikligini_ezmez()
    {
        await Reset();
        EkBinaRuleReport first;
        await using (var db = Fx.Db()) first = await EkBinaRuleSeeder.ApplyAsync(db);
        Assert.Equal(5, first.DefaultsUpdated);
        Assert.Equal(10, first.OverridesAdded);
        Assert.Equal(7, first.HolidaysAdded);

        var settings = await Rules.GetRuleSettingsAsync();
        Assert.Equal(2, settings.AdvanceBookingDays.Days);
        Assert.Equal(2, settings.WeeklyDeskLimit.Limit);
        Assert.Equal(540, settings.RoomMaxDurationMinutes.Minutes);
        Assert.Equal(30, settings.RoomSlotMinutes.Minutes);
        Assert.True(settings.WorkingDaysOnly.Enabled);

        var gm = await Rules.GetRuleSettingsForUserAsync((await User(Grades.Gm)).Id, null);
        Assert.Equal(15, gm.AdvanceBookingDays.Days);
        Assert.False(gm.WorkingDaysOnly.Enabled);
        var manager = await Rules.GetRuleSettingsForUserAsync((await User(Grades.Manager)).Id, null);
        Assert.Equal(5, manager.AdvanceBookingDays.Days);
        var assistant = await Rules.GetRuleSettingsForUserAsync((await User(Grades.AssistantManager)).Id, null);
        Assert.Equal(2, assistant.AdvanceBookingDays.Days);
        Assert.True(assistant.WorkingDaysOnly.Enabled);

        // Admin degisiklikleri: varsayilan, calisma gunu kurali ve tek hucre.
        await SetBase(RuleKeys.AdvanceBookingDays, 7);
        await Rules.UpdateAsync(new UpdateRuleCommand(RuleKeys.WorkingDaysOnly, Enabled: false), Actor(w.Admin));
        await using (var db = Fx.Db())
        {
            (await db.ReservationRuleOverrides.SingleAsync(o => o.RuleKey == RuleKeys.AdvanceBookingDays && o.Grade == Grades.Gm)).IntValue = 20;
            await db.SaveChangesAsync();
        }

        EkBinaRuleReport second;
        await using (var db = Fx.Db()) second = await EkBinaRuleSeeder.ApplyAsync(db);
        Assert.Equal(0, second.DefaultsUpdated);
        Assert.Equal(0, second.OverridesAdded);
        Assert.Equal(0, second.HolidaysAdded);

        var after = await Rules.GetRuleSettingsAsync();
        Assert.Equal(7, after.AdvanceBookingDays.Days);
        Assert.False(after.WorkingDaysOnly.Enabled);
        Assert.Equal(20, (await Rules.GetRuleSettingsForUserAsync((await User(Grades.Gm)).Id, null)).AdvanceBookingDays.Days);
    }
}
