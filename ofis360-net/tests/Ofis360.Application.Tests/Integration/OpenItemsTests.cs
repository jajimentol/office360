using Microsoft.EntityFrameworkCore;
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

public sealed class OpenItemsWorld : IAsyncLifetime
{
    public WorldFixture Fx { get; } = new();
    public SessionUser AdminA { get; private set; } = null!;
    public SessionUser AdminB { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Fx.InitializeAsync();
        AdminA = await Fx.CreateTestUserAsync(Roles.Admin);
        AdminB = await Fx.CreateTestUserAsync(Roles.Admin);
    }

    public Task DisposeAsync() => Fx.DisposeAsync();
}

/// <summary>Son admin korumasi, bitmis toplanti, kullanim ozeti ve oda onceligi eksigi.</summary>
[Collection(OracleCollection.Name)]
public class OpenItemsTests(OpenItemsWorld w) : IClassFixture<OpenItemsWorld>
{
    private WorldFixture Fx => w.Fx;
    private string Today => Fx.Clock.TodayKey();

    private async Task OnlyTwoActiveAdmins()
    {
        await using var db = Fx.Db();
        await db.Users.Where(u => u.Role == Roles.Admin && u.Id != w.AdminA.Id && u.Id != w.AdminB.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.Active, false));
        await db.Users.Where(u => u.Id == w.AdminA.Id || u.Id == w.AdminB.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.Active, true));
    }

    private static async Task<bool> TryDeactivate(UserService users, string targetId, AuditActor actor)
    {
        try
        {
            await users.UpdateAsync(targetId, new UserUpdateInput(Active: false), actor);
            return true;
        }
        catch (AppException ex) when (ex.Status == 400)
        {
            return false;
        }
    }

    private RoomReservationService RoomsAt(IClock clock)
    {
        var db = Fx.Svc<Ofis360DbContext>();
        var rules = Fx.Svc<RuleService>();
        return new RoomReservationService(db, new AvailabilityService(db, rules, clock), rules, Fx.Svc<NotificationService>(), Fx.Svc<AuditService>(), clock);
    }

    private async Task<string> Room(int start, int end, SessionUser user)
    {
        var row = new RoomReservation
        {
            RoomId = Fx.World.SmallRoomId, UserId = user.Id, Date = DayKeys.ToDateOnly(Today), Status = RoomReservationStatus.Booked,
            Title = "Toplantı", Purpose = "Ekip", AttendeeCount = 2, StartMinutes = start, EndMinutes = end,
            StartAt = new DateTimeOffset(DayKeys.WithMinutes(Today, start), TimeSpan.Zero),
            EndAt = new DateTimeOffset(DayKeys.WithMinutes(Today, end), TimeSpan.Zero),
        };
        await using var db = Fx.Db();
        db.RoomReservations.Add(row);
        await db.SaveChangesAsync();
        return row.Id;
    }

    private FixedClock Afternoon() => new(new DateTimeOffset(DayKeys.WithMinutes(Today, 12 * 60), TimeSpan.Zero)); // Istanbul 15:00

    private async Task Desk(string code, string dayKey, SessionUser user, string status)
    {
        await using var db = Fx.Db();
        db.DeskReservations.Add(new DeskReservation { DeskId = Fx.World.Desks[code], UserId = user.Id, Date = DayKeys.ToDateOnly(dayKey), Status = status });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Eszamanli_islemde_son_admin_korunur()
    {
        for (var round = 0; round < 8; round++)
        {
            await OnlyTwoActiveAdmins();
            var (first, second) = (Fx.Svc<UserService>(), Fx.Svc<UserService>());

            var results = await Task.WhenAll(
                TryDeactivate(first, w.AdminB.Id, Actor(w.AdminA)),
                TryDeactivate(second, w.AdminA.Id, Actor(w.AdminB)));

            Assert.Equal(1, results.Count(ok => ok));
            await using var db = Fx.Db();
            Assert.Equal(1, await db.Users.CountAsync(u => u.Role == Roles.Admin && u.Active));
        }

        await OnlyTwoActiveAdmins();
    }

    [Fact]
    public async Task Bugun_bitmis_toplanti_iptal_edilemez()
    {
        var user = await Fx.CreateTestUserAsync();
        var id = await Room(10 * 60, 11 * 60, user);

        var error = await ExpectAppErrorAsync(() => RoomsAt(Afternoon()).CancelAsync(id, null, user));

        Assert.Equal(400, error.Status);
    }

    [Fact]
    public async Task Bugun_henuz_bitmemis_toplanti_iptal_edilebilir()
    {
        var user = await Fx.CreateTestUserAsync();
        var id = await Room(16 * 60, 17 * 60, user);

        var row = await RoomsAt(Afternoon()).CancelAsync(id, null, user);

        Assert.Equal(RoomReservationStatus.Cancelled, row.Status);
    }

    [Fact]
    public async Task Kullanim_ozeti_gelmedi_gunlerini_ofis_gunu_saymaz()
    {
        var user = await Fx.CreateTestUserAsync();
        var (came, missed) = (DayKeys.AddDays(Today, -14), DayKeys.AddDays(Today, -7));
        await Desk("T-A01", came, user, DeskReservationStatus.Completed);
        await Desk("T-A02", missed, user, DeskReservationStatus.NoShow);

        var summary = await Fx.Svc<PersonalService>().GetPersonalSummaryAsync(user.Id);

        Assert.Equal(1, summary.WeeklyTrend.Sum(p => p.Days));
        Assert.Equal(1, summary.WeekdayDistribution.Sum(p => p.Count));
        Assert.Equal(1, summary.Totals.Last30Days);
        Assert.Equal("T-A01", Assert.Single(summary.FavoriteDesks).Code);
        Assert.Equal(1, Assert.Single(summary.FloorPreference).Count);
        Assert.Equal(1, summary.Totals.NoShow);
    }

    [Fact]
    public async Task Oncelik_suresi_olup_aktif_kisisi_olmayan_oda_uyarilir()
    {
        var person = await Fx.CreateTestUserAsync();
        var service = Fx.Svc<RoomPolicyService>();
        await using (var db = Fx.Db())
        {
            await db.MeetingRooms.Where(r => r.Id == Fx.World.RoomId).ExecuteUpdateAsync(s => s.SetProperty(r => r.PriorityOpenDays, 1));
        }
        Assert.Contains(Fx.World.RoomId, (await service.ListPriorityGapsAsync()).Select(g => g.RoomId));

        await using (var db = Fx.Db())
        {
            db.RoomPriorityUsers.Add(new RoomPriorityUser { RoomId = Fx.World.RoomId, UserId = person.Id });
            await db.SaveChangesAsync();
        }
        Assert.DoesNotContain(Fx.World.RoomId, (await Fx.Svc<RoomPolicyService>().ListPriorityGapsAsync()).Select(g => g.RoomId));

        await using (var db = Fx.Db())
        {
            await db.Users.Where(u => u.Id == person.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.Active, false));
        }
        Assert.Contains(Fx.World.RoomId, (await Fx.Svc<RoomPolicyService>().ListPriorityGapsAsync()).Select(g => g.RoomId));
    }
}
