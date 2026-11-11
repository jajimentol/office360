using Microsoft.EntityFrameworkCore;
using Ofis360.Application.Common;
using Ofis360.Application.Identity;
using Ofis360.Application.Services;
using Ofis360.Application.Tests.Fixtures;
using Ofis360.Domain.Common;
using Ofis360.Seed;
using static Ofis360.Application.Tests.Fixtures.TestHelpers;

namespace Ofis360.Application.Tests.Integration;

/// <summary>Kaynak: tests/integration/palladium.test.ts (5 test) — plandan uretilen verinin tutarliligi ve seed'in idempotentligi.</summary>
public sealed class PalladiumWorld : IAsyncLifetime
{
    public WorldFixture Fx { get; } = new();
    public SessionUser Employee { get; private set; } = null!;
    public string FloorId { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await Fx.InitializeAsync();
        Employee = await Fx.CreateTestUserAsync();
        await using var db = Fx.Db();
        var result = await PalladiumSeeder.SeedAsync(db);
        FloorId = result.Floor.Id;
    }

    public Task DisposeAsync() => Fx.DisposeAsync();
}

[Collection(OracleCollection.Name)]
[TestCaseOrderer(PriorityOrderer.TypeName, PriorityOrderer.AssemblyName)]
public class PalladiumSeedTests(PalladiumWorld pw) : IClassFixture<PalladiumWorld>
{
    private WorldFixture Fx => pw.Fx;
    private string Tomorrow => DayKeys.AddDays(Fx.Clock.TodayKey(), 1);

    [Fact, Order(1)]
    public async Task Bina_kat_bolge_masa_ve_odalari_plandaki_adetlerle_olusturur()
    {
        await using var db = Fx.Db();
        var floor = await db.Floors.Include(f => f.Building).SingleAsync(f => f.Id == pw.FloorId);
        Assert.Equal("PT", floor.Building.Code);
        Assert.True(floor.Building.Active);
        Assert.Equal(36, floor.Number);
        Assert.Equal(PalladiumPlan.Desks.Length, await db.Desks.CountAsync(d => d.FloorId == pw.FloorId));
        Assert.Equal(PalladiumPlan.Rooms.Length, await db.MeetingRooms.CountAsync(r => r.FloorId == pw.FloorId));
        Assert.Equal(PalladiumPlan.Zones.Length, await db.Zones.CountAsync(z => z.FloorId == pw.FloorId));
        Assert.Equal(PalladiumPlan.Elements.Length, await db.FloorPlanElements.CountAsync(e => e.FloorId == pw.FloorId));
        // Plan: 58 masa (18'i bolge mudurlugu alaninda, rezervasyona kapali), 9 toplanti odasi (2x12, 2x8, 5x5 kisi).
        Assert.Equal(58, PalladiumPlan.Desks.Length);
        Assert.Equal([5, 5, 5, 5, 5, 8, 8, 12, 12], PalladiumPlan.Rooms.Select(r => r.Capacity).OrderBy(c => c).ToArray());
    }

