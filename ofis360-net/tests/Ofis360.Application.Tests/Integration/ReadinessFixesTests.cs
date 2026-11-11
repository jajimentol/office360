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

public sealed class ReadinessWorld : IAsyncLifetime
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
public class ReadinessFixesTests(ReadinessWorld w) : IClassFixture<ReadinessWorld>
{
    private static int _offset;

    private WorldFixture Fx => w.Fx;
    private ClosureService Closures => Fx.Svc<ClosureService>();
    private string Today => Fx.Clock.TodayKey();
    private AuditActor Admin => Actor(w.Admin);

    /// <summary>Her test ileride ayri bir gune yazar; kapatmalar birbirini etkilemez.</summary>
    private string NextDay() => DayKeys.AddDays(Today, 2 + 3 * Interlocked.Increment(ref _offset) % 12);

    private async Task<string> DeskBooking(string deskCode, string dayKey, SessionUser user)
    {
        var deskId = Fx.World.Desks[deskCode];
        var row = new DeskReservation
        {
            DeskId = deskId, UserId = user.Id, Date = DayKeys.ToDateOnly(dayKey),
            ActiveDeskKey = DeskReservation.BuildActiveDeskKey(deskId, dayKey),
            ActiveUserDayKey = DeskReservation.BuildActiveUserDayKey(user.Id, dayKey),
        };
        await using var db = Fx.Db();
        db.DeskReservations.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    private async Task<string> Status(string id)
    {
        await using var db = Fx.Db();
        return (await db.DeskReservations.AsNoTracking().SingleAsync(r => r.Id == id)).Status;
    }

    [Fact]
    public async Task Uzun_notlu_kapatma_basarili_olur_ve_gerekce_kisalir()
    {
        var day = DayKeys.AddDays(Today, 40);
        var id = await DeskBooking("T-A01", day, await Fx.CreateTestUserAsync());
        var note = new string('n', 240);

        var result = await Closures.CreateAsync(new ClosureInput(ClosureScopes.Desk, Fx.World.Desks["T-A01"], day, day, ClosureReasons.Maintenance, note), Admin);
        Assert.Equal(1, result.CancelledReservations);

        await using var db = Fx.Db();
        var row = await db.DeskReservations.AsNoTracking().SingleAsync(r => r.Id == id);
        Assert.Equal(DeskReservationStatus.Cancelled, row.Status);
        Assert.Equal(240, row.CancelReason!.Length);
    }

    [Fact]
    public async Task Kapatma_uzatilinca_yeni_kapsanan_rezervasyon_iptal_olur()
    {
        var start = DayKeys.AddDays(Today, 50);
        var later = DayKeys.AddDays(Today, 53);
        var id = await DeskBooking("T-A02", later, await Fx.CreateTestUserAsync());
        var closure = (await Closures.CreateAsync(new ClosureInput(ClosureScopes.Desk, Fx.World.Desks["T-A02"], start, start, ClosureReasons.Maintenance, "Bakım"), Admin)).Closure;
        Assert.Equal(DeskReservationStatus.Booked, await Status(id));

        await Closures.UpdateAsync(closure.Id, new ClosureUpdateInput(EndDayKey: later), Admin);
        Assert.Equal(DeskReservationStatus.Cancelled, await Status(id));
    }

    [Fact]
    public async Task Kapatma_yeniden_etkinlesince_iptal_eder()
    {
        var day = DayKeys.AddDays(Today, 60);
        var closure = (await Closures.CreateAsync(new ClosureInput(ClosureScopes.Desk, Fx.World.Desks["T-A03"], day, day, ClosureReasons.Maintenance, "Bakım"), Admin)).Closure;
        await Closures.UpdateAsync(closure.Id, new ClosureUpdateInput(Active: false), Admin);
        var id = await DeskBooking("T-A03", day, await Fx.CreateTestUserAsync());
        Assert.Equal(DeskReservationStatus.Booked, await Status(id));

        await Closures.UpdateAsync(closure.Id, new ClosureUpdateInput(Active: true), Admin);
        Assert.Equal(DeskReservationStatus.Cancelled, await Status(id));
    }

    [Fact]
    public async Task Kapatma_bitisi_baslangictan_once_olamaz_not_guncellemesi_iptal_etmez()
    {
        var day = DayKeys.AddDays(Today, 70);
        var id = await DeskBooking("T-A04", day, await Fx.CreateTestUserAsync());
        var other = DayKeys.AddDays(Today, 69);
        var closure = (await Closures.CreateAsync(new ClosureInput(ClosureScopes.Desk, Fx.World.Desks["T-A04"], other, other, ClosureReasons.Maintenance, "Bakım"), Admin)).Closure;

        var error = await Assert.ThrowsAsync<AppException>(() => Closures.UpdateAsync(closure.Id, new ClosureUpdateInput(EndDayKey: DayKeys.AddDays(other, -1)), Admin));
        Assert.Equal(400, error.Status);

        await Closures.UpdateAsync(closure.Id, new ClosureUpdateInput(Note: "Yeni not"), Admin);
        Assert.Equal(DeskReservationStatus.Booked, await Status(id));
    }

    [Fact]
    public async Task Pasife_alinan_kullanicinin_gelecek_rezervasyonlari_iptal_edilir()
    {
        var user = await Fx.CreateTestUserAsync();
        var future = DayKeys.AddDays(Today, 80);
        var past = DayKeys.AddDays(Today, -80);
        var futureId = await DeskBooking("T-A01", future, user);
        var pastId = await DeskBooking("T-A01", past, user);

        var roomDayKey = DayKeys.AddDays(Today, 81);
        var room = new RoomReservation
        {
            RoomId = Fx.World.RoomId, UserId = user.Id, Date = DayKeys.ToDateOnly(roomDayKey), StartMinutes = 600, EndMinutes = 660, Title = "Toplantı", Purpose = "Ekip", AttendeeCount = 2,
            StartAt = new DateTimeOffset(DayKeys.WithMinutes(roomDayKey, 600), TimeSpan.Zero), EndAt = new DateTimeOffset(DayKeys.WithMinutes(roomDayKey, 660), TimeSpan.Zero),
        };
        await using (var db = Fx.Db())
        {
            db.RoomReservations.Add(room);
            await db.SaveChangesAsync();
        }

        await Fx.Svc<UserService>().UpdateAsync(user.Id, new UserUpdateInput(Active: false), Admin);

        await using var verify = Fx.Db();
        var desk = await verify.DeskReservations.AsNoTracking().SingleAsync(r => r.Id == futureId);
        Assert.Equal(DeskReservationStatus.Cancelled, desk.Status);
        Assert.Null(desk.ActiveDeskKey);
        Assert.Equal(DeskReservationStatus.Booked, (await verify.DeskReservations.AsNoTracking().SingleAsync(r => r.Id == pastId)).Status);
        Assert.Equal(RoomReservationStatus.Cancelled, (await verify.RoomReservations.AsNoTracking().SingleAsync(r => r.Id == room.Id)).Status);
    }

    [Fact]
    public async Task Saatlik_giris_istanbul_saatine_gore_gruplanir()
    {
        var day = DayKeys.AddDays(Today, -100);
        var date = DayKeys.ToDateOnly(day);
        var user = await Fx.CreateTestUserAsync();
        await using (var db = Fx.Db())
        {
            db.DeskReservations.Add(new DeskReservation
            {
                DeskId = Fx.World.Desks["T-A02"], UserId = user.Id, Date = date, Status = DeskReservationStatus.Completed,
                CheckedInAt = new DateTimeOffset(date.ToDateTime(new TimeOnly(9, 15)), TimeSpan.FromHours(3)),
            });
            await db.SaveChangesAsync();
        }

        await using var read = Fx.Db();
        var hours = await read.CheckInHourly.Where(x => x.Day == date).ToListAsync();
        Assert.Equal(9, Assert.Single(hours).Hour);
    }

    [Fact]
    public async Task Rezervasyonlarim_iptal_ve_gelmedi_kayitlarini_yaklasanlardan_ayirir()
    {
        var user = await Fx.CreateTestUserAsync();
        var future = DayKeys.AddDays(Today, 90);
        var active = await DeskBooking("T-A01", future, user);
        var noShow = await DeskBooking("T-A02", DayKeys.AddDays(Today, 91), user);
        var cancelled = await DeskBooking("T-A03", DayKeys.AddDays(Today, 92), user);
        await using (var db = Fx.Db())
        {
            await db.DeskReservations.Where(r => r.Id == noShow).ExecuteUpdateAsync(u => u
                .SetProperty(r => r.Status, DeskReservationStatus.NoShow).SetProperty(r => r.ActiveDeskKey, (string?)null).SetProperty(r => r.ActiveUserDayKey, (string?)null));
        }
        await Fx.Svc<DeskReservationService>().CancelAsync(cancelled, "Plan değişti", w.Admin);

        var result = await Fx.Svc<DeskReservationService>().GetUserReservationsAsync(user.Id);
        Assert.Equal(active, Assert.Single(result.UpcomingDesks).Id);
        Assert.Contains(result.PastDesks, r => r.Id == noShow && r.Status == DeskReservationStatus.NoShow);
        Assert.Contains(result.PastDesks, r => r.Id == cancelled && r.Status == DeskReservationStatus.Cancelled);
    }

    [Fact]
    public async Task Sonuclanmis_rezervasyon_iptal_edilemez()
    {
        var user = await Fx.CreateTestUserAsync();
        var id = await DeskBooking("T-A04", DayKeys.AddDays(Today, 95), user);
        await using (var db = Fx.Db())
        {
            await db.DeskReservations.Where(r => r.Id == id).ExecuteUpdateAsync(u => u
                .SetProperty(r => r.Status, DeskReservationStatus.NoShow).SetProperty(r => r.ActiveDeskKey, (string?)null).SetProperty(r => r.ActiveUserDayKey, (string?)null));
        }
        var error = await Assert.ThrowsAsync<AppException>(() => Fx.Svc<DeskReservationService>().CancelAsync(id, null, w.Admin));
        Assert.Equal(409, error.Status);
    }

    [Fact]
    public void Dogrulayicilar_hatali_girisi_reddeder()
    {
        Assert.False(new FloorUpdateValidator().Validate(new FloorUpdateInput(PlanWidth: -5)).IsValid);
        Assert.False(new FloorUpdateValidator().Validate(new FloorUpdateInput(Name: "")).IsValid);
        Assert.True(new FloorUpdateValidator().Validate(new FloorUpdateInput(Name: "36. Kat")).IsValid);
        Assert.False(new ZoneUpdateValidator().Validate(new ZoneUpdateInput(Kind: "HACK")).IsValid);
        Assert.True(new ZoneUpdateValidator().Validate(new ZoneUpdateInput(Kind: ZoneKinds.Work)).IsValid);
    }

    [Theory]
    [InlineData(null, 50)]
    [InlineData(-1, 1)]
    [InlineData(0, 1)]
    [InlineData(20, 20)]
    [InlineData(100000, Paging.MaxTake)]
    public void Sayfa_boyutu_sinirlidir(int? take, int expected) => Assert.Equal(expected, Paging.Take(take));

    [Fact]
    public void Atlama_negatif_olamaz() => Assert.Equal(0, Paging.Skip(-5));
}
