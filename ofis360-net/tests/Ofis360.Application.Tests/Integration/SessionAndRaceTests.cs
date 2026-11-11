using Microsoft.EntityFrameworkCore;
using Ofis360.Application.Common;
using Ofis360.Application.Identity;
using Ofis360.Application.Services;
using Ofis360.Application.Tests.Fixtures;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;

namespace Ofis360.Application.Tests.Integration;

public sealed class SessionWorld : IAsyncLifetime
{
    public WorldFixture Fx { get; } = new();

    public async Task InitializeAsync() => await Fx.InitializeAsync();

    public Task DisposeAsync() => Fx.DisposeAsync();
}

[Collection(OracleCollection.Name)]
public class SessionAndRaceTests(SessionWorld w) : IClassFixture<SessionWorld>
{
    private WorldFixture Fx => w.Fx;
    private SessionService Sessions => Fx.Svc<SessionService>();

    private async Task Change(string token, Action<AppSession> change)
    {
        var hash = SessionService.Hash(token);
        await using var db = Fx.Db();
        change(await db.AppSessions.SingleAsync(s => s.TokenHash == hash));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Bilet_kullaniciyi_cozer_ve_veritabaninda_acik_durmaz()
    {
        var user = await Fx.CreateTestUserAsync();
        var token = await Sessions.CreateAsync(user.Id);

        Assert.True(token.Length >= 40);
        Assert.NotEqual(user.Id, token);
        var resolved = await Sessions.ResolveAsync(token);
        Assert.Equal(user.Id, resolved?.Id);

        await using var db = Fx.Db();
        Assert.False(await db.AppSessions.AnyAsync(s => s.TokenHash == token));
        Assert.True(await db.AppSessions.AnyAsync(s => s.UserId == user.Id));
    }

    [Fact]
    public async Task Kullanici_kimligi_bilet_olarak_gecmez()
    {
        var user = await Fx.CreateTestUserAsync();
        await Sessions.CreateAsync(user.Id);
        Assert.Null(await Sessions.ResolveAsync(user.Id));
        Assert.Null(await Sessions.ResolveAsync("rastgele"));
        Assert.Null(await Sessions.ResolveAsync(null));
    }

    [Fact]
    public async Task Cikis_oturumu_sunucuda_kapatir()
    {
        var user = await Fx.CreateTestUserAsync();
        var token = await Sessions.CreateAsync(user.Id);
        await Sessions.RevokeAsync(token);
        Assert.Null(await Sessions.ResolveAsync(token));
    }

    [Fact]
    public async Task Mutlak_sure_dolunca_gecersiz()
    {
        var token = await Sessions.CreateAsync((await Fx.CreateTestUserAsync()).Id);
        await Change(token, s => s.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1));
        Assert.Null(await Sessions.ResolveAsync(token));
    }

    [Fact]
    public async Task Bosta_kalma_suresi_dolunca_gecersiz()
    {
        var token = await Sessions.CreateAsync((await Fx.CreateTestUserAsync()).Id);
        await Change(token, s => s.LastSeenAt = DateTimeOffset.UtcNow - SessionPolicy.Default.Idle - TimeSpan.FromMinutes(1));
        Assert.Null(await Sessions.ResolveAsync(token));
    }

    [Fact]
    public async Task Kullanim_bosta_kalma_suresini_uzatir()
    {
        var token = await Sessions.CreateAsync((await Fx.CreateTestUserAsync()).Id);
        var old = DateTimeOffset.UtcNow.AddMinutes(-30);
        await Change(token, s => s.LastSeenAt = old);

        Assert.NotNull(await Sessions.ResolveAsync(token));

        var hash = SessionService.Hash(token);
        await using var db = Fx.Db();
        Assert.True((await db.AppSessions.SingleAsync(s => s.TokenHash == hash)).LastSeenAt > old.AddMinutes(25));
    }

    [Fact]
    public async Task Pasif_kullanicinin_oturumu_duser()
    {
        var user = await Fx.CreateTestUserAsync();
        var token = await Sessions.CreateAsync(user.Id);
        await using (var db = Fx.Db())
        {
            (await db.Users.SingleAsync(u => u.Id == user.Id)).Active = false;
            await db.SaveChangesAsync();
        }
        Assert.Null(await Sessions.ResolveAsync(token));
    }

    private sealed class FakeValidator(string? email, bool configured = true) : ISsoTicketValidator
    {
        public bool IsConfigured => configured;
        public Task<string?> ValidateAsync(string ticket, CancellationToken ct = default) => Task.FromResult(email);
    }

    [Fact]
    public async Task Kurumsal_giris_yapilandirilmadiysa_kapali_kalir()
    {
        await using var db = Fx.Db();
        var error = await Assert.ThrowsAsync<AppException>(() => new SsoSignInService(db, new NotConfiguredSsoTicketValidator()).ResolveUserAsync("x"));
        Assert.Equal(503, error.Status);
    }

    [Fact]
    public async Task Kurumsal_giris_gecerli_bileti_kisiye_baglar()
    {
        var user = await Fx.CreateTestUserAsync();
        await using var db = Fx.Db();
        var found = await new SsoSignInService(db, new FakeValidator(user.Email.ToUpperInvariant())).ResolveUserAsync("x");
        Assert.Equal(user.Id, found.Id);

        Assert.Equal(401, (await Assert.ThrowsAsync<AppException>(() => new SsoSignInService(db, new FakeValidator(null)).ResolveUserAsync("x"))).Status);
        Assert.Equal(403, (await Assert.ThrowsAsync<AppException>(() => new SsoSignInService(db, new FakeValidator("yok@ray.test")).ResolveUserAsync("x"))).Status);
    }

