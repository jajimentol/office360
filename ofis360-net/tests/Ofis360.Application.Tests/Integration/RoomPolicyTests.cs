using Microsoft.EntityFrameworkCore;
using Ofis360.Application.Common;
using Ofis360.Application.Identity;
using Ofis360.Application.Services;
using Ofis360.Application.Tests.Fixtures;
using Ofis360.Application.Validation;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;
using Ofis360.Domain.Rules;
using Ofis360.Seed;
using static Ofis360.Application.Tests.Fixtures.TestHelpers;

namespace Ofis360.Application.Tests.Integration;

public sealed class RoomPolicyWorld : IAsyncLifetime
{
    public WorldFixture Fx { get; } = new();
    public SessionUser Admin { get; private set; } = null!;
    public Dictionary<string, string> Rooms { get; } = [];
    public string FloorId { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await Fx.InitializeAsync();
        Admin = await Fx.CreateTestUserAsync(Roles.Admin);
        await using var db = Fx.Db();
        FloorId = (await PalladiumSeeder.SeedAsync(db)).Floor.Id;
        await EkBinaRoomSeeder.ApplyAsync(db);
        foreach (var room in await db.MeetingRooms.Where(r => r.FloorId == FloorId).ToListAsync()) Rooms[room.Code] = room.Id;
    }

    public Task DisposeAsync() => Fx.DisposeAsync();
}

[Collection(OracleCollection.Name)]
public class RoomPolicyTests(RoomPolicyWorld w) : IClassFixture<RoomPolicyWorld>
{
    private const string Large = "PT36-A09";
    private const string Entrance = "PT36-A02";
    private const string Team = "PT36-A06";

    private static int _slot;

    private WorldFixture Fx => w.Fx;
    private RoomReservationService Service => Fx.Svc<RoomReservationService>();
    private string Today => Fx.Clock.TodayKey();
    private string WorkDay(int n) => WorkCalendar.Default.LatestBookableDay(Today, n);

    /// <summary>Testler birbirinin saatine denk gelmesin diye her rezervasyona ayri bir saat verir.</summary>
    private static int NextStart() => 480 + 30 * (Interlocked.Increment(ref _slot) % 18);

    private async Task<SessionUser> User(string? grade, bool secretary = false)
    {
        var user = await Fx.CreateTestUserAsync();
        await using var db = Fx.Db();
        var row = await db.Users.SingleAsync(u => u.Id == user.Id);
        row.Grade = grade;
        row.IsSecretary = secretary;
        await db.SaveChangesAsync();
        return user with { Grade = grade, IsSecretary = secretary };
    }

    private Task<RoomReservation> Book(string code, string day, int attendees, SessionUser session, string? target = null)
    {
        var start = NextStart();
        return Service.CreateAsync(new CreateRoomReservationCommand(w.Rooms[code], day, start, start + 30, "Toplantı", "Ekip", attendees, target), session);
    }

    private static async Task Blocked(Func<Task> act, string code)
    {
        var error = await Assert.ThrowsAsync<AppException>(act);
        Assert.Contains(error.Violations ?? [], v => v.Code == code);
    }

    private Task SetPriority(string code, params string[] userIds) => Fx.Svc<RoomPolicyService>().SaveAsync(w.Rooms[code], new RoomPolicyInput(
        false, 1, EkBinaRoomSeederEntrance, userIds), Actor(w.Admin));

    private static readonly RoomGradeRuleInput[] EkBinaRoomSeederEntrance =
        [new(Grades.Manager, 5), new(Grades.SeniorManager, 5), new(Grades.Director, 5), new(Grades.DeputyGm, 1, true), new(Grades.Gm, 1, true)];

    [Fact]
    public async Task Uzman_hicbir_odayi_acamaz()
    {
        var user = await User(Grades.Specialist);
        await Blocked(() => Book(Team, WorkDay(1), 3, user), "GRADE_NOT_ALLOWED");
        await Blocked(() => Book(Entrance, WorkDay(1), 5, user), "GRADE_NOT_ALLOWED");
        await Blocked(() => Book(Large, WorkDay(1), 9, user), "SECRETARY_ONLY");
    }

