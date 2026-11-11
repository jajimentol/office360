using Microsoft.EntityFrameworkCore;
using Ofis360.Application.Common;
using Ofis360.Application.Identity;
using Ofis360.Application.Services;
using Ofis360.Application.Tests.Fixtures;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;
using Ofis360.Domain.Rules;
using static Ofis360.Application.Tests.Fixtures.TestHelpers;

namespace Ofis360.Application.Tests.Integration;

public sealed class NoShowWorld : IAsyncLifetime
{
    public WorldFixture Fx { get; } = new();

    public async Task InitializeAsync() => await Fx.InitializeAsync();

    public Task DisposeAsync() => Fx.DisposeAsync();
}

[Collection(OracleCollection.Name)]
public class NoShowTests(NoShowWorld w) : IClassFixture<NoShowWorld>
{
    private static int _day;
    private static readonly DateOnly Base = new(2027, 3, 1);
    private WorldFixture Fx => w.Fx;

    /// <summary>Her test ayri bir takvim gunu kullanir; birbirinin kaydini islemez.</summary>
    private static DateOnly NextDay() => Base.AddDays(Interlocked.Increment(ref _day) * 20);

    private static FixedClock At(DateOnly day, int hour, int minute = 0) =>
        new(new DateTimeOffset(day.ToDateTime(new TimeOnly(hour, minute)), TimeSpan.FromHours(3)).ToUniversalTime());

    private sealed class Feed(bool configured, bool healthy, params string[] emails) : IAttendanceFeed
    {
        public bool IsConfigured => configured;

        public Task<AttendanceSnapshot> GetDayAsync(string dayKey, CancellationToken ct = default) =>
            Task.FromResult(new AttendanceSnapshot(healthy, emails.ToDictionary(e => e.ToLowerInvariant(), _ => DateTimeOffset.UtcNow)));
    }

    /// <summary>Yalnizca belirtilen gunler saglikli; girisler o gunlerin kaydidir.</summary>
    private sealed class DayFeed(IReadOnlySet<string> healthyDays, params string[] emails) : IAttendanceFeed
    {
        public bool IsConfigured => true;

        public Task<AttendanceSnapshot> GetDayAsync(string dayKey, CancellationToken ct = default) =>
            Task.FromResult(new AttendanceSnapshot(healthyDays.Contains(dayKey), emails.ToDictionary(e => e.ToLowerInvariant(), _ => DateTimeOffset.UtcNow)));
    }

    private NoShowService Service(IClock clock, IAttendanceFeed? feed = null) => new(
        Fx.Svc<Ofis360.Infrastructure.Persistence.Ofis360DbContext>(), Fx.Svc<RuleService>(), feed ?? new Feed(true, true),
        Fx.Svc<NotificationService>(), Fx.Svc<AuditService>(), clock);

    private async Task<(string Id, SessionUser User)> Desk(DateOnly day, string status = DeskReservationStatus.Booked, DateTimeOffset? createdAt = null, string desk = "T-A01")
    {
        var user = await Fx.CreateTestUserAsync();
        var dayKey = DayKeys.FromDateOnly(day);
        var deskId = Fx.World.Desks[desk];
        var row = new DeskReservation
        {
            DeskId = deskId, UserId = user.Id, Date = day, Status = status,
            ActiveDeskKey = DeskReservation.BuildActiveDeskKey(deskId, dayKey),
            ActiveUserDayKey = DeskReservation.BuildActiveUserDayKey(user.Id, dayKey),
            CreatedAt = createdAt ?? new DateTimeOffset(day.AddDays(-2).ToDateTime(new TimeOnly(10, 0)), TimeSpan.FromHours(3)),
        };
        await using var db = Fx.Db();
        db.DeskReservations.Add(row);
        await db.SaveChangesAsync();
        return (row.Id, user);
    }

    private async Task<DeskReservation> Load(string id)
    {
        await using var db = Fx.Db();
        return await db.DeskReservations.AsNoTracking().SingleAsync(r => r.Id == id);
    }

