using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ofis360.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AUDIT_LOG",
                columns: table => new
                {
                    ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    ACTOR_ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: true),
                    ACTOR_NAME = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    ACTOR_ROLE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    ACTION = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    ENTITY_TYPE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    ENTITY_ID = table.Column<string>(type: "NVARCHAR2(64)", maxLength: 64, nullable: true),
                    SUMMARY = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: false),
                    METADATA = table.Column<string>(type: "CLOB", maxLength: 400, nullable: true),
                    CREATED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(3) WITH TIME ZONE", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AUDIT_LOG", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "BUILDING",
                columns: table => new
                {
                    ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    CODE = table.Column<string>(type: "NVARCHAR2(12)", maxLength: 12, nullable: false),
                    NAME = table.Column<string>(type: "NVARCHAR2(80)", maxLength: 80, nullable: false),
                    ADDRESS = table.Column<string>(type: "NVARCHAR2(240)", maxLength: 240, nullable: true),
                    SORT_ORDER = table.Column<int>(type: "NUMBER(10)", nullable: false, defaultValue: 0),
                    ACTIVE = table.Column<bool>(type: "BOOLEAN", nullable: false, defaultValue: true),
                    IS_DEMO = table.Column<bool>(type: "BOOLEAN", nullable: false, defaultValue: false),
                    CREATED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(3) WITH TIME ZONE", nullable: false),
                    UPDATED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(3) WITH TIME ZONE", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BUILDING", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "CAPACITY_SHORTFALL",
                columns: table => new
                {
                    ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    SHORTFALL_DATE = table.Column<DateTime>(type: "DATE", nullable: false),
                    REQUESTS = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    FULFILLED = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    UNMET = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    SOURCE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false, defaultValue: "SYSTEM"),
                    CREATED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(3) WITH TIME ZONE", nullable: false),
                    UPDATED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(3) WITH TIME ZONE", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CAPACITY_SHORTFALL", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "DEPARTMENT",
                columns: table => new
                {
                    ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    CODE = table.Column<string>(type: "NVARCHAR2(40)", maxLength: 40, nullable: false),
                    NAME = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    COLOR = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false, defaultValue: "#0f4c81"),
                    ACTIVE = table.Column<bool>(type: "BOOLEAN", nullable: false, defaultValue: true),
                    CREATED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(3) WITH TIME ZONE", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DEPARTMENT", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "DESK_FEATURE",
                columns: table => new
                {
                    ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    FEATURE_KEY = table.Column<string>(type: "NVARCHAR2(40)", maxLength: 40, nullable: false),
                    NAME = table.Column<string>(type: "NVARCHAR2(80)", maxLength: 80, nullable: false),
                    DESCRIPTION = table.Column<string>(type: "NVARCHAR2(240)", maxLength: 240, nullable: true),
                    ICON = table.Column<string>(type: "NVARCHAR2(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DESK_FEATURE", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "RESERVATION_RULE",
                columns: table => new
                {
                    ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    RULE_KEY = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    NAME = table.Column<string>(type: "NVARCHAR2(120)", maxLength: 120, nullable: false),
                    DESCRIPTION = table.Column<string>(type: "NVARCHAR2(400)", maxLength: 400, nullable: false),
                    CATEGORY = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false, defaultValue: "DESK"),
                    ENABLED = table.Column<bool>(type: "BOOLEAN", nullable: false, defaultValue: true),
                    EDITABLE_LIMIT = table.Column<bool>(type: "BOOLEAN", nullable: false, defaultValue: true),
                    INT_VALUE = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    SECOND_VALUE = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    UNIT = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: true),
                    MIN_VALUE = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    MAX_VALUE = table.Column<int>(type: "NUMBER(10)", nullable: true),
                    SYSTEM_RULE = table.Column<bool>(type: "BOOLEAN", nullable: false, defaultValue: false),
                    SORT_ORDER = table.Column<int>(type: "NUMBER(10)", nullable: false, defaultValue: 0),
                    UPDATED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(3) WITH TIME ZONE", nullable: false),
                    UPDATED_BY_ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RESERVATION_RULE", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "ROOM_FEATURE",
                columns: table => new
                {
                    ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    FEATURE_KEY = table.Column<string>(type: "NVARCHAR2(40)", maxLength: 40, nullable: false),
                    NAME = table.Column<string>(type: "NVARCHAR2(80)", maxLength: 80, nullable: false),
                    ICON = table.Column<string>(type: "NVARCHAR2(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ROOM_FEATURE", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "SYSTEM_SETTING",
                columns: table => new
                {
                    ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    SETTING_KEY = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    SETTING_VALUE = table.Column<string>(type: "NVARCHAR2(2000)", maxLength: 2000, nullable: true),
                    LABEL = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    DESCRIPTION = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: true),
                    SETTING_TYPE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false, defaultValue: "TEXT"),
                    SETTING_GROUP = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false, defaultValue: "GENERAL"),
                    SORT_ORDER = table.Column<int>(type: "NUMBER(10)", nullable: false, defaultValue: 0),
                    UPDATED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(3) WITH TIME ZONE", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SYSTEM_SETTING", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "FLOOR",
                columns: table => new
                {
                    ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    BUILDING_ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    FLOOR_NUMBER = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    NAME = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: false),
                    DESCRIPTION = table.Column<string>(type: "NVARCHAR2(240)", maxLength: 240, nullable: true),
                    RESERVABLE = table.Column<bool>(type: "BOOLEAN", nullable: false, defaultValue: true),
                    ACTIVE = table.Column<bool>(type: "BOOLEAN", nullable: false, defaultValue: true),
                    PLAN_WIDTH = table.Column<int>(type: "NUMBER(10)", nullable: false, defaultValue: 1000),
                    PLAN_HEIGHT = table.Column<int>(type: "NUMBER(10)", nullable: false, defaultValue: 640),
                    CREATED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(3) WITH TIME ZONE", nullable: false),
                    UPDATED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(3) WITH TIME ZONE", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FLOOR", x => x.ID);
                    table.ForeignKey(
                        name: "FK_FLOOR_BUILDING_BUILDING_ID",
                        column: x => x.BUILDING_ID,
                        principalTable: "BUILDING",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "APP_USER",
                columns: table => new
                {
                    ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    EMAIL = table.Column<string>(type: "NVARCHAR2(320)", maxLength: 320, nullable: false),
                    FULL_NAME = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    TITLE = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    ROLE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false, defaultValue: "EMPLOYEE"),
                    DEPARTMENT_ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    ACCESSIBILITY_APPROVED = table.Column<bool>(type: "BOOLEAN", nullable: false, defaultValue: false),
                    IS_DEMO_ACCOUNT = table.Column<bool>(type: "BOOLEAN", nullable: false, defaultValue: false),
                    ACTIVE = table.Column<bool>(type: "BOOLEAN", nullable: false, defaultValue: true),
                    PREFERRED_FLOOR_ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: true),
                    PREFERS_QUIET_ZONE = table.Column<bool>(type: "BOOLEAN", nullable: false, defaultValue: false),
                    NOTIFICATIONS_ENABLED = table.Column<bool>(type: "BOOLEAN", nullable: false, defaultValue: true),
                    CREATED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(3) WITH TIME ZONE", nullable: false),
                    UPDATED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(3) WITH TIME ZONE", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_APP_USER", x => x.ID);
                    table.ForeignKey(
                        name: "FK_APP_USER_DEPARTMENT_DEPARTMENT_ID",
                        column: x => x.DEPARTMENT_ID,
                        principalTable: "DEPARTMENT",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FLOOR_PLAN_ELEMENT",
                columns: table => new
                {
                    ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    FLOOR_ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    ELEMENT_TYPE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    LABEL = table.Column<string>(type: "NVARCHAR2(80)", maxLength: 80, nullable: true),
                    X = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    Y = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    WIDTH = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    HEIGHT = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    ROTATION = table.Column<int>(type: "NUMBER(10)", nullable: false, defaultValue: 0),
                    STYLE = table.Column<string>(type: "NVARCHAR2(400)", maxLength: 400, nullable: false, defaultValue: "{}"),
                    LAYER_ORDER = table.Column<int>(type: "NUMBER(10)", nullable: false, defaultValue: 0),
                    LOCKED = table.Column<bool>(type: "BOOLEAN", nullable: false, defaultValue: false),
                    VISIBLE = table.Column<bool>(type: "BOOLEAN", nullable: false, defaultValue: true),
                    LINKED_RESOURCE_ID = table.Column<string>(type: "NVARCHAR2(64)", maxLength: 64, nullable: true),
                    LINKED_RESOURCE_TYPE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: true),
                    CREATED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(3) WITH TIME ZONE", nullable: false),
                    UPDATED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(3) WITH TIME ZONE", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FLOOR_PLAN_ELEMENT", x => x.ID);
                    table.ForeignKey(
                        name: "FK_FLOOR_PLAN_ELEMENT_FLOOR_FLOOR_ID",
                        column: x => x.FLOOR_ID,
                        principalTable: "FLOOR",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MEETING_ROOM",
                columns: table => new
                {
                    ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    CODE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    NAME = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: false),
                    FLOOR_ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    CAPACITY = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    X = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    Y = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    WIDTH = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    HEIGHT = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    PLACED = table.Column<bool>(type: "BOOLEAN", nullable: false, defaultValue: true),
                    ACTIVE = table.Column<bool>(type: "BOOLEAN", nullable: false, defaultValue: true),
                    NOTES = table.Column<string>(type: "NVARCHAR2(240)", maxLength: 240, nullable: true),
                    CREATED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(3) WITH TIME ZONE", nullable: false),
                    UPDATED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(3) WITH TIME ZONE", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MEETING_ROOM", x => x.ID);
                    table.ForeignKey(
                        name: "FK_MEETING_ROOM_FLOOR_FLOOR_ID",
                        column: x => x.FLOOR_ID,
                        principalTable: "FLOOR",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ZONE",
                columns: table => new
                {
                    ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    FLOOR_ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    CODE = table.Column<string>(type: "NVARCHAR2(40)", maxLength: 40, nullable: false),
                    NAME = table.Column<string>(type: "NVARCHAR2(60)", maxLength: 60, nullable: false),
                    KIND = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false, defaultValue: "WORK"),
                    X = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    Y = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    WIDTH = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    HEIGHT = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    WINDOW_SIDE = table.Column<bool>(type: "BOOLEAN", nullable: false, defaultValue: false),
                    QUIET = table.Column<bool>(type: "BOOLEAN", nullable: false, defaultValue: false),
                    RESERVABLE = table.Column<bool>(type: "BOOLEAN", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZONE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_ZONE_FLOOR_FLOOR_ID",
                        column: x => x.FLOOR_ID,
                        principalTable: "FLOOR",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NOTIFICATION",
                columns: table => new
                {
                    ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    USER_ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    NOTIFICATION_TYPE = table.Column<string>(type: "NVARCHAR2(50)", maxLength: 50, nullable: false),
                    TITLE = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false),
                    BODY = table.Column<string>(type: "NVARCHAR2(1000)", maxLength: 1000, nullable: false),
                    LINK = table.Column<string>(type: "NVARCHAR2(400)", maxLength: 400, nullable: true),
                    SEVERITY = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false, defaultValue: "INFO"),
                    READ_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(3) WITH TIME ZONE", nullable: true),
                    CREATED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(3) WITH TIME ZONE", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NOTIFICATION", x => x.ID);
                    table.ForeignKey(
                        name: "FK_NOTIFICATION_APP_USER_USER_ID",
                        column: x => x.USER_ID,
                        principalTable: "APP_USER",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ROOM_FEATURE_LINK",
                columns: table => new
                {
                    ROOM_ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    FEATURE_ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ROOM_FEATURE_LINK", x => new { x.ROOM_ID, x.FEATURE_ID });
                    table.ForeignKey(
                        name: "FK_ROOM_FEATURE_LINK_MEETING_ROOM_ROOM_ID",
                        column: x => x.ROOM_ID,
                        principalTable: "MEETING_ROOM",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ROOM_FEATURE_LINK_ROOM_FEATURE_FEATURE_ID",
                        column: x => x.FEATURE_ID,
                        principalTable: "ROOM_FEATURE",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ROOM_RESERVATION",
                columns: table => new
                {
                    ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    ROOM_ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    USER_ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    RESERVATION_DATE = table.Column<DateTime>(type: "DATE", nullable: false),
                    START_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(3) WITH TIME ZONE", nullable: false),
                    END_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(3) WITH TIME ZONE", nullable: false),
                    START_MINUTES = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    END_MINUTES = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    TITLE = table.Column<string>(type: "NVARCHAR2(120)", maxLength: 120, nullable: false),
                    PURPOSE = table.Column<string>(type: "NVARCHAR2(120)", maxLength: 120, nullable: false),
                    ATTENDEE_COUNT = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    STATUS = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false, defaultValue: "BOOKED"),
                    SOURCE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false, defaultValue: "EMPLOYEE"),
                    CANCELLED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(3) WITH TIME ZONE", nullable: true),
                    CANCELLED_BY_ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: true),
                    CANCEL_REASON = table.Column<string>(type: "NVARCHAR2(240)", maxLength: 240, nullable: true),
                    CREATED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(3) WITH TIME ZONE", nullable: false),
                    UPDATED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(3) WITH TIME ZONE", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ROOM_RESERVATION", x => x.ID);
                    table.ForeignKey(
                        name: "FK_ROOM_RESERVATION_APP_USER_USER_ID",
                        column: x => x.USER_ID,
                        principalTable: "APP_USER",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ROOM_RESERVATION_MEETING_ROOM_ROOM_ID",
                        column: x => x.ROOM_ID,
                        principalTable: "MEETING_ROOM",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DESK",
                columns: table => new
                {
                    ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    CODE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    FLOOR_ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    ZONE_ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    X = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    Y = table.Column<int>(type: "NUMBER(10)", nullable: false),
                    WIDTH = table.Column<int>(type: "NUMBER(10)", nullable: false, defaultValue: 46),
                    HEIGHT = table.Column<int>(type: "NUMBER(10)", nullable: false, defaultValue: 34),
                    ROTATION = table.Column<int>(type: "NUMBER(10)", nullable: false, defaultValue: 0),
                    PLACED = table.Column<bool>(type: "BOOLEAN", nullable: false, defaultValue: true),
                    ACTIVE = table.Column<bool>(type: "BOOLEAN", nullable: false, defaultValue: true),
                    ACCESSIBLE_ONLY = table.Column<bool>(type: "BOOLEAN", nullable: false, defaultValue: false),
                    POPULAR = table.Column<bool>(type: "BOOLEAN", nullable: false, defaultValue: false),
                    NOTES = table.Column<string>(type: "NVARCHAR2(240)", maxLength: 240, nullable: true),
                    CREATED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(3) WITH TIME ZONE", nullable: false),
                    UPDATED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(3) WITH TIME ZONE", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DESK", x => x.ID);
                    table.ForeignKey(
                        name: "FK_DESK_FLOOR_FLOOR_ID",
                        column: x => x.FLOOR_ID,
                        principalTable: "FLOOR",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DESK_ZONE_ZONE_ID",
                        column: x => x.ZONE_ID,
                        principalTable: "ZONE",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CLOSURE",
                columns: table => new
                {
                    ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    SCOPE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    FLOOR_ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: true),
                    DESK_ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: true),
                    ROOM_ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: true),
                    START_DATE = table.Column<DateTime>(type: "DATE", nullable: false),
                    END_DATE = table.Column<DateTime>(type: "DATE", nullable: false),
                    REASON = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false),
                    NOTE = table.Column<string>(type: "NVARCHAR2(240)", maxLength: 240, nullable: false),
                    ACTIVE = table.Column<bool>(type: "BOOLEAN", nullable: false, defaultValue: true),
                    CREATED_BY_ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    CREATED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(3) WITH TIME ZONE", nullable: false),
                    UPDATED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(3) WITH TIME ZONE", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CLOSURE", x => x.ID);
                    table.ForeignKey(
                        name: "FK_CLOSURE_DESK_DESK_ID",
                        column: x => x.DESK_ID,
                        principalTable: "DESK",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CLOSURE_FLOOR_FLOOR_ID",
                        column: x => x.FLOOR_ID,
                        principalTable: "FLOOR",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CLOSURE_MEETING_ROOM_ROOM_ID",
                        column: x => x.ROOM_ID,
                        principalTable: "MEETING_ROOM",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DESK_FEATURE_LINK",
                columns: table => new
                {
                    DESK_ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    FEATURE_ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DESK_FEATURE_LINK", x => new { x.DESK_ID, x.FEATURE_ID });
                    table.ForeignKey(
                        name: "FK_DESK_FEATURE_LINK_DESK_DESK_ID",
                        column: x => x.DESK_ID,
                        principalTable: "DESK",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DESK_FEATURE_LINK_DESK_FEATURE_FEATURE_ID",
                        column: x => x.FEATURE_ID,
                        principalTable: "DESK_FEATURE",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DESK_RESERVATION",
                columns: table => new
                {
                    ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    DESK_ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    USER_ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: false),
                    RESERVATION_DATE = table.Column<DateTime>(type: "DATE", nullable: false),
                    STATUS = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false, defaultValue: "BOOKED"),
                    ACTIVE_DESK_KEY = table.Column<string>(type: "NVARCHAR2(48)", maxLength: 48, nullable: true),
                    ACTIVE_USER_DAY_KEY = table.Column<string>(type: "NVARCHAR2(48)", maxLength: 48, nullable: true),
                    NOTE = table.Column<string>(type: "NVARCHAR2(240)", maxLength: 240, nullable: true),
                    SOURCE = table.Column<string>(type: "NVARCHAR2(20)", maxLength: 20, nullable: false, defaultValue: "EMPLOYEE"),
                    CHECKED_IN_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(3) WITH TIME ZONE", nullable: true),
                    CANCELLED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(3) WITH TIME ZONE", nullable: true),
                    CANCELLED_BY_ID = table.Column<string>(type: "NVARCHAR2(32)", maxLength: 32, nullable: true),
                    CANCEL_REASON = table.Column<string>(type: "NVARCHAR2(240)", maxLength: 240, nullable: true),
                    CREATED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(3) WITH TIME ZONE", nullable: false),
                    UPDATED_AT = table.Column<DateTimeOffset>(type: "TIMESTAMP(3) WITH TIME ZONE", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DESK_RESERVATION", x => x.ID);
                    table.ForeignKey(
                        name: "FK_DESK_RESERVATION_APP_USER_USER_ID",
                        column: x => x.USER_ID,
                        principalTable: "APP_USER",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DESK_RESERVATION_DESK_DESK_ID",
                        column: x => x.DESK_ID,
                        principalTable: "DESK",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_APP_USER_ACTIVE_NAME",
                table: "APP_USER",
                columns: new[] { "ACTIVE", "FULL_NAME" });

            migrationBuilder.CreateIndex(
                name: "IX_APP_USER_DEPARTMENT",
                table: "APP_USER",
                column: "DEPARTMENT_ID");

            migrationBuilder.CreateIndex(
                name: "IX_APP_USER_ROLE",
                table: "APP_USER",
                column: "ROLE");

            migrationBuilder.CreateIndex(
                name: "UX_APP_USER_EMAIL",
                table: "APP_USER",
                column: "EMAIL",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AUDIT_LOG_ACTOR",
                table: "AUDIT_LOG",
                column: "ACTOR_ID");

            migrationBuilder.CreateIndex(
                name: "IX_AUDIT_LOG_CREATED",
                table: "AUDIT_LOG",
                column: "CREATED_AT");

            migrationBuilder.CreateIndex(
                name: "IX_AUDIT_LOG_ENTITY_TYPE",
                table: "AUDIT_LOG",
                column: "ENTITY_TYPE");

            migrationBuilder.CreateIndex(
                name: "UX_BUILDING_CODE",
                table: "BUILDING",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_CAPACITY_SHORTFALL_DATE",
                table: "CAPACITY_SHORTFALL",
                column: "SHORTFALL_DATE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CLOSURE_DATES",
                table: "CLOSURE",
                columns: new[] { "START_DATE", "END_DATE" });

            migrationBuilder.CreateIndex(
                name: "IX_CLOSURE_DESK_ID",
                table: "CLOSURE",
                column: "DESK_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CLOSURE_FLOOR_ID",
                table: "CLOSURE",
                column: "FLOOR_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CLOSURE_ROOM_ID",
                table: "CLOSURE",
                column: "ROOM_ID");

            migrationBuilder.CreateIndex(
                name: "IX_CLOSURE_SCOPE",
                table: "CLOSURE",
                column: "SCOPE");

            migrationBuilder.CreateIndex(
                name: "UX_DEPARTMENT_CODE",
                table: "DEPARTMENT",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DESK_FLOOR",
                table: "DESK",
                column: "FLOOR_ID");

            migrationBuilder.CreateIndex(
                name: "IX_DESK_FLOOR_PLACED",
                table: "DESK",
                columns: new[] { "FLOOR_ID", "PLACED" });

            migrationBuilder.CreateIndex(
                name: "IX_DESK_ZONE",
                table: "DESK",
                column: "ZONE_ID");

            migrationBuilder.CreateIndex(
                name: "UX_DESK_CODE",
                table: "DESK",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_DESK_FEATURE_KEY",
                table: "DESK_FEATURE",
                column: "FEATURE_KEY",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DESK_FEATURE_LINK_FEATURE_ID",
                table: "DESK_FEATURE_LINK",
                column: "FEATURE_ID");

            migrationBuilder.CreateIndex(
                name: "IX_DESK_RES_DATE",
                table: "DESK_RESERVATION",
                column: "RESERVATION_DATE");

            migrationBuilder.CreateIndex(
                name: "IX_DESK_RES_DATE_STATUS",
                table: "DESK_RESERVATION",
                columns: new[] { "RESERVATION_DATE", "STATUS" });

            migrationBuilder.CreateIndex(
                name: "IX_DESK_RES_DESK_DATE",
                table: "DESK_RESERVATION",
                columns: new[] { "DESK_ID", "RESERVATION_DATE" });

            migrationBuilder.CreateIndex(
                name: "IX_DESK_RES_DESK_DATE_STATUS",
                table: "DESK_RESERVATION",
                columns: new[] { "DESK_ID", "RESERVATION_DATE", "STATUS" });

            migrationBuilder.CreateIndex(
                name: "IX_DESK_RES_STATUS",
                table: "DESK_RESERVATION",
                column: "STATUS");

            migrationBuilder.CreateIndex(
                name: "IX_DESK_RES_USER_DATE",
                table: "DESK_RESERVATION",
                columns: new[] { "USER_ID", "RESERVATION_DATE" });

            migrationBuilder.CreateIndex(
                name: "IX_DESK_RES_USER_DATE_STATUS",
                table: "DESK_RESERVATION",
                columns: new[] { "USER_ID", "RESERVATION_DATE", "STATUS" });

            migrationBuilder.CreateIndex(
                name: "UX_DESK_RES_ACTIVE_DESK",
                table: "DESK_RESERVATION",
                column: "ACTIVE_DESK_KEY",
                unique: true,
                filter: "\"ACTIVE_DESK_KEY\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_DESK_RES_ACTIVE_USER_DAY",
                table: "DESK_RESERVATION",
                column: "ACTIVE_USER_DAY_KEY",
                unique: true,
                filter: "\"ACTIVE_USER_DAY_KEY\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FLOOR_BUILDING_ACTIVE",
                table: "FLOOR",
                columns: new[] { "BUILDING_ID", "ACTIVE" });

            migrationBuilder.CreateIndex(
                name: "UX_FLOOR_BUILDING_NUMBER",
                table: "FLOOR",
                columns: new[] { "BUILDING_ID", "FLOOR_NUMBER" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FLOOR_PLAN_ELEMENT_FLOOR_LAYER",
                table: "FLOOR_PLAN_ELEMENT",
                columns: new[] { "FLOOR_ID", "LAYER_ORDER" });

            migrationBuilder.CreateIndex(
                name: "IX_MEETING_ROOM_FLOOR",
                table: "MEETING_ROOM",
                column: "FLOOR_ID");

            migrationBuilder.CreateIndex(
                name: "IX_MEETING_ROOM_FLOOR_PLACED",
                table: "MEETING_ROOM",
                columns: new[] { "FLOOR_ID", "PLACED" });

            migrationBuilder.CreateIndex(
                name: "UX_MEETING_ROOM_CODE",
                table: "MEETING_ROOM",
                column: "CODE",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NOTIFICATION_CREATED",
                table: "NOTIFICATION",
                column: "CREATED_AT");

            migrationBuilder.CreateIndex(
                name: "IX_NOTIFICATION_USER_READ",
                table: "NOTIFICATION",
                columns: new[] { "USER_ID", "READ_AT" });

            migrationBuilder.CreateIndex(
                name: "UX_RESERVATION_RULE_KEY",
                table: "RESERVATION_RULE",
                column: "RULE_KEY",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ROOM_FEATURE_KEY",
                table: "ROOM_FEATURE",
                column: "FEATURE_KEY",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ROOM_FEATURE_LINK_FEATURE_ID",
                table: "ROOM_FEATURE_LINK",
                column: "FEATURE_ID");

            migrationBuilder.CreateIndex(
                name: "IX_ROOM_RES_DATE",
                table: "ROOM_RESERVATION",
                column: "RESERVATION_DATE");

            migrationBuilder.CreateIndex(
                name: "IX_ROOM_RES_DATE_STATUS",
                table: "ROOM_RESERVATION",
                columns: new[] { "RESERVATION_DATE", "STATUS" });

            migrationBuilder.CreateIndex(
                name: "IX_ROOM_RES_ROOM_DATE",
                table: "ROOM_RESERVATION",
                columns: new[] { "ROOM_ID", "RESERVATION_DATE" });

            migrationBuilder.CreateIndex(
                name: "IX_ROOM_RES_ROOM_DATE_STATUS",
                table: "ROOM_RESERVATION",
                columns: new[] { "ROOM_ID", "RESERVATION_DATE", "STATUS" });

            migrationBuilder.CreateIndex(
                name: "IX_ROOM_RES_STATUS",
                table: "ROOM_RESERVATION",
                column: "STATUS");

            migrationBuilder.CreateIndex(
                name: "IX_ROOM_RES_USER_DATE",
                table: "ROOM_RESERVATION",
                columns: new[] { "USER_ID", "RESERVATION_DATE" });

            migrationBuilder.CreateIndex(
                name: "UX_SYSTEM_SETTING_KEY",
                table: "SYSTEM_SETTING",
                column: "SETTING_KEY",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ZONE_FLOOR_CODE",
                table: "ZONE",
                columns: new[] { "FLOOR_ID", "CODE" },
                unique: true);
            // CHECK kisitlari ham SQL ile (bkz. OracleCheckConstraints). Degerler lib/enums.ts ile birebir.
            migrationBuilder.AddIn("APP_USER", "CK_APP_USER_ROLE", "ROLE", "EMPLOYEE", "MANAGER", "ADMIN");
            migrationBuilder.AddIn("SYSTEM_SETTING", "CK_SYSTEM_SETTING_TYPE", "SETTING_TYPE", "TEXT", "NUMBER", "BOOLEAN", "TIME");
            migrationBuilder.AddIn("ZONE", "CK_ZONE_KIND", "KIND", "WORK", "MEETING", "SOCIAL", "CORRIDOR", "CORE", "RESTRICTED");
            migrationBuilder.AddIn("DESK_RESERVATION", "CK_DESK_RES_STATUS", "STATUS", "BOOKED", "CHECKED_IN", "COMPLETED", "CANCELLED", "NO_SHOW");
            migrationBuilder.AddIn("DESK_RESERVATION", "CK_DESK_RES_SOURCE", "SOURCE", "EMPLOYEE", "ADMIN", "SEED");
            migrationBuilder.AddIn("ROOM_RESERVATION", "CK_ROOM_RES_STATUS", "STATUS", "BOOKED", "COMPLETED", "CANCELLED", "NO_SHOW");
            migrationBuilder.AddIn("ROOM_RESERVATION", "CK_ROOM_RES_SOURCE", "SOURCE", "EMPLOYEE", "ADMIN", "SEED");
            migrationBuilder.AddIn("RESERVATION_RULE", "CK_RESERVATION_RULE_CATEGORY", "CATEGORY", "DESK", "ROOM", "GENERAL");
            migrationBuilder.AddIn("CAPACITY_SHORTFALL", "CK_CAPACITY_SHORTFALL_SOURCE", "SOURCE", "SYSTEM", "SEED");
            migrationBuilder.AddIn("CLOSURE", "CK_CLOSURE_SCOPE", "SCOPE", "FLOOR", "DESK", "ROOM");
            migrationBuilder.AddIn("CLOSURE", "CK_CLOSURE_REASON", "REASON", "MAINTENANCE", "EVENT", "RESERVED_USE", "OTHER");
            migrationBuilder.AddIn("NOTIFICATION", "CK_NOTIFICATION_SEVERITY", "SEVERITY", "INFO", "SUCCESS", "WARNING");
            migrationBuilder.AddIn("FLOOR_PLAN_ELEMENT", "CK_FLOOR_PLAN_ELEMENT_TYPE", "ELEMENT_TYPE",
                "WALL", "ROOM", "CORRIDOR", "DOOR", "WINDOW", "ELEVATOR", "STAIRS", "KITCHEN", "WC", "RESTRICTED", "TERRACE", "TEXT");
            migrationBuilder.AddIn("FLOOR_PLAN_ELEMENT", "CK_FLOOR_PLAN_ELEMENT_LINK_TYPE", "LINKED_RESOURCE_TYPE", "DESK", "MEETING_ROOM");
            migrationBuilder.Sql("ALTER TABLE \"AUDIT_LOG\" ADD CONSTRAINT \"CK_AUDIT_LOG_METADATA_JSON\" CHECK (\"METADATA\" IS JSON)");
            migrationBuilder.Sql("ALTER TABLE \"CLOSURE\" ADD CONSTRAINT \"CK_CLOSURE_DATE_ORDER\" CHECK (\"END_DATE\" >= \"START_DATE\")");

            // PL/SQL paketi ve toplulastirma view'lari (harita §6). Is kurallari C# Domain'de kalir.
            migrationBuilder.Sql(Sql.OracleObjects.ReservationPackageSpec);
            migrationBuilder.Sql(Sql.OracleObjects.ReservationPackageBody);
            migrationBuilder.Sql(Sql.OracleObjects.FloorDayOccupancyView);
            migrationBuilder.Sql(Sql.OracleObjects.CheckInHourlyView);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)

        {
            foreach (var statement in Sql.OracleObjects.Drop)
            {
                migrationBuilder.Sql(statement);
            }

            migrationBuilder.DropTable(
                name: "AUDIT_LOG");

            migrationBuilder.DropTable(
                name: "CAPACITY_SHORTFALL");

            migrationBuilder.DropTable(
                name: "CLOSURE");

            migrationBuilder.DropTable(
                name: "DESK_FEATURE_LINK");

            migrationBuilder.DropTable(
                name: "DESK_RESERVATION");

            migrationBuilder.DropTable(
                name: "FLOOR_PLAN_ELEMENT");

            migrationBuilder.DropTable(
                name: "NOTIFICATION");

            migrationBuilder.DropTable(
                name: "RESERVATION_RULE");

            migrationBuilder.DropTable(
                name: "ROOM_FEATURE_LINK");

            migrationBuilder.DropTable(
                name: "ROOM_RESERVATION");

            migrationBuilder.DropTable(
                name: "SYSTEM_SETTING");

            migrationBuilder.DropTable(
                name: "DESK_FEATURE");

            migrationBuilder.DropTable(
                name: "DESK");

            migrationBuilder.DropTable(
                name: "ROOM_FEATURE");

            migrationBuilder.DropTable(
                name: "APP_USER");

            migrationBuilder.DropTable(
                name: "MEETING_ROOM");

            migrationBuilder.DropTable(
                name: "ZONE");

            migrationBuilder.DropTable(
                name: "DEPARTMENT");

            migrationBuilder.DropTable(
                name: "FLOOR");

            migrationBuilder.DropTable(
                name: "BUILDING");
        }
    }
}
