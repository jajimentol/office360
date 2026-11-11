using Microsoft.EntityFrameworkCore;
using Ofis360.Application.Common;
using Ofis360.Application.Identity;
using Ofis360.Application.Services;
using Ofis360.Application.Tests.Fixtures;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;
using Ofis360.Seed;
using static Ofis360.Application.Tests.Fixtures.TestHelpers;

namespace Ofis360.Application.Tests.Integration;

/// <summary>
/// Formdan onceki kurulu veritabanini taklit eder: A03/B03 acik, PT36-B04 aktif oda, B04 bolgesi yok,
/// kroki ve masa elle tasinmis, kapanacak alanlarda gecmis ve gelecek rezervasyonlar var.
/// </summary>
public sealed class LegacyPalladiumWorld : IAsyncLifetime
{
    public WorldFixture Fx { get; } = new();
    public SessionUser Employee { get; private set; } = null!;
    public SessionUser Admin { get; private set; } = null!;
    public string FloorId { get; private set; } = string.Empty;
    public string RoomId { get; private set; } = string.Empty;
    public string ClosedFuture { get; private set; } = string.Empty;
    public string ClosedPast { get; private set; } = string.Empty;
    public string ClosedCheckedIn { get; private set; } = string.Empty;
    public string OpenFuture { get; private set; } = string.Empty;
    public string RoomFuture { get; private set; } = string.Empty;
    public string RoomPast { get; private set; } = string.Empty;

    public const int MovedDeskX = 777;
    public const int MovedElementX = 33;
    public const int RoomX = 1331;

    public async Task InitializeAsync()
    {
        await Fx.InitializeAsync();
        Employee = await Fx.CreateTestUserAsync();
        Admin = await Fx.CreateTestUserAsync(Roles.Admin);
        var other = await Fx.CreateTestUserAsync();
        var third = await Fx.CreateTestUserAsync();

        await using var db = Fx.Db();
        var result = await PalladiumSeeder.SeedAsync(db);
        FloorId = result.Floor.Id;

        var floor = await db.Floors.SingleAsync(f => f.Id == FloorId);
        floor.Name = "36. Kat";
        floor.Description = "Eski açıklama";
        foreach (var zone in await db.Zones.Where(z => z.FloorId == FloorId && (z.Code == "A03" || z.Code == "B03")).ToListAsync())
        {
            zone.Name = $"{zone.Code} Güney Açık Ofis";
            zone.Kind = ZoneKinds.Work;
            zone.Reservable = true;
        }
        await db.Zones.Where(z => z.FloorId == FloorId && z.Code == "B04").ExecuteDeleteAsync();
        await db.FloorPlanElements.Where(e => e.FloorId == FloorId && e.Label == "36-B-04 Bölge Müdürü").ExecuteDeleteAsync();

        var room = new MeetingRoom { Code = PalladiumFormUpdater.ClosedRoomCode, Name = "Çamlıca", FloorId = FloorId, Capacity = 8, X = RoomX, Y = 998, Width = 158, Height = 252, Placed = true, Active = true };
        db.MeetingRooms.Add(room);
        RoomId = room.Id;

        (await db.Desks.SingleAsync(d => d.Code == "PT36-A05-01")).X = MovedDeskX;
        (await db.FloorPlanElements.SingleAsync(e => e.FloorId == FloorId && e.Label == "36-A-04 Bölge Müdürü")).X = MovedElementX;
        await db.SaveChangesAsync();

        var today = Fx.Clock.TodayKey();
        var tomorrow = DayKeys.AddDays(today, 1);
        var yesterday = DayKeys.AddDays(today, -1);
        var closedDesk = await db.Desks.SingleAsync(d => d.Code == "PT36-A03-01");
        var closedDesk2 = await db.Desks.SingleAsync(d => d.Code == "PT36-B03-01");
        var openDesk = await db.Desks.SingleAsync(d => d.Code == "PT36-A05-02");

        DeskReservation Desk(Desk desk, string userId, string dayKey, string status = DeskReservationStatus.Booked) => new()
        {
            DeskId = desk.Id, UserId = userId, Date = DayKeys.ToDateOnly(dayKey), Status = status,
            ActiveDeskKey = DeskReservation.BuildActiveDeskKey(desk.Id, dayKey),
            ActiveUserDayKey = DeskReservation.BuildActiveUserDayKey(userId, dayKey),
        };
        RoomReservation Room(string dayKey) => new()
        {
            RoomId = room.Id, UserId = Employee.Id, Date = DayKeys.ToDateOnly(dayKey),
            StartAt = new DateTimeOffset(DayKeys.WithMinutes(dayKey, 600), TimeSpan.Zero),
            EndAt = new DateTimeOffset(DayKeys.WithMinutes(dayKey, 660), TimeSpan.Zero),
            StartMinutes = 600, EndMinutes = 660, Title = "Test toplantısı", Purpose = "Ekip", AttendeeCount = 3,
        };

        var closedFuture = Desk(closedDesk, Employee.Id, tomorrow);
        var closedPast = Desk(closedDesk, Employee.Id, yesterday);
        var closedCheckedIn = Desk(closedDesk2, third.Id, today, DeskReservationStatus.CheckedIn);
        var openFuture = Desk(openDesk, other.Id, tomorrow);
        var roomFuture = Room(tomorrow);
        var roomPast = Room(yesterday);
        db.AddRange(closedFuture, closedPast, closedCheckedIn, openFuture, roomFuture, roomPast);
        await db.SaveChangesAsync();

        ClosedFuture = closedFuture.Id;
        ClosedPast = closedPast.Id;
        ClosedCheckedIn = closedCheckedIn.Id;
        OpenFuture = openFuture.Id;
        RoomFuture = roomFuture.Id;
        RoomPast = roomPast.Id;
    }

