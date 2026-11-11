using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ofis360.Application.Common;
using Ofis360.Application.Identity;
using Ofis360.Application.Services;
using Ofis360.Application.Tests.Fixtures;
using Ofis360.Domain.Common;
using Ofis360.Infrastructure;
using Ofis360.Infrastructure.Persistence;
using Oracle.ManagedDataAccess.Client;

namespace Ofis360.Application.Tests.Integration;

/// <summary>
/// RESERVATION_STATUS_HISTORY (harita §6, onay 2026-11-08): masa/oda rezervasyonlarinin her olusumu ve her durum
/// degisikligi trigger ile yazilir; aktor CLIENT_IDENTIFIER'dir; tablo salt eklemelidir.
/// </summary>
[Collection(OracleCollection.Name)]
public class ReservationStatusHistoryTests(WorldFixture fx) : IClassFixture<WorldFixture>
{
    private sealed record Row(string? OldStatus, string NewStatus, string? ActorId);

    private string Day(int offset) => DayKeys.AddDays(DayKeys.StartOfWeek(DayKeys.AddDays(fx.Clock.TodayKey(), 7)), offset);

    private static async Task<List<Row>> HistoryAsync(string type, string reservationId)
    {
        await using var connection = new OracleConnection(OracleFixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = """
            SELECT OLD_STATUS, NEW_STATUS, ACTOR_ID FROM RESERVATION_STATUS_HISTORY
             WHERE RESERVATION_TYPE = :t AND RESERVATION_ID = :id ORDER BY ID
            """;
        command.Parameters.Add("t", type);
        command.Parameters.Add("id", reservationId);
        var rows = new List<Row>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new Row(reader.IsDBNull(0) ? null : reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2)));
        }
        return rows;
    }

    [Fact]
    public async Task Masa_rezervasyonu_olusma_ve_iptal_gecislerini_yazar()
    {
        var user = await fx.CreateTestUserAsync();
        var svc = fx.Svc<DeskReservationService>();
        var reservation = await svc.CreateAsync(new CreateDeskReservationCommand(fx.World.Desks["T-A01"], Day(0)), user);
        await fx.Svc<DeskReservationService>().CancelAsync(reservation.Id, "Plan değişti", user);

        var rows = await HistoryAsync("DESK", reservation.Id);
        Assert.Equal([new Row(null, "BOOKED", null), new Row("BOOKED", "CANCELLED", null)], rows);
    }

    [Fact]
    public async Task Oda_rezervasyonu_PLSQL_BOOK_ROOM_ile_eklense_de_yazilir()
    {
        var user = await fx.CreateTestUserAsync();
        var reservation = await fx.Svc<RoomReservationService>().CreateAsync(
            new CreateRoomReservationCommand(fx.World.RoomId, Day(1), 10 * 60, 11 * 60, "Geçmiş testi", "Durum izi", 4), user);
        await fx.Svc<RoomReservationService>().CancelAsync(reservation.Id, null, user);

        var rows = await HistoryAsync("ROOM", reservation.Id);
        Assert.Equal([new Row(null, "BOOKED", null), new Row("BOOKED", "CANCELLED", null)], rows);
    }

    [Fact]
    public async Task Durum_degismeyen_guncelleme_satir_uretmez_toplu_guncelleme_her_satiri_yazar()
    {
        var a = await fx.CreateTestUserAsync();
        var b = await fx.CreateTestUserAsync();
        var r1 = await fx.Svc<DeskReservationService>().CreateAsync(new CreateDeskReservationCommand(fx.World.Desks["T-A02"], Day(2)), a);
        var r2 = await fx.Svc<DeskReservationService>().CreateAsync(new CreateDeskReservationCommand(fx.World.Desks["T-A03"], Day(2)), b);

        await using (var db = fx.Db())
        {
            // Ayni deger: trigger WHEN kosulu satir uretmemeli.
            await db.DeskReservations.Where(r => r.Id == r1.Id).ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, "BOOKED"));
            // Uygulama disi toplu degisiklik (SQL istemcisi benzeri): iki satir da yakalanmali.
            await db.DeskReservations.Where(r => r.Id == r1.Id || r.Id == r2.Id).ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, "NO_SHOW"));
        }

        Assert.Equal([new Row(null, "BOOKED", null), new Row("BOOKED", "NO_SHOW", null)], await HistoryAsync("DESK", r1.Id));
        Assert.Equal([new Row(null, "BOOKED", null), new Row("BOOKED", "NO_SHOW", null)], await HistoryAsync("DESK", r2.Id));
    }

    [Fact]
    public async Task Aktor_istegin_kullanicisidir_ve_havuzdaki_baglantida_sonraki_istege_tasinmaz()
    {
        // Uretimdeki DI yolu: AddOfis360Infrastructure + istek basina ICurrentUser.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOfis360Infrastructure(OracleFixture.ConnectionString);
        services.AddOfis360Application();
        services.AddScoped<FakeCurrentUser>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<FakeCurrentUser>());
        await using var provider = services.BuildServiceProvider();

        var actor = await fx.CreateTestUserAsync(role: Roles.Admin);
        var owner = await fx.CreateTestUserAsync();

        string reservationId;
        using (var scope = provider.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<FakeCurrentUser>().User = owner;
            var created = await scope.ServiceProvider.GetRequiredService<DeskReservationService>()
                .CreateAsync(new CreateDeskReservationCommand(fx.World.Desks["T-A04"], Day(3)), owner);
            reservationId = created.Id;
        }
        using (var scope = provider.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<FakeCurrentUser>().User = actor;
            await scope.ServiceProvider.GetRequiredService<DeskReservationService>().CancelAsync(reservationId, "Yönetici iptali", actor);
        }
        // Oturumsuz istek ayni havuzdan baglanti alir: onceki aktor (admin) yazilmamali.
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Ofis360DbContext>();
            await db.DeskReservations.Where(r => r.Id == reservationId).ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, "NO_SHOW"));
        }

        var rows = await HistoryAsync("DESK", reservationId);
        Assert.Equal(
            [new Row(null, "BOOKED", owner.Id), new Row("BOOKED", "CANCELLED", actor.Id), new Row("CANCELLED", "NO_SHOW", null)],
            rows);
    }

    [Fact]
    public async Task Gecmis_tablosu_salt_eklemelidir()
    {
        await using var db = fx.Db();
        var update = await Assert.ThrowsAsync<OracleException>(() =>
            db.Database.ExecuteSqlRawAsync("UPDATE RESERVATION_STATUS_HISTORY SET NEW_STATUS = 'BOOKED'"));
        Assert.Equal(20002, update.Number);
        var delete = await Assert.ThrowsAsync<OracleException>(() =>
            db.Database.ExecuteSqlRawAsync("DELETE FROM RESERVATION_STATUS_HISTORY"));
        Assert.Equal(20002, delete.Number);
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public SessionUser? User { get; set; }
    }
}
