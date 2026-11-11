using Microsoft.EntityFrameworkCore;
using Ofis360.Application.Common;
using Ofis360.Application.Identity;
using Ofis360.Application.Services;
using Ofis360.Application.Tests.Fixtures;
using Ofis360.Application.Validation;
using Ofis360.Domain.Common;
using static Ofis360.Application.Tests.Fixtures.TestHelpers;

namespace Ofis360.Application.Tests.Integration;

/// <summary>Kaynak: tests/integration/floor-plan.test.ts (20 test) — kat plani editoru kabul kriterleri 1-12.</summary>
public sealed class FloorPlanWorld : IAsyncLifetime
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
[TestCaseOrderer(PriorityOrderer.TypeName, PriorityOrderer.AssemblyName)]
public class FloorPlanTests(FloorPlanWorld pw) : IClassFixture<FloorPlanWorld>
{
    private WorldFixture Fx => pw.Fx;
    private TestWorld World => pw.Fx.World;
    private string Today => pw.Fx.Clock.TodayKey();
    private AuditActor Admin => Actor(pw.Admin);
    private static readonly PlanElementValidator Validator = new();

    private static PlanElementInput Element(
        string type = "ROOM", string? label = "Çalışma alanı", int x = 40, int y = 40, int width = 200, int height = 120,
        int rotation = 0, int layerOrder = 10, bool locked = false, bool visible = true, string? linkedResourceId = null, string? linkedResourceType = null,
        string? id = null) => new(
        id ?? $"oge-{Guid.NewGuid().ToString("N")[..7]}", type, label, x, y, width, height, rotation,
        new PlanStyleInput("#ffffff", "#94a3b8", 2), layerOrder, locked, visible, linkedResourceId, linkedResourceType);

    private static PlanElementInput ToInput(Domain.Plan.PlanElement e) => new(
        e.Id, e.Type, e.Label, e.X, e.Y, e.Width, e.Height, e.Rotation,
        new PlanStyleInput(e.Style.Fill, e.Style.Stroke, e.Style.StrokeWidth, e.Style.FontSize), e.LayerOrder, e.Locked, e.Visible, e.LinkedResourceId, e.LinkedResourceType);

    private Task<SavePlanResult> SaveAsync(string floorId, IReadOnlyList<PlanElementInput> elements,
        IReadOnlyList<DeskPlacementInput>? desks = null, IReadOnlyList<RoomPlacementInput>? rooms = null) =>
        Fx.Svc<FloorPlanService>().SaveAsync(new SavePlanInput(floorId, elements, desks, rooms), Admin);

    private Task<List<Domain.Plan.PlanElement>> ElementsAsync(string floorId) => Fx.Svc<FloorPlanService>().GetPlanElementsAsync(floorId);

    [Fact, Order(1)]
    public async Task Bos_bir_kata_kroki_ogeleri_eklenebilir()
    {
        Assert.Empty(await ElementsAsync(World.FloorId));

        var result = await SaveAsync(World.FloorId,
        [
            Element(type: "WALL", label: null, x: 0, y: 0, width: 1000, height: 12, layerOrder: 30),
            Element(type: "ROOM", label: "A Bölgesi", x: 40, y: 40, width: 300, height: 200),
            Element(type: "CORRIDOR", label: "Ana koridor", x: 40, y: 260, width: 500, height: 56),
            Element(type: "DOOR", label: null, x: 60, y: 250, width: 44, height: 12),
            Element(type: "ELEVATOR", label: "Asansör", x: 600, y: 60, width: 100, height: 90),
            Element(type: "KITCHEN", label: "Mutfak", x: 600, y: 200, width: 200, height: 140),
            Element(type: "TEXT", label: "Giriş katı", x: 40, y: 10, width: 160, height: 24),
        ]);

        Assert.Equal(7, result.CreatedCount);
        Assert.Equal(0, result.DeletedCount);

        var saved = await ElementsAsync(World.FloorId);
        Assert.Equal(7, saved.Count);
        Assert.Contains("WALL", saved.Select(e => e.Type));
        Assert.Contains("ELEVATOR", saved.Select(e => e.Type));
        Assert.Equal("A Bölgesi", saved.Single(e => e.Type == "ROOM").Label);
        // Stil JSON olarak saklanir ve geri cozulur.
        Assert.Equal(2, saved[0].Style.StrokeWidth);
    }

