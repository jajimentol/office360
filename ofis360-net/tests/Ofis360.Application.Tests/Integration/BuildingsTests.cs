using Microsoft.EntityFrameworkCore;
using Ofis360.Application.Common;
using Ofis360.Application.Identity;
using Ofis360.Application.Services;
using Ofis360.Application.Tests.Fixtures;
using Ofis360.Application.Validation;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;
using static Ofis360.Application.Tests.Fixtures.TestHelpers;

namespace Ofis360.Application.Tests.Integration;

/// <summary>Kaynak: tests/integration/buildings.test.ts (9 test). Pasif bina calisan deneyiminden ve raporlardan duser, verisi korunur.</summary>
public sealed class BuildingsWorld : IAsyncLifetime
{
    public WorldFixture Fx { get; } = new();
    public SessionUser Admin { get; private set; } = null!;
    public SessionUser Employee { get; private set; } = null!;
    public string HiddenBuildingId { get; private set; } = string.Empty;
    public string HiddenFloorId { get; private set; } = string.Empty;
    public string HiddenDeskId { get; private set; } = string.Empty;
    public string HiddenRoomId { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await Fx.InitializeAsync();
        Admin = await Fx.CreateTestUserAsync(Roles.Admin);
        Employee = await Fx.CreateTestUserAsync();

        await using var db = Fx.Db();
        var hidden = new Building { Code = "DEMO", Name = "Demo Bina", Order = 90, Active = false, IsDemo = true };
        var floor = new Floor { BuildingId = hidden.Id, Number = 1, Name = "1. Kat", Description = "Demo", Reservable = true };
        var zone = new Zone { FloorId = floor.Id, Code = "A", Name = "Demo Bölge", Kind = "WORK", X = 20, Y = 20, Width = 300, Height = 200 };
        var desk = new Desk { Code = "DEMO-D01", FloorId = floor.Id, ZoneId = zone.Id, X = 80, Y = 80 };
        var room = new MeetingRoom { Code = "DEMO-R01", Name = "Demo Oda", FloorId = floor.Id, Capacity = 6, X = 400, Y = 40, Width = 200, Height = 120 };
        db.AddRange(hidden, floor, zone, desk, room);
        await db.SaveChangesAsync();
        HiddenBuildingId = hidden.Id; HiddenFloorId = floor.Id; HiddenDeskId = desk.Id; HiddenRoomId = room.Id;
    }

    public Task DisposeAsync() => Fx.DisposeAsync();
}

[Collection(OracleCollection.Name)]
[TestCaseOrderer(PriorityOrderer.TypeName, PriorityOrderer.AssemblyName)]
public class BuildingsTests(BuildingsWorld bw) : IClassFixture<BuildingsWorld>
{
    private WorldFixture Fx => bw.Fx;
    private TestWorld World => bw.Fx.World;
    private string Today => bw.Fx.Clock.TodayKey();
    private string Tomorrow => DayKeys.AddDays(Today, 1);

    [Fact, Order(1)]
    public async Task Ayni_kat_numarasi_iki_farkli_binada_tanimlanabilir()
    {
        // Test binasinda 1. kat zaten var -> catisma
        var error = await ExpectAppErrorAsync(() => Fx.Svc<SpaceService>().CreateFloorAsync(
            new FloorInput(World.BuildingId, 1, "1. Kat (kopya)", ""), Actor(bw.Admin)));
        Assert.Equal(409, error.Status);

        var other = await Fx.Svc<SpaceService>().CreateFloorAsync(
            new FloorInput(bw.HiddenBuildingId, 4, "4. Kat", "Demo binada 4. kat"), Actor(bw.Admin));
        Assert.Equal(bw.HiddenBuildingId, other.BuildingId);
        Assert.Equal(4, other.Number);
    }

    [Fact, Order(2)]
    public async Task Kat_listesi_binayla_birlikte_ve_bina_sirasina_gore_doner()
    {
        var floors = await Fx.Svc<SpaceService>().ListFloorsAsync();
        Assert.All(floors, f => Assert.NotNull(f.Floor.Building));
        var orders = floors.Select(f => f.Floor.Building.Order).ToList();
        Assert.Equal(orders.OrderBy(o => o).ToList(), orders);

        var activeOnly = await Fx.Svc<SpaceService>().ListFloorsAsync(onlyActiveBuildings: true);
        Assert.DoesNotContain(activeOnly, f => f.Floor.BuildingId == bw.HiddenBuildingId);
    }

    [Fact, Order(3)]
    public async Task Bina_listesi_varsayilan_olarak_yalnizca_aktif_binalari_verir()
    {
        var active = await Fx.Svc<SpaceService>().ListBuildingsAsync();
        Assert.DoesNotContain(bw.HiddenBuildingId, active.Select(b => b.Building.Id));
        var all = await Fx.Svc<SpaceService>().ListBuildingsAsync(includeInactive: true);
        Assert.Contains(bw.HiddenBuildingId, all.Select(b => b.Building.Id));
    }