    [Fact]
    public async Task Mudur_sekizlik_odayi_bes_kisiyle_acar_besligi_acamaz()
    {
        await SetPriority(Entrance);
        var manager = await User(Grades.Manager);
        await Blocked(() => Book(Entrance, WorkDay(1), 4, manager), "MIN_ATTENDEES");
        Assert.Equal("BOOKED", (await Book(Entrance, WorkDay(1), 5, manager)).Status);
        await Blocked(() => Book(Team, WorkDay(1), 3, manager), "GRADE_NOT_ALLOWED");
    }

    [Fact]
    public async Task Kidemli_mudur_ve_direktor_beslikte_uc_kisi_olmali()
    {
        foreach (var grade in new[] { Grades.SeniorManager, Grades.Director })
        {
            var user = await User(grade);
            await Blocked(() => Book(Team, WorkDay(2), 2, user), "MIN_ATTENDEES");
            Assert.Equal("BOOKED", (await Book(Team, WorkDay(2), 3, user)).Status);
        }
    }

    [Fact]
    public async Task Gmy_ve_gm_tek_basina_acar_ama_on_ikilik_sekretere_aittir()
    {
        await SetPriority(Entrance);
        foreach (var grade in new[] { Grades.DeputyGm, Grades.Gm })
        {
            var user = await User(grade);
            Assert.Equal("BOOKED", (await Book(Team, WorkDay(3), 1, user)).Status);
            Assert.Equal("BOOKED", (await Book(Entrance, WorkDay(1), 1, user)).Status);
            await Blocked(() => Book(Large, WorkDay(1), 1, user), "SECRETARY_ONLY");
        }
    }