    public Task DisposeAsync() => Fx.DisposeAsync();
}

[Collection(OracleCollection.Name)]
[TestCaseOrderer(PriorityOrderer.TypeName, PriorityOrderer.AssemblyName)]
public class PalladiumFormUpdateTests(LegacyPalladiumWorld w) : IClassFixture<LegacyPalladiumWorld>
{
    private WorldFixture Fx => w.Fx;
    private string Tomorrow => DayKeys.AddDays(Fx.Clock.TodayKey(), 1);

    private async Task<PalladiumFormReport> Apply()
    {
        await using var db = Fx.Db();
        return await PalladiumFormUpdater.ApplyAsync(db, Fx.Clock.TodayKey());
    }

    [Fact, Order(1)]
    public async Task Form_kat_ve_bolgeleri_gunceller_odayi_pasife_alir()
    {
        var report = await Apply();
        Assert.True(report.FloorUpdated);
        Assert.Equal(2, report.ZonesUpdated);
        Assert.True(report.ZoneCreated);
        Assert.True(report.ElementCreated);
        Assert.True(report.RoomDeactivated);
        Assert.Equal(1, report.DeskReservationsCancelled);
        Assert.Equal(1, report.RoomReservationsCancelled);

        await using var db = Fx.Db();
        var floor = await db.Floors.SingleAsync(f => f.Id == w.FloorId);
        Assert.Equal("Palladium Tower-36.Kat", floor.Name);
        Assert.Equal("Anadolu Bölge Çalışma Ofisi", floor.Description);

        var closedZones = await db.Zones.Where(z => z.FloorId == w.FloorId && (z.Code == "A03" || z.Code == "B03" || z.Code == "B04")).ToListAsync();
        Assert.Equal(3, closedZones.Count);
        Assert.All(closedZones, z => { Assert.False(z.Reservable); Assert.Equal(ZoneKinds.Restricted, z.Kind); });

        var room = await db.MeetingRooms.SingleAsync(r => r.Id == w.RoomId);
        Assert.False(room.Active);
        Assert.False(room.Placed);

        // Bolge ve kroki ogesi odanin elle ayarlanmis konumundan uretilir.
        Assert.Equal(LegacyPalladiumWorld.RoomX, closedZones.Single(z => z.Code == "B04").X);
        var element = await db.FloorPlanElements.SingleAsync(e => e.FloorId == w.FloorId && e.Label == "36-B-04 Bölge Müdürü");
        Assert.Equal(LegacyPalladiumWorld.RoomX, element.X);
    }

    [Fact, Order(2)]
    public async Task Elle_yapilan_kroki_duzenlemelerine_dokunmaz()
    {
        await using var db = Fx.Db();
        Assert.Equal(LegacyPalladiumWorld.MovedDeskX, (await db.Desks.SingleAsync(d => d.Code == "PT36-A05-01")).X);
        Assert.Equal(LegacyPalladiumWorld.MovedElementX, (await db.FloorPlanElements.SingleAsync(e => e.FloorId == w.FloorId && e.Label == "36-A-04 Bölge Müdürü")).X);
        Assert.Equal(58, await db.Desks.CountAsync(d => d.FloorId == w.FloorId));
    }

    [Fact, Order(3)]
    public async Task Yalniz_kapanan_alandaki_gelecek_rezervasyonlari_iptal_eder()
    {
        await using var db = Fx.Db();
        var future = await db.DeskReservations.SingleAsync(r => r.Id == w.ClosedFuture);
        Assert.Equal(DeskReservationStatus.Cancelled, future.Status);
        Assert.Null(future.ActiveDeskKey);
        Assert.Null(future.ActiveUserDayKey);
        Assert.Equal(PalladiumFormUpdater.CancelReason, future.CancelReason);

        var roomFuture = await db.RoomReservations.SingleAsync(r => r.Id == w.RoomFuture);
        Assert.Equal(RoomReservationStatus.Cancelled, roomFuture.Status);

        Assert.Equal(DeskReservationStatus.Booked, (await db.DeskReservations.SingleAsync(r => r.Id == w.ClosedPast)).Status);
        Assert.Equal(DeskReservationStatus.CheckedIn, (await db.DeskReservations.SingleAsync(r => r.Id == w.ClosedCheckedIn)).Status);
        Assert.Equal(DeskReservationStatus.Booked, (await db.DeskReservations.SingleAsync(r => r.Id == w.OpenFuture)).Status);
        Assert.Equal(RoomReservationStatus.Booked, (await db.RoomReservations.SingleAsync(r => r.Id == w.RoomPast)).Status);
    }

