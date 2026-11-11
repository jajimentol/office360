// Yuk sonrasi veri butunlugu denetimi (OFIS360_PERF). Her sorgu 0 satir/0 sayi dondurmeli.
import { sql, Recorder } from './lib.mjs';
const R = new Recorder('integrity');
const S = 'OFIS360_PERF';
const checks = [
  ['INT-01', 'Aynı masa/gün için birden fazla aktif rezervasyon', `SELECT COUNT(*) FROM (SELECT DESK_ID, RESERVATION_DATE FROM ${S}.DESK_RESERVATION WHERE STATUS IN ('BOOKED','CHECKED_IN') GROUP BY DESK_ID, RESERVATION_DATE HAVING COUNT(*) > 1);`],
  ['INT-02', 'Aynı kullanıcı/gün için birden fazla aktif masa', `SELECT COUNT(*) FROM (SELECT USER_ID, RESERVATION_DATE FROM ${S}.DESK_RESERVATION WHERE STATUS IN ('BOOKED','CHECKED_IN') GROUP BY USER_ID, RESERVATION_DATE HAVING COUNT(*) > 1);`],
  ['INT-03', 'Çakışan aktif oda rezervasyonu', `SELECT COUNT(*) FROM ${S}.ROOM_RESERVATION a JOIN ${S}.ROOM_RESERVATION b ON a.ROOM_ID = b.ROOM_ID AND a.RESERVATION_DATE = b.RESERVATION_DATE AND a.ID < b.ID AND a.START_MINUTES < b.END_MINUTES AND b.START_MINUTES < a.END_MINUTES WHERE a.STATUS = 'BOOKED' AND b.STATUS = 'BOOKED';`],
  ['INT-04', 'Haftalık 3 gün limitini aşan kullanıcı-hafta (gelecek, uygulama üzerinden oluşturulan)', `SELECT COUNT(*) FROM (SELECT USER_ID, TRUNC(RESERVATION_DATE, 'IW') FROM ${S}.DESK_RESERVATION WHERE STATUS IN ('BOOKED','CHECKED_IN') AND RESERVATION_DATE > TRUNC(SYSDATE) AND SOURCE = 'EMPLOYEE' GROUP BY USER_ID, TRUNC(RESERVATION_DATE, 'IW') HAVING COUNT(*) > 3);`],
  ['INT-05', 'Aktif durumda olup aktif anahtarı boş masa rezervasyonu (benzersiz index korumasız)', `SELECT COUNT(*) FROM ${S}.DESK_RESERVATION WHERE STATUS IN ('BOOKED','CHECKED_IN') AND (ACTIVE_DESK_KEY IS NULL OR ACTIVE_USER_DAY_KEY IS NULL);`],
  ['INT-06', 'Durum geçmişinde karşılığı olmayan rezervasyon (trigger sonrası oluşturulan)', `SELECT COUNT(*) FROM ${S}.DESK_RESERVATION r WHERE r.CREATED_AT > SYSTIMESTAMP - INTERVAL '1' DAY AND NOT EXISTS (SELECT 1 FROM ${S}.RESERVATION_STATUS_HISTORY h WHERE h.RESERVATION_TYPE='DESK' AND h.RESERVATION_ID = r.ID);`],
  ['INT-07', 'Son durumu geçmişin son satırıyla uyuşmayan rezervasyon', `SELECT COUNT(*) FROM ${S}.DESK_RESERVATION r WHERE r.CREATED_AT > SYSTIMESTAMP - INTERVAL '1' DAY AND r.STATUS <> (SELECT MAX(h.NEW_STATUS) KEEP (DENSE_RANK LAST ORDER BY h.ID) FROM ${S}.RESERVATION_STATUS_HISTORY h WHERE h.RESERVATION_TYPE='DESK' AND h.RESERVATION_ID = r.ID);`],
  ['INT-08', 'İptal edilmiş ama iptal zamanı boş rezervasyon', `SELECT COUNT(*) FROM ${S}.DESK_RESERVATION WHERE STATUS = 'CANCELLED' AND CANCELLED_AT IS NULL AND CREATED_AT > SYSTIMESTAMP - INTERVAL '1' DAY;`],
];
for (const [id, name, q] of checks) {
  const n = Number(sql(q)[0][0]);
  R.check(id, { area: 'Veri bütünlüğü', role: '-', type: 'Veri bütünlüğü/DB', scenario: `${name} (yük testleri sonrası)`, expected: '0' }, n === 0, String(n), q.replace(/\s+/g, ' ').slice(0, 220));
}
const vol = sql(`SELECT (SELECT COUNT(*) FROM ${S}.DESK_RESERVATION), (SELECT COUNT(*) FROM ${S}.ROOM_RESERVATION), (SELECT COUNT(*) FROM ${S}.RESERVATION_STATUS_HISTORY), (SELECT COUNT(*) FROM ${S}.AUDIT_LOG), (SELECT COUNT(*) FROM ${S}.NOTIFICATION), (SELECT COUNT(*) FROM ${S}.APP_USER) FROM DUAL;`)[0];
console.log(`hacim: masa_rez=${vol[0]} oda_rez=${vol[1]} durum_gecmisi=${vol[2]} denetim=${vol[3]} bildirim=${vol[4]} kullanici=${vol[5]}`);
R.save();
