#!/usr/bin/env bash
# Yedekleme/geri yukleme provasi (izole): OFIS360_PERF semasi Data Pump ile disa aktarilir, OFIS360_RESTORE olarak
# geri yuklenir, tablo satir sayilari karsilastirilir, trigger/paket durumu kontrol edilir, prova semasi silinir.
set -euo pipefail
C=ofis360-oracle
TS=$(date +%Y%m%d%H%M%S)
SQLP() { docker exec -i $C bash -c 'sqlplus -s / as sysdba'; }
echo "== expdp OFIS360_PERF"
T0=$(date +%s)
docker exec $C bash -c "expdp \"system/\$ORACLE_PWD@localhost:1521/FREEPDB1\" SCHEMAS=OFIS360_PERF DIRECTORY=DATA_PUMP_DIR DUMPFILE=qa_perf_$TS.dmp LOGFILE=qa_perf_exp_$TS.log" 2>&1 | grep -E "exported|successfully|ORA-|Dump file" | head -40
T1=$(date +%s)
echo "== impdp -> OFIS360_RESTORE"
docker exec $C bash -c "impdp \"system/\$ORACLE_PWD@localhost:1521/FREEPDB1\" DIRECTORY=DATA_PUMP_DIR DUMPFILE=qa_perf_$TS.dmp REMAP_SCHEMA=OFIS360_PERF:OFIS360_RESTORE LOGFILE=qa_perf_imp_$TS.log" 2>&1 | grep -E "imported|successfully|completed|ORA-" | head -40
T2=$(date +%s)
echo "== karsilastirma"
SQLP <<SQL
SET PAGES 200 LINES 200 FEEDBACK OFF HEADING OFF
ALTER SESSION SET CONTAINER = FREEPDB1;
SET SERVEROUTPUT ON
DECLARE a NUMBER; b NUMBER; diff NUMBER := 0;
BEGIN
  FOR t IN (SELECT table_name FROM dba_tables WHERE owner = 'OFIS360_PERF' ORDER BY table_name) LOOP
    EXECUTE IMMEDIATE 'SELECT COUNT(*) FROM OFIS360_PERF."' || t.table_name || '"' INTO a;
    EXECUTE IMMEDIATE 'SELECT COUNT(*) FROM OFIS360_RESTORE."' || t.table_name || '"' INTO b;
    IF a <> b THEN diff := diff + 1; END IF;
    DBMS_OUTPUT.PUT_LINE(RPAD(t.table_name, 32) || LPAD(a, 8) || LPAD(b, 8) || CASE WHEN a = b THEN '  ok' ELSE '  FARK' END);
  END LOOP;
  DBMS_OUTPUT.PUT_LINE('TABLO_FARKI=' || diff);
END;
/
SELECT 'GECERSIZ_NESNE=' || COUNT(*) FROM dba_objects WHERE owner = 'OFIS360_RESTORE' AND status <> 'VALID';
SELECT 'TRIGGER=' || LISTAGG(trigger_name || ':' || status, ',') WITHIN GROUP (ORDER BY trigger_name) FROM dba_triggers WHERE owner = 'OFIS360_RESTORE';
SELECT 'MIGRATION=' || LISTAGG("MigrationId", ',') WITHIN GROUP (ORDER BY "MigrationId") FROM OFIS360_RESTORE."__EFMigrationsHistory";
DROP USER OFIS360_RESTORE CASCADE;
SELECT 'PROVA_SEMASI_SILINDI=' || CASE WHEN COUNT(*) = 0 THEN 'evet' ELSE 'hayir' END FROM dba_users WHERE username = 'OFIS360_RESTORE';
EXIT
SQL
docker exec $C bash -c "rm -f /opt/oracle/admin/FREE/dpdump/*/qa_perf_$TS.dmp /opt/oracle/admin/FREE/dpdump/qa_perf_$TS.dmp" || true
echo "SURE: disa aktarim $((T1-T0))s, geri yukleme $((T2-T1))s"
