using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ofis360.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RuleOverridesAndHolidays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HOLIDAY",
                columns: table => new
                {
                    ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    HOLIDAY_DATE = table.Column<DateTime>(type: "DATE", nullable: false),
                    NAME = table.Column<string>(type: "NVARCHAR2(80)", maxLength: 80, nullable: false),
                    CREATED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(3) WITH TIME ZONE", nullable: false),
                    CREATED_BY_ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HOLIDAY", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "RESERVATION_RULE_OVERRIDE",
                columns: table => new
                {
                    ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    RULE_KEY = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    GRADE = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    BUILDING_ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: true),
                    ENABLED = table.Column<bool>(type: "BOOLEAN", nullable: true),
                    INT_VALUE = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    UPDATED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(3) WITH TIME ZONE", nullable: false),
                    UPDATED_BY_ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RESERVATION_RULE_OVERRIDE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_RESERVATION_RULE_OVERRIDE_BUILDING_BUILDING_ID",
                        column: x => x.BUILDING_ID,
                        principalTable: "BUILDING",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_HOLIDAY_DATE",
                table: "HOLIDAY",
                column: "HOLIDAY_DATE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RESERVATION_RULE_OVERRIDE_BUILDING_ID",
                table: "RESERVATION_RULE_OVERRIDE",
                column: "BUILDING_ID");

            migrationBuilder.CreateIndex(
                name: "UX_RULE_OVERRIDE_SCOPE",
                table: "RESERVATION_RULE_OVERRIDE",
                columns: new[] { "RULE_KEY", "GRADE", "BUILDING_ID" },
                unique: true,
                filter: "\"BUILDING_ID\" IS NOT NULL");
            migrationBuilder.AddIn("RESERVATION_RULE_OVERRIDE", "CK_RULE_OVERRIDE_GRADE", "GRADE",
                "ASSISTANT_SPECIALIST", "SPECIALIST", "SENIOR_SPECIALIST", "ASSISTANT_MANAGER", "MANAGER",
                "SENIOR_MANAGER", "DIRECTOR", "DEPUTY_GM", "GM");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HOLIDAY");

            migrationBuilder.DropTable(
                name: "RESERVATION_RULE_OVERRIDE");
        }
    }
}