    [Fact, Order(2)]
    public async Task Mevcut_oge_guncellenir_kaldirilan_oge_silinir()
    {
        var current = await ElementsAsync(World.FloorId);
        var room = current.Single(e => e.Type == "ROOM");
        var withoutText = current.Where(e => e.Type != "TEXT").ToList();

        var result = await SaveAsync(World.FloorId, withoutText.Select(e => e.Id == room.Id
            ? ToInput(e) with { Label = "Yeni ad", X = 100, Y = 100, Width = 260, Height = 180 }
            : ToInput(e)).ToList());

        Assert.Equal(1, result.DeletedCount);
        Assert.Equal(6, result.UpdatedCount);

        var saved = await ElementsAsync(World.FloorId);
        Assert.Equal(6, saved.Count);
        Assert.DoesNotContain(saved, e => e.Type == "TEXT");
        var updated = saved.Single(e => e.Id == room.Id);
        Assert.Equal("Yeni ad", updated.Label);
        Assert.Equal(100, updated.X);
        Assert.Equal(260, updated.Width);
    }

    [Fact, Order(3)]
    public async Task Kilit_gorunurluk_katman_sirasi_ve_donus_acisi_korunur()
    {
        var current = await ElementsAsync(World.FloorId);
        await SaveAsync(World.FloorId, current.Select((e, index) => index == 0
            ? ToInput(e) with { Locked = true, Visible = false, LayerOrder = 99, Rotation = 45 }
            : ToInput(e)).ToList());

        var changed = (await ElementsAsync(World.FloorId)).Single(e => e.Id == current[0].Id);
        Assert.True(changed.Locked);
        Assert.False(changed.Visible);
        Assert.Equal(99, changed.LayerOrder);
        Assert.Equal(45, changed.Rotation);
    }

    [Fact, Order(4)]
    public async Task Plan_sinirlari_disindaki_oge_kaydedilemez()
    {
        var error = await ExpectAppErrorAsync(() => SaveAsync(World.FloorId, [Element(label: "Taşan alan", x: 900, y: 500, width: 400, height: 400)]));
        Assert.Equal(400, error.Status);
        Assert.Contains("sınırlarının", error.Message);
    }

    [Fact, Order(5)]
    public void Desteklenmeyen_oge_turu_sema_tarafindan_reddedilir()
    {
        Assert.False(Validator.Validate(Element(type: "UZAY_ISTASYONU")).IsValid);
    }

    [Fact, Order(6)]
    public async Task Bilinmeyen_kata_kayit_yapilamaz()
    {
        var error = await ExpectAppErrorAsync(() => SaveAsync("olmayan-kat", []));
        Assert.Equal(404, error.Status);
    }

    [Fact, Order(7)]
    public async Task Baska_kata_ait_masa_bu_kata_tasinamaz()
    {
        var error = await ExpectAppErrorAsync(() => SaveAsync(World.FloorId, [], [new DeskPlacementInput(World.Desks["T-GM01"], 100, 100)]));
        Assert.Contains("bu kata ait değil", error.Message);
    }

    [Fact, Order(8)]
    public async Task Bagli_kaynak_turu_ile_kimligi_birlikte_verilmelidir()
    {
        var error = await ExpectAppErrorAsync(() => SaveAsync(World.FloorId, [Element(linkedResourceType: "DESK", linkedResourceId: null)]));
        Assert.Contains("bağlı kaynak kimliği eksik", error.Message);
    }

    [Fact, Order(9)]
    public async Task Gecersiz_kayit_sonrasi_kroki_bozulmadan_kalir_atomiklik()
    {
        var before = await ElementsAsync(World.FloorId);
        await ExpectAppErrorAsync(() => SaveAsync(World.FloorId, [Element(label: "Sınır dışı", x: 5, y: 5, width: 5000, height: 20)]));
        var after = await ElementsAsync(World.FloorId);
        Assert.Equal(before.Count, after.Count);
    }

