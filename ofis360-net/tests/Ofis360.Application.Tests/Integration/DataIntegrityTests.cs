using Microsoft.EntityFrameworkCore;
using Ofis360.Application.Common;
using Ofis360.Application.Identity;
using Ofis360.Application.Persistence;
using Ofis360.Application.Services;
using Ofis360.Application.Tests.Fixtures;
using Ofis360.Application.Validation;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;
using Ofis360.Infrastructure.Persistence;
using static Ofis360.Application.Tests.Fixtures.TestHelpers;

namespace Ofis360.Application.Tests.Integration;

public sealed class DataIntegrityWorld : IAsyncLifetime
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

/// <summary>Veri butunlugu: es zamanli limit, kapanan alandaki rezervasyonlar, sonuclanmis kayitlarin korunmasi.</summary>
[Collection(OracleCollection.Name)]
public class DataIntegrityTests(DataIntegrityWorld w) : IClassFixture<DataIntegrityWorld>
{
    private static int _offset;

    private WorldFixture Fx => w.Fx;
    private string Today => Fx.Clock.TodayKey();
    private AuditActor Admin => Actor(w.Admin);
    private SpaceService Spaces => Fx.Svc<SpaceService>();
    private string Desk1 => Fx.World.Desks["T-A01"];

    /// <summary>Her test ileride ayri bir gune yazar.</summary>
    private string NextDay() => DayKeys.AddDays(Today, 100 + Interlocked.Increment(ref _offset));

