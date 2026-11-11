namespace Ofis360.Infrastructure.Persistence.Sql;

/// <summary>
/// Rezervasyon durum gecmisi (harita §6, kullanici onayi 2026-11-08). Veri duzeyinde denetim izidir; ekrana yansimaz.
/// Masa ve oda rezervasyonlarinda her INSERT ve her STATUS degisikligi trigger ile tabloya yazilir — uygulama disindan
/// (SQL istemcisi, toplu guncelleme) yapilan degisiklikler de yakalanir. Aktor, uygulamanin her baglantida ayarladigi
/// CLIENT_IDENTIFIER'dir (bkz. <see cref="ClientIdentifierInterceptor"/>); uygulama disi degisikliklerde bos kalir ve
/// DB_USER sutunu veritabani kullanicisini gosterir. Tablo salt eklemelidir: UPDATE/DELETE trigger ile reddedilir.
/// Uygulama denetim kaydi (AUDIT_LOG, is olaylari) kaynakla ayni kalir.
/// </summary>
public static class StatusHistoryObjects
{
    public const string Table = """
        CREATE TABLE RESERVATION_STATUS_HISTORY (
          ID               NUMBER GENERATED ALWAYS AS IDENTITY CONSTRAINT PK_RESERVATION_STATUS_HISTORY PRIMARY KEY,
          RESERVATION_TYPE VARCHAR2(10) NOT NULL,
          RESERVATION_ID   VARCHAR2(400) NOT NULL,
          OLD_STATUS       VARCHAR2(20),
          NEW_STATUS       VARCHAR2(20) NOT NULL,
          CHANGED_AT       TIMESTAMP(3) WITH TIME ZONE DEFAULT SYSTIMESTAMP NOT NULL,
          ACTOR_ID         VARCHAR2(400),
          DB_USER          VARCHAR2(128) DEFAULT SYS_CONTEXT('USERENV', 'SESSION_USER') NOT NULL,
          CONSTRAINT CK_RES_STATUS_HIST_TYPE CHECK (RESERVATION_TYPE IN ('DESK', 'ROOM'))
        )
        """;

    public const string Index = """
        CREATE INDEX IX_RES_STATUS_HIST_RES ON RESERVATION_STATUS_HISTORY (RESERVATION_TYPE, RESERVATION_ID, CHANGED_AT)
        """;

    public const string IndexChangedAt = """
        CREATE INDEX IX_RES_STATUS_HIST_CHANGED ON RESERVATION_STATUS_HISTORY (CHANGED_AT)
        """;

    public const string DeskTrigger = """
        CREATE OR REPLACE TRIGGER TRG_DESK_RES_STATUS_HIST
        AFTER INSERT OR UPDATE OF STATUS ON DESK_RESERVATION
        FOR EACH ROW
        WHEN (OLD.STATUS IS NULL OR NEW.STATUS <> OLD.STATUS)
        BEGIN
          INSERT INTO RESERVATION_STATUS_HISTORY (RESERVATION_TYPE, RESERVATION_ID, OLD_STATUS, NEW_STATUS, ACTOR_ID)
          VALUES ('DESK', :NEW.ID, :OLD.STATUS, :NEW.STATUS, SYS_CONTEXT('USERENV', 'CLIENT_IDENTIFIER'));
        END;
        """;

    public const string RoomTrigger = """
        CREATE OR REPLACE TRIGGER TRG_ROOM_RES_STATUS_HIST
        AFTER INSERT OR UPDATE OF STATUS ON ROOM_RESERVATION
        FOR EACH ROW
        WHEN (OLD.STATUS IS NULL OR NEW.STATUS <> OLD.STATUS)
        BEGIN
          INSERT INTO RESERVATION_STATUS_HISTORY (RESERVATION_TYPE, RESERVATION_ID, OLD_STATUS, NEW_STATUS, ACTOR_ID)
          VALUES ('ROOM', :NEW.ID, :OLD.STATUS, :NEW.STATUS, SYS_CONTEXT('USERENV', 'CLIENT_IDENTIFIER'));
        END;
        """;

    public const string AppendOnlyTrigger = """
        CREATE OR REPLACE TRIGGER TRG_RES_STATUS_HIST_APPEND_ONLY
        BEFORE UPDATE OR DELETE ON RESERVATION_STATUS_HISTORY
        BEGIN
          RAISE_APPLICATION_ERROR(-20002, 'RESERVATION_STATUS_HISTORY yalnizca eklemeye aciktir.');
        END;
        """;

    public static readonly string[] Create = [Table, Index, IndexChangedAt, DeskTrigger, RoomTrigger, AppendOnlyTrigger];

    public static readonly string[] Drop =
    [
        "DROP TRIGGER TRG_RES_STATUS_HIST_APPEND_ONLY",
        "DROP TRIGGER TRG_ROOM_RES_STATUS_HIST",
        "DROP TRIGGER TRG_DESK_RES_STATUS_HIST",
        "DROP TABLE RESERVATION_STATUS_HISTORY PURGE",
    ];
}
