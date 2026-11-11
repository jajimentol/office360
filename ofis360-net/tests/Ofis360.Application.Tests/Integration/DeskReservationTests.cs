using Microsoft.EntityFrameworkCore;
using Ofis360.Application.Common;
using Ofis360.Application.Services;
using Ofis360.Application.Tests.Fixtures;
using Ofis360.Application.Validation;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;
using static Ofis360.Application.Tests.Fixtures.TestHelpers;

namespace Ofis360.Application.Tests.Integration;

/// <summary>Kaynak: tests/integration/desk-reservation.test.ts (19 test). Gercek servis katmani ve Oracle kullanilir.</summary>
[Collection(OracleCollection.Name)]
[TestCaseOrderer(PriorityOrderer.TypeName, PriorityOrderer.AssemblyName)]
public class DeskReservationTests(WorldFixture fx) : IClassFixture<WorldFixture>
{
    private TestWorld World => fx.World;
    private string Today => fx.Clock.TodayKey();
    // Kabul kriterleri gunun hangi gune denk geldiginden bagimsiz: gelecek haftanin gunleri (14 gunluk pencere icinde).
    private string NextWeekMonday => DayKeys.StartOfWeek(DayKeys.AddDays(Today, 7));
    private string Day(int offset) => DayKeys.AddDays(NextWeekMonday, offset);

    [Fact, Order(1)]
    public async Task Calisan_uygun_masayi_rezerve_eder_ve_masa_digerine_kapali_gorunur()
    {
        var owner = await fx.CreateTestUserAsync();
        var other = await fx.CreateTestUserAsync();

        var reservation = await fx.Svc<DeskReservationService>().CreateAsync(new CreateDeskReservationCommand(World.Desks["T-A01"], Day(0)), owner);
        Assert.Equal("BOOKED", reservation.Status);

        var planForOther = await fx.Svc<AvailabilityService>().GetFloorPlanAsync(World.FloorId, Day(0), RuleUser(other));
        var deskForOther = planForOther.Desks.Single(d => d.Code == "T-A01");
        Assert.Equal("RESERVED", deskForOther.State);
        Assert.False(deskForOther.Selectable);
        Assert.Equal(owner.FullName, deskForOther.Occupant?.FullName);

        var planForOwner = await fx.Svc<AvailabilityService>().GetFloorPlanAsync(World.FloorId, Day(0), RuleUser(owner));
        Assert.Equal("MINE", planForOwner.Desks.Single(d => d.Code == "T-A01").State);
    }

    [Fact, Order(2)]
    public async Task Ayni_masayi_ikinci_kez_rezerve_etme_engellenir()
    {
        var other = await fx.CreateTestUserAsync();
        var error = await ExpectAppErrorAsync(() => fx.Svc<DeskReservationService>().CreateAsync(new CreateDeskReservationCommand(World.Desks["T-A01"], Day(0)), other));
        Assert.True(HasViolation(error, "DESK_OCCUPIED"));
    }

    [Fact, Order(3)]
    public async Task Ayni_calisan_ayni_gun_ikinci_masa_yapamaz()
    {
        var user = await fx.CreateTestUserAsync();
        await fx.Svc<DeskReservationService>().CreateAsync(new CreateDeskReservationCommand(World.Desks["T-A02"], Day(1)), user);

        var error = await ExpectAppErrorAsync(() => fx.Svc<DeskReservationService>().CreateAsync(new CreateDeskReservationCommand(World.Desks["T-A03"], Day(1)), user));
        Assert.True(HasViolation(error, "ONE_DESK_PER_DAY"));
        Assert.Contains("T-A02", error.Message);
    }

    [Fact, Order(4)]
    public async Task Haftalik_limit_asildiginda_engellenir_ve_gerekce_doner()
    {
        var user = await fx.CreateTestUserAsync();
        var svc = fx.Svc<DeskReservationService>();
        await svc.CreateAsync(new CreateDeskReservationCommand(World.Desks["T-A01"], Day(1)), user);
        await svc.CreateAsync(new CreateDeskReservationCommand(World.Desks["T-A02"], Day(2)), user);
        await svc.CreateAsync(new CreateDeskReservationCommand(World.Desks["T-A03"], Day(3)), user);

        var error = await ExpectAppErrorAsync(() => svc.CreateAsync(new CreateDeskReservationCommand(World.Desks["T-A04"], Day(4)), user));
        Assert.True(HasViolation(error, "WEEKLY_LIMIT_REACHED"));

        // Engellendiginde alternatif oneriler de dondurulur.
        var details = Assert.IsType<object>(error.Details, exactMatch: false);
        var alternatives = details.GetType().GetProperty("alternatives")?.GetValue(details);
        Assert.NotNull(alternatives);
        Assert.IsAssignableFrom<System.Collections.IEnumerable>(alternatives);
    }

