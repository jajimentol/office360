using Ofis360.Application.Common;
using Ofis360.Application.Identity;
using Ofis360.Application.Services;
using Ofis360.Application.Tests.Fixtures;
using Ofis360.Application.Validation;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;
using Ofis360.Infrastructure.Persistence;
using static Ofis360.Application.Tests.Fixtures.TestHelpers;

namespace Ofis360.Application.Tests.Integration;

public sealed class ScreenAccuracyWorld : IAsyncLifetime
{
    public WorldFixture Fx { get; } = new();
    public SessionUser Admin { get; private set; } = null!;
    public string InactiveDeskId { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await Fx.InitializeAsync();
        Admin = await Fx.CreateTestUserAsync(Roles.Admin);
        await using var db = Fx.Db();
        var desk = new Desk { Code = "T-PASIF", FloorId = Fx.World.FloorId, ZoneId = Fx.World.ZoneId, X = 900, Y = 900, Active = false };
        db.Add(desk);
        await db.SaveChangesAsync();
        InactiveDeskId = desk.Id;
    }

    public Task DisposeAsync() => Fx.DisposeAsync();
}

/// <summary>Ekranlarda gosterilen sayilarin ve filtrelerin gercegi yansitmasi.</summary>
[Collection(OracleCollection.Name)]
public class ScreenAccuracyTests(ScreenAccuracyWorld w) : IClassFixture<ScreenAccuracyWorld>
{
    private static int _offset;

    private WorldFixture Fx => w.Fx;
    private string Today => Fx.Clock.TodayKey();
    private AuditActor Admin => Actor(w.Admin);
    private ClosureService Closures => Fx.Svc<ClosureService>();

    private string PastDay() => DayKeys.AddDays(Today, -200 - Interlocked.Increment(ref _offset));

    private async Task Desk(string deskId, string dayKey, SessionUser user, string status)
    {
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

    private ClosureInput Closure(string start, string end) =>
        new(ClosureScopes.Desk, Fx.World.Desks["T-E01"], start, end, ClosureReasons.Maintenance, "Test bakım");

    private RoomReservationService RoomsAt(IClock clock)
    {
        var db = Fx.Svc<Ofis360DbContext>();
        var rules = Fx.Svc<RuleService>();
        return new RoomReservationService(db, new AvailabilityService(db, rules, clock), rules, Fx.Svc<NotificationService>(), Fx.Svc<AuditService>(), clock);
    }

    [Fact]
    public async Task Haftalik_sayac_bugunku_rezervasyonu_da_sayar()
    {
        var user = await Fx.CreateTestUserAsync();
        await Desk(Fx.World.Desks["T-A02"], Today, user, DeskReservationStatus.Booked);

        var home = await Fx.Svc<PersonalService>().GetEmployeeHomeSummaryAsync(user.Id);
        var personal = await Fx.Svc<PersonalService>().GetPersonalSummaryAsync(user.Id);

        Assert.Equal(1, home.FairUse.WeeklyUsed);
        Assert.Equal(1, personal.FairUse.WeeklyUsed);
    }

    [Fact]
    public async Task Ayni_masanin_gelmedi_ve_yeni_kaydi_tek_doluluk_sayilir()
    {
        var day = PastDay();
        var (first, second) = (await Fx.CreateTestUserAsync(), await Fx.CreateTestUserAsync());
        await Desk(Fx.World.Desks["T-A01"], day, first, DeskReservationStatus.NoShow);
        await Desk(Fx.World.Desks["T-A01"], day, second, DeskReservationStatus.Completed);

        var result = await Fx.Svc<AnalyticsService>().GetAnalyticsAsync(new AnalyticsFilters(day, day, Fx.World.FloorId));

        Assert.Equal(1, result.OccupancyTrend.Single().Reservations);
    }

    [Fact]
    public async Task Kullanimdan_kalkmis_masa_doluluga_girmez()
    {
        var day = PastDay();
        var user = await Fx.CreateTestUserAsync();
        await Desk(w.InactiveDeskId, day, user, DeskReservationStatus.Completed);

        var result = await Fx.Svc<AnalyticsService>().GetAnalyticsAsync(new AnalyticsFilters(day, day, Fx.World.FloorId));

        Assert.Equal(0, result.OccupancyTrend.Single().Reservations);
        Assert.Equal(0, result.FloorPressure.Single().PeakOccupancy);
    }

    [Fact]
    public async Task Bugun_gecmis_saat_uygun_sayilmaz()
    {
        // Istanbul 15:00
        var clock = new FixedClock(new DateTimeOffset(DayKeys.WithMinutes(Today, 12 * 60), TimeSpan.Zero));
        var query = new RoomAvailabilityQuery(Today, FloorId: Fx.World.FloorId);

        var past = await RoomsAt(clock).GetAvailabilityAsync(query with { StartMinutes = 10 * 60, EndMinutes = 11 * 60 });
        var later = await RoomsAt(clock).GetAvailabilityAsync(query with { StartMinutes = 16 * 60, EndMinutes = 17 * 60 });

        Assert.False(past.Rooms.Single(r => r.Id == Fx.World.RoomId).AvailableForRequest);
        Assert.True(later.Rooms.Single(r => r.Id == Fx.World.RoomId).AvailableForRequest);
    }

    [Fact]
    public async Task Ters_saat_araligi_uygun_sayilmaz()
    {
        var day = DayKeys.AddDays(Today, 150);
        var result = await Fx.Svc<RoomReservationService>().GetAvailabilityAsync(new RoomAvailabilityQuery(day, 11 * 60, 10 * 60, FloorId: Fx.World.FloorId));

        Assert.False(result.Rooms.Single(r => r.Id == Fx.World.RoomId).AvailableForRequest);
    }

    [Fact]
    public async Task Bitisi_gecmis_kapatma_olusturulamaz()
    {
        var yesterday = DayKeys.AddDays(Today, -1);
        var error = await ExpectAppErrorAsync(() => Closures.CreateAsync(Closure(DayKeys.AddDays(Today, -3), yesterday), Admin));
        Assert.Equal(400, error.Status);
    }

    [Fact]
    public async Task Baslangici_gecmis_kapatma_olusturulamaz()
    {
        var error = await ExpectAppErrorAsync(() => Closures.CreateAsync(Closure(DayKeys.AddDays(Today, -1), DayKeys.AddDays(Today, 300)), Admin));
        Assert.Equal(400, error.Status);
    }

    [Fact]
    public async Task Kapatma_bitisi_gecmise_cekilemez_ama_bugun_olabilir()
    {
        var closure = new Closure
        {
            Scope = ClosureScopes.Desk, DeskId = Fx.World.Desks["T-E01"], Reason = ClosureReasons.Maintenance, Note = "Eski kayıt", CreatedById = w.Admin.Id,
            StartDate = DayKeys.ToDateOnly(DayKeys.AddDays(Today, -5)), EndDate = DayKeys.ToDateOnly(DayKeys.AddDays(Today, 3)),
        };
        await using (var db = Fx.Db())
        {
            db.Closures.Add(closure);
            await db.SaveChangesAsync();
        }

        var error = await ExpectAppErrorAsync(() => Closures.UpdateAsync(closure.Id, new ClosureUpdateInput(EndDayKey: DayKeys.AddDays(Today, -1)), Admin));
        Assert.Equal(400, error.Status);

        var updated = await Closures.UpdateAsync(closure.Id, new ClosureUpdateInput(EndDayKey: Today, Active: false), Admin);
        Assert.Equal(DayKeys.ToDateOnly(Today), updated.EndDate);
    }
}