    private async Task<(string Id, SessionUser Owner)> TodayReservation(string deskCode)
    {
        var owner = await Fx.CreateTestUserAsync();
        var created = await Fx.Svc<DeskReservationService>().CreateAsync(new CreateDeskReservationCommand(Fx.World.Desks[deskCode], Fx.Clock.TodayKey()), owner);
        return (created.Id, owner);
    }

    [Fact]
    public async Task Check_in_ve_iptal_yarisinda_kayit_tutarli_kalir()
    {
        var admin = await Fx.CreateTestUserAsync(Roles.Admin);

        for (var i = 0; i < 15; i++)
        {
            var (id, owner) = await TodayReservation("T-A01");
            var barrier = new Barrier(2);
            var checkIn = Task.Run(async () =>
            {
                barrier.SignalAndWait();
                try { await Fx.Svc<DeskReservationService>().CheckInAsync(id, owner); } catch (AppException) { }
            });
            var cancel = Task.Run(async () =>
            {
                barrier.SignalAndWait();
                await Fx.Svc<DeskReservationService>().CancelAsync(id, null, admin);
            });
            await Task.WhenAll(checkIn, cancel);

            await using var db = Fx.Db();
            var row = await db.DeskReservations.AsNoTracking().SingleAsync(r => r.Id == id);
            Assert.Equal(DeskReservationStatus.Cancelled, row.Status);
            Assert.Null(row.ActiveDeskKey);
            Assert.Null(row.ActiveUserDayKey);
        }
    }

    [Fact]
    public async Task Iptal_edilmis_kayda_check_in_ikinci_iptal_409()
    {
        var (id, owner) = await TodayReservation("T-A02");
        var desks = Fx.Svc<DeskReservationService>();
        await desks.CancelAsync(id, null, owner);

        Assert.Equal(409, (await Assert.ThrowsAsync<AppException>(() => desks.CheckInAsync(id, owner))).Status);
        Assert.Equal(409, (await Assert.ThrowsAsync<AppException>(() => desks.CancelAsync(id, null, owner))).Status);
    }

    [Fact]
    public async Task Check_in_iki_kez_guvenli_ve_sonra_iptal_edilebilir()
    {
        var (id, owner) = await TodayReservation("T-A03");
        var desks = Fx.Svc<DeskReservationService>();

        Assert.Equal(DeskReservationStatus.CheckedIn, (await desks.CheckInAsync(id, owner)).Status);
        Assert.Equal(DeskReservationStatus.CheckedIn, (await desks.CheckInAsync(id, owner)).Status);
        var cancelled = await desks.CancelAsync(id, "Vazgeçti", owner);
        Assert.Equal(DeskReservationStatus.Cancelled, cancelled.Status);
        Assert.Null(cancelled.ActiveDeskKey);
        Assert.NotNull(cancelled.CancelledAt);
        Assert.NotNull(cancelled.Desk);
    }

    [Fact]
    public async Task Aktif_durumda_anahtarsiz_kayit_veritabanina_giremez()
    {
        var user = await Fx.CreateTestUserAsync();
        await using var db = Fx.Db();
        db.DeskReservations.Add(new DeskReservation
        {
            DeskId = Fx.World.Desks["T-A04"], UserId = user.Id, Date = DayKeys.ToDateOnly(Fx.Clock.TodayKey()), Status = DeskReservationStatus.CheckedIn,
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}

public class StartupConfigurationTests
{
    private const string GoodConnection = "User Id=APP;Password=gizli;Data Source=db:1521/PROD";

    [Fact]
    public void Gelistirmede_serbest()
    {
        Assert.Empty(StartupConfiguration.Validate(true, null, ["http://localhost:4210"], AuthModes.Demo));
        Assert.Equal(AuthModes.Demo, AuthModes.Resolve(null, isDevelopment: true));
    }

    [Fact]
    public void Uretimde_varsayilan_kurumsal_giristir() =>
        Assert.Equal(AuthModes.Handoff, AuthModes.Resolve(null, isDevelopment: false));

    [Fact]
    public void Dogru_uretim_ayari_gecer() =>
        Assert.Empty(StartupConfiguration.Validate(false, GoodConnection, ["https://ofis.sirket.com"], AuthModes.Handoff));

    [Fact]
    public void Ayni_kaynaktan_sunulan_kurulumda_cors_listesi_bos_olabilir() =>
        Assert.Empty(StartupConfiguration.Validate(false, GoodConnection, [], AuthModes.Handoff));

    [Theory]
    [InlineData(null, "https://a.com", AuthModes.Handoff)]
    [InlineData("", "https://a.com", AuthModes.Handoff)]
    [InlineData("User Id=OFIS360;Password=Ofis360Dev_2026;Data Source=x", "https://a.com", AuthModes.Handoff)]
    [InlineData(GoodConnection, "http://a.com", AuthModes.Handoff)]
    [InlineData(GoodConnection, "https://localhost:4210", AuthModes.Handoff)]
    [InlineData(GoodConnection, "http://localhost:4210", AuthModes.Handoff)]
    [InlineData(GoodConnection, "https://a.com", AuthModes.Demo)]
    [InlineData(GoodConnection, "https://a.com", "Bilinmeyen")]
    public void Eksik_veya_tehlikeli_ayar_uretimde_reddedilir(string? connection, string origin, string mode) =>
        Assert.NotEmpty(StartupConfiguration.Validate(false, connection, [origin], mode));
}