    [Fact, Order(10)]
    public async Task Masa_tasindiginda_rezervasyonlari_korunur()
    {
        var user = await Fx.CreateTestUserAsync();
        var day = DayKeys.StartOfWeek(DayKeys.AddDays(Today, 7));
        var deskId = World.Desks["T-A01"];

        var reservation = await Fx.Svc<DeskReservationService>().CreateAsync(new CreateDeskReservationCommand(deskId, day), user);
        int before;
        await using (var db = Fx.Db()) before = await db.DeskReservations.CountAsync(r => r.DeskId == deskId);

        await SaveAsync(World.FloorId, (await ElementsAsync(World.FloorId)).Select(ToInput).ToList(), [new DeskPlacementInput(deskId, 620, 380)]);

        await using (var db = Fx.Db())
        {
            var desk = await db.Desks.SingleAsync(d => d.Id == deskId);
            Assert.Equal(620, desk.X);
            Assert.Equal(380, desk.Y);
            Assert.Equal(before, await db.DeskReservations.CountAsync(r => r.DeskId == deskId));
            Assert.Equal("BOOKED", (await db.DeskReservations.SingleAsync(r => r.Id == reservation.Id)).Status);
        }
    }

    [Fact, Order(11)]
    public async Task Masanin_olcusu_ve_donus_acisi_kaydedilir_ozellikleri_korunur()
    {
        var deskId = World.Desks["T-P01"];
        Domain.Entities.Desk before;
        int beforeCount;
        await using (var db = Fx.Db())
        {
            before = await db.Desks.AsNoTracking().SingleAsync(d => d.Id == deskId);
            beforeCount = await db.DeskReservations.CountAsync(r => r.DeskId == deskId);
        }

        await SaveAsync(World.FloorId, (await ElementsAsync(World.FloorId)).Select(ToInput).ToList(), [new DeskPlacementInput(deskId, 400, 200, 90, 60, 45)]);

        await using (var db = Fx.Db())
        {
            var after = await db.Desks.AsNoTracking().SingleAsync(d => d.Id == deskId);
            Assert.Equal(400, after.X);
            Assert.Equal(90, after.Width);
            Assert.Equal(60, after.Height);
            Assert.Equal(45, after.Rotation);
            // Ozellikler ve rezervasyonlar degismez.
            Assert.Equal(before.Code, after.Code);
            Assert.Equal(before.Popular, after.Popular);
            Assert.Equal(before.AccessibleOnly, after.AccessibleOnly);
            Assert.Equal(before.ZoneId, after.ZoneId);
            Assert.Equal(beforeCount, await db.DeskReservations.CountAsync(r => r.DeskId == deskId));
        }
    }

    [Fact, Order(12)]
    public async Task Olcusuyle_birlikte_plan_sinirini_asan_masa_reddedilir()
    {
        var error = await ExpectAppErrorAsync(() => SaveAsync(World.FloorId, [], [new DeskPlacementInput(World.Desks["T-A02"], 990, 300, 200, 60, 0)]));
        Assert.Contains("sınırlarının", error.Message);
    }

    [Fact, Order(13)]
    public async Task Editor_goruntusu_masanin_olcu_aci_ve_kat_bilgisini_icerir()
    {
        var snapshot = await Fx.Svc<FloorPlanService>().GetSnapshotAsync(World.FloorId);
        var desk = snapshot.Desks.Single(d => d.Code == "T-P01");
        Assert.Equal(90, desk.Width);
        Assert.Equal(60, desk.Height);
        Assert.Equal(45, desk.Rotation);
        Assert.Equal("1. Kat", desk.FloorName);
        Assert.Equal("A Bölgesi", desk.ZoneName);
    }

    [Fact, Order(14)]
    public async Task Oda_tasindiginda_ve_boyutlandirildiginda_rezervasyonlari_korunur()
    {
        var roomId = World.RoomId;
        int before;
        await using (var db = Fx.Db()) before = await db.RoomReservations.CountAsync(r => r.RoomId == roomId);

        await SaveAsync(World.FloorId, (await ElementsAsync(World.FloorId)).Select(ToInput).ToList(), rooms: [new RoomPlacementInput(roomId, 300, 400, 240, 150)]);

        await using (var db = Fx.Db())
        {
            var room = await db.MeetingRooms.SingleAsync(r => r.Id == roomId);
            Assert.Equal(300, room.X);
            Assert.Equal(240, room.Width);
            Assert.Equal(before, await db.RoomReservations.CountAsync(r => r.RoomId == roomId));
        }
    }

