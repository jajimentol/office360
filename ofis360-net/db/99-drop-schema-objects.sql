-- =====================================================================================
-- DIKKAT: Baglanilan semadaki TUM Ofis360 nesnelerini (tablo, view, sequence, paket, tetikleyici) siler.
-- Yalnizca test/gelistirme semasini sifirlamak icin. Canli semada CALISTIRMAYIN.
--
-- Kullanim: sqlplus OFIS360_TEST/parola@//sunucu:1521/SERVIS @99-drop-schema-objects.sql
-- Sonra 01-schema.sql ile sema yeniden kurulabilir.
-- =====================================================================================
SET SERVEROUTPUT ON
BEGIN
  FOR t IN (SELECT trigger_name FROM user_triggers) LOOP
    EXECUTE IMMEDIATE 'DROP TRIGGER "' || t.trigger_name || '"';
  END LOOP;
  FOR p IN (SELECT object_name FROM user_objects WHERE object_type = 'PACKAGE') LOOP
    EXECUTE IMMEDIATE 'DROP PACKAGE "' || p.object_name || '"';
  END LOOP;
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
  DBMS_OUTPUT.PUT_LINE('Sema temizlendi.');
END;
/