    private async Task<string> Desk(string deskCode, string dayKey, SessionUser user, string status = DeskReservationStatus.Booked)
    {
        var deskId = Fx.World.Desks[deskCode];
        var active = status is DeskReservationStatus.Booked or DeskReservationStatus.CheckedIn;
        var row = new DeskReservation
        {
            DeskId = deskId, UserId = user.Id, Date = DayKeys.ToDateOnly(dayKey), Status = status,
            ActiveDeskKey = active ? DeskReservation.BuildActiveDeskKey(deskId, dayKey) : null,
            ActiveUserDayKey = active ? DeskReservation.BuildActiveUserDayKey(user.Id, dayKey) : null,
        };
        await using var db = Fx.Db();
        db.DeskReservations.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    private async Task<string> Room(string dayKey, int start, int end, SessionUser user, string status = RoomReservationStatus.Booked, DateTimeOffset? startedAt = null)
    {
        var row = new RoomReservation
        {
            RoomId = Fx.World.RoomId, UserId = user.Id, Date = DayKeys.ToDateOnly(dayKey), Status = status, Title = "Toplantı", Purpose = "Ekip", AttendeeCount = 2,
            StartMinutes = start, EndMinutes = end, StartedAt = startedAt,
            StartAt = new DateTimeOffset(DayKeys.WithMinutes(dayKey, start), TimeSpan.Zero),
            EndAt = new DateTimeOffset(DayKeys.WithMinutes(dayKey, end), TimeSpan.Zero),
        };
        await using var db = Fx.Db();
        db.RoomReservations.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    private async Task<DeskReservation> LoadDesk(string id)
    {
        await using var db = Fx.Db();
        return await db.DeskReservations.AsNoTracking().SingleAsync(r => r.Id == id);
    }

    private async Task<string> RoomStatus(string id)
    {
        await using var db = Fx.Db();
        return (await db.RoomReservations.AsNoTracking().SingleAsync(r => r.Id == id)).Status;
    }

    private async Task<int> Notices(string userId)
    {
        await using var db = Fx.Db();
        return await db.Notifications.CountAsync(n => n.UserId == userId && n.Type == NotificationTypes.ResourceClosed);
    }

    [Fact]
    public async Task Haftalik_limit_es_zamanli_isteklerle_asilamaz()
    {
        await using (var db = Fx.Db())
        {
            await db.ReservationRules.Where(r => r.Key == RuleKeys.WeeklyDeskLimit).ExecuteUpdateAsync(u => u.SetProperty(r => r.Enabled, true).SetProperty(r => r.IntValue, 2));
        }
        var monday = DayKeys.StartOfWeek(DayKeys.AddDays(Today, 7));
        for (var round = 0; round < 4; round++)
        {
            var user = await Fx.CreateTestUserAsync();
            var services = Enumerable.Range(0, 5).Select(_ => Fx.Svc<DeskReservationService>()).ToList();
            var barrier = new Barrier(5);
            var attempts = services.Select((service, i) => Task.Run(async () =>
            {
                barrier.SignalAndWait();
                try
                {
                    var deskCode = round == 0 ? "T-A01" : $"T-A0{round}";
                    await service.CreateAsync(new CreateDeskReservationCommand(Fx.World.Desks[deskCode], DayKeys.AddDays(monday, i)), user);
                    return true;
                }
                catch (AppException)
                {
                    return false;
                }
            })).ToList();
            var results = await Task.WhenAll(attempts);

            Assert.Equal(2, results.Count(r => r));
            await using var db = Fx.Db();
            await db.DeskReservations.Where(r => r.UserId == user.Id).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task Masa_pasife_alininca_gelecek_rezervasyon_iptal_edilir_ve_haber_verilir()
    {
        var user = await Fx.CreateTestUserAsync();
        var future = await Desk("T-A03", NextDay(), user);
        var past = await Desk("T-A03", DayKeys.AddDays(Today, -3), user, DeskReservationStatus.Completed);
        var deskId = Fx.World.Desks["T-A03"];
        try
        {
            await Spaces.UpdateDeskAsync(deskId, new DeskUpdateInput(Active: false), Admin);

            var row = await LoadDesk(future);
            Assert.Equal(DeskReservationStatus.Cancelled, row.Status);
            Assert.Null(row.ActiveDeskKey);
            Assert.Null(row.ActiveUserDayKey);
            Assert.False(string.IsNullOrEmpty(row.CancelReason));
            Assert.Equal(1, await Notices(user.Id));
            Assert.Equal(DeskReservationStatus.Completed, (await LoadDesk(past)).Status);
        }
        finally
        {
            await Spaces.UpdateDeskAsync(deskId, new DeskUpdateInput(Active: true), Admin);
        }
    }

    [Fact]
    public async Task Baska_alanlar_guncellenince_rezervasyon_iptal_edilmez()
    {
        var user = await Fx.CreateTestUserAsync();
        var future = await Desk("T-A04", NextDay(), user);

        await Spaces.UpdateDeskAsync(Fx.World.Desks["T-A04"], new DeskUpdateInput(Popular: false), Admin);
        await Spaces.UpdateZoneAsync(Fx.World.ZoneId, new ZoneUpdateInput(Quiet: false), Admin);

        Assert.Equal(DeskReservationStatus.Booked, (await LoadDesk(future)).Status);
    }

    [Fact]
    public async Task Bolge_kapatilinca_gelecek_rezervasyon_iptal_edilir()
    {
        var user = await Fx.CreateTestUserAsync();
        var future = await Desk("T-A02", NextDay(), user);
        try
        {
            await Spaces.UpdateZoneAsync(Fx.World.ZoneId, new ZoneUpdateInput(Reservable: false), Admin);
            Assert.Equal(DeskReservationStatus.Cancelled, (await LoadDesk(future)).Status);
            Assert.Equal(1, await Notices(user.Id));
        }
        finally
        {
            await Spaces.UpdateZoneAsync(Fx.World.ZoneId, new ZoneUpdateInput(Reservable: true), Admin);
        }
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Kat_kapatilinca_masa_ve_oda_rezervasyonlari_iptal_edilir(bool reservable, bool active)
    {
        var user = await Fx.CreateTestUserAsync();
        var day = NextDay();
        var desk = await Desk("T-A02", day, user);
        var room = await Room(day, 600, 660, user);
        try
        {
            await Spaces.UpdateFloorAsync(Fx.World.FloorId, new FloorUpdateInput(Reservable: reservable, Active: active), Admin);
            Assert.Equal(DeskReservationStatus.Cancelled, (await LoadDesk(desk)).Status);
            Assert.Equal(RoomReservationStatus.Cancelled, await RoomStatus(room));
        }
        finally
        {
            await Spaces.UpdateFloorAsync(Fx.World.FloorId, new FloorUpdateInput(Reservable: true, Active: true), Admin);
        }
    }

    [Fact]
    public async Task Bina_pasife_alinca_rezervasyonlar_iptal_edilir()
    {
        var user = await Fx.CreateTestUserAsync();
        var day = NextDay();
        var desk = await Desk("T-A02", day, user);
        var room = await Room(day, 600, 660, user);
        try
        {
            await Spaces.UpdateBuildingAsync(Fx.World.BuildingId, new BuildingUpdateInput(Active: false), Admin);
            Assert.Equal(DeskReservationStatus.Cancelled, (await LoadDesk(desk)).Status);
            Assert.Equal(RoomReservationStatus.Cancelled, await RoomStatus(room));
        }
        finally
        {
            await Spaces.UpdateBuildingAsync(Fx.World.BuildingId, new BuildingUpdateInput(Active: true), Admin);
        }
    }

    [Fact]
    public async Task Oda_pasife_alinca_rezervasyon_iptal_edilir_baslamis_toplantiya_dokunulmaz()
    {
        var user = await Fx.CreateTestUserAsync();
        var day = NextDay();
        var booked = await Room(day, 600, 660, user);
        var started = await Room(day, 720, 780, user, startedAt: DateTimeOffset.UtcNow);
        try
        {
            await Spaces.UpdateRoomAsync(Fx.World.RoomId, new RoomUpdateInput(Active: false), Admin);
            Assert.Equal(RoomReservationStatus.Cancelled, await RoomStatus(booked));
            Assert.Equal(RoomReservationStatus.Booked, await RoomStatus(started));
            Assert.Equal(1, await Notices(user.Id));
        }
        finally
        {
            await Spaces.UpdateRoomAsync(Fx.World.RoomId, new RoomUpdateInput(Active: true), Admin);
        }
    }

    [Fact]
    public async Task Kapatma_yalnizca_gerceklesmemis_masa_kaydini_iptal_eder()
    {
        var day = NextDay();
        var booked = await Desk("T-A01", day, await Fx.CreateTestUserAsync());
        var noShow = await Desk("T-A02", day, await Fx.CreateTestUserAsync(), DeskReservationStatus.NoShow);
        var completed = await Desk("T-A03", day, await Fx.CreateTestUserAsync(), DeskReservationStatus.Completed);
        var checkedIn = await Desk("T-A04", day, await Fx.CreateTestUserAsync(), DeskReservationStatus.CheckedIn);

        var result = await Fx.Svc<ClosureService>().CreateAsync(new ClosureInput(ClosureScopes.Floor, Fx.World.FloorId, day, day, ClosureReasons.Maintenance, "Test bakımı"), Admin);

        Assert.Equal(1, result.CancelledReservations);
        Assert.Equal(DeskReservationStatus.Cancelled, (await LoadDesk(booked)).Status);
        Assert.Equal(DeskReservationStatus.NoShow, (await LoadDesk(noShow)).Status);
        Assert.Equal(DeskReservationStatus.Completed, (await LoadDesk(completed)).Status);
        Assert.Equal(DeskReservationStatus.CheckedIn, (await LoadDesk(checkedIn)).Status);
    }

    [Fact]
    public async Task Kapatma_biten_veya_baslayan_toplantiya_dokunmaz()
    {
        var dayKey = NextDay();
        var user = await Fx.CreateTestUserAsync();
        var ended = await Room(dayKey, 540, 600, user);
        var started = await Room(dayKey, 780, 900, user, startedAt: DateTimeOffset.UtcNow);
        var empty = await Room(dayKey, 600, 660, user, RoomReservationStatus.NoShow);
        var completed = await Room(dayKey, 660, 720, user, RoomReservationStatus.Completed);
        var later = await Room(dayKey, 960, 1020, user);

        var day = DayKeys.ToDateOnly(dayKey);
        var clock = new FixedClock(new DateTimeOffset(day.ToDateTime(new TimeOnly(14, 0)), TimeSpan.FromHours(3)).ToUniversalTime());
        var service = new ClosureService(Fx.Svc<Ofis360DbContext>(), Fx.Svc<NotificationService>(), Fx.Svc<AuditService>(), clock);
        var result = await service.CreateAsync(new ClosureInput(ClosureScopes.Room, Fx.World.RoomId, dayKey, dayKey, ClosureReasons.Event, "Etkinlik"), Admin);

        Assert.Equal(1, result.CancelledReservations);
        Assert.Equal(RoomReservationStatus.Cancelled, await RoomStatus(later));
        Assert.Equal(RoomReservationStatus.Booked, await RoomStatus(ended));
        Assert.Equal(RoomReservationStatus.Booked, await RoomStatus(started));
        Assert.Equal(RoomReservationStatus.NoShow, await RoomStatus(empty));
        Assert.Equal(RoomReservationStatus.Completed, await RoomStatus(completed));
    }
}
