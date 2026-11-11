namespace Ofis360.Infrastructure.Persistence.Sql;

/// <summary>
/// PL/SQL paketi ve view'lar (harita §6). Migration'dan calistirilir; Up/Down icin idempotent CREATE OR REPLACE.
/// Is kurallari C# Domain'de kalir; burada yalnizca butunluk (oda cakismasi) ve toplulastirma vardir.
/// </summary>
public static class OracleObjects
{
    public const string ReservationPackageSpec = """
        CREATE OR REPLACE PACKAGE OFIS360_RESERVATION_PKG AS
          -- Ayni gun aktif masa rezervasyonu yoksa 1, varsa 0.
          FUNCTION DESK_IS_FREE(p_desk_id IN VARCHAR2, p_day IN DATE) RETURN NUMBER;

          -- Yari acik aralik [start, end) ile cakisan aktif oda rezervasyonu sayisi.
          FUNCTION ROOM_OVERLAP_COUNT(p_room_id IN VARCHAR2, p_day IN DATE, p_start_min IN NUMBER, p_end_min IN NUMBER) RETURN NUMBER;

          -- Oda satirini kilitler (SELECT ... FOR UPDATE), cakisma yoksa INSERT eder; cakismada -20001.
          PROCEDURE BOOK_ROOM(
            p_id IN VARCHAR2, p_room_id IN VARCHAR2, p_user_id IN VARCHAR2, p_day IN DATE,
            p_start_at IN TIMESTAMP WITH TIME ZONE, p_end_at IN TIMESTAMP WITH TIME ZONE,
            p_start_min IN NUMBER, p_end_min IN NUMBER, p_title IN VARCHAR2, p_purpose IN VARCHAR2,
            p_attendees IN NUMBER, p_source IN VARCHAR2, p_now IN TIMESTAMP WITH TIME ZONE);
        END OFIS360_RESERVATION_PKG;
        """;

    public const string ReservationPackageBody = """
        CREATE OR REPLACE PACKAGE BODY OFIS360_RESERVATION_PKG AS
          FUNCTION DESK_IS_FREE(p_desk_id IN VARCHAR2, p_day IN DATE) RETURN NUMBER IS
            v_count NUMBER;
          BEGIN
            SELECT COUNT(*) INTO v_count FROM DESK_RESERVATION
             WHERE DESK_ID = p_desk_id AND RESERVATION_DATE = p_day
               AND STATUS IN ('BOOKED','CHECKED_IN','COMPLETED','NO_SHOW');
            RETURN CASE WHEN v_count = 0 THEN 1 ELSE 0 END;
          END DESK_IS_FREE;

          FUNCTION ROOM_OVERLAP_COUNT(p_room_id IN VARCHAR2, p_day IN DATE, p_start_min IN NUMBER, p_end_min IN NUMBER) RETURN NUMBER IS
            v_count NUMBER;
          BEGIN
            SELECT COUNT(*) INTO v_count FROM ROOM_RESERVATION
             WHERE ROOM_ID = p_room_id AND RESERVATION_DATE = p_day
               AND STATUS IN ('BOOKED','COMPLETED','NO_SHOW')
               AND START_MINUTES < p_end_min AND p_start_min < END_MINUTES;
            RETURN v_count;
          END ROOM_OVERLAP_COUNT;

          PROCEDURE BOOK_ROOM(
            p_id IN VARCHAR2, p_room_id IN VARCHAR2, p_user_id IN VARCHAR2, p_day IN DATE,
            p_start_at IN TIMESTAMP WITH TIME ZONE, p_end_at IN TIMESTAMP WITH TIME ZONE,
            p_start_min IN NUMBER, p_end_min IN NUMBER, p_title IN VARCHAR2, p_purpose IN VARCHAR2,
            p_attendees IN NUMBER, p_source IN VARCHAR2, p_now IN TIMESTAMP WITH TIME ZONE) IS
            v_lock VARCHAR2(32);
          BEGIN
            SELECT ID INTO v_lock FROM MEETING_ROOM WHERE ID = p_room_id FOR UPDATE;
            IF ROOM_OVERLAP_COUNT(p_room_id, p_day, p_start_min, p_end_min) > 0 THEN
              RAISE_APPLICATION_ERROR(-20001, 'ROOM_CONFLICT');
            END IF;
            INSERT INTO ROOM_RESERVATION (ID, ROOM_ID, USER_ID, RESERVATION_DATE, START_AT, END_AT, START_MINUTES, END_MINUTES,
              TITLE, PURPOSE, ATTENDEE_COUNT, STATUS, SOURCE, CREATED_AT, UPDATED_AT)
            VALUES (p_id, p_room_id, p_user_id, p_day, p_start_at, p_end_at, p_start_min, p_end_min,
              p_title, p_purpose, p_attendees, 'BOOKED', p_source, p_now, p_now);
          END BOOK_ROOM;
        END OFIS360_RESERVATION_PKG;
        """;

    public const string FloorDayOccupancyView = """
        CREATE OR REPLACE VIEW V_FLOOR_DAY_OCCUPANCY AS
        SELECT d.FLOOR_ID AS FLOOR_ID, r.RESERVATION_DATE AS DAY, COUNT(*) AS N
          FROM DESK_RESERVATION r
          JOIN DESK d ON d.ID = r.DESK_ID
          JOIN FLOOR f ON f.ID = d.FLOOR_ID
          JOIN BUILDING b ON b.ID = f.BUILDING_ID
         WHERE b.ACTIVE = TRUE
           AND r.STATUS IN ('BOOKED','CHECKED_IN','COMPLETED','NO_SHOW')
         GROUP BY d.FLOOR_ID, r.RESERVATION_DATE
        """;

    public const string CheckInHourlyView = """
        CREATE OR REPLACE VIEW V_CHECKIN_HOURLY AS
        SELECT r.RESERVATION_DATE AS DAY,
               TO_NUMBER(TO_CHAR(r.CHECKED_IN_AT AT TIME ZONE 'Europe/Istanbul', 'HH24')) AS HOUR,
               COUNT(*) AS N
          FROM DESK_RESERVATION r
          JOIN DESK d ON d.ID = r.DESK_ID
          JOIN FLOOR f ON f.ID = d.FLOOR_ID
          JOIN BUILDING b ON b.ID = f.BUILDING_ID
         WHERE r.CHECKED_IN_AT IS NOT NULL AND b.ACTIVE = TRUE
         GROUP BY r.RESERVATION_DATE, TO_NUMBER(TO_CHAR(r.CHECKED_IN_AT AT TIME ZONE 'Europe/Istanbul', 'HH24'))
        """;

    public static readonly string[] Drop =
    [
        "DROP VIEW V_CHECKIN_HOURLY",
        "DROP VIEW V_FLOOR_DAY_OCCUPANCY",
        "DROP PACKAGE OFIS360_RESERVATION_PKG",
    ];
}