    [Fact, Order(5)]
    public async Task Admin_haftalik_limiti_artirdiginda_yeni_limit_uygulanir()
    {
        var admin = await fx.CreateTestUserAsync(Roles.Admin);
        var user = await fx.CreateTestUserAsync();
        var svc = fx.Svc<DeskReservationService>();

        await svc.CreateAsync(new CreateDeskReservationCommand(World.Desks["T-A01"], Day(2)), user);
        await svc.CreateAsync(new CreateDeskReservationCommand(World.Desks["T-A02"], Day(3)), user);
        await svc.CreateAsync(new CreateDeskReservationCommand(World.Desks["T-A03"], Day(4)), user);
        await ExpectAppErrorAsync(() => svc.CreateAsync(new CreateDeskReservationCommand(World.Desks["T-A04"], Day(5)), user));

        await fx.Svc<RuleService>().UpdateAsync(new UpdateRuleCommand("WEEKLY_DESK_LIMIT", IntValue: 5), Actor(admin));

        var reservation = await fx.Svc<DeskReservationService>().CreateAsync(new CreateDeskReservationCommand(World.Desks["T-A04"], Day(5)), user);
        Assert.Equal("BOOKED", reservation.Status);

        await fx.Svc<RuleService>().UpdateAsync(new UpdateRuleCommand("WEEKLY_DESK_LIMIT", IntValue: 3), Actor(admin));
    }

    [Fact, Order(6)]
    public async Task Ayni_populer_masa_arka_arkaya_ucuncu_kez_secilemez()
    {
        var user = await fx.CreateTestUserAsync();
        var svc = fx.Svc<DeskReservationService>();
        await svc.CreateAsync(new CreateDeskReservationCommand(World.Desks["T-P01"], Day(0)), user);
        await svc.CreateAsync(new CreateDeskReservationCommand(World.Desks["T-P01"], Day(1)), user);

        var error = await ExpectAppErrorAsync(() => svc.CreateAsync(new CreateDeskReservationCommand(World.Desks["T-P01"], Day(2)), user));
        Assert.True(HasViolation(error, "POPULAR_STREAK_LIMIT"));

        // Farkli bir masa secildiginde ayni gun icin rezervasyon yapilabilir.
        var fallback = await svc.CreateAsync(new CreateDeskReservationCommand(World.Desks["T-A04"], Day(2)), user);
        Assert.Equal("BOOKED", fallback.Status);
    }

    [Fact, Order(7)]
    public async Task Iptal_edildiginde_masa_tekrar_rezerve_edilebilir()
    {
        var owner = await fx.CreateTestUserAsync();
        var other = await fx.CreateTestUserAsync();
        var svc = fx.Svc<DeskReservationService>();

        var reservation = await svc.CreateAsync(new CreateDeskReservationCommand(World.Desks["T-P02"], Day(0)), owner);
        await ExpectAppErrorAsync(() => svc.CreateAsync(new CreateDeskReservationCommand(World.Desks["T-P02"], Day(0)), other));

        var cancelled = await fx.Svc<DeskReservationService>().CancelAsync(reservation.Id, null, owner);
        Assert.Equal("CANCELLED", cancelled.Status);
        Assert.Null(cancelled.ActiveDeskKey);

        var newReservation = await fx.Svc<DeskReservationService>().CreateAsync(new CreateDeskReservationCommand(World.Desks["T-P02"], Day(0)), other);
        Assert.Equal("BOOKED", newReservation.Status);

        await using var db = fx.Db();
        var notifications = await db.Notifications.Where(n => n.UserId == owner.Id).ToListAsync();
        Assert.Contains(notifications, n => n.Type == "DESK_RESERVATION_CANCELLED");
    }

