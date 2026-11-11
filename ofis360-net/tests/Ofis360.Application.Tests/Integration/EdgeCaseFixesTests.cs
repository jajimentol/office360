using Ofis360.Application.Common;
using Ofis360.Application.Identity;
using Ofis360.Application.Services;
using Ofis360.Application.Tests.Fixtures;
using Ofis360.Application.Validation;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;
using static Ofis360.Application.Tests.Fixtures.TestHelpers;

namespace Ofis360.Application.Tests.Integration;

public sealed class EdgeCaseWorld : IAsyncLifetime
{
    public WorldFixture Fx { get; } = new();
    public SessionUser Admin { get; private set; } = null!;
    public SessionUser Secretary { get; private set; } = null!;
    public string InactiveBuildingFloorId { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await Fx.InitializeAsync();
        Admin = await Fx.CreateTestUserAsync(Roles.Admin);
        Secretary = await Fx.CreateTestUserAsync();

        await using var db = Fx.Db();
        (await db.Users.FindAsync(Secretary.Id))!.IsSecretary = true;
        var building = new Building { Code = "KPT", Name = "Kapalı Bina", Order = 92, Active = false, IsDemo = true };
        var floor = new Floor { BuildingId = building.Id, Number = 77, Name = "Kapalı Binada Kat", Description = "x", Reservable = true };
        db.AddRange(building, floor);
        await db.SaveChangesAsync();
        InactiveBuildingFloorId = floor.Id;
    }

    public Task DisposeAsync() => Fx.DisposeAsync();
}

/// <summary>Orta oncelikli bulgular: muafiyet, haftalik sayim, e-posta tekilligi, kural ve tercih dogrulamalari, kapatma listesi.</summary>
[Collection(OracleCollection.Name)]
public class EdgeCaseFixesTests(EdgeCaseWorld w) : IClassFixture<EdgeCaseWorld>
{
    private static int _offset;
    private static int _mail;

    private WorldFixture Fx => w.Fx;
    private string Today => Fx.Clock.TodayKey();
    private AuditActor Admin => Actor(w.Admin);
    private UserService Users => Fx.Svc<UserService>();
    private ClosureService Closures => Fx.Svc<ClosureService>();

    /// <summary>Rezervasyon penceresinin cok otesinde bir hafta ici.</summary>
    private string FarWeekday()
    {
        var day = DayKeys.AddDays(Today, 40 + 7 * Interlocked.Increment(ref _offset));
        while (DayKeys.IsWeekend(day)) day = DayKeys.AddDays(day, 1);
        return day;
    }

    private string Mail(string prefix) => $"{prefix}{Interlocked.Increment(ref _mail)}.{Guid.NewGuid():N}@ray.test";

    private UserInput NewUser(string email) => new("Deneme Kişi", email, "Uzman", Roles.Employee, Fx.World.DepartmentId);

    private async Task Desk(string code, string dayKey, SessionUser user, string status)
    {
        var deskId = Fx.World.Desks[code];
        var active = status is DeskReservationStatus.Booked or DeskReservationStatus.CheckedIn;
        await using var db = Fx.Db();
        db.DeskReservations.Add(new DeskReservation
        {
            DeskId = deskId, UserId = user.Id, Date = DayKeys.ToDateOnly(dayKey), Status = status,
            ActiveDeskKey = active ? DeskReservation.BuildActiveDeskKey(deskId, dayKey) : null,
            ActiveUserDayKey = active ? DeskReservation.BuildActiveUserDayKey(user.Id, dayKey) : null,
        });
        await db.SaveChangesAsync();
    }

    private async Task<string> InsertClosure(Action<Closure> set)
    {
        var closure = new Closure
        {
            Scope = ClosureScopes.Floor, Reason = ClosureReasons.Maintenance, Note = "Test", CreatedById = w.Admin.Id,
            StartDate = DayKeys.ToDateOnly(DayKeys.AddDays(Today, 200)), EndDate = DayKeys.ToDateOnly(DayKeys.AddDays(Today, 201)),
        };
        set(closure);
        await using var db = Fx.Db();
        db.Closures.Add(closure);
        await db.SaveChangesAsync();
        return closure.Id;
    }

    [Fact]
    public async Task Admin_sekreter_adina_pencere_disi_masa_ayirabilir()
    {
        var row = await Fx.Svc<DeskReservationService>().CreateAsync(
            new CreateDeskReservationCommand(Fx.World.Desks["T-A03"], FarWeekday(), null, w.Secretary.Id), w.Admin);
        Assert.Equal(DeskReservationStatus.Booked, row.Status);
    }

    [Fact]
    public async Task Admin_sekreter_adina_pencere_disi_oda_ayirabilir()
    {
        var row = await Fx.Svc<RoomReservationService>().CreateAsync(
            new CreateRoomReservationCommand(Fx.World.RoomId, FarWeekday(), 600, 660, "Yönetim", "Ekip", 3, w.Secretary.Id), w.Admin);
        Assert.Equal(RoomReservationStatus.Booked, row.Status);
    }

    [Fact]
    public async Task Admin_normal_calisan_adina_pencere_disina_ayiramaz()
    {
        var employee = await Fx.CreateTestUserAsync();
        var error = await ExpectAppErrorAsync(() => Fx.Svc<DeskReservationService>().CreateAsync(
            new CreateDeskReservationCommand(Fx.World.Desks["T-A04"], FarWeekday(), null, employee.Id), w.Admin));
        Assert.Equal(422, error.Status);
    }

