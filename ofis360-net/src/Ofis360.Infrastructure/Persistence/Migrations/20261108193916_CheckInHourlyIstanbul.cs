using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ofis360.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CheckInHourlyIstanbul : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // QA-010: saatlik grafik yerel saat bekliyor.
            migrationBuilder.Sql(Sql.OracleObjects.CheckInHourlyView);
            // Ornek veri yerel saati UTC gibi yazmisti; yalnizca seed kayitlari gercek ana cekilir.
            migrationBuilder.Sql("UPDATE \"DESK_RESERVATION\" SET \"CHECKED_IN_AT\" = \"CHECKED_IN_AT\" - INTERVAL '3' HOUR WHERE \"SOURCE\" = 'SEED' AND \"CHECKED_IN_AT\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"DESK_RESERVATION\" SET \"CHECKED_IN_AT\" = \"CHECKED_IN_AT\" + INTERVAL '3' HOUR WHERE \"SOURCE\" = 'SEED' AND \"CHECKED_IN_AT\" IS NOT NULL");
            migrationBuilder.Sql(Sql.OracleObjects.CheckInHourlyView.Replace("r.CHECKED_IN_AT AT TIME ZONE 'Europe/Istanbul'", "SYS_EXTRACT_UTC(r.CHECKED_IN_AT)"));
        }
    }
}
