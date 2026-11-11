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

/// <summary>Kaynak: tests/integration/capacity-scale.test.ts (15 test). Masa havuzu, toplu yerlestirme, sayaclar, sayfalama, olcekli metrikler.</summary>
public sealed class CapacityWorld : IAsyncLifetime
{
    public WorldFixture Fx { get; } = new();
    public SessionUser Admin { get; private set; } = null!;
    public SessionUser Employee { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Fx.InitializeAsync();
        Admin = await Fx.CreateTestUserAsync(Roles.Admin);
        Employee = await Fx.CreateTestUserAsync();
    }

    public Task DisposeAsync() => Fx.DisposeAsync();
}

[Collection(OracleCollection.Name)]
[TestCaseOrderer(PriorityOrderer.TypeName, PriorityOrderer.AssemblyName)]
public class CapacityScaleTests(CapacityWorld cw) : IClassFixture<CapacityWorld>
{
    private WorldFixture Fx => cw.Fx;
    private TestWorld World => cw.Fx.World;
    private string Today => cw.Fx.Clock.TodayKey();

    /// <summary>Havuzda bekleyen (krokiye yerlestirilmemis) bir masa olusturur.</summary>
    private async Task<Desk> CreatePooledDeskAsync(string code)
    {
        await using var db = Fx.Db();
        var zone = await db.Zones.FirstAsync(z => z.FloorId == World.FloorId && z.Reservable);
        var desk = new Desk { Code = code, FloorId = World.FloorId, ZoneId = zone.Id, X = 0, Y = 0, Placed = false, Active = true };
        db.Desks.Add(desk);
        await db.SaveChangesAsync();
        return desk;
    }

    [Fact, Order(1)]
    public async Task Mevcut_masalar_varsayilan_olarak_yerlesik_sayilir()
    {
        await using var db = Fx.Db();
        Assert.True((await db.Desks.SingleAsync(d => d.Id == World.Desks["T-A01"])).Placed);
    }

    [Fact, Order(2)]
    public async Task Havuzdaki_masa_editor_goruntusunde_ayri_listelenir()
    {
        var pooled = await CreatePooledDeskAsync("T-POOL01");
        var snapshot = await Fx.Svc<FloorPlanService>().GetSnapshotAsync(World.FloorId);
        Assert.Contains("T-POOL01", snapshot.UnplacedDesks.Select(d => d.Code));
        Assert.DoesNotContain("T-POOL01", snapshot.Desks.Select(d => d.Code));
        Assert.False(pooled.Placed);
    }

    [Fact, Order(3)]
    public async Task Havuzdaki_masa_calisan_kat_planinda_gorunmez()
    {
        var plan = await Fx.Svc<AvailabilityService>().GetFloorPlanAsync(World.FloorId, DayKeys.AddDays(Today, 1), RuleUser(cw.Employee));
        Assert.DoesNotContain("T-POOL01", plan.Desks.Select(d => d.Code));
    }

    [Fact, Order(4)]
    public async Task Havuzdaki_masa_rezerve_edilemez()
    {
        await using var db = Fx.Db();
        var desk = await db.Desks.SingleAsync(d => d.Code == "T-POOL01");
        var error = await ExpectAppErrorAsync(() => Fx.Svc<DeskReservationService>().CreateAsync(new CreateDeskReservationCommand(desk.Id, DayKeys.AddDays(Today, 1)), cw.Employee));
        Assert.Contains("konumlandırılmamıştır", error.Message);
    }

    [Fact, Order(5)]
    public async Task Krokiye_konum_verildiginde_masa_yerlesik_olur_ve_rezervasyona_acilir()
    {
        string deskId;
        await using (var db = Fx.Db()) deskId = (await db.Desks.SingleAsync(d => d.Code == "T-POOL01")).Id;

        await Fx.Svc<FloorPlanService>().SaveAsync(new SavePlanInput(World.FloorId, [], [new DeskPlacementInput(deskId, 300, 220, 46, 34, 0)]), Actor(cw.Admin));

        await using (var db = Fx.Db())
        {
            var updated = await db.Desks.SingleAsync(d => d.Id == deskId);
            Assert.True(updated.Placed);
            Assert.Equal(300, updated.X);
        }

        var plan = await Fx.Svc<AvailabilityService>().GetFloorPlanAsync(World.FloorId, DayKeys.AddDays(Today, 1), RuleUser(cw.Employee));
        var view = plan.Desks.SingleOrDefault(d => d.Code == "T-POOL01");
        Assert.NotNull(view);
        Assert.True(view.Selectable);
        Assert.DoesNotContain(view.Violations, v => v.Code == "DESK_NOT_PLACED");
    }