    [Fact]
    public async Task Sekreter_on_ikilik_odayi_kisi_sarti_ve_pencere_olmadan_acar()
    {
        await using (var db = Fx.Db())
        {
            var advance = await db.ReservationRules.SingleAsync(r => r.Key == RuleKeys.AdvanceBookingDays);
            advance.IntValue = 2;
            var workingDays = await db.ReservationRules.SingleAsync(r => r.Key == RuleKeys.WorkingDaysOnly);
            workingDays.Enabled = true;
            await db.SaveChangesAsync();
        }

        try
        {
            var secretary = await User(Grades.Specialist, secretary: true);
            Assert.Equal("BOOKED", (await Book(Large, WorkDay(25), 1, secretary)).Status);

            var saturday = DayKeys.AddDays(Today, 1);
            while (DayKeys.WeekdayIndex(saturday) != 6) saturday = DayKeys.AddDays(saturday, 1);
            Assert.Equal("BOOKED", (await Book(Large, saturday, 1, secretary)).Status);

            var plain = await User(Grades.Gm);
            await Blocked(() => Book(Team, saturday, 1, plain), "NON_WORKING_DAY");
        }
        finally
        {
            await using var db = Fx.Db();
            (await db.ReservationRules.SingleAsync(r => r.Key == RuleKeys.AdvanceBookingDays)).IntValue = 14;
            (await db.ReservationRules.SingleAsync(r => r.Key == RuleKeys.WorkingDaysOnly)).Enabled = false;
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Sekreter_baskasi_adina_acar_ve_iptal_eder_calisan_edemez()
    {
        var secretary = await User(Grades.Specialist, secretary: true);
        var target = await User(Grades.Specialist);
        var other = await User(Grades.Specialist);

        var reservation = await Book(Large, WorkDay(4), 2, secretary, target.Id);
        Assert.Equal(target.Id, reservation.UserId);
        Assert.Equal(ReservationSources.Admin, reservation.Source);

        await Assert.ThrowsAsync<AppException>(() => Book(Team, WorkDay(4), 3, other, target.Id));
        await Assert.ThrowsAsync<AppException>(() => Service.CancelAsync(reservation.Id, null, other));

        var cancelled = await Service.CancelAsync(reservation.Id, "Planlar değişti", secretary);
        Assert.Equal(RoomReservationStatus.Cancelled, cancelled.Status);
    }

    [Fact]
    public async Task Sekreter_baskasi_adina_masa_acar()
    {
        var secretary = await User(Grades.Specialist, secretary: true);
        var target = await User(Grades.Specialist);
        var desks = Fx.Svc<DeskReservationService>();

        var reservation = await desks.CreateAsync(new CreateDeskReservationCommand(Fx.World.Desks["T-A01"], WorkDay(5), null, target.Id), secretary);
        Assert.Equal(target.Id, reservation.UserId);
        var cancelled = await desks.CancelAsync(reservation.Id, null, secretary);
        Assert.Equal(DeskReservationStatus.Cancelled, cancelled.Status);
    }

    [Fact]
    public async Task Olmayan_kisi_adina_rezervasyon_yapilamaz()
    {
        var secretary = await User(Grades.Specialist, secretary: true);
        var error = await Assert.ThrowsAsync<AppException>(() => Book(Large, WorkDay(6), 2, secretary, "yok"));
        Assert.Equal(404, error.Status);
    }

    [Fact]
    public async Task Oncelikli_kisi_erken_alir_digerleri_son_is_gununu_bekler()
    {
        var holder = await User(Grades.Manager);
        var other = await User(Grades.Manager);
        await SetPriority(Entrance, holder.Id);
        try
        {
            await Blocked(() => Book(Entrance, WorkDay(3), 5, other), "PRIORITY_WINDOW");
            Assert.Equal("BOOKED", (await Book(Entrance, WorkDay(3), 5, holder)).Status);
            Assert.Equal("BOOKED", (await Book(Entrance, WorkDay(1), 5, other)).Status);
        }
        finally
        {
            await SetPriority(Entrance);
        }
    }

    [Fact]
    public async Task Gm_ve_gmy_onceligi_beklemez_direktor_bekler()
    {
        var holder = await User(Grades.Manager);
        await SetPriority(Entrance, holder.Id);
        try
        {
            foreach (var grade in new[] { Grades.DeputyGm, Grades.Gm })
            {
                Assert.Equal("BOOKED", (await Book(Entrance, WorkDay(4), 1, await User(grade))).Status);
            }
            var director = await User(Grades.Director);
            await Blocked(() => Book(Entrance, WorkDay(4), 5, director), "PRIORITY_WINDOW");
        }
        finally
        {
            await SetPriority(Entrance);
        }
    }

    [Fact]
    public async Task Oda_listesi_kisiti_gosterir_alternatifte_onermez()
    {
        var specialist = await User(Grades.Specialist);
        var query = new RoomAvailabilityQuery(WorkDay(12), 600, 660, MinCapacity: 1, FloorId: w.FloorId, UserId: specialist.Id);
        var result = await Service.GetAvailabilityAsync(query);

        Assert.All(result.Rooms, r => { Assert.NotNull(r.AccessReason); Assert.False(r.AvailableForRequest); });
        var large = result.Rooms.Single(r => r.Code == Large);
        Assert.Contains("sekreter", large.AccessReason!);

        var director = await User(Grades.Director);
        var forDirector = await Service.GetAvailabilityAsync(query with { MinCapacity = 3, UserId = director.Id });
        var team = forDirector.Rooms.Single(r => r.Code == Team);
        Assert.Null(team.AccessReason);
        Assert.Equal(3, team.MinAttendees);
        Assert.True(team.AvailableForRequest);
        Assert.False(forDirector.Rooms.Single(r => r.Code == Large).AvailableForRequest);

        var suggestion = await Service.SuggestAlternativesAsync(WorkDay(12), w.Rooms[Team], 600, 660, 3, default, director.Id);
        Assert.DoesNotContain(suggestion.AlternativeRooms, r => r.Code == Large);
    }

    [Fact]
    public async Task Politika_gecersiz_degerleri_reddeder_ve_denetim_yazar()
    {
        var policy = Fx.Svc<RoomPolicyService>();
        var room = w.Rooms["PT36-B09"];
        var actor = Actor(w.Admin);
        Task Save(RoomPolicyInput input) => policy.SaveAsync(room, input, actor);

        await Assert.ThrowsAsync<AppException>(() => Save(new RoomPolicyInput(false, null, [new("STAJYER", 1)], [])));
        await Assert.ThrowsAsync<AppException>(() => Save(new RoomPolicyInput(false, null, [new(Grades.Gm, 0)], [])));
        await Assert.ThrowsAsync<AppException>(() => Save(new RoomPolicyInput(false, null, [new(Grades.Gm, 6)], [])));
        await Assert.ThrowsAsync<AppException>(() => Save(new RoomPolicyInput(false, null, [new(Grades.Gm, 1), new(Grades.Gm, 2)], [])));
        await Assert.ThrowsAsync<AppException>(() => Save(new RoomPolicyInput(false, 91, [], [])));
        await Assert.ThrowsAsync<AppException>(() => Save(new RoomPolicyInput(false, null, [], [w.Admin.Id])));
        await Assert.ThrowsAsync<AppException>(() => Save(new RoomPolicyInput(false, 1, [], ["yok"])));
        await Assert.ThrowsAsync<AppException>(() => policy.GetAsync("yok"));

        var saved = await policy.SaveAsync(room, new RoomPolicyInput(false, 2, [new(Grades.Gm, 2)], [w.Admin.Id]), actor);
        Assert.Equal(2, saved.PriorityOpenDays);
        Assert.Equal(w.Admin.FullName, Assert.Single(saved.PriorityUsers).FullName);
        Assert.Equal(2, Assert.Single(saved.GradeRules).MinAttendees);

        await using var db = Fx.Db();
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == AuditActions.RoomPolicyUpdate && a.EntityId == room));
    }

