using Microsoft.EntityFrameworkCore;
using Ofis360.Infrastructure.Persistence;

namespace Ofis360.Application.Tests.Fixtures;

/// <summary>
/// Tek Oracle baglantisi, tum entegrasyon testleri icin paylasilir (sirali koleksiyon).
/// Testler mevcut bir Oracle veritabanindaki AYRI bir test semasina karsi calisir (Docker gerekmez):
///   OFIS360_TEST_CONNECTION="User Id=OFIS360_TEST;Password=...;Data Source=sunucu:1521/SERVIS"
/// Sema yoksa db/00-create-user.sql ile olusturulur; tablolar ilk calistirmada migration'larla kurulur.
/// Testler bu semaya yazar: canli ya da gelistirme semasini KULLANMAYIN.
/// </summary>
public sealed class OracleFixture : IAsyncLifetime
{
    /// <summary>Koleksiyon fixture'i once baslar; sinif fixture'lari (WorldFixture) bu statik degeri kullanir.</summary>
    public static string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var external = Environment.GetEnvironmentVariable("OFIS360_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(external))
        {
            throw new InvalidOperationException(
                "OFIS360_TEST_CONNECTION ortam degiskeni tanimli degil. Entegrasyon testleri ayri bir Oracle test semasi ister; " +
                "ornek: User Id=OFIS360_TEST;Password=...;Data Source=sunucu:1521/SERVIS (bkz. db/README.md).");
        }
        ConnectionString = external;

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public Ofis360DbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<Ofis360DbContext>()
            .UseOracle(ConnectionString, OracleOptions.Configure)
            .Options;
        return new Ofis360DbContext(options);
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

[CollectionDefinition(Name)]
public sealed class OracleCollection : ICollectionFixture<OracleFixture>
{
    public const string Name = "oracle";
}
