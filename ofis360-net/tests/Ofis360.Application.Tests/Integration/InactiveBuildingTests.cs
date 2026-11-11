using Microsoft.EntityFrameworkCore;
using Ofis360.Application.Common;
using Ofis360.Application.Identity;
using Ofis360.Application.Services;
using Ofis360.Application.Tests.Fixtures;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;
using Ofis360.Seed;

namespace Ofis360.Application.Tests.Integration;

/// <summary>Pasif demo binanin kayitlari calisan ekranlarinda, sayaclarda ve raporlarda yer almaz.</summary>
public sealed class InactiveBuildingWorld : IAsyncLifetime
{
    public WorldFixture Fx { get; } = new();
    public SessionUser User { get; private set; } = null!;
    public string DemoDeskId { get; private set; } = string.Empty;
    public string DemoRoomId { get; private set; } = string.Empty;
    public string FloorName { get; } = "Demo Kat Gizli";

    public async Task InitializeAsync()
    {
        await Fx.InitializeAsync();
        User = await Fx.CreateTestUserAsync();

        await using var db = Fx.Db();
        var building = new Building { Code = "DMO", Name = "Demo Bina", Order = 90, Active = false, IsDemo = true };
        var floor = new Floor { BuildingId = building.Id, Number = 7, Name = FloorName, Description = "x", Reservable = true };
        var zone = new Zone { FloorId = floor.Id, Code = "DZ", Name = "Demo Bölge", Kind = ZoneKinds.Work, X = 0, Y = 0, Width = 100, Height = 100, Reservable = true };
        var desk = new Desk { Code = "DMO-01", FloorId = floor.Id, ZoneId = zone.Id, X = 10, Y = 10 };
        var room = new MeetingRoom { Code = "DMO-R", Name = "Demo Oda", FloorId = floor.Id, Capacity = 6, X = 40, Y = 40, Width = 20, Height = 20 };
        db.AddRange(building, floor, zone, desk, room);
        await db.SaveChangesAsync();
        DemoDeskId = desk.Id;
        DemoRoomId = room.Id;
    }

    public Task DisposeAsync() => Fx.DisposeAsync();
}

[Collection(OracleCollection.Name)]
public class InactiveBuildingTests(InactiveBuildingWorld w) : IClassFixture<InactiveBuildingWorld>
{
    private WorldFixture Fx => w.Fx;
    private string Today => Fx.Clock.TodayKey();

    private sealed class HealthyFeed : IAttendanceFeed
    {
        public bool IsConfigured => true;

        public Task<AttendanceSnapshot> GetDayAsync(string dayKey, CancellationToken ct = default) =>
            Task.FromResult(new AttendanceSnapshot(true, new Dictionary<string, DateTimeOffset>()));
    }

