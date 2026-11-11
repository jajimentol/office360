using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ofis360.Application;
using Ofis360.Application.Common;
using Ofis360.Application.Identity;
using Ofis360.Application.Persistence;
using Ofis360.Application.Services;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;
using Ofis360.Domain.Rules;
using Ofis360.Infrastructure.Persistence;

namespace Ofis360.Application.Tests.Fixtures;

/// <summary>Kaynak: tests/helpers/fixtures.ts TestWorld — entegrasyon testleri icin sade bir ofis kurulumu.</summary>
public sealed record TestWorld(
    string DepartmentId, string BuildingId, string FloorId, string RestrictedFloorId, string ZoneId,
    IReadOnlyDictionary<string, string> Desks, string RoomId, string SmallRoomId);

/// <summary>
/// Her test sinifi icin bir kez kurulan dunya (kaynaktaki beforeAll(buildWorld)). Servisler istek basina
/// DbContext yasam dongusunu taklit etmek icin her cagrida yeni bir DI scope'undan alinir.
/// </summary>
public sealed class WorldFixture : IAsyncLifetime
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static int _userCounter;
    private ServiceProvider _provider = null!;
    private readonly List<IServiceScope> _scopes = [];

    public TestWorld World { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<Ofis360DbContext>(o => o.UseOracle(OracleFixture.ConnectionString, OracleOptions.Configure));
        services.AddScoped<IOfis360Db>(sp => sp.GetRequiredService<Ofis360DbContext>());
        services.AddSingleton<IClock, SystemClock>();
        services.AddOfis360Application();
        _provider = services.BuildServiceProvider();

        await Gate.WaitAsync();
        try
        {
            World = await BuildWorldAsync();
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>Yeni bir scope'tan servis (ayri "istek"). Scope'lar fixture sonunda birlikte kapatilir.</summary>
    public T Svc<T>() where T : notnull
    {
        var scope = _provider.CreateScope();
        _scopes.Add(scope);
        return scope.ServiceProvider.GetRequiredService<T>();
    }

    /// <summary>Kaynaktaki dogrudan `prisma.*` erisiminin karsiligi: taze bir DbContext.</summary>
    public Ofis360DbContext Db() => Svc<Ofis360DbContext>();

    public IClock Clock => _provider.GetRequiredService<IClock>();

    public async Task ResetDatabaseAsync()
    {
        await using var db = new Ofis360DbContext(new DbContextOptionsBuilder<Ofis360DbContext>()
            .UseOracle(OracleFixture.ConnectionString, OracleOptions.Configure).Options);
        await db.CapacityShortfalls.ExecuteDeleteAsync();
        await db.AuditLogs.ExecuteDeleteAsync();
        await db.Notifications.ExecuteDeleteAsync();
        await db.DeskReservations.ExecuteDeleteAsync();
        await db.RoomReservations.ExecuteDeleteAsync();
        await db.Closures.ExecuteDeleteAsync();
        await db.DeskFeatureLinks.ExecuteDeleteAsync();
        await db.RoomFeatureLinks.ExecuteDeleteAsync();
        await db.FloorPlanElements.ExecuteDeleteAsync();
        await db.Desks.ExecuteDeleteAsync();
        await db.MeetingRooms.ExecuteDeleteAsync();
        await db.Zones.ExecuteDeleteAsync();
        await db.Floors.ExecuteDeleteAsync();
        await db.Buildings.ExecuteDeleteAsync();
        await db.DeskFeatures.ExecuteDeleteAsync();
        await db.RoomFeatures.ExecuteDeleteAsync();
        await db.Users.ExecuteDeleteAsync();
        await db.Departments.ExecuteDeleteAsync();
        await db.ReservationRuleOverrides.ExecuteDeleteAsync();
        await db.Holidays.ExecuteDeleteAsync();
        await db.ReservationRules.ExecuteDeleteAsync();
        await db.SystemSettings.ExecuteDeleteAsync();
    }

    private async Task<TestWorld> BuildWorldAsync()
    {
        await ResetDatabaseAsync();
        await using var db = new Ofis360DbContext(new DbContextOptionsBuilder<Ofis360DbContext>()
            .UseOracle(OracleFixture.ConnectionString, OracleOptions.Configure).Options);

        foreach (var rule in RuleDefinitions.All) db.ReservationRules.Add(Services.RuleService.ToEntity(rule));

        var department = new Department { Code = "TEST", Name = "Test Departmanı" };
        var building = new Building { Code = "TEST", Name = "Test Binası", Order = 1, Active = true };
        var floor = new Floor { BuildingId = building.Id, Number = 1, Name = "1. Kat", Description = "Test katı", Reservable = true };
        var restrictedFloor = new Floor { BuildingId = building.Id, Number = 4, Name = "4. Kat", Description = "Genel Müdürlük", Reservable = false };
        var zone = new Zone { FloorId = floor.Id, Code = "A", Name = "A Bölgesi", Kind = ZoneKinds.Work, X = 40, Y = 40, Width = 400, Height = 200, WindowSide = true };
        var restrictedZone = new Zone { FloorId = restrictedFloor.Id, Code = "GM", Name = "Genel Müdürlük", Kind = ZoneKinds.Restricted, X = 40, Y = 40, Width = 300, Height = 200, Reservable = false };
        db.AddRange(department, building, floor, restrictedFloor, zone, restrictedZone);

        var deskSpecs = new (string Code, bool Popular, bool AccessibleOnly, int X, int Y)[]
        {
            ("T-A01", false, false, 80, 80), ("T-A02", false, false, 160, 80), ("T-A03", false, false, 240, 80), ("T-A04", false, false, 320, 80),
            ("T-P01", true, false, 80, 160), ("T-P02", true, false, 160, 160), ("T-E01", false, true, 240, 160),
        };
        var desks = new Dictionary<string, string>();
        foreach (var spec in deskSpecs)
        {
            var desk = new Desk { Code = spec.Code, FloorId = floor.Id, ZoneId = zone.Id, X = spec.X, Y = spec.Y, Popular = spec.Popular, AccessibleOnly = spec.AccessibleOnly };
            db.Desks.Add(desk);
            desks[spec.Code] = desk.Id;
        }
        var restrictedDesk = new Desk { Code = "T-GM01", FloorId = restrictedFloor.Id, ZoneId = restrictedZone.Id, X = 100, Y = 100 };
        db.Desks.Add(restrictedDesk);
        desks["T-GM01"] = restrictedDesk.Id;

        var room = new MeetingRoom { Code = "T-R01", Name = "Test Toplantı Odası", FloorId = floor.Id, Capacity = 10, X = 500, Y = 40, Width = 200, Height = 120 };
        var smallRoom = new MeetingRoom { Code = "T-R02", Name = "Küçük Oda", FloorId = floor.Id, Capacity = 4, X = 500, Y = 200, Width = 200, Height = 120 };
        db.AddRange(room, smallRoom);
        await db.SaveChangesAsync();

        return new TestWorld(department.Id, building.Id, floor.Id, restrictedFloor.Id, zone.Id, desks, room.Id, smallRoom.Id);
    }

    public async Task<SessionUser> CreateTestUserAsync(string role = Roles.Employee, bool accessibilityApproved = false, string? fullName = null)
    {
        var n = Interlocked.Increment(ref _userCounter);
        await using var db = Db();
        var user = new AppUser
        {
            Email = $"test{n}@ofis360.local",
            FullName = fullName ?? $"Test Kullanıcı {n}",
            Title = "Uzman",
            Role = role,
            DepartmentId = World.DepartmentId,
            AccessibilityApproved = accessibilityApproved,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var departmentName = await db.Departments.Where(d => d.Id == World.DepartmentId).Select(d => d.Name).FirstAsync();
        return new SessionUser(user.Id, user.Email, user.FullName, user.Title, user.Role, user.DepartmentId, departmentName,
            user.AccessibilityApproved, null, false, true);
    }

    public Task DisposeAsync()
    {
        foreach (var scope in _scopes) scope.Dispose();
        _provider.Dispose();
        return Task.CompletedTask;
    }
}

public static class TestHelpers
{
    public static AuditActor Actor(SessionUser user) => new(user.Id, user.FullName, user.Role);

    public static DeskRuleUser RuleUser(SessionUser user) => new(user.Id, user.FullName, user.AccessibilityApproved);

    /// <summary>Kaynak: expectAppError — AppException beklenir ve tipli olarak dondurulur.</summary>
    public static async Task<AppException> ExpectAppErrorAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (AppException ex)
        {
            return ex;
        }
        throw new Xunit.Sdk.XunitException("Hata bekleniyordu ancak işlem başarıyla tamamlandı.");
    }

    public static bool HasViolation(AppException ex, string code) => ex.Violations?.Any(v => v.Code == code) == true;
}