    [Fact, Order(4)]
    public async Task Kat_ozetinde_pasif_binanin_katlari_yer_almaz()
    {
        var summaries = await Fx.Svc<AvailabilityService>().GetFloorSummariesAsync(Tomorrow);
        Assert.DoesNotContain(bw.HiddenFloorId, summaries.Select(s => s.Id));
        Assert.All(summaries, s => Assert.NotEmpty(s.BuildingName));
    }

    [Fact, Order(5)]
    public async Task Pasif_binadaki_masa_rezerve_edilemez_gerekce_bina_kuralidir()
    {
        var error = await ExpectAppErrorAsync(() => Fx.Svc<DeskReservationService>().CreateAsync(new CreateDeskReservationCommand(bw.HiddenDeskId, Tomorrow), bw.Employee));
        Assert.Contains("Demo Bina", error.Message);
        Assert.Contains("açık değildir", error.Message);
    }

    [Fact, Order(6)]
    public async Task Pasif_binadaki_oda_rezerve_edilemez()
    {
        var error = await ExpectAppErrorAsync(() => Fx.Svc<RoomReservationService>().CreateAsync(
            new CreateRoomReservationCommand(bw.HiddenRoomId, Tomorrow, 10 * 60, 11 * 60, "Demo toplantı", "Test", 3), bw.Employee));
        Assert.Contains("açık değildir", error.Message);
    }

    [Fact, Order(7)]
    public async Task Kat_plani_binayi_tasir_ve_pasif_binada_rezervasyona_kapalidir()
    {
        var plan = await Fx.Svc<AvailabilityService>().GetFloorPlanAsync(bw.HiddenFloorId, Tomorrow, RuleUser(bw.Employee), includeInactive: true);
        Assert.Equal("Demo Bina", plan.Floor.BuildingName);
        Assert.False(plan.Floor.BuildingActive);
        Assert.False(plan.Floor.Reservable);
        Assert.False(plan.Desks[0].Selectable);
        Assert.Contains(plan.Desks[0].Violations, v => v.Code == "BUILDING_INACTIVE");
    }

    [Fact, Order(8)]
    public async Task Raporlar_pasif_binanin_masalarini_ve_rezervasyonlarini_saymaz()
    {
        await using var db = Fx.Db();
        // Pasif binaya dogrudan (kural motoru disinda) bir rezervasyon yazilir: gecmis demo verisi gibi.
        db.DeskReservations.Add(new DeskReservation { DeskId = bw.HiddenDeskId, UserId = bw.Employee.Id, Date = DayKeys.ToDateOnly(Today), Status = "COMPLETED", Source = "SEED" });
        await db.SaveChangesAsync();

        var svc = Fx.Svc<AnalyticsService>();
        var (from, to) = svc.DefaultRange();
        var analytics = await svc.GetAnalyticsAsync(new AnalyticsFilters(from, to));
        Assert.DoesNotContain(bw.HiddenFloorId, analytics.FloorPressure.Select(f => f.FloorId));
        var hiddenDeskCount = await db.Desks.CountAsync(d => d.FloorId == bw.HiddenFloorId);
        var placedActive = await db.Desks.CountAsync(d => d.Placed && d.Active);
        Assert.True(analytics.Kpis.TotalUsableDesks < placedActive + 1 - hiddenDeskCount + 1);
    }

    [Fact, Order(9)]
    public async Task Bina_yeniden_acildiginda_katlari_geri_doner_veri_silinmemistir()
    {
        await Fx.Svc<SpaceService>().UpdateBuildingAsync(bw.HiddenBuildingId, new BuildingUpdateInput(Active: true), Actor(bw.Admin));
        var summaries = await Fx.Svc<AvailabilityService>().GetFloorSummariesAsync(Tomorrow);
        Assert.Contains(bw.HiddenFloorId, summaries.Select(s => s.Id));

        await using var db = Fx.Db();
        Assert.Equal(1, await db.Desks.CountAsync(d => d.FloorId == bw.HiddenFloorId));
        Assert.Equal(1, await db.DeskReservations.CountAsync(r => r.DeskId == bw.HiddenDeskId));

        var plan = await Fx.Svc<AvailabilityService>().GetFloorPlanAsync(bw.HiddenFloorId, Tomorrow, RuleUser(bw.Employee));
        Assert.True(plan.Desks[0].Selectable);

        // Testin geri kalani icin tekrar kapatilir.
        await Fx.Svc<SpaceService>().UpdateBuildingAsync(bw.HiddenBuildingId, new BuildingUpdateInput(Active: false), Actor(bw.Admin));
    }
}
