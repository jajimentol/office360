using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ofis360.Infrastructure.Persistence;

/// <summary>dotnet ef tasarim zamani fabrikasi. Baglanti dizesi OFIS360_CONNECTION ortam degiskeninden okunur.</summary>
public sealed class Ofis360DbContextFactory : IDesignTimeDbContextFactory<Ofis360DbContext>
{
    public Ofis360DbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("OFIS360_CONNECTION")
            ?? ConnectionStrings.LocalDev;
        var options = new DbContextOptionsBuilder<Ofis360DbContext>()
            .UseOracle(connection, OracleOptions.Configure)
            .Options;
        return new Ofis360DbContext(options);
    }
}

public static class OracleOptions
{
    /// <summary>
    /// MaxBatchSize(1): Oracle EF 10.23.x ifade gruplamasi bu semada (native BOOLEAN + NVARCHAR2) "TTCExecuteSql: Unexpected Packet"
    /// hatasi veriyor; satir satir INSERT guvenli ve bu olcekte yeterince hizli (2.500 satir ~6 sn).
    /// SplitQuery: cok koleksiyonlu Include'lar (kat plani: masa x ozellik x oda x kroki) tek JOIN'de kartezyen patlamasi
    /// yapiyordu (Palladium kati 4+ dakika); bolunmus sorgu varsayilan.
    /// </summary>
    public static void Configure(Oracle.EntityFrameworkCore.Infrastructure.OracleDbContextOptionsBuilder o)
        => o.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion23)
            .MaxBatchSize(1)
            .CommandTimeout(30)
            .UseQuerySplittingBehavior(Microsoft.EntityFrameworkCore.QuerySplittingBehavior.SplitQuery);
}

public static class ConnectionStrings
{
    /// <summary>docker/oracle/compose.yml ile ayaga kalkan yerel gelistirme semasi.</summary>
    public const string LocalDev = "User Id=OFIS360;Password=Ofis360Dev_2026;Data Source=localhost:1521/FREEPDB1";

    /// <summary>Ayni konteynerdeki yerel test semasi (OFIS360_TEST_CONNECTION ile ezilebilir).</summary>
    public const string LocalTest = "User Id=OFIS360_TEST;Password=Ofis360Test_2026;Data Source=localhost:1521/FREEPDB1";
}
