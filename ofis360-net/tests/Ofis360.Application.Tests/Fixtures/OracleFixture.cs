using Microsoft.EntityFrameworkCore;
using Ofis360.Infrastructure.Persistence;
using Testcontainers.Oracle;

namespace Ofis360.Application.Tests.Fixtures;

/// <summary>
/// Tek Oracle baglantisi, tum entegrasyon testleri icin paylasilir (sirali koleksiyon).
/// OFIS360_TEST_CONNECTION verilirse calisan yerel konteyner (docker/oracle/compose.yml, OFIS360_TEST semasi)
/// kullanilir; verilmezse Testcontainers ile gecici bir Oracle Free konteyneri baslatilir (1-3 dk).
/// </summary>
public sealed class OracleFixture : IAsyncLifetime
{
    private OracleContainer? _container;

    /// <summary>Koleksiyon fixture'i once baslar; sinif fixture'lari (WorldFixture) bu statik degeri kullanir.</summary>
    public static string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var external = Environment.GetEnvironmentVariable("OFIS360_TEST_CONNECTION");
        if (!string.IsNullOrWhiteSpace(external))
        {
            ConnectionString = external;
        }
        else
        {
            _container = new OracleBuilder("container-registry.oracle.com/database/free:latest")
                .WithUsername("OFIS360_TEST")
                .WithPassword("Ofis360Test_2026")
                .Build();
            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();
        }

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

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}

[CollectionDefinition(Name)]
public sealed class OracleCollection : ICollectionFixture<OracleFixture>
{
    public const string Name = "oracle";
}
