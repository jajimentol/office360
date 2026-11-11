using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ofis360.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RoomPriorityBypass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BYPASS_PRIORITY",
                table: "ROOM_ACCESS_RULE",
                type: "BOOLEAN",
                nullable: false,
                defaultValue: false);

            // Karar: GM ve GMY oncelikli odalarda beklemez. Mevcut oda kurallarina bir kez uygulanir.
            migrationBuilder.Sql("UPDATE \"ROOM_ACCESS_RULE\" SET \"BYPASS_PRIORITY\" = TRUE WHERE \"GRADE\" IN ('DEPUTY_GM','GM')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BYPASS_PRIORITY",
                table: "ROOM_ACCESS_RULE");
        }
    }
}