    [Fact]
    public async Task Form_komutu_duzenlenen_odaya_dokunmaz_ve_tekrar_yazmaz()
    {
        await using var db = Fx.Db();
        Assert.Equal(0, await EkBinaRoomSeeder.ApplyAsync(db));

        var large = await db.MeetingRooms.AsNoTracking().SingleAsync(r => r.Code == Large);
        Assert.True(large.SecretaryOnly);
        var entrance = await db.MeetingRooms.AsNoTracking().Include(r => r.AccessRules).SingleAsync(r => r.Code == "PT36-B02");
        Assert.Equal(5, entrance.AccessRules.Count);
        Assert.Equal(1, entrance.PriorityOpenDays);
        Assert.Equal(5, entrance.AccessRules.Single(r => r.Grade == Grades.Manager).MinAttendees);
        Assert.Equal(1, entrance.AccessRules.Single(r => r.Grade == Grades.Gm).MinAttendees);
        Assert.True(entrance.AccessRules.Single(r => r.Grade == Grades.Gm).BypassPriority);
        Assert.True(entrance.AccessRules.Single(r => r.Grade == Grades.DeputyGm).BypassPriority);
        Assert.False(entrance.AccessRules.Single(r => r.Grade == Grades.Director).BypassPriority);
        var team = await db.MeetingRooms.AsNoTracking().Include(r => r.AccessRules).SingleAsync(r => r.Code == "PT36-B06");
        Assert.DoesNotContain(team.AccessRules, r => r.Grade == Grades.Manager);
    }

    [Fact]
    public async Task Kisi_aramasi_yalniz_aktifleri_getirir()
    {
        await using (var db = Fx.Db())
        {
            db.Users.Add(new AppUser { Email = "aramapasif@ray.test", FullName = "Aramalık Pasif", Title = "Uzman", DepartmentId = Fx.World.DepartmentId, Active = false });
            db.Users.Add(new AppUser { Email = "aramaaktif@ray.test", FullName = "Aramalık Aktif", Title = "Uzman", DepartmentId = Fx.World.DepartmentId });
            await db.SaveChangesAsync();
        }
        var found = await Fx.Svc<UserService>().SearchPeopleAsync("aramalık");
        Assert.Equal("Aramalık Aktif", Assert.Single(found).FullName);
    }
}