    private async Task<string> Room(DateOnly day, int start, int end, DateTimeOffset? startedAt = null, SessionUser? owner = null)
    {
        owner ??= await Fx.CreateTestUserAsync();
        var dayKey = DayKeys.FromDateOnly(day);
        var row = new RoomReservation
        {
            RoomId = Fx.World.RoomId, UserId = owner.Id, Date = day, Title = "Toplantı", Purpose = "Ekip", AttendeeCount = 2,
            StartMinutes = start, EndMinutes = end, StartedAt = startedAt,
            StartAt = new DateTimeOffset(DayKeys.WithMinutes(dayKey, start), TimeSpan.Zero),
            EndAt = new DateTimeOffset(DayKeys.WithMinutes(dayKey, end), TimeSpan.Zero),
        };
        await using var db = Fx.Db();
        db.RoomReservations.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    private async Task<string> RoomStatus(string id)
    {
        await using var db = Fx.Db();
        return (await db.RoomReservations.AsNoTracking().SingleAsync(r => r.Id == id)).Status;
    }

    private async Task Rule(string key, bool? enabled = null, int? value = null, int? second = null)
    {
        await using var db = Fx.Db();
        var rule = await db.ReservationRules.SingleAsync(r => r.Key == key);
        if (enabled is bool e) rule.Enabled = e;
        if (value is int v) rule.IntValue = v;
        if (second is int s) rule.SecondValue = s;
        await db.SaveChangesAsync();
    }

    private Task ResetRules() => Rule(RuleKeys.NoShowRelease, true, 120, 540);

    [Fact]
    public async Task Sure_dolmadan_dokunmaz_dolunca_masayi_serbest_birakir()
    {
        var day = NextDay();
        var (id, user) = await Desk(day);

        Assert.Equal(0, (await Service(At(day, 10, 59)).RunAsync()).DesksReleased);
        Assert.Equal(DeskReservationStatus.Booked, (await Load(id)).Status);

        Assert.Equal(1, (await Service(At(day, 11)).RunAsync()).DesksReleased);
        var row = await Load(id);
        Assert.Equal(DeskReservationStatus.NoShow, row.Status);
        Assert.Null(row.ActiveDeskKey);
        Assert.Null(row.ActiveUserDayKey);

        await using var db = Fx.Db();
        Assert.True(await db.Notifications.AnyAsync(n => n.UserId == user.Id && n.Type == NotificationTypes.DeskNoShowReleased));
        var history = await db.Database.SqlQuery<string>($"SELECT NEW_STATUS AS \"Value\" FROM RESERVATION_STATUS_HISTORY WHERE RESERVATION_ID = {id} ORDER BY ID").ToListAsync();
        Assert.Equal(DeskReservationStatus.NoShow, history[^1]);
    }

    [Fact]
    public async Task Check_in_yapani_ve_iptal_edileni_islemez()
    {
        var day = NextDay();
        var (checkedIn, _) = await Desk(day, DeskReservationStatus.CheckedIn);
        var (open, _) = await Desk(day, desk: "T-A02");

        var result = await Service(At(day, 12)).RunAsync();
        Assert.Equal(1, result.DesksReleased);
        Assert.Equal(DeskReservationStatus.CheckedIn, (await Load(checkedIn)).Status);
        Assert.Equal(DeskReservationStatus.NoShow, (await Load(open)).Status);
    }

    [Fact]
    public async Task Ayni_gun_gec_acilan_rezervasyonda_sure_acilistan_baslar()
    {
        var day = NextDay();
        var created = new DateTimeOffset(day.ToDateTime(new TimeOnly(10, 30)), TimeSpan.FromHours(3));
        var (id, _) = await Desk(day, createdAt: created);

        await Service(At(day, 12, 29)).RunAsync();
        Assert.Equal(DeskReservationStatus.Booked, (await Load(id)).Status);
        await Service(At(day, 12, 30)).RunAsync();
        Assert.Equal(DeskReservationStatus.NoShow, (await Load(id)).Status);
    }

    [Fact]
    public async Task Ikinci_calistirma_bir_sey_yapmaz()
    {
        var day = NextDay();
        await Desk(day);
        Assert.True((await Service(At(day, 11)).RunAsync()).HasChanges);
        Assert.False((await Service(At(day, 11, 1)).RunAsync()).HasChanges);
    }

    [Fact]
    public async Task Yakin_gecmisi_tamamlar_cok_eskiye_dokunmaz()
    {
        var day = NextDay();
        var (recent, _) = await Desk(day.AddDays(-3));
        var (old, _) = await Desk(day.AddDays(-10), desk: "T-A02");

        await Service(At(day, 8)).RunAsync();
        Assert.Equal(DeskReservationStatus.NoShow, (await Load(recent)).Status);
        Assert.Equal(DeskReservationStatus.Booked, (await Load(old)).Status);
    }

    [Fact]
    public async Task Parametreler_panelden_degisir_ve_kapatilabilir()
    {
        try
        {
            var day = NextDay();
            await Rule(RuleKeys.NoShowRelease, value: 30, second: 600);
            var (id, _) = await Desk(day);
            await Service(At(day, 10, 29)).RunAsync();
            Assert.Equal(DeskReservationStatus.Booked, (await Load(id)).Status);
            await Service(At(day, 10, 30)).RunAsync();
            Assert.Equal(DeskReservationStatus.NoShow, (await Load(id)).Status);

            await Rule(RuleKeys.NoShowRelease, enabled: false);
            var day2 = NextDay();
            var (id2, _) = await Desk(day2);
            await Service(At(day2, 18)).RunAsync();
            Assert.Equal(DeskReservationStatus.Booked, (await Load(id2)).Status);
        }
        finally
        {
            await ResetRules();
        }
    }

    [Fact]
    public async Task Turnike_girisi_check_in_sayilir()
    {
        var day = NextDay();
        var (present, user) = await Desk(day);
        var (absent, _) = await Desk(day, desk: "T-A02");

        var result = await Service(At(day, 11), new Feed(true, true, user.Email.ToUpperInvariant())).RunAsync();
        Assert.Equal(1, result.CheckedIn);
        Assert.Equal(1, result.DesksReleased);
        var row = await Load(present);
        Assert.Equal(DeskReservationStatus.CheckedIn, row.Status);
        Assert.NotNull(row.CheckedInAt);
        Assert.NotNull(row.ActiveDeskKey);
        Assert.Equal(DeskReservationStatus.NoShow, (await Load(absent)).Status);
    }

    [Fact]
    public async Task Turnike_bagli_degilken_kimse_gelmedi_sayilmaz()
    {
        var day = NextDay();
        var (id, _) = await Desk(day);
        var (past, _) = await Desk(day.AddDays(-2), desk: "T-A02");

        var result = await Service(At(day, 15), new NotConfiguredAttendanceFeed()).RunAsync();

        Assert.Equal(AttendanceFeedStatus.NotConfigured, result.FeedStatus);
        Assert.Equal(0, result.DesksReleased);
        Assert.Equal(DeskReservationStatus.Booked, (await Load(id)).Status);
        Assert.Equal(DeskReservationStatus.Booked, (await Load(past)).Status);
    }

    [Fact]
    public async Task Onceki_gunun_turnike_verisi_saglamsiz_ise_o_gun_isaretlenmez()
    {
        var day = NextDay();
        var yesterday = day.AddDays(-1);
        var (outage, _) = await Desk(yesterday);
        var (fine, _) = await Desk(day.AddDays(-2), desk: "T-A02");

        var healthy = new HashSet<string> { DayKeys.FromDateOnly(day), DayKeys.FromDateOnly(day.AddDays(-2)) };
        var result = await Service(At(day, 15), new DayFeed(healthy)).RunAsync();

        Assert.Equal(AttendanceFeedStatus.Unavailable, result.FeedStatus);
        Assert.Equal(DeskReservationStatus.Booked, (await Load(outage)).Status);
        Assert.Equal(DeskReservationStatus.NoShow, (await Load(fine)).Status);
    }

    [Fact]
    public async Task Onceki_gun_girisi_olan_kisi_gelmedi_sayilmaz()
    {
        var day = NextDay();
        var yesterday = day.AddDays(-1);
        var (id, user) = await Desk(yesterday);

        var healthy = new HashSet<string> { DayKeys.FromDateOnly(day), DayKeys.FromDateOnly(yesterday) };
        await Service(At(day, 8), new DayFeed(healthy, user.Email)).RunAsync();

        Assert.Equal(DeskReservationStatus.CheckedIn, (await Load(id)).Status);
    }

    [Fact]
    public async Task Turnike_verisi_gelmiyorsa_kimse_gelmedi_sayilmaz()
    {
        var day = NextDay();
        var (id, _) = await Desk(day);

        var result = await Service(At(day, 15), new Feed(true, false)).RunAsync();
        Assert.Equal(AttendanceFeedStatus.Unavailable, result.FeedStatus);
        Assert.Equal(0, result.DesksReleased);
        Assert.Equal(DeskReservationStatus.Booked, (await Load(id)).Status);
    }

    [Fact]
    public async Task Bos_toplanti_isaretlenir_baslayan_tamamlanir_devam_edene_dokunulmaz()
    {
        var day = NextDay();
        var empty = await Room(day, 540, 600);
        var started = await Room(day, 600, 660, DateTimeOffset.UtcNow);
        var running = await Room(day, 720, 780);

        var result = await Service(At(day, 11)).RunAsync();
        Assert.Equal(1, result.RoomsNoShow);
        Assert.Equal(1, result.RoomsCompleted);
        Assert.Equal(RoomReservationStatus.NoShow, await RoomStatus(empty));
        Assert.Equal(RoomReservationStatus.Completed, await RoomStatus(started));
        Assert.Equal(RoomReservationStatus.Booked, await RoomStatus(running));
    }

    [Fact]
    public async Task Serbest_kalan_masa_baskasina_ve_ayni_kisiye_yeniden_acilir()
    {
        var today = Fx.Clock.Today;
        var (id, owner) = await Desk(today, desk: "T-A03");
        await using (var db = Fx.Db())
        {
            await db.DeskReservations.Where(r => r.Id == id).ExecuteUpdateAsync(u => u
                .SetProperty(r => r.Status, DeskReservationStatus.NoShow)
                .SetProperty(r => r.ActiveDeskKey, (string?)null)
                .SetProperty(r => r.ActiveUserDayKey, (string?)null));
        }

        var dayKey = DayKeys.FromDateOnly(today);
        var plan = await Fx.Svc<AvailabilityService>().GetFloorPlanAsync(Fx.World.FloorId, dayKey, RuleUser(await Fx.CreateTestUserAsync()));
        Assert.Null(plan.Desks.Single(d => d.Code == "T-A03").Occupant);

        var desks = Fx.Svc<DeskReservationService>();
        var again = await desks.CreateAsync(new CreateDeskReservationCommand(Fx.World.Desks["T-A04"], dayKey), owner);
        Assert.Equal("BOOKED", again.Status);
        var other = await Fx.CreateTestUserAsync();
        Assert.Equal("BOOKED", (await desks.CreateAsync(new CreateDeskReservationCommand(Fx.World.Desks["T-A03"], dayKey), other)).Status);
    }

    private RoomReservationService RoomService(IClock clock) => new(
        Fx.Svc<Ofis360.Infrastructure.Persistence.Ofis360DbContext>(), Fx.Svc<AvailabilityService>(), Fx.Svc<RuleService>(), Fx.Svc<NotificationService>(), Fx.Svc<AuditService>(), clock);

    [Fact]
    public async Task Toplanti_baslat_saat_araliginda_ve_yetkiyle_calisir()
    {
        var day = NextDay();
        var owner = await Fx.CreateTestUserAsync();
        var id = await Room(day, 600, 660, owner: owner);

        await Assert.ThrowsAsync<AppException>(() => RoomService(At(day, 9, 44)).StartAsync(id, owner));
        await Assert.ThrowsAsync<AppException>(() => RoomService(At(day, 11)).StartAsync(id, owner));
        await Assert.ThrowsAsync<AppException>(() => RoomService(At(day.AddDays(1), 10)).StartAsync(id, owner));

        var other = await Fx.CreateTestUserAsync();
        Assert.Equal(403, (await Assert.ThrowsAsync<AppException>(() => RoomService(At(day, 10)).StartAsync(id, other))).Status);

        Assert.NotNull((await RoomService(At(day, 9, 45)).StartAsync(id, owner)).StartedAt);
        Assert.NotNull((await RoomService(At(day, 9, 50)).StartAsync(id, owner)).StartedAt);

        await Service(At(day, 11)).RunAsync();
        Assert.Equal(RoomReservationStatus.Completed, await RoomStatus(id));
    }

    [Fact]
    public async Task Sekreter_toplantiyi_baslatabilir()
    {
        var day = NextDay();
        var id = await Room(day, 600, 660);
        var secretary = await Fx.CreateTestUserAsync() with { IsSecretary = true };
        Assert.NotNull((await RoomService(At(day, 10)).StartAsync(id, secretary)).StartedAt);
    }

    [Fact]
    public async Task Gelmedi_sayilan_kayda_check_in_yapilamaz()
    {
        var today = Fx.Clock.Today;
        var (id, owner) = await Desk(today, DeskReservationStatus.NoShow, desk: "T-A02");
        var error = await Assert.ThrowsAsync<AppException>(() => Fx.Svc<DeskReservationService>().CheckInAsync(id, owner));
        Assert.Equal(409, error.Status);
    }
}