    [Fact, Order(8)]
    public async Task Gecmis_tarihli_rezervasyon_iptal_edilemez()
    {
        var user = await fx.CreateTestUserAsync();
        var pastDay = DayKeys.AddDays(Today, -3);
        await using var db = fx.Db();
        var past = new DeskReservation
        {
            DeskId = World.Desks["T-A01"], UserId = user.Id, Date = DayKeys.ToDateOnly(pastDay), Status = "COMPLETED",
            ActiveDeskKey = $"{World.Desks["T-A01"]}#{pastDay}", ActiveUserDayKey = $"{user.Id}#{pastDay}",
        };
        db.DeskReservations.Add(past);
        await db.SaveChangesAsync();

        var error = await ExpectAppErrorAsync(() => fx.Svc<DeskReservationService>().CancelAsync(past.Id, null, user));
        Assert.Equal(400, error.Status);
    }

    [Fact, Order(9)]
    public async Task Masa_pasiflestirildiginde_calisan_rezervasyon_yapamaz()
    {
        var admin = await fx.CreateTestUserAsync(Roles.Admin);
        var user = await fx.CreateTestUserAsync();

        await fx.Svc<SpaceService>().UpdateDeskAsync(World.Desks["T-A04"], new DeskUpdateInput(Active: false), Actor(admin));

        var error = await ExpectAppErrorAsync(() => fx.Svc<DeskReservationService>().CreateAsync(new CreateDeskReservationCommand(World.Desks["T-A04"], Day(6)), user));
        Assert.True(HasViolation(error, "DESK_INACTIVE"));

        await fx.Svc<SpaceService>().UpdateDeskAsync(World.Desks["T-A04"], new DeskUpdateInput(Active: true), Actor(admin));
    }

    [Fact, Order(10)]
    public async Task Kapatma_kaydi_mevcut_rezervasyonu_iptal_eder_ve_bilgilendirir()
    {
        var admin = await fx.CreateTestUserAsync(Roles.Admin);
        var user = await fx.CreateTestUserAsync();
        var closureDay = DayKeys.AddDays(Today, 9);

        var reservation = await fx.Svc<DeskReservationService>().CreateAsync(new CreateDeskReservationCommand(World.Desks["T-A03"], closureDay), user);

        var result = await fx.Svc<ClosureService>().CreateAsync(
            new ClosureInput("DESK", World.Desks["T-A03"], closureDay, closureDay, "MAINTENANCE", "Monitör değişimi"), Actor(admin));
        Assert.Equal(1, result.CancelledReservations);

        await using var db = fx.Db();
        var updated = await db.DeskReservations.SingleAsync(r => r.Id == reservation.Id);
        Assert.Equal("CANCELLED", updated.Status);
        var notifications = await db.Notifications.Where(n => n.UserId == user.Id).ToListAsync();
        Assert.Contains(notifications, n => n.Type == "RESOURCE_CLOSED");

        // Kapatma suresince yeni rezervasyon da olusturulamaz.
        var error = await ExpectAppErrorAsync(() => fx.Svc<DeskReservationService>().CreateAsync(new CreateDeskReservationCommand(World.Desks["T-A03"], closureDay), user));
        Assert.True(HasViolation(error, "CLOSURE_ACTIVE"));
    }

    [Fact, Order(11)]
    public async Task Yetkisiz_calisan_erisilebilirlik_masasini_secemez()
    {
        var user = await fx.CreateTestUserAsync();
        var error = await ExpectAppErrorAsync(() => fx.Svc<DeskReservationService>().CreateAsync(new CreateDeskReservationCommand(World.Desks["T-E01"], DayKeys.AddDays(Today, 10)), user));
        Assert.True(HasViolation(error, "ACCESSIBLE_ONLY"));
    }

    [Fact, Order(12)]
    public async Task Yetkili_calisan_erisilebilirlik_masasini_rezerve_eder()
    {
        var user = await fx.CreateTestUserAsync(accessibilityApproved: true);
        var reservation = await fx.Svc<DeskReservationService>().CreateAsync(new CreateDeskReservationCommand(World.Desks["T-E01"], DayKeys.AddDays(Today, 10)), user);
        Assert.Equal("BOOKED", reservation.Status);
    }

