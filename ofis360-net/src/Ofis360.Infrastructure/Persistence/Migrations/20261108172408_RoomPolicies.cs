using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ofis360.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RoomPolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PRIORITY_OPEN_DAYS",
                table: "MEETING_ROOM",
                type: "NUMBER(10)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SECRETARY_ONLY",
                table: "MEETING_ROOM",
                type: "BOOLEAN",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ROOM_ACCESS_RULE",
                columns: table => new
                {
                    ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    ROOM_ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    GRADE = table.Column<string>(type: "NVARCHAR2(30)", maxLength: 30, nullable: false),
                    MIN_ATTENDEES = table.Column<int>(type: "NUMBER(10)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ROOM_ACCESS_RULE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_ROOM_ACCESS_RULE_MEETING_ROOM_ROOM_ID",
                        column: x => x.ROOM_ID,
                        principalTable: "MEETING_ROOM",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ROOM_PRIORITY_USER",
                columns: table => new
                {
                    ROOM_ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    USER_ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ROOM_PRIORITY_USER", x => new { x.ROOM_ID, x.USER_ID });
                    table.ForeignKey(
                        name: "FK_ROOM_PRIORITY_USER_APP_USER_USER_ID",
                        column: x => x.USER_ID,
                        principalTable: "APP_USER",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ROOM_PRIORITY_USER_MEETING_ROOM_ROOM_ID",
                        column: x => x.ROOM_ID,
                        principalTable: "MEETING_ROOM",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_ROOM_ACCESS_RULE",
                table: "ROOM_ACCESS_RULE",
                columns: new[] { "ROOM_ID", "GRADE" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ROOM_PRIORITY_USER",
                table: "ROOM_PRIORITY_USER",
                column: "USER_ID");

            migrationBuilder.AddIn("ROOM_ACCESS_RULE", "CK_ROOM_ACCESS_GRADE", "GRADE",
                "ASSISTANT_SPECIALIST", "SPECIALIST", "SENIOR_SPECIALIST", "ASSISTANT_MANAGER", "MANAGER",
                "SENIOR_MANAGER", "DIRECTOR", "DEPUTY_GM", "GM");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ROOM_ACCESS_RULE");

            migrationBuilder.DropTable(
                name: "ROOM_PRIORITY_USER");

            migrationBuilder.DropColumn(
                name: "PRIORITY_OPEN_DAYS",
                table: "MEETING_ROOM");

            migrationBuilder.DropColumn(
                name: "SECRETARY_ONLY",
                table: "MEETING_ROOM");
        }
    }
}
