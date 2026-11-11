using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Ofis360.Application.Common;
using Ofis360.Application.Identity;
using Ofis360.Application.Services;
using Ofis360.Application.Tests.Fixtures;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;

namespace Ofis360.Application.Tests.Integration;

/// <summary>Kaynak: tests/integration/analytics.test.ts (8 test) — kabul kriterleri 11-13.</summary>
public sealed class AnalyticsWorld : IAsyncLifetime
{
    public WorldFixture Fx { get; } = new();
    public SessionUser UserA { get; private set; } = null!;
    public SessionUser UserB { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Fx.InitializeAsync();
        var world = Fx.World;
        var today = Fx.Clock.TodayKey();
        UserA = await Fx.CreateTestUserAsync(fullName: "Ayşe Test");
        UserB = await Fx.CreateTestUserAsync(fullName: "Bora Test");

        // Gecmis veriyi dogrudan yaz (kural motoru gecmis tarihe izin vermez).
        var history = new (string Day, string DeskCode, string UserId, string Status)[]
        {
            (DayKeys.AddDays(today, -3), "T-A01", UserA.Id, "COMPLETED"),
            (DayKeys.AddDays(today, -3), "T-P01", UserB.Id, "NO_SHOW"),
            (DayKeys.AddDays(today, -2), "T-P01", UserA.Id, "COMPLETED"),
            (DayKeys.AddDays(today, -1), "T-A02", UserA.Id, "CANCELLED"),
            (DayKeys.AddDays(today, -1), "T-P01", UserB.Id, "COMPLETED"),
        };
        await using (var db = Fx.Db())
        {
            foreach (var row in history)
            {
                var cancelled = row.Status == "CANCELLED";
                db.DeskReservations.Add(new DeskReservation
                {
                    DeskId = world.Desks[row.DeskCode], UserId = row.UserId, Date = DayKeys.ToDateOnly(row.Day), Status = row.Status,
                    ActiveDeskKey = cancelled ? null : $"{world.Desks[row.DeskCode]}#{row.Day}",
                    ActiveUserDayKey = cancelled ? null : $"{row.UserId}#{row.Day}",
                    CheckedInAt = row.Status == "COMPLETED" ? new DateTimeOffset(DayKeys.WithMinutes(row.Day, 9 * 60), TimeSpan.Zero).AddHours(-3) : null,
                });
            }
            await db.SaveChangesAsync();
        }

        // Bugun ve gelecek icin servis uzerinden rezervasyon olustur.
        await Fx.Svc<DeskReservationService>().CreateAsync(new CreateDeskReservationCommand(world.Desks["T-A03"], today), UserA);
        try
        {
            await Fx.Svc<RoomReservationService>().CreateAsync(
                new CreateRoomReservationCommand(world.RoomId, today, 23 * 60, 23 * 60 + 30, "Raporlama testi", "Proje çalışması", 4), UserB);
        }
        catch (AppException)
        {
            // Mesai saatleri disinda kalirsa dogrudan veritabanina yaz.
            await using var db = Fx.Db();
            db.RoomReservations.Add(new RoomReservation
            {
                RoomId = world.RoomId, UserId = UserB.Id, Date = DayKeys.ToDateOnly(today),
                StartAt = new DateTimeOffset(DayKeys.WithMinutes(today, 600), TimeSpan.Zero), EndAt = new DateTimeOffset(DayKeys.WithMinutes(today, 660), TimeSpan.Zero),
                StartMinutes = 600, EndMinutes = 660, Title = "Raporlama testi", Purpose = "Proje çalışması", AttendeeCount = 4, Status = "BOOKED",
            });
            await db.SaveChangesAsync();
        }
    }

    public Task DisposeAsync() => Fx.DisposeAsync();
}

[Collection(OracleCollection.Name)]
[TestCaseOrderer(PriorityOrderer.TypeName, PriorityOrderer.AssemblyName)]
public class AnalyticsTests(AnalyticsWorld aw) : IClassFixture<AnalyticsWorld>
{
    private WorldFixture Fx => aw.Fx;
    private TestWorld World => aw.Fx.World;
    private string Today => aw.Fx.Clock.TodayKey();

