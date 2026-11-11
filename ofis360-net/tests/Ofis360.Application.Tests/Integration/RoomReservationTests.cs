using Microsoft.EntityFrameworkCore;
using Ofis360.Application.Common;
using Ofis360.Application.Services;
using Ofis360.Application.Tests.Fixtures;
using Ofis360.Application.Validation;
using Ofis360.Domain.Common;
using static Ofis360.Application.Tests.Fixtures.TestHelpers;

namespace Ofis360.Application.Tests.Integration;

/// <summary>Kaynak: tests/integration/room-reservation.test.ts (11 test).</summary>
[Collection(OracleCollection.Name)]
[TestCaseOrderer(PriorityOrderer.TypeName, PriorityOrderer.AssemblyName)]
public class RoomReservationTests(WorldFixture fx) : IClassFixture<WorldFixture>
{
    private TestWorld World => fx.World;
    private string Today => fx.Clock.TodayKey();
    private string Day => DayKeys.AddDays(Today, 3);

    private CreateRoomReservationCommand Cmd(string roomId, string day, int start, int end, string title, string purpose, int attendees) =>
        new(roomId, day, start, end, title, purpose, attendees);

    [Fact, Order(1)]
    public async Task Uygun_oda_ve_saat_icin_rezervasyon_olusur()
    {
        var user = await fx.CreateTestUserAsync();
        var reservation = await fx.Svc<RoomReservationService>().CreateAsync(
            Cmd(World.RoomId, Day, 10 * 60, 11 * 60, "Sprint planlama", "Ekip içi koordinasyon", 6), user);

        Assert.Equal("BOOKED", reservation.Status);
        Assert.Equal(DayKeys.WithMinutes(Day, 10 * 60), reservation.StartAt.UtcDateTime);

        await using var db = fx.Db();
        Assert.Contains(await db.Notifications.Where(n => n.UserId == user.Id).ToListAsync(), n => n.Type == "ROOM_RESERVATION_CREATED");
    }

    [Fact, Order(2)]
    public async Task Cakisan_saat_araligi_engellenir_ve_alternatifler_onerilir()
    {
        var user = await fx.CreateTestUserAsync();
        var error = await ExpectAppErrorAsync(() => fx.Svc<RoomReservationService>().CreateAsync(
            Cmd(World.RoomId, Day, 10 * 60 + 30, 11 * 60 + 30, "Çakışan toplantı", "Proje çalışması", 4), user));

        Assert.True(HasViolation(error, "ROOM_CONFLICT"));
        var details = Assert.IsType<RoomSuggestion>(error.Details);
        Assert.True(details.AlternativeSlots.Count + details.AlternativeRooms.Count > 0);
    }

    [Fact, Order(3)]
    public async Task Bitisik_saat_araligina_izin_verilir()
    {
        var user = await fx.CreateTestUserAsync();
        var reservation = await fx.Svc<RoomReservationService>().CreateAsync(
            Cmd(World.RoomId, Day, 11 * 60, 12 * 60, "Ardışık toplantı", "Ekip içi koordinasyon", 4), user);
        Assert.Equal("BOOKED", reservation.Status);
    }

    [Fact, Order(4)]
    public async Task Kapasitesi_yetersiz_oda_secilemez()
    {
        var user = await fx.CreateTestUserAsync();
        var error = await ExpectAppErrorAsync(() => fx.Svc<RoomReservationService>().CreateAsync(
            Cmd(World.SmallRoomId, Day, 14 * 60, 15 * 60, "Büyük toplantı", "Değerlendirme toplantısı", 9), user));
        Assert.True(HasViolation(error, "CAPACITY_EXCEEDED"));
    }

    [Fact, Order(5)]
    public async Task Mesai_saatleri_disinda_rezervasyon_yapilamaz()
    {
        var user = await fx.CreateTestUserAsync();
        var error = await ExpectAppErrorAsync(() => fx.Svc<RoomReservationService>().CreateAsync(
            Cmd(World.RoomId, Day, 6 * 60, 7 * 60, "Erken toplantı", "Proje çalışması", 3), user));
        Assert.True(HasViolation(error, "OUTSIDE_WORKING_HOURS"));
    }

    [Fact, Order(6)]
    public async Task Gecmis_tarih_icin_rezervasyon_yapilamaz()
    {
        var user = await fx.CreateTestUserAsync();
        var error = await ExpectAppErrorAsync(() => fx.Svc<RoomReservationService>().CreateAsync(
            Cmd(World.RoomId, DayKeys.AddDays(Today, -1), 10 * 60, 11 * 60, "Geçmiş toplantı", "Proje çalışması", 3), user));
        Assert.True(HasViolation(error, "PAST_DATE"));
    }