    private async Task<string> Desk(string dayKey, string status, SessionUser? user = null)
    {
        user ??= await Fx.CreateTestUserAsync();
        var active = status is DeskReservationStatus.Booked or DeskReservationStatus.CheckedIn;
        var row = new DeskReservation
        {
            DeskId = w.DemoDeskId, UserId = user.Id, Date = DayKeys.ToDateOnly(dayKey), Status = status,
            ActiveDeskKey = active ? DeskReservation.BuildActiveDeskKey(w.DemoDeskId, dayKey) : null,
            ActiveUserDayKey = active ? DeskReservation.BuildActiveUserDayKey(user.Id, dayKey) : null,
        };
        await using var db = Fx.Db();
        db.DeskReservations.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    private async Task<string> Room(string dayKey, string status, SessionUser user)
    {
        var row = new RoomReservation
        {
            RoomId = w.DemoRoomId, UserId = user.Id, Date = DayKeys.ToDateOnly(dayKey), Status = status, StartMinutes = 600, EndMinutes = 660, Title = "Demo", Purpose = "Ekip", AttendeeCount = 2,
            StartAt = new DateTimeOffset(DayKeys.WithMinutes(dayKey, 600), TimeSpan.Zero), EndAt = new DateTimeOffset(DayKeys.WithMinutes(dayKey, 660), TimeSpan.Zero),
        };
        await using var db = Fx.Db();
        db.RoomReservations.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    [Fact]
    public async Task Kullanim_ozetinde_ve_rezervasyonlarimda_gorunmez()
    {
        var user = await Fx.CreateTestUserAsync();
        await Desk(DayKeys.AddDays(Today, -5), DeskReservationStatus.Completed, user);
        await Desk(DayKeys.AddDays(Today, 3), DeskReservationStatus.Booked, user);
        await Room(DayKeys.AddDays(Today, 3), RoomReservationStatus.Booked, user);

        var summary = await Fx.Svc<PersonalService>().GetPersonalSummaryAsync(user.Id);
        Assert.DoesNotContain(summary.FloorPreference, f => f.Name == w.FloorName);
        Assert.Empty(summary.FavoriteDesks);

        var list = await Fx.Svc<DeskReservationService>().GetUserReservationsAsync(user.Id);
        Assert.Empty(list.UpcomingDesks);
        Assert.Empty(list.PastDesks);
        Assert.Empty(list.UpcomingRooms);
    }

    [Fact]
    public async Task Ana_sayfada_gorunmez()
    {
        var user = await Fx.CreateTestUserAsync();
        await Desk(Today, DeskReservationStatus.Booked, user);
        await Room(Today, RoomReservationStatus.Booked, user);

        var home = await Fx.Svc<PersonalService>().GetEmployeeHomeSummaryAsync(user.Id);
        Assert.Null(home.TodayDesk);
        Assert.Empty(home.TodayRooms);
    }

    [Fact]
    public async Task Haftalik_hakka_ve_ayni_gun_kuralina_sayilmaz()
    {
        var user = await Fx.CreateTestUserAsync();
        var day = DayKeys.AddDays(Today, 40);
        await Desk(day, DeskReservationStatus.Completed, user);

        var context = await Fx.Svc<AvailabilityService>().BuildUserDeskContextAsync(user.Id, DayKeys.AddDays(day, 1));
        Assert.Equal(0, context.WeeklyReservationCount);
        Assert.Null((await Fx.Svc<AvailabilityService>().BuildUserDeskContextAsync(user.Id, day)).SameDayReservation);
    }

    [Fact]
    public async Task Sekreter_listesi_gizler_admin_listesi_gosterir()
    {
        var user = await Fx.CreateTestUserAsync();
        var id = await Desk(DayKeys.AddDays(Today, 50), DeskReservationStatus.Booked, user);
        var desks = Fx.Svc<DeskReservationService>();

        Assert.DoesNotContain((await desks.ListAsync(new ListDeskReservationsQuery(UserId: user.Id, ActiveBuildingsOnly: true))).Items, r => r.Id == id);
        Assert.Contains((await desks.ListAsync(new ListDeskReservationsQuery(UserId: user.Id))).Items, r => r.Id == id);
    }

    [Fact]
    public async Task No_show_isi_ve_rapor_pasif_binayi_islemez()
    {
        var day = DayKeys.AddDays(Today, -2);
        var id = await Desk(day, DeskReservationStatus.Booked);
        var noShow = await Desk(DayKeys.AddDays(Today, -3), DeskReservationStatus.NoShow);

        // Turnike bagli sayilir: aksi halde is hicbir kaydi islemez ve bu test bos gecer.
        await new NoShowService(Fx.Svc<Ofis360.Infrastructure.Persistence.Ofis360DbContext>(), Fx.Svc<RuleService>(), new HealthyFeed(),
            Fx.Svc<NotificationService>(), Fx.Svc<AuditService>(), Fx.Clock).RunAsync();
        await using (var db = Fx.Db())
        {
            Assert.Equal(DeskReservationStatus.Booked, (await db.DeskReservations.AsNoTracking().SingleAsync(r => r.Id == id)).Status);
            Assert.True(await db.DeskReservations.AnyAsync(r => r.Id == noShow));
        }

        var report = await Fx.Svc<AttendanceReportService>().BuildAsync(DayKeys.AddDays(Today, -4), Today);
        Assert.Equal(0, report.Totals.DeskNoShow);
    }

    [Fact]
    public async Task Form_komutu_gelecek_demo_kayitlarini_iptal_eder_gecmise_dokunmaz()
    {
        var user = await Fx.CreateTestUserAsync();
        var future = await Desk(DayKeys.AddDays(Today, 60), DeskReservationStatus.Booked, user);
        var past = await Desk(DayKeys.AddDays(Today, -60), DeskReservationStatus.Booked, user);
        var room = await Room(DayKeys.AddDays(Today, 60), RoomReservationStatus.Booked, user);

        await using (var db = Fx.Db())
        {
            Assert.True(await PalladiumFormUpdater.CancelInactiveDemoReservationsAsync(db, Fx.Clock.Today, DateTimeOffset.UtcNow) >= 2);
        }

        await using var verify = Fx.Db();
        var row = await verify.DeskReservations.AsNoTracking().SingleAsync(r => r.Id == future);
        Assert.Equal(DeskReservationStatus.Cancelled, row.Status);
        Assert.Null(row.ActiveUserDayKey);
        Assert.Equal(DeskReservationStatus.Booked, (await verify.DeskReservations.AsNoTracking().SingleAsync(r => r.Id == past)).Status);
        Assert.Equal(RoomReservationStatus.Cancelled, (await verify.RoomReservations.AsNoTracking().SingleAsync(r => r.Id == room)).Status);
    }
}