    [Fact, Order(13)]
    public async Task Rezervasyona_kapali_kattaki_masa_secilemez()
    {
        var user = await fx.CreateTestUserAsync();
        var error = await ExpectAppErrorAsync(() => fx.Svc<DeskReservationService>().CreateAsync(new CreateDeskReservationCommand(World.Desks["T-GM01"], DayKeys.AddDays(Today, 11)), user));
        Assert.True(HasViolation(error, "FLOOR_NOT_RESERVABLE"));
    }

    [Fact, Order(14)]
    public async Task Calisan_baskasi_adina_rezervasyon_olusturamaz()
    {
        var user = await fx.CreateTestUserAsync();
        var target = await fx.CreateTestUserAsync();
        var error = await ExpectAppErrorAsync(() => fx.Svc<DeskReservationService>().CreateAsync(
            new CreateDeskReservationCommand(World.Desks["T-A01"], DayKeys.AddDays(Today, 12), TargetUserId: target.Id), user));
        Assert.Equal(403, error.Status);
    }

    [Fact, Order(15)]
    public async Task Admin_baskasi_adina_rezervasyon_olusturabilir()
    {
        var admin = await fx.CreateTestUserAsync(Roles.Admin);
        var target = await fx.CreateTestUserAsync();
        var reservation = await fx.Svc<DeskReservationService>().CreateAsync(
            new CreateDeskReservationCommand(World.Desks["T-A01"], DayKeys.AddDays(Today, 12), TargetUserId: target.Id), admin);
        Assert.Equal(target.Id, reservation.UserId);
        Assert.Equal("ADMIN", reservation.Source);
    }

    [Fact, Order(16)]
    public async Task Calisan_baskasinin_rezervasyonunu_iptal_edemez_admin_edebilir()
    {
        var owner = await fx.CreateTestUserAsync();
        var stranger = await fx.CreateTestUserAsync();
        var admin = await fx.CreateTestUserAsync(Roles.Admin);
        var day = DayKeys.AddDays(Today, 13);

        var reservation = await fx.Svc<DeskReservationService>().CreateAsync(new CreateDeskReservationCommand(World.Desks["T-A02"], day), owner);

        var forbidden = await ExpectAppErrorAsync(() => fx.Svc<DeskReservationService>().CancelAsync(reservation.Id, null, stranger));
        Assert.Equal(403, forbidden.Status);

        var cancelled = await fx.Svc<DeskReservationService>().CancelAsync(reservation.Id, "Planlı bakım", admin);
        Assert.Equal("CANCELLED", cancelled.Status);

        await using var db = fx.Db();
        var notifications = await db.Notifications.Where(n => n.UserId == owner.Id).ToListAsync();
        Assert.Contains(notifications, n => n.Type == "DESK_RESERVATION_CANCELLED_BY_ADMIN");
    }

    [Fact, Order(17)]
    public async Task Uygun_alternatif_masalari_onceliklendirerek_dondurur()
    {
        var user = await fx.CreateTestUserAsync();
        var alternatives = await fx.Svc<DeskReservationService>().SuggestAlternativeDesksAsync(
            RuleUser(user), DayKeys.AddDays(Today, 14), World.FloorId, World.ZoneId, limit: 3);

        Assert.NotEmpty(alternatives);
        Assert.True(alternatives.Count <= 3);
        // Erisilebilirlik masasi yetkisiz kullaniciya onerilmez.
        Assert.DoesNotContain(alternatives, a => a.Code == "T-E01");
    }

    [Fact, Order(18)]
    public async Task Olusturma_ve_iptal_islemleri_denetim_kaydina_yazilir()
    {
        await using var db = fx.Db();
        var actions = await db.AuditLogs.Select(l => l.Action).ToListAsync();
        Assert.Contains("DESK_RESERVATION_CREATE", actions);
        Assert.Contains("DESK_RESERVATION_CANCEL", actions);
        Assert.Contains("CLOSURE_CREATE", actions);
        Assert.Contains("RULE_UPDATE", actions);
    }

    [Fact, Order(19)]
    public async Task AppException_HTTP_durum_kodunu_tasir()
    {
        var user = await fx.CreateTestUserAsync();
        var error = await ExpectAppErrorAsync(() => fx.Svc<DeskReservationService>().CreateAsync(new CreateDeskReservationCommand("yok-boyle-bir-masa", Day(0)), user));
        Assert.Equal(404, error.Status);
    }
}
