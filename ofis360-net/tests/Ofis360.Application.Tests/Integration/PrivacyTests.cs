using Ofis360.Application.Common;
using Ofis360.Application.Identity;
using Ofis360.Application.Services;
using Ofis360.Application.Tests.Fixtures;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;
using static Ofis360.Application.Tests.Fixtures.TestHelpers;

namespace Ofis360.Application.Tests.Integration;

public sealed class PrivacyWorld : IAsyncLifetime
{
    public WorldFixture Fx { get; } = new();
    public string InactiveFloorId { get; private set; } = string.Empty;
    public string InactiveBuildingFloorId { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await Fx.InitializeAsync();
        await using var db = Fx.Db();
        var floor = new Floor { BuildingId = Fx.World.BuildingId, Number = 88, Name = "Pasif Kat", Description = "x", Reservable = true, Active = false };
        var building = new Building { Code = "PSF", Name = "Pasif Bina", Order = 91, Active = false, IsDemo = true };
        var hidden = new Floor { BuildingId = building.Id, Number = 89, Name = "Pasif Binada Kat", Description = "x", Reservable = true };
        db.AddRange(floor, building, hidden);
        await db.SaveChangesAsync();
        InactiveFloorId = floor.Id;
        InactiveBuildingFloorId = hidden.Id;
    }

    public Task DisposeAsync() => Fx.DisposeAsync();
}

/// <summary>Calisanlar birbirinin gecmis masa kaydini ve toplanti ayrintisini goremez; pasif kat plani okunamaz.</summary>
[Collection(OracleCollection.Name)]
public class PrivacyTests(PrivacyWorld w) : IClassFixture<PrivacyWorld>
{
    private static int _offset;

    private WorldFixture Fx => w.Fx;
    private string Today => Fx.Clock.TodayKey();
    private AvailabilityService Plans => Fx.Svc<AvailabilityService>();
    private RoomReservationService Rooms => Fx.Svc<RoomReservationService>();

    private string PastDay() => DayKeys.AddDays(Today, -100 - Interlocked.Increment(ref _offset));
    private string FutureDay() => DayKeys.AddDays(Today, 100 + Interlocked.Increment(ref _offset));

    private async Task Desk(string deskCode, string dayKey, SessionUser user)
    {
        var deskId = Fx.World.Desks[deskCode];
        await using var db = Fx.Db();
        db.DeskReservations.Add(new DeskReservation
        {
            DeskId = deskId, UserId = user.Id, Date = DayKeys.ToDateOnly(dayKey), Status = DeskReservationStatus.Completed,
        });
        await db.SaveChangesAsync();
    }

    private async Task Room(string dayKey, SessionUser user, string title = "Gizli birlesme gorusmesi")
    {
        await using var db = Fx.Db();
        db.RoomReservations.Add(new RoomReservation
        {
            RoomId = Fx.World.RoomId, UserId = user.Id, Date = DayKeys.ToDateOnly(dayKey), Status = RoomReservationStatus.Booked,
            Title = title, Purpose = "Ekip", AttendeeCount = 4, StartMinutes = 600, EndMinutes = 660,
            StartAt = new DateTimeOffset(DayKeys.WithMinutes(dayKey, 600), TimeSpan.Zero),
            EndAt = new DateTimeOffset(DayKeys.WithMinutes(dayKey, 660), TimeSpan.Zero),
        });
        await db.SaveChangesAsync();
    }

    private async Task<DeskView> PlanDesk(string dayKey, SessionUser viewer, bool managing = false)
    {
        var plan = await Plans.GetFloorPlanAsync(Fx.World.FloorId, dayKey, RuleUser(viewer), viewerIsManaging: managing);
        return plan.Desks.Single(d => d.Code == "T-A01");
    }

    private async Task<RoomBusySlot> Slot(string dayKey, SessionUser viewer, bool managing = false)
    {
        var result = await Rooms.GetAvailabilityAsync(new RoomAvailabilityQuery(dayKey, FloorId: Fx.World.FloorId, UserId: viewer.Id, ViewerIsManaging: managing));
        return result.Rooms.Single(r => r.Id == Fx.World.RoomId).BusySlots.Single();
    }

    [Fact]
    public async Task Gecmis_tarihli_planda_baskasinin_adi_gorunmez_masa_dolu_kalir()
    {
        var day = PastDay();
        var (owner, other) = (await Fx.CreateTestUserAsync(), await Fx.CreateTestUserAsync());
        await Desk("T-A01", day, owner);

        var desk = await PlanDesk(day, other);

        Assert.Null(desk.Occupant);
        Assert.Equal(DeskViewStates.Reserved, desk.State);
    }

    [Fact]
    public async Task Gecmis_tarihte_doluluk_ozeti_gizlemeden_etkilenmez()
    {
        var day = PastDay();
        var (owner, other) = (await Fx.CreateTestUserAsync(), await Fx.CreateTestUserAsync());
        await Desk("T-A01", day, owner);

        var plan = await Plans.GetFloorPlanAsync(Fx.World.FloorId, day, RuleUser(other));

        Assert.Equal(1, plan.Summary.Reserved);
    }