    [Fact, Order(4)]
    public async Task Ikinci_calistirma_hicbir_sey_degistirmez()
    {
        int zones, elements, audits;
        await using (var db = Fx.Db())
        {
            zones = await db.Zones.CountAsync(z => z.FloorId == w.FloorId);
            elements = await db.FloorPlanElements.CountAsync(e => e.FloorId == w.FloorId);
            audits = await db.AuditLogs.CountAsync();
        }

        var report = await Apply();
        Assert.False(report.HasChanges);

        await using (var db = Fx.Db())
        {
            Assert.Equal(zones, await db.Zones.CountAsync(z => z.FloorId == w.FloorId));
            Assert.Equal(elements, await db.FloorPlanElements.CountAsync(e => e.FloorId == w.FloorId));
            Assert.Equal(audits, await db.AuditLogs.CountAsync());
        }
    }

    [Fact, Order(5)]
    public async Task Kapali_bolge_masasi_secilemez_ve_kapasiteye_sayilmaz()
    {
        var availability = Fx.Svc<AvailabilityService>();
        var summary = (await availability.GetFloorSummariesAsync(Tomorrow)).Single(s => s.Id == w.FloorId);
        Assert.Equal(40, summary.TotalDesks);
        Assert.Equal(9, summary.RoomCount);

        var plan = await availability.GetFloorPlanAsync(w.FloorId, Tomorrow, RuleUser(w.Employee));
        Assert.Equal(40, plan.Summary.Total);
        Assert.Equal(18, plan.Summary.Closed);

        var closed = plan.Desks.Single(d => d.Code == "PT36-A03-02");
        Assert.Equal(DeskViewStates.Closed, closed.State);
        Assert.False(closed.Selectable);
        Assert.Contains(closed.Violations, v => v.Code == "ZONE_NOT_RESERVABLE");

        Assert.True(plan.Desks.Single(d => d.Code == "PT36-B05-05").Selectable);

        var error = await Assert.ThrowsAsync<AppException>(() =>
            Fx.Svc<DeskReservationService>().CreateAsync(new CreateDeskReservationCommand(closed.Id, Tomorrow), w.Employee));
        Assert.Equal("RULE_VIOLATION", error.Code);
    }

    [Fact, Order(6)]
    public async Task Kapatilan_oda_listede_ve_krokide_gorunmez_rezerve_edilemez()
    {
        var rooms = Fx.Svc<RoomReservationService>();
        var availability = await rooms.GetAvailabilityAsync(new RoomAvailabilityQuery(Tomorrow, FloorId: w.FloorId));
        Assert.DoesNotContain(availability.Rooms, r => r.Id == w.RoomId);
        Assert.Equal(9, availability.Rooms.Count);

        var plan = await Fx.Svc<AvailabilityService>().GetFloorPlanAsync(w.FloorId, Tomorrow, RuleUser(w.Employee));
        Assert.DoesNotContain(plan.Rooms, r => r.Id == w.RoomId);

        var error = await Assert.ThrowsAsync<AppException>(() => rooms.CreateAsync(
            new CreateRoomReservationCommand(w.RoomId, Tomorrow, 600, 660, "Yasak toplantı", "Ekip", 3), w.Employee));
        Assert.Equal("RULE_VIOLATION", error.Code);
    }

    [Fact, Order(7)]
    public async Task Pasif_oda_otomatik_yerlestirmeyle_krokiye_donmez()
    {
        await Fx.Svc<FloorPlanService>().AutoPlacePoolItemsAsync(w.FloorId, Actor(w.Admin));

        await using var db = Fx.Db();
        Assert.False((await db.MeetingRooms.SingleAsync(r => r.Id == w.RoomId)).Placed);
    }

    [Fact, Order(8)]
    public async Task Iptal_durum_gecmisine_yazilir()
    {
        await using var db = Fx.Db();
        var rows = await db.Database
            .SqlQuery<string>($"SELECT NEW_STATUS AS \"Value\" FROM RESERVATION_STATUS_HISTORY WHERE RESERVATION_ID = {w.ClosedFuture} ORDER BY ID")
            .ToListAsync();
        Assert.Equal(DeskReservationStatus.Cancelled, rows[^1]);
    }
}
