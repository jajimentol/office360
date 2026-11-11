using Microsoft.EntityFrameworkCore.Migrations;

namespace Ofis360.Infrastructure.Persistence.Migrations;

/// <summary>
/// Oracle EF Core saglayicisi (10.23.x) CREATE TABLE ifadesini <c>EXECUTE IMMEDIATE '...'</c> icine sarar ve
/// model uzerindeki <c>HasCheckConstraint</c> metnindeki tek tirnaklari kacirmaz (ORA-06550 / PLS-00103).
/// Bu yuzden degerler listesi iceren CHECK kisitlari migration'larda ham SQL ile eklenir; <c>migrationBuilder.Sql</c>
/// saglayici tarafindan oldugu gibi gonderilir.
/// </summary>
public static class OracleCheckConstraints
{
    /// <summary>"COLUMN IN ('A','B',...)" CHECK kisiti ekler.</summary>
    public static void AddIn(this MigrationBuilder migrationBuilder, string table, string constraint, string column, params string[] values)
    {
        var list = string.Join(",", values.Select(v => $"'{v.Replace("'", "''")}'"));
        migrationBuilder.Sql($"ALTER TABLE \"{table}\" ADD CONSTRAINT \"{constraint}\" CHECK (\"{column}\" IN ({list}))");
    }

    public static void DropConstraint(this MigrationBuilder migrationBuilder, string table, string constraint)
    {
        migrationBuilder.Sql($"ALTER TABLE \"{table}\" DROP CONSTRAINT \"{constraint}\"");
    }
}