    [Fact, Order(6)]
    public async Task Havuzdaki_masalari_krokinin_bos_alanlarina_yerlestirir()
    {
        await CreatePooledDeskAsync("T-POOL02");
        await CreatePooledDeskAsync("T-POOL03");

        List<(string Id, int X, int Y)> before;
        await using (var db = Fx.Db())
            before = (await db.Desks.Where(d => d.FloorId == World.FloorId && d.Placed).Select(d => new { d.Id, d.X, d.Y }).ToListAsync()).Select(d => (d.Id, d.X, d.Y)).ToList();

        var result = await Fx.Svc<FloorPlanService>().AutoPlacePoolItemsAsync(World.FloorId, Actor(cw.Admin));
        Assert.True(result.PlacedDesks >= 2);

        await using (var db = Fx.Db())
        {
            var ids = before.Select(b => b.Id).ToList();
            var after = await db.Desks.Where(d => ids.Contains(d.Id)).ToDictionaryAsync(d => d.Id);
            // Mevcut masalar oynatilmaz.
            foreach (var desk in before)
            {
                Assert.Equal(desk.X, after[desk.Id].X);
                Assert.Equal(desk.Y, after[desk.Id].Y);
            }
            Assert.Equal(0, await db.Desks.CountAsync(d => d.FloorId == World.FloorId && !d.Placed));
        }
    }

    [Fact, Order(7)]
    public async Task Yerlestirilen_masalar_birbirleriyle_cakismaz()
    {
        await using var db = Fx.Db();
        var desks = await db.Desks.Where(d => d.FloorId == World.FloorId && d.Placed).ToListAsync();
        for (var i = 0; i < desks.Count; i++)
        {
            for (var j = i + 1; j < desks.Count; j++)
            {
                var a = desks[i];
                var b = desks[j];
                var overlap = Math.Abs(a.X - b.X) < (a.Width + b.Width) / 2.0 && Math.Abs(a.Y - b.Y) < (a.Height + b.Height) / 2.0;
                Assert.False(overlap, $"{a.Code} ve {b.Code} çakışıyor");
            }
        }
    }

    [Fact, Order(8)]
    public async Task Havuz_bosken_yerlestirme_yapmaz()
    {
        var result = await Fx.Svc<FloorPlanService>().AutoPlacePoolItemsAsync(World.FloorId, Actor(cw.Admin));
        Assert.Equal(0, result.PlacedDesks);
        Assert.Equal(0, result.RemainingDesks);
    }

    [Fact, Order(9)]
    public async Task Kat_kapasite_sayaclari_toplam_yerlesik_yerlestirilmemis_ayrimini_verir()
    {
        await CreatePooledDeskAsync("T-POOL04");
        var stats = await Fx.Svc<SpaceService>().GetFloorCapacityStatsAsync();
        var floor = stats.Single(s => s.FloorId == World.FloorId);
        Assert.Equal(floor.PlacedDesks + floor.UnplacedDesks, floor.TotalDesks);
        Assert.Equal(1, floor.UnplacedDesks);
        Assert.Equal(floor.TotalDesks, floor.ActiveDesks + floor.ClosedDesks);
    }

    [Fact, Order(10)]
    public async Task Masa_listesi_yerlesim_durumuna_gore_filtrelenir_ve_sayfalanir()
    {
        var unplaced = await Fx.Svc<SpaceService>().ListDesksAsync(new DeskListFilters(FloorId: World.FloorId, Placement: "unplaced"));
        Assert.Single(unplaced.Items);
        Assert.Equal("T-POOL04", unplaced.Items[0].Desk.Code);

        var firstPage = await Fx.Svc<SpaceService>().ListDesksAsync(new DeskListFilters(FloorId: World.FloorId, Take: 2, Skip: 0));
        Assert.Equal(2, firstPage.Items.Count);
        Assert.True(firstPage.Total > 2);

        var secondPage = await Fx.Svc<SpaceService>().ListDesksAsync(new DeskListFilters(FloorId: World.FloorId, Take: 2, Skip: 2));
        Assert.NotEqual(firstPage.Items[0].Desk.Id, secondPage.Items[0].Desk.Id);
    }

