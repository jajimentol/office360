using Microsoft.EntityFrameworkCore;
using Ofis360.Application.Tests.Fixtures;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;
using Ofis360.Infrastructure.Persistence;

namespace Ofis360.Application.Tests;

/// <summary>Oracle semasinin kaynak semantigini (nullable unique anahtarlar, CHECK kisitlari, cascade) dogrular.</summary>
[Collection(OracleCollection.Name)]
public class SchemaTests(OracleFixture oracle)
{
    private sealed record World(AppUser User, Desk Desk, Floor Floor);

    private static async Task<World> SeedWorldAsync(Ofis360DbContext db)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var dept = new Department { Code = $"S{suffix}", Name = "Şema Testi" };
        var user = new AppUser { Email = $"schema.{suffix}@ofis360.local", FullName = "Şema Kullanıcı", Title = "Uzman", DepartmentId = dept.Id };
        var building = new Building { Code = $"B{suffix}", Name = "Test Binası" };
        var floor = new Floor { BuildingId = building.Id, Number = 1, Name = "1. Kat", Description = "" };
        var zone = new Zone { FloorId = floor.Id, Code = "A", Name = "A Bölgesi", X = 0, Y = 0, Width = 100, Height = 100 };
        var desk = new Desk { Code = $"T-{suffix}", FloorId = floor.Id, ZoneId = zone.Id, X = 10, Y = 10 };
        db.AddRange(dept, user, building, floor, zone, desk);
        await db.SaveChangesAsync();
        return new World(user, desk, floor);
    }

    [Fact]
    public async Task Tum_tablolar_ve_check_kisitlari_olusturuldu()
    {
        await using var db = oracle.CreateContext();
        var tables = await db.Database.SqlQueryRaw<string>("SELECT table_name AS \"Value\" FROM user_tables").ToListAsync();
        string[] expected =
        [
            "DEPARTMENT", "APP_USER", "BUILDING", "FLOOR", "ZONE", "DESK_FEATURE", "DESK", "DESK_FEATURE_LINK",
            "ROOM_FEATURE", "MEETING_ROOM", "ROOM_FEATURE_LINK", "DESK_RESERVATION", "ROOM_RESERVATION",
            "RESERVATION_RULE", "CAPACITY_SHORTFALL", "CLOSURE", "NOTIFICATION", "AUDIT_LOG", "SYSTEM_SETTING", "FLOOR_PLAN_ELEMENT",
            "RESERVATION_STATUS_HISTORY", "RESERVATION_RULE_OVERRIDE", "HOLIDAY", "ROOM_ACCESS_RULE", "ROOM_PRIORITY_USER", "APP_SESSION",
        ];
        foreach (var name in expected)
        {
            Assert.Contains(name, tables);
        }

        var checks = await db.Database.SqlQueryRaw<string>(
            "SELECT constraint_name AS \"Value\" FROM user_constraints WHERE constraint_type = 'C' AND constraint_name LIKE 'CK%'").ToListAsync();
        Assert.Contains("CK_DESK_RES_STATUS", checks);
        Assert.Contains("CK_ZONE_KIND", checks);
        Assert.Contains("CK_APP_USER_GRADE", checks);
        Assert.Contains("CK_RULE_OVERRIDE_GRADE", checks);
        Assert.Contains("CK_ROOM_ACCESS_GRADE", checks);
        Assert.Contains("CK_DESK_RES_ACTIVE_KEYS", checks);
        Assert.Contains("CK_AUDIT_LOG_METADATA_JSON", checks);
        Assert.Contains("CK_FLOOR_PLAN_ELEMENT_TYPE", checks);
    }

    [Fact]
    public async Task Aktif_anahtarlar_cift_rezervasyonu_engeller_iptal_edilenler_cakismaz()
    {
        await using var db = oracle.CreateContext();
        var world = await SeedWorldAsync(db);
        var day = new DateOnly(2026, 8, 24);
        var dayKey = DayKeys.FromDateOnly(day);

        // Iki iptal edilmis kayit: anahtarlar NULL → unique index cakismaz.
        db.DeskReservations.AddRange(
            new DeskReservation { DeskId = world.Desk.Id, UserId = world.User.Id, Date = day, Status = DeskReservationStatus.Cancelled },
            new DeskReservation { DeskId = world.Desk.Id, UserId = world.User.Id, Date = day, Status = DeskReservationStatus.Cancelled });
        await db.SaveChangesAsync();

        // Aktif kayit
        db.DeskReservations.Add(new DeskReservation
        {
            DeskId = world.Desk.Id, UserId = world.User.Id, Date = day,
            ActiveDeskKey = DeskReservation.BuildActiveDeskKey(world.Desk.Id, dayKey),
            ActiveUserDayKey = DeskReservation.BuildActiveUserDayKey(world.User.Id, dayKey),
        });
        await db.SaveChangesAsync();

        // Ayni masa + gun icin ikinci aktif kayit → ORA-00001
        await using var dup = oracle.CreateContext();
        dup.DeskReservations.Add(new DeskReservation
        {
            DeskId = world.Desk.Id, UserId = world.User.Id, Date = day,
            ActiveDeskKey = DeskReservation.BuildActiveDeskKey(world.Desk.Id, dayKey),
            ActiveUserDayKey = DeskReservation.BuildActiveUserDayKey("baska-kullanici", dayKey),
        });
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => dup.SaveChangesAsync());
        Assert.Contains("ORA-00001", ex.InnerException?.Message ?? ex.Message);

        // Tarih DATE sutunundan DateOnly olarak geri okunur.
        await using var verify = oracle.CreateContext();
        var stored = await verify.DeskReservations.Where(r => r.DeskId == world.Desk.Id && r.ActiveDeskKey != null).SingleAsync();
        Assert.Equal(day, stored.Date);
        Assert.Equal("2026-08-24", stored.DayKey);
    }

    /// <summary>EmptyStringConverter: Oracle'da "" NULL'dur; uygulama bos dizgi yazar ve bos dizgi okur.</summary>
    [Fact]
    public async Task Bos_dizgi_alanlar_Oracle_NULL_uzerinden_bos_dizgi_olarak_geri_doner()
    {
        await using var db = oracle.CreateContext();
        var code = $"E{Guid.NewGuid().ToString("N")[..8]}";
        db.Buildings.Add(new Building { Code = code, Name = "Adressiz Bina", Address = "" });
        db.SystemSettings.Add(new SystemSetting { Key = $"test.{code}", Value = "", Label = "Duyuru", Description = "" });
        await db.SaveChangesAsync();

        await using var verify = oracle.CreateContext();
        var building = await verify.Buildings.SingleAsync(b => b.Code == code);
        Assert.Equal("", building.Address);
        var setting = await verify.SystemSettings.SingleAsync(s => s.Key == $"test.{code}");
        Assert.Equal("", setting.Value);
        Assert.Equal("", setting.Description);

        var rawNull = await verify.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS \"Value\" FROM \"BUILDING\" WHERE \"CODE\" = {0} AND \"ADDRESS\" IS NULL", code).SingleAsync();
        Assert.Equal(1, rawNull);
    }

    [Fact]
    public async Task Check_kisiti_gecersiz_durumu_reddeder()
    {
        await using var db = oracle.CreateContext();
        var world = await SeedWorldAsync(db);
        db.DeskReservations.Add(new DeskReservation { DeskId = world.Desk.Id, UserId = world.User.Id, Date = new DateOnly(2026, 8, 25), Status = "BOZUK" });
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Contains("ORA-02290", ex.InnerException?.Message ?? ex.Message);
    }

    [Fact]
    public async Task UpdatedAt_SaveChanges_sirasinda_guncellenir_ve_kat_silme_cascade_yapar()
    {
        await using var db = oracle.CreateContext();
        var world = await SeedWorldAsync(db);
        var before = world.Desk.UpdatedAt;
        await Task.Delay(20);
        world.Desk.Notes = "güncellendi";
        await db.SaveChangesAsync();
        Assert.True(world.Desk.UpdatedAt > before);

        db.Floors.Remove(world.Floor);
        await db.SaveChangesAsync();

        await using var verify = oracle.CreateContext();
        Assert.False(await verify.Desks.AnyAsync(d => d.Id == world.Desk.Id));
        Assert.False(await verify.Zones.AnyAsync(z => z.FloorId == world.Floor.Id));
    }
}
