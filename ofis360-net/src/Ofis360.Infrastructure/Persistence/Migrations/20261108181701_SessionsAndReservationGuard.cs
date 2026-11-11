using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ofis360.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SessionsAndReservationGuard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "APP_SESSION",
                columns: table => new
                {
                    TOKEN_HASH = table.Column<string>(type: "NVARCHAR2(64)", maxLength: 64, nullable: false),
                    USER_ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    CREATED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(3) WITH TIME ZONE", nullable: false),
                    LAST_SEEN_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(3) WITH TIME ZONE", nullable: false),
                    EXPIRES_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(3) WITH TIME ZONE", nullable: false),
                    REVOKED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(3) WITH TIME ZONE", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_APP_SESSION", x => x.TOKEN_HASH);
                    table.ForeignKey(
                        name: "FK_APP_SESSION_APP_USER_USER_ID",
                        column: x => x.USER_ID,
                        principalTable: "APP_USER",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_APP_SESSION_EXPIRES",
                table: "APP_SESSION",
                column: "EXPIRES_AT");

            migrationBuilder.CreateIndex(
                name: "IX_APP_SESSION_USER",
                table: "APP_SESSION",
                column: "USER_ID");

            // QA-003: aktif gorunup anahtari bos kalan kayitlar yaristan arta kalmistir; iptal gerceklestigi icin iptale cekilir.
            migrationBuilder.Sql("UPDATE \"DESK_RESERVATION\" SET \"STATUS\" = 'CANCELLED', \"CANCEL_REASON\" = NVL(\"CANCEL_REASON\", 'Veri duzeltmesi: check-in/iptal yarisi'), \"CANCELLED_AT\" = NVL(\"CANCELLED_AT\", SYSTIMESTAMP) WHERE \"STATUS\" IN ('BOOKED','CHECKED_IN') AND (\"ACTIVE_DESK_KEY\" IS NULL OR \"ACTIVE_USER_DAY_KEY\" IS NULL)");
            migrationBuilder.Sql("ALTER TABLE \"DESK_RESERVATION\" ADD CONSTRAINT \"CK_DESK_RES_ACTIVE_KEYS\" CHECK (\"STATUS\" NOT IN ('BOOKED','CHECKED_IN') OR (\"ACTIVE_DESK_KEY\" IS NOT NULL AND \"ACTIVE_USER_DAY_KEY\" IS NOT NULL))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropConstraint("DESK_RESERVATION", "CK_DESK_RES_ACTIVE_KEYS");

            migrationBuilder.DropTable(
                name: "APP_SESSION");
        }
    }
}