    [Fact, Order(11)]
    public async Task Kullanici_listesi_sayfalanir_ve_toplam_sayi_verilir()
    {
        for (var i = 0; i < 5; i++) await Fx.CreateTestUserAsync();
        var page = await Fx.Svc<UserService>().ListAsync(new UserListFilters(Take: 3, Skip: 0));
        Assert.Equal(3, page.Items.Count);
        Assert.True(page.Total >= 7);
        Assert.Equal(3, page.Take);

        var stats = await Fx.Svc<UserService>().GetStatsAsync();
        Assert.Equal(page.Total, stats.Total);
        Assert.Equal(stats.Total, stats.Active + stats.Passive);
    }

    [Fact, Order(12)]
    public async Task Kullanici_listesi_ada_gore_aranabilir()
    {
        await using var db = Fx.Db();
        var target = await db.Users.FirstAsync(u => u.Role == "ADMIN");
        var result = await Fx.Svc<UserService>().ListAsync(new UserListFilters(Search: target.FullName[..5]));
        Assert.Contains(target.Id, result.Items.Select(u => u.User.Id));
    }

    [Fact, Order(13)]
    public async Task Kapasite_acigi_ve_calisan_sayilari_KPI_olarak_yansir()
    {
        await using (var db = Fx.Db())
        {
            db.CapacityShortfalls.Add(new CapacityShortfall { Date = DayKeys.ToDateOnly(DayKeys.AddDays(Today, -1)), Requests = 40, Fulfilled = 25, Unmet = 15, Source = "SEED" });
            await db.SaveChangesAsync();
        }

        var svc = Fx.Svc<AnalyticsService>();
        var (from, to) = svc.DefaultRange();
        var analytics = await svc.GetAnalyticsAsync(new AnalyticsFilters(from, to));
        Assert.True(analytics.Kpis.TotalEmployees > 0);
        Assert.True(analytics.Kpis.ActiveEmployees <= analytics.Kpis.TotalEmployees);
        Assert.Equal(15, analytics.Kpis.UnmetRequests);
        Assert.Equal(1, analytics.Kpis.UnmetRequestDays);
        Assert.True(analytics.Kpis.TotalUsableDesks > 0);
        Assert.True(analytics.Kpis.AvailableDesksToday <= analytics.Kpis.TotalUsableDesks);
    }

    [Fact, Order(14)]
    public async Task Kat_bazinda_kapasite_baskisi_hesaplanir()
    {
        var svc = Fx.Svc<AnalyticsService>();
        var (from, to) = svc.DefaultRange();
        var analytics = await svc.GetAnalyticsAsync(new AnalyticsFilters(from, to));
        var floor = analytics.FloorPressure.SingleOrDefault(f => f.FloorId == World.FloorId);
        Assert.NotNull(floor);
        Assert.True(floor.Capacity > 0);
        Assert.True(floor.PeakOccupancy >= floor.AverageOccupancy);
        Assert.True(floor.SaturatedDays >= 0);
    }

    [Fact, Order(15)]
    public async Task Departman_gelis_orani_normalize_edilir_ve_kisisel_veri_sizdirmaz()
    {
        var svc = Fx.Svc<AnalyticsService>();
        var (from, to) = svc.DefaultRange();
        var analytics = await svc.GetAnalyticsAsync(new AnalyticsFilters(from, to));
        Assert.All(analytics.DepartmentDistribution, d => Assert.InRange(d.AttendanceRate, 0, 100));
        Assert.All(analytics.Fairness.Distribution, b => Assert.IsType<string>(b.Bucket));
        Assert.DoesNotContain("@", System.Text.Json.JsonSerializer.Serialize(analytics.Fairness));
    }
}