    [Fact, Order(7)]
    public async Task Admin_mesai_saatlerini_degistirdiginde_yeni_aralik_uygulanir()
    {
        var admin = await fx.CreateTestUserAsync(Roles.Admin);
        var user = await fx.CreateTestUserAsync();

        await fx.Svc<RuleService>().UpdateAsync(new UpdateRuleCommand("ROOM_WORKING_HOURS", IntValue: 6 * 60, SecondValue: 20 * 60), Actor(admin));

        var reservation = await fx.Svc<RoomReservationService>().CreateAsync(
            Cmd(World.RoomId, DayKeys.AddDays(Today, 4), 6 * 60 + 30, 7 * 60 + 30, "Erken toplantı", "Proje çalışması", 3), user);
        Assert.Equal("BOOKED", reservation.Status);

        await fx.Svc<RuleService>().UpdateAsync(new UpdateRuleCommand("ROOM_WORKING_HOURS", IntValue: 8 * 60, SecondValue: 19 * 60), Actor(admin));
    }

    [Fact, Order(8)]
    public async Task Dolu_ve_bos_araliklari_dogru_dondurur()
    {
        var availability = await fx.Svc<RoomReservationService>().GetAvailabilityAsync(new RoomAvailabilityQuery(Day));
        var room = availability.Rooms.SingleOrDefault(r => r.Id == World.RoomId);

        Assert.NotNull(room);
        Assert.True(room.BusySlots.Count >= 2);
        Assert.Contains(room.FreeSlots, slot => slot.StartMinutes >= 12 * 60);
        // Dolu aralik bos aralik olarak dondurulmemeli.
        Assert.DoesNotContain(room.FreeSlots, slot => slot.StartMinutes < 11 * 60 && slot.EndMinutes > 10 * 60);
    }

    [Fact, Order(9)]
    public async Task Kapasite_filtresi_uygulanir()
    {
        var availability = await fx.Svc<RoomReservationService>().GetAvailabilityAsync(new RoomAvailabilityQuery(Day, MinCapacity: 8));
        Assert.All(availability.Rooms, room => Assert.True(room.Capacity >= 8));
    }

    [Fact, Order(10)]
    public async Task Iptal_edilen_saat_araligi_yeniden_rezerve_edilebilir()
    {
        var owner = await fx.CreateTestUserAsync();
        var other = await fx.CreateTestUserAsync();
        var targetDay = DayKeys.AddDays(Today, 5);

        var reservation = await fx.Svc<RoomReservationService>().CreateAsync(Cmd(World.RoomId, targetDay, 13 * 60, 14 * 60, "İlk toplantı", "Proje çalışması", 3), owner);
        await ExpectAppErrorAsync(() => fx.Svc<RoomReservationService>().CreateAsync(Cmd(World.RoomId, targetDay, 13 * 60, 14 * 60, "İkinci toplantı", "Proje çalışması", 3), other));

        await fx.Svc<RoomReservationService>().CancelAsync(reservation.Id, null, owner);

        var replacement = await fx.Svc<RoomReservationService>().CreateAsync(Cmd(World.RoomId, targetDay, 13 * 60, 14 * 60, "İkinci toplantı", "Proje çalışması", 3), other);
        Assert.Equal("BOOKED", replacement.Status);
    }

    [Fact, Order(11)]
    public async Task Oda_kapatildiginda_mevcut_rezervasyonlar_iptal_edilir()
    {
        var admin = await fx.CreateTestUserAsync(Roles.Admin);
        var user = await fx.CreateTestUserAsync();
        var targetDay = DayKeys.AddDays(Today, 8);

        var reservation = await fx.Svc<RoomReservationService>().CreateAsync(Cmd(World.SmallRoomId, targetDay, 15 * 60, 16 * 60, "Değerlendirme", "Değerlendirme toplantısı", 3), user);

        var result = await fx.Svc<ClosureService>().CreateAsync(
            new ClosureInput("ROOM", World.SmallRoomId, targetDay, targetDay, "EVENT", "Kurum içi eğitim"), Actor(admin));
        Assert.Equal(1, result.CancelledReservations);

        await using var db = fx.Db();
        Assert.Equal("CANCELLED", (await db.RoomReservations.SingleAsync(r => r.Id == reservation.Id)).Status);

        var availability = await fx.Svc<RoomReservationService>().GetAvailabilityAsync(new RoomAvailabilityQuery(targetDay));
        var closedRoom = availability.Rooms.Single(r => r.Id == World.SmallRoomId);
        Assert.True(closedRoom.Closed);
        Assert.Empty(closedRoom.FreeSlots);
    }
}
