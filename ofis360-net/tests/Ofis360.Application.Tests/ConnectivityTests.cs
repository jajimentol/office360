using Microsoft.EntityFrameworkCore;
using Ofis360.Application.Tests.Fixtures;
using Ofis360.Domain.Entities;

namespace Ofis360.Application.Tests;

[Collection(OracleCollection.Name)]
public class ConnectivityTests(OracleFixture oracle)
{
    [Fact]
    public async Task Migration_applied_and_select_from_dual_works()
    {
        await using var db = oracle.CreateContext();

        var pending = await db.Database.GetPendingMigrationsAsync();
        Assert.Empty(pending);

        var one = await db.Database.SqlQueryRaw<int>("SELECT 1 AS \"Value\" FROM DUAL").SingleAsync();
        Assert.Equal(1, one);
    }

    [Fact]
    public async Task Department_and_user_roundtrip_with_unique_email()
    {
        await using var db = oracle.CreateContext();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var dept = new Department { Code = $"T{suffix}", Name = "Test Departmanı" };
        var user = new AppUser
        {
            Email = $"test.{suffix}@ofis360.local",
            FullName = "Test Kullanıcı",
            Title = "Uzman",
            DepartmentId = dept.Id,
        };
        db.Departments.Add(dept);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        await using var verify = oracle.CreateContext();
        var loaded = await verify.Users.Include(u => u.Department).SingleAsync(u => u.Id == user.Id);
        Assert.Equal("EMPLOYEE", loaded.Role);
        Assert.True(loaded.Active);
        Assert.True(loaded.NotificationsEnabled);
        Assert.Equal("Test Departmanı", loaded.Department.Name);

        await using var dup = oracle.CreateContext();
        dup.Users.Add(new AppUser { Email = user.Email, FullName = "Kopya", Title = "x", DepartmentId = dept.Id });
        await Assert.ThrowsAsync<DbUpdateException>(() => dup.SaveChangesAsync());
    }
}
