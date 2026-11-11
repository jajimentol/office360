using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ofis360.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UserGradeAndSecretary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GRADE",
                table: "APP_USER",
                type: "NVARCHAR2(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IS_SECRETARY",
                table: "APP_USER",
                type: "BOOLEAN",
                nullable: false,
                defaultValue: false);

            // NULL = kademesi henuz atanmamis; CHECK kisiti NULL degeri kabul eder.
            migrationBuilder.AddIn("APP_USER", "CK_APP_USER_GRADE", "GRADE",
                "ASSISTANT_SPECIALIST", "SPECIALIST", "SENIOR_SPECIALIST", "ASSISTANT_MANAGER", "MANAGER",
                "SENIOR_MANAGER", "DIRECTOR", "DEPUTY_GM", "GM");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropConstraint("APP_USER", "CK_APP_USER_GRADE");

            migrationBuilder.DropColumn(
                name: "GRADE",
                table: "APP_USER");

            migrationBuilder.DropColumn(
                name: "IS_SECRETARY",
                table: "APP_USER");
        }
    }
}