    private Task<AnalyticsResult> Analytics(int daysBack, string? floorId = null, string? departmentId = null) =>
        Fx.Svc<AnalyticsService>().GetAnalyticsAsync(new AnalyticsFilters(DayKeys.AddDays(Today, -daysBack), Today, floorId, departmentId));

    [Fact, Order(1)]
    public async Task KPI_degerleri_gercek_rezervasyonlardan_uretilir()
    {
        var analytics = await Analytics(7);
        Assert.Equal(1, analytics.Kpis.TodayReservations);
        Assert.Equal(5, analytics.Kpis.TotalDeskReservations);
        Assert.Equal(7, analytics.Kpis.ActiveDeskCount);
        Assert.True(analytics.Kpis.NoShowRate > 0);
        Assert.True(analytics.Kpis.CancellationRate > 0);
    }

    [Fact, Order(2)]
    public async Task Doluluk_trendi_ve_rezervasyon_kullanim_karsilastirmasi_dolu_gelir()
    {
        var analytics = await Analytics(3);
        Assert.Equal(4, analytics.OccupancyTrend.Count);
        Assert.Equal(1, analytics.OccupancyTrend[^1].Reservations);

        var historyRow = analytics.BookedVsUsed.Single(row => row.DayKey == DayKeys.AddDays(Today, -3));
        Assert.Equal(2, historyRow.Booked);
        Assert.Equal(1, historyRow.Used);
        Assert.Equal(1, historyRow.NoShow);
    }

    [Fact, Order(3)]
    public async Task Masa_isi_haritasi_en_cok_kullanilan_masayi_one_cikarir()
    {
        var analytics = await Analytics(7);
        Assert.Equal("T-P01", analytics.DeskHeatmap[0].Code);
        Assert.Equal(3, analytics.DeskHeatmap[0].Reservations);
    }

    [Fact, Order(4)]
    public async Task Adil_kullanim_gostergesi_anonim_kovalar_dondurur()
    {
        var analytics = await Analytics(7);
        Assert.Equal(2, analytics.Fairness.ActiveEmployees);
        Assert.True(analytics.Fairness.GiniIndex >= 0);
        // Kisi adi veya kimligi raporda yer almaz.
        var json = JsonSerializer.Serialize(analytics.Fairness);
        Assert.DoesNotContain("Ayşe", json);
        Assert.DoesNotContain(aw.UserA.Id, json);
    }

    [Fact, Order(5)]
    public async Task Kat_filtresi_sonuclari_daraltir()
    {
        var filtered = await Analytics(7, floorId: World.RestrictedFloorId);
        Assert.Equal(0, filtered.Kpis.TotalDeskReservations);
        var unfiltered = await Analytics(7, floorId: World.FloorId);
        Assert.Equal(5, unfiltered.Kpis.TotalDeskReservations);
    }

    [Fact, Order(6)]
    public async Task Tarih_araligi_filtresi_dogru_calisir()
    {
        var narrow = await Fx.Svc<AnalyticsService>().GetAnalyticsAsync(new AnalyticsFilters(Today, Today));
        Assert.Equal(1, narrow.Kpis.TotalDeskReservations);
        Assert.Equal(1, narrow.Filters.DayCount);
    }

    [Fact, Order(7)]
    public async Task Departman_filtresi_uygulanir()
    {
        await using var db = Fx.Db();
        var other = new Department { Code = "BOS", Name = "Boş Departman" };
        db.Departments.Add(other);
        await db.SaveChangesAsync();

        var analytics = await Analytics(7, departmentId: other.Id);
        Assert.Equal(0, analytics.Kpis.TotalDeskReservations);
    }

    [Fact, Order(8)]
    public async Task Calisanin_kendi_istatistiklerini_hesaplar()
    {
        var summary = await Fx.Svc<PersonalService>().GetPersonalSummaryAsync(aw.UserA.Id);
        Assert.Equal(3, summary.Totals.DeskReservations);
        Assert.Equal(1, summary.Totals.Cancelled);
        Assert.Equal(3, summary.FairUse.WeeklyLimit);
        Assert.NotEmpty(summary.FavoriteDesks);
        Assert.Equal(12, summary.WeeklyTrend.Count);
    }
}
