-- =====================================================================================
-- Ofis360 - uygulama semasi (kullanicisi) olusturma.  DBA tarafindan BIR KEZ calistirilir.
--
-- Kullanim (SQL*Plus / SQLcl, PDB'ye DBA olarak bagliyken):
--   sqlplus sys@//sunucu:1521/SERVIS as sysdba @00-create-user.sql
-- Calistirinca kullanici adi, parola ve tablespace sorulur.
-- Kullanici zaten varsa olusturulmaz, yalnizca yetkileri tamamlanir (tekrar calistirmak guvenlidir).
-- SQL Developer'da: "Run Script" (F5) ile calistirin; degiskenler ayni sekilde sorulur.
-- =====================================================================================
SET VERIFY OFF
SET SERVEROUTPUT ON

ACCEPT app_user     CHAR DEFAULT 'OFIS360' PROMPT 'Sema kullanicisi [OFIS360]: '
ACCEPT app_password CHAR PROMPT 'Parola: ' HIDE
ACCEPT app_tbs      CHAR DEFAULT 'USERS'   PROMPT 'Varsayilan tablespace [USERS]: '

DECLARE
  v_user  VARCHAR2(128) := UPPER('&&app_user');
  v_count NUMBER;
BEGIN
  SELECT COUNT(*) INTO v_count FROM dba_users WHERE username = v_user;
  IF v_count = 0 THEN
    EXECUTE IMMEDIATE 'CREATE USER ' || v_user || ' IDENTIFIED BY "' || '&&app_password' || '" DEFAULT TABLESPACE &&app_tbs QUOTA UNLIMITED ON &&app_tbs';
    DBMS_OUTPUT.PUT_LINE(v_user || ' olusturuldu.');
  ELSE
    DBMS_OUTPUT.PUT_LINE(v_user || ' zaten var; yalnizca yetkiler kontrol ediliyor.');
  END IF;
  -- Uygulamanin ihtiyac duydugu en az yetki: tablo, sequence, view, PL/SQL paket ve tetikleyici.
  EXECUTE IMMEDIATE 'GRANT CREATE SESSION, CREATE TABLE, CREATE SEQUENCE, CREATE VIEW, CREATE PROCEDURE, CREATE TRIGGER, CREATE TYPE TO ' || v_user;
END;
/

UNDEFINE app_user
UNDEFINE app_password
UNDEFINE app_tbs