    [Fact, Order(2)]
    public async Task Tum_masalar_yerlesik_aktif_plan_icinde_ve_cakismiyor()
    {
        await using var db = Fx.Db();
        var floor = await db.Floors.SingleAsync(f => f.Id == pw.FloorId);
        var desks = await db.Desks.Include(d => d.Zone).Where(d => d.FloorId == pw.FloorId).ToListAsync();

        foreach (var desk in desks)
        {
            Assert.True(desk.Placed, desk.Code);
            Assert.True(desk.Active, desk.Code);
            Assert.True(desk.X - desk.Width / 2.0 >= 0, desk.Code);
            Assert.True(desk.Y - desk.Height / 2.0 >= 0, desk.Code);
            Assert.True(desk.X + desk.Width / 2.0 <= floor.PlanWidth, desk.Code);
            Assert.True(desk.Y + desk.Height / 2.0 <= floor.PlanHeight, desk.Code);
            // Masa merkezi kendi bolgesinin icinde olmali.
            Assert.InRange(desk.X, desk.Zone.X, desk.Zone.X + desk.Zone.Width);
            Assert.InRange(desk.Y, desk.Zone.Y, desk.Zone.Y + desk.Zone.Height);
        }

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

    [Fact, Order(3)]
    public async Task Toplanti_odalari_plan_icinde_ve_masalarla_cakismiyor()
    {
        await using var db = Fx.Db();
        var floor = await db.Floors.SingleAsync(f => f.Id == pw.FloorId);
        var rooms = await db.MeetingRooms.Where(r => r.FloorId == pw.FloorId).ToListAsync();
        var desks = await db.Desks.Where(d => d.FloorId == pw.FloorId).ToListAsync();

        foreach (var room in rooms)
        {
            Assert.True(room.Placed);
            Assert.True(room.X >= 0);
            Assert.True(room.Y >= 0);
            Assert.True(room.X + room.Width <= floor.PlanWidth);
            Assert.True(room.Y + room.Height <= floor.PlanHeight);
            foreach (var desk in desks)
            {
                var inside = desk.X > room.X && desk.X < room.X + room.Width && desk.Y > room.Y && desk.Y < room.Y + room.Height;
                Assert.False(inside, $"{desk.Code} masası {room.Code} odasının içinde");
            }
        }
    }

    [Fact, Order(4)]
    public async Task Yeniden_calistirildiginda_hicbir_sey_eklemez_ve_konumlari_ezmez()
    {
        int originalX, originalY;
        string deskId;
        await using (var db = Fx.Db())
        {
            var desk = await db.Desks.SingleAsync(d => d.Code == "PT36-A05-01");
            deskId = desk.Id;
            originalX = desk.X;
            originalY = desk.Y;
            // Admin masayi elle tasimis olsun.
            desk.X += 40;
            desk.Y += 20;
            await db.SaveChangesAsync();
        }

        await using (var db = Fx.Db())
        {
            var second = await PalladiumSeeder.SeedAsync(db);
            Assert.False(second.Report.BuildingCreated);
            Assert.False(second.Report.FloorCreated);
            Assert.Equal(0, second.Report.ZonesCreated);
            Assert.Equal(0, second.Report.DesksCreated);
            Assert.Equal(0, second.Report.RoomsCreated);
            Assert.True(second.Report.ElementsSkipped);
        }

        await using (var db = Fx.Db())
        {
            var after = await db.Desks.SingleAsync(d => d.Id == deskId);
            Assert.Equal(originalX + 40, after.X);
            Assert.Equal(originalY + 20, after.Y);
            after.X = originalX;
            after.Y = originalY;
            await db.SaveChangesAsync();
        }
    }

    [Fact, Order(5)]
    public async Task Calisan_Palladium_katini_gorur_ve_bir_masayi_rezerve_edebilir()
    {
        var summaries = await Fx.Svc<AvailabilityService>().GetFloorSummariesAsync(Tomorrow);
        var palladium = summaries.SingleOrDefault(s => s.Id == pw.FloorId);
        Assert.NotNull(palladium);
        Assert.Equal("Palladium Tower", palladium.BuildingName);
        Assert.Equal(40, palladium.TotalDesks);
        Assert.Equal(9, palladium.RoomCount);

        var plan = await Fx.Svc<AvailabilityService>().GetFloorPlanAsync(pw.FloorId, Tomorrow, RuleUser(pw.Employee));
        Assert.Equal(58, plan.Desks.Count);
        Assert.Equal(9, plan.Rooms.Count);
        Assert.Contains(plan.Elements, e => e.Type == "TERRACE");
        var target = plan.Desks.Single(d => d.Code == "PT36-B05-05");
        Assert.True(target.Selectable);
        Assert.Contains("WINDOW", target.Features.Select(f => f.Key));

        var reservation = await Fx.Svc<DeskReservationService>().CreateAsync(new CreateDeskReservationCommand(target.Id, Tomorrow), pw.Employee);
        Assert.Equal("BOOKED", reservation.Status);
    }
}
