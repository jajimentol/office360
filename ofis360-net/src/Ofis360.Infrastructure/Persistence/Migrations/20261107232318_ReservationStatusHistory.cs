using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ofis360.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReservationStatusHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rezervasyon durum gecmisi: tablo + trigger'lar (bkz. Sql/StatusHistoryObjects).
            foreach (var statement in Sql.StatusHistoryObjects.Create)
            {
                migrationBuilder.Sql(statement);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var statement in Sql.StatusHistoryObjects.Drop)
            {
                migrationBuilder.Sql(statement);
            }
        }
    }
}
