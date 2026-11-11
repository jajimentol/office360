-- Ofis360 sema kullanicilari (FREEPDB1 icinde). Her acilista calisir; idempotent.
ALTER SESSION SET CONTAINER = FREEPDB1;

DECLARE
  PROCEDURE ensure_user(p_name VARCHAR2, p_pwd VARCHAR2) IS
    v_count NUMBER;
  BEGIN
    SELECT COUNT(*) INTO v_count FROM dba_users WHERE username = p_name;
    IF v_count = 0 THEN
      EXECUTE IMMEDIATE 'CREATE USER ' || p_name || ' IDENTIFIED BY "' || p_pwd || '" DEFAULT TABLESPACE USERS QUOTA UNLIMITED ON USERS';
      EXECUTE IMMEDIATE 'GRANT CONNECT, RESOURCE, CREATE VIEW, CREATE PROCEDURE, CREATE TRIGGER, CREATE SEQUENCE, CREATE TYPE TO ' || p_name;
    END IF;
  END;
BEGIN
  ensure_user('OFIS360',      'Ofis360Dev_2026');
  ensure_user('OFIS360_TEST', 'Ofis360Test_2026');
END;
/