    [Fact]
    public async Task Kisi_gecmis_tarihte_kendi_kaydini_gorur()
    {
        var day = PastDay();
        var owner = await Fx.CreateTestUserAsync();
        await Desk("T-A01", day, owner);

        var desk = await PlanDesk(day, owner);

        Assert.Equal(owner.FullName, desk.Occupant?.FullName);
        Assert.Equal(DeskViewStates.Mine, desk.State);
    }

    [Fact]
    public async Task Yonetim_yetkilisi_gecmis_tarihte_adi_gorur()
    {
        var day = PastDay();
        var (owner, admin) = (await Fx.CreateTestUserAsync(), await Fx.CreateTestUserAsync(Roles.Admin));
        await Desk("T-A01", day, owner);

        Assert.Equal(owner.FullName, (await PlanDesk(day, admin, managing: true)).Occupant?.FullName);
    }

    [Fact]
    public async Task Bugun_ve_ileri_tarihte_ad_gorunmeye_devam_eder()
    {
        var day = FutureDay();
        var (owner, other) = (await Fx.CreateTestUserAsync(), await Fx.CreateTestUserAsync());
        await Desk("T-A01", day, owner);

        Assert.Equal(owner.FullName, (await PlanDesk(day, other)).Occupant?.FullName);
    }

    [Fact]
    public async Task Baskasinin_toplantisi_yalnizca_dolu_gorunur()
    {
        var day = FutureDay();
        var (owner, other) = (await Fx.CreateTestUserAsync(), await Fx.CreateTestUserAsync());
        await Room(day, owner);

        var slot = await Slot(day, other);

        Assert.Equal("Dolu", slot.Title);
        Assert.Null(slot.OwnerName);
        Assert.Equal(0, slot.AttendeeCount);
        Assert.Equal(600, slot.StartMinutes);
        Assert.Equal(660, slot.EndMinutes);
    }

    [Fact]
    public async Task Doluluk_saati_gizlemeden_etkilenmez()
    {
        var day = FutureDay();
        var (owner, other) = (await Fx.CreateTestUserAsync(), await Fx.CreateTestUserAsync());
        await Room(day, owner);

        var result = await Rooms.GetAvailabilityAsync(new RoomAvailabilityQuery(day, FloorId: Fx.World.FloorId, UserId: other.Id));

        Assert.DoesNotContain(result.Rooms.Single(r => r.Id == Fx.World.RoomId).FreeSlots, s => s.StartMinutes < 660 && s.EndMinutes > 600);
    }

    [Fact]
    public async Task Kisi_kendi_toplantisinin_ayrintisini_gorur()
    {
        var day = FutureDay();
        var owner = await Fx.CreateTestUserAsync();
        await Room(day, owner);

        var slot = await Slot(day, owner);

        Assert.Equal("Gizli birlesme gorusmesi", slot.Title);
        Assert.Equal(owner.FullName, slot.OwnerName);
    }

    [Fact]
    public async Task Yonetim_yetkilisi_toplanti_ayrintisini_gorur()
    {
        var day = FutureDay();
        var (owner, admin) = (await Fx.CreateTestUserAsync(), await Fx.CreateTestUserAsync(Roles.Admin));
        await Room(day, owner);

        var slot = await Slot(day, admin, managing: true);

        Assert.Equal("Gizli birlesme gorusmesi", slot.Title);
        Assert.Equal(owner.FullName, slot.OwnerName);
    }

    [Fact]
    public async Task Calisan_pasif_katin_planini_okuyamaz()
    {
        var user = await Fx.CreateTestUserAsync();
        var error = await ExpectAppErrorAsync(() => Plans.GetFloorPlanAsync(w.InactiveFloorId, FutureDay(), RuleUser(user)));
        Assert.Equal(404, error.Status);
    }

    [Fact]
    public async Task Calisan_pasif_binadaki_katin_planini_okuyamaz()
    {
        var user = await Fx.CreateTestUserAsync();
        var error = await ExpectAppErrorAsync(() => Plans.GetFloorPlanAsync(w.InactiveBuildingFloorId, FutureDay(), RuleUser(user)));
        Assert.Equal(404, error.Status);
    }

    [Fact]
    public async Task Rezervasyon_yonetimi_yetkisi_pasif_kati_acmaz()
    {
        var secretary = await Fx.CreateTestUserAsync();
        var error = await ExpectAppErrorAsync(() => Plans.GetFloorPlanAsync(w.InactiveFloorId, FutureDay(), RuleUser(secretary), viewerIsManaging: true));
        Assert.Equal(404, error.Status);
    }

    [Fact]
    public async Task Admin_pasif_kati_gorebilir()
    {
        var admin = await Fx.CreateTestUserAsync(Roles.Admin);
        var plan = await Plans.GetFloorPlanAsync(w.InactiveFloorId, FutureDay(), RuleUser(admin), includeInactive: true);
        Assert.Equal(w.InactiveFloorId, plan.Floor.Id);
    }
}
