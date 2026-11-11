-- Oturum sahibi semadaki TUM tablolari/view'lari/sequence'lari dusurur (yalnizca yerel gelistirme/test).
-- Kullanim: sqlplus OFIS360_TEST/...@//localhost:1521/FREEPDB1 @reset-schema.sql
SET SERVEROUTPUT ON
BEGIN
  FOR t IN (SELECT table_name FROM user_tables) LOOP
    EXECUTE IMMEDIATE 'DROP TABLE "' || t.table_name || '" CASCADE CONSTRAINTS PURGE';
  END LOOP;
  FOR v IN (SELECT view_name FROM user_views) LOOP
    EXECUTE IMMEDIATE 'DROP VIEW "' || v.view_name || '"';
  END LOOP;
  FOR s IN (SELECT sequence_name FROM user_sequences) LOOP
    EXECUTE IMMEDIATE 'DROP SEQUENCE "' || s.sequence_name || '"';
  END LOOP;
  EXECUTE IMMEDIATE 'PURGE RECYCLEBIN';
END;
/
SELECT COUNT(*) AS remaining_tables FROM user_tables;
EXIT