    [Fact]
    public async Task Gelmedi_ve_ayni_gun_yeni_kayit_haftada_tek_gun_sayilir()
    {
        var user = await Fx.CreateTestUserAsync();
        await Desk("T-A01", Today, user, DeskReservationStatus.NoShow);
        await Desk("T-A02", Today, user, DeskReservationStatus.Booked);
        var weekStart = DayKeys.StartOfWeek(Today);
        var otherDay = weekStart == Today ? DayKeys.AddDays(Today, 1) : weekStart;

        var home = await Fx.Svc<PersonalService>().GetEmployeeHomeSummaryAsync(user.Id);
        var context = await Fx.Svc<AvailabilityService>().BuildUserDeskContextAsync(user.Id, otherDay);

        Assert.Equal(1, home.FairUse.WeeklyUsed);
        Assert.Equal(1, context.WeeklyReservationCount);
    }

    [Fact]
    public async Task Ayni_eposta_buyuk_harfle_eklenemez()
    {
        var email = Mail("kisi");
        await Users.CreateAsync(NewUser(email), Admin);

        var error = await ExpectAppErrorAsync(() => Users.CreateAsync(NewUser(email.ToUpperInvariant()), Admin));

        Assert.Equal(409, error.Status);
    }

    [Fact]
    public async Task Eposta_kucuk_harfle_saklanir()
    {
        var email = Mail("Karisik");
        var user = await Users.CreateAsync(NewUser(email), Admin);
        Assert.Equal(email.ToLowerInvariant(), user.Email);
    }

    [Fact]
    public async Task Baskasinin_epostasi_buyuk_harfle_alinamaz_kendi_harfi_degisebilir()
    {
        var (first, second) = (Mail("birinci"), Mail("ikinci"));
        await Users.CreateAsync(NewUser(first), Admin);
        var user = await Users.CreateAsync(NewUser(second), Admin);

        var error = await ExpectAppErrorAsync(() => Users.UpdateAsync(user.Id, new UserUpdateInput(Email: first.ToUpperInvariant()), Admin));
        Assert.Equal(409, error.Status);

        var same = await Users.UpdateAsync(user.Id, new UserUpdateInput(Email: second.ToUpperInvariant()), Admin);
        Assert.Equal(second.ToLowerInvariant(), same.Email);
    }

    [Fact]
    public async Task Gelmedi_saati_0000_kaydedilemez()
    {
        var error = await ExpectAppErrorAsync(() => Fx.Svc<RuleService>().UpdateAsync(new UpdateRuleCommand(RuleKeys.NoShowRelease, SecondValue: Optional<int?>.Of(0)), Admin));
        Assert.Equal(400, error.Status);
    }

    [Fact]
    public async Task Olmayan_kat_tercih_edilemez()
    {
        var user = await Fx.CreateTestUserAsync();
        var error = await ExpectAppErrorAsync(() => Users.UpdateOwnPreferencesAsync(user.Id, new PreferencesInput(Optional<string?>.Of("yok"))));
        Assert.Equal(400, error.Status);
    }

    [Fact]
    public async Task Cok_uzun_kat_tercihi_hata_vermeden_reddedilir()
    {
        var user = await Fx.CreateTestUserAsync();
        var error = await ExpectAppErrorAsync(() => Users.UpdateOwnPreferencesAsync(user.Id, new PreferencesInput(Optional<string?>.Of(new string('x', 5000)))));
        Assert.Equal(400, error.Status);
    }

    [Fact]
    public async Task Pasif_binadaki_kat_tercih_edilemez()
    {
        var user = await Fx.CreateTestUserAsync();
        var error = await ExpectAppErrorAsync(() => Users.UpdateOwnPreferencesAsync(user.Id, new PreferencesInput(Optional<string?>.Of(w.InactiveBuildingFloorId))));
        Assert.Equal(400, error.Status);
    }

    [Fact]
    public async Task Gecerli_kat_tercih_edilir_ve_temizlenebilir()
    {
        var user = await Fx.CreateTestUserAsync();

        var saved = await Users.UpdateOwnPreferencesAsync(user.Id, new PreferencesInput(Optional<string?>.Of(Fx.World.FloorId)));
        Assert.Equal(Fx.World.FloorId, saved.PreferredFloorId);

        var cleared = await Users.UpdateOwnPreferencesAsync(user.Id, new PreferencesInput(Optional<string?>.Of(null)));
        Assert.Null(cleared.PreferredFloorId);
    }

    [Fact]
    public async Task Kapatma_listesinde_pasif_bina_gorunmez()
    {
        var id = await InsertClosure(c => c.FloorId = w.InactiveBuildingFloorId);
        Assert.DoesNotContain(id, (await Closures.ListAsync()).Select(c => c.Id));
    }

    [Fact]
    public async Task Yururluktekiler_listesinde_suresi_gecen_kapatma_yok()
    {
        var expired = await InsertClosure(c =>
        {
            c.FloorId = Fx.World.FloorId;
            c.StartDate = DayKeys.ToDateOnly(DayKeys.AddDays(Today, -9));
            c.EndDate = DayKeys.ToDateOnly(DayKeys.AddDays(Today, -1));
        });
        var running = await InsertClosure(c => c.FloorId = Fx.World.FloorId);

        var active = (await Closures.ListAsync(active: true)).Select(c => c.Id).ToList();
        var all = (await Closures.ListAsync()).Select(c => c.Id).ToList();

        Assert.DoesNotContain(expired, active);
        Assert.Contains(running, active);
        Assert.Contains(expired, all);
    }
}