    [Fact, Order(15)]
    public async Task Kroki_tamamen_silinse_bile_masalar_odalar_ve_rezervasyonlar_korunur()
    {
        int deskCount, reservationCount;
        await using (var db = Fx.Db())
        {
            deskCount = await db.Desks.CountAsync(d => d.FloorId == World.FloorId);
            reservationCount = await db.DeskReservations.CountAsync();
        }

        await SaveAsync(World.FloorId, []);

        Assert.Empty(await ElementsAsync(World.FloorId));
        await using (var db = Fx.Db())
        {
            Assert.Equal(deskCount, await db.Desks.CountAsync(d => d.FloorId == World.FloorId));
            Assert.Equal(reservationCount, await db.DeskReservations.CountAsync());
        }
    }

    [Fact, Order(16)]
    public async Task Calisan_ekranindaki_kat_plani_ayni_krokiyi_icerir()
    {
        var user = await Fx.CreateTestUserAsync();
        await SaveAsync(World.FloorId,
        [
            Element(type: "WALL", label: null, x: 0, y: 0, width: 800, height: 12),
            Element(type: "KITCHEN", label: "Mutfak", x: 500, y: 300, width: 200, height: 140),
        ]);

        var plan = await Fx.Svc<AvailabilityService>().GetFloorPlanAsync(World.FloorId, DayKeys.AddDays(Today, 1), RuleUser(user));
        Assert.Equal(2, plan.Elements.Count);
        Assert.Equal(["KITCHEN", "WALL"], plan.Elements.Select(e => e.Type).OrderBy(t => t, StringComparer.Ordinal).ToList());
        Assert.Equal("Mutfak", plan.Elements.Single(e => e.Type == "KITCHEN").Label);
        // Masalar krokiden bagimsiz olarak gelmeye devam eder.
        Assert.NotEmpty(plan.Desks);
    }

    [Fact, Order(17)]
    public async Task Farkli_katlarin_krokileri_birbirinden_bagimsiz()
    {
        await SaveAsync(World.RestrictedFloorId, [Element(type: "RESTRICTED", label: "Genel Müdürlük", x: 20, y: 20, width: 300, height: 200)]);

        var first = await ElementsAsync(World.FloorId);
        var second = await ElementsAsync(World.RestrictedFloorId);
        Assert.Equal(2, first.Count);
        Assert.Single(second);
        Assert.Equal("Genel Müdürlük", second[0].Label);
    }

    [Fact, Order(18)]
    public async Task Kroki_kopyalama_baska_kattan_ogeleri_gecici_kimliklerle_dondurur()
    {
        var copied = await Fx.Svc<FloorPlanService>().CopyPlanFromFloorAsync(World.FloorId);
        Assert.Equal(2, copied.Count);
        Assert.All(copied, e => Assert.StartsWith("kopya-", e.Id));
    }

    [Fact, Order(19)]
    public async Task Editor_goruntusu_kat_kroki_masa_oda_ve_bolgeleri_birlikte_dondurur()
    {
        var snapshot = await Fx.Svc<FloorPlanService>().GetSnapshotAsync(World.FloorId);
        Assert.True(snapshot.Floor.PlanWidth > 0);
        Assert.Equal(2, snapshot.Elements.Count);
        Assert.NotEmpty(snapshot.Desks);
        Assert.NotEmpty(snapshot.Rooms);
        Assert.NotEmpty(snapshot.Zones);
        Assert.True(snapshot.Desks[0].ReservationCount >= 0);
    }

    [Fact, Order(20)]
    public async Task Kroki_kaydetme_islemi_denetim_kaydina_yazilir()
    {
        await using var db = Fx.Db();
        var logs = await db.AuditLogs.Where(l => l.Action == "FLOOR_PLAN_SAVE").ToListAsync();
        Assert.NotEmpty(logs);
        Assert.Contains("krokisi güncellendi", logs[0].Summary);
    }
}
