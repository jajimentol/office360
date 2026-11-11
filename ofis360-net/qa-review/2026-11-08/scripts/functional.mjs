// Fonksiyonel dogruluk, is kurallari, eszamanlilik ve veri butunlugu testleri.
// Izole perf ornegi (5211) + OFIS360_PERF semasi. Her kosu kendi sentetik kullanicilarini admin API'si ile olusturur.
// Calistirma: node functional.mjs
import { Client, loginAs, demoAccounts, Recorder, istanbulDayKey, sql, BASE } from './lib.mjs';

const R = new Recorder('functional');
const accounts = await demoAccounts();
const adminId = accounts.find((a) => a.role === 'ADMIN').id;
const managerId = accounts.find((a) => a.role === 'MANAGER').id;
const adm = await loginAs(adminId);
const deptId = accounts[0].departmentId;
const tag = Date.now().toString(36);
let seq = 0;

async function newUser(role = 'EMPLOYEE') {
  const n = ++seq;
  const r = await adm.post('/admin/users', { fullName: `Fonksiyonel Test ${tag}-${n}`, email: `fn.${tag}.${n}@ofis360.local`, title: 'Uzman', role, departmentId: deptId });
  if (r.status !== 200) throw new Error(`kullanıcı oluşturulamadı ${r.status} ${r.text}`);
  return { id: r.json.id, c: await loginAs(r.json.id) };
}
async function freeDesks(client, day) {
  const fl = await client.get(`/floors/summaries?date=${day}`);
  const out = [];
  for (const f of fl.json.filter((x) => x.reservable)) {
    const plan = await client.get(`/floors/${f.id}/plan?date=${day}`);
    out.push(...plan.json.desks.filter((x) => x.selectable && !x.popular && !x.accessibleOnly));
  }
  return out;
}
const count = (rs) => rs.reduce((a, r) => ((a[r.status] = (a[r.status] ?? 0) + 1), a), {});
// Gelecek haftanin is gunleri (Istanbul). Bugun 2026-11-08 Pazar ise 2026-11-09..13.
function nextWeekdays(n, startOffset = 1) {
  const out = [];
  for (let o = startOffset; out.length < n && o < 30; o++) {
    const k = istanbulDayKey(o);
    const wd = new Date(`${k}T12:00:00Z`).getUTCDay();
    if (wd !== 0 && wd !== 6) out.push(k);
  }
  return out;
}
const week = nextWeekdays(5);

// ---------- Is kurallari ----------
{
  const u = await newUser();
  const pool = await freeDesks(u.c, week[0]);
  const desk = pool[0];
  const past = await u.c.post('/desk-reservations', { deskId: desk.id, date: istanbulDayKey(-1) });
  const far = await u.c.post('/desk-reservations', { deskId: desk.id, date: istanbulDayKey(40) });
  R.check('FN-RULE-01', { area: 'Masa', role: 'Çalışan', type: 'Fonksiyonel/API', scenario: 'Geçmiş tarih ve 14 günden ileri tarih reddedilir', expected: '4xx, kural ihlali kodu' },
    past.status >= 400 && past.status < 500 && far.status >= 400 && far.status < 500, `geçmiş=${past.status} ${past.json?.error?.violations?.[0]?.code ?? past.json?.error?.code}; +40 gün=${far.status} ${far.json?.error?.violations?.[0]?.code ?? far.json?.error?.code}`);
  const ok = await u.c.post('/desk-reservations', { deskId: desk.id, date: week[0], note: 'Türkçe karakter: İıŞşĞğÜüÖöÇç 😀' });
  const mine = await u.c.get('/me/reservations');
  const saved = JSON.stringify(mine.json).includes('İıŞşĞğÜüÖöÇç 😀');
  const notif = await u.c.get('/me/notifications');
  R.check('FN-RULE-02', { area: 'Masa', role: 'Çalışan', type: 'Fonksiyonel/API', scenario: 'Rezervasyon oluşur, Türkçe karakter/emoji not korunur, uygulama içi bildirim düşer', expected: '200, not aynen, bildirim ≥1' },
    ok.status === 200 && saved && notif.json.items.length >= 1, `oluşturma=${ok.status}, not korundu=${saved}, bildirim=${notif.json.items.length}, kanallar=${notif.json.channels.map((c) => `${c.key}:${c.enabled}`).join(',')}`);
  const second = await u.c.post('/desk-reservations', { deskId: pool[1].id, date: week[0] });
  R.check('FN-RULE-03', { area: 'Masa', role: 'Çalışan', type: 'Fonksiyonel/API', scenario: 'Aynı gün ikinci masa reddedilir', expected: '4xx ONE_DESK_PER_DAY' }, second.status >= 400 && second.status < 500, `HTTP ${second.status} ${JSON.stringify(second.json?.error?.violations?.map((v) => v.code) ?? second.json?.error?.code)}`);
  const cancel1 = await u.c.post(`/desk-reservations/${ok.json.id}/cancel`, { reason: 'test' });
  const cancel2 = await u.c.post(`/desk-reservations/${ok.json.id}/cancel`, { reason: 'test' });
  R.check('FN-RULE-04', { area: 'Masa', role: 'Çalışan', type: 'Fonksiyonel/API', scenario: 'İptal; aynı iptalin tekrar gönderilmesi (yenile/geri tuşu) tutarlı yanıt verir', expected: '200 sonra 200/409 (500 değil), durum CANCELLED' },
    cancel1.status === 200 && cancel2.status < 500, `ilk=${cancel1.status}, tekrar=${cancel2.status} ${cancel2.json?.error?.code ?? ''}`);
  const hist = sql(`SELECT OLD_STATUS, NEW_STATUS, NVL(ACTOR_ID,'-') FROM OFIS360_PERF.RESERVATION_STATUS_HISTORY WHERE RESERVATION_ID='${ok.json.id}' ORDER BY ID;`);
  R.check('FN-HIST-01', { area: 'Durum geçmişi', role: 'Çalışan', type: 'Veri bütünlüğü/DB', scenario: 'Oluşturma ve iptal RESERVATION_STATUS_HISTORY\'ye aktörle yazılır (canlı API)', expected: '(-→BOOKED, u) (BOOKED→CANCELLED, u)' },
    hist.length === 2 && hist[0][1] === 'BOOKED' && hist[1][1] === 'CANCELLED' && hist.every((h) => h[2] === u.id), JSON.stringify(hist));
}

// ---------- Eszamanlilik ----------
{
  const users = await Promise.all(Array.from({ length: 20 }, () => newUser()));
  const desk = (await freeDesks(users[0].c, week[1]))[0];
  const rs = await Promise.all(users.map((u) => u.c.post('/desk-reservations', { deskId: desk.id, date: week[1] })));
  const db = sql(`SELECT COUNT(*) FROM OFIS360_PERF.DESK_RESERVATION r WHERE r.DESK_ID='${desk.id}' AND r.RESERVATION_DATE=DATE '${week[1]}' AND r.STATUS IN ('BOOKED','CHECKED_IN');`)[0][0];
  const c = count(rs);
  R.check('FN-CONC-01', { area: 'Masa', role: 'Çalışan ×20', type: 'Eşzamanlılık', scenario: '20 farklı kullanıcı aynı masa/gün için aynı anda istek atar', expected: 'Tam 1 başarı, diğerleri 409/4xx, DB\'de 1 aktif kayıt, 500 yok' },
    c[200] === 1 && db === '1' && !c[500], `yanıtlar ${JSON.stringify(c)}, DB aktif=${db}`);
}
{
  const u = await newUser();
  const desks = (await freeDesks(u.c, week[2])).slice(0, 10);
  const rs = await Promise.all(desks.map((d) => u.c.post('/desk-reservations', { deskId: d.id, date: week[2] })));
  const db = sql(`SELECT COUNT(*) FROM OFIS360_PERF.DESK_RESERVATION WHERE USER_ID='${u.id}' AND RESERVATION_DATE=DATE '${week[2]}' AND STATUS IN ('BOOKED','CHECKED_IN');`)[0][0];
  const c = count(rs);
  R.check('FN-CONC-02', { area: 'Masa', role: 'Çalışan', type: 'Eşzamanlılık', scenario: 'Aynı kullanıcı aynı gün için 10 farklı masaya eşzamanlı istek (çift tıklama/çoklu sekme)', expected: 'Tam 1 başarı, DB\'de 1, 500 yok' },
    c[200] === 1 && db === '1' && !c[500], `yanıtlar ${JSON.stringify(c)}, DB aktif=${db}`);
}
{
  const u = await newUser();
  const all = await freeDesks(u.c, week[0]);
  const rs = await Promise.all(week.map((d, i) => u.c.post('/desk-reservations', { deskId: all[i + 5].id, date: d })));
  const c = count(rs);
  const db = sql(`SELECT COUNT(*) FROM OFIS360_PERF.DESK_RESERVATION WHERE USER_ID='${u.id}' AND STATUS IN ('BOOKED','CHECKED_IN');`)[0][0];
  R.check('FN-CONC-03', { area: 'Masa', role: 'Çalışan', type: 'Eşzamanlılık', scenario: 'Haftalık limit (3 gün) — aynı haftanın 5 gününe eşzamanlı 5 istek', expected: 'En fazla 3 başarı' },
    Number(db) <= 3, `yanıtlar ${JSON.stringify(c)}, DB aktif=${db} (hafta: ${week[0]}..${week[4]})`);
}
{
  const users = await Promise.all(Array.from({ length: 10 }, () => newUser()));
  const av = await users[0].c.get(`/rooms/availability?date=${week[3]}&start=600&end=660&minCapacity=2`);
  const room = av.json.rooms.find((x) => x.availableForRequest && !x.closed);
  const rs = await Promise.all(users.map((u, i) => u.c.post('/room-reservations', { roomId: room.id, date: week[3], startMinutes: 600 + (i % 2) * 30, endMinutes: 660 + (i % 2) * 30, title: 'Yarış', purpose: 'eşzamanlılık', attendeeCount: 2 })));
  const db = sql(`SELECT COUNT(*) FROM OFIS360_PERF.ROOM_RESERVATION WHERE ROOM_ID='${room.id}' AND RESERVATION_DATE=DATE '${week[3]}' AND STATUS='BOOKED' AND START_MINUTES < 690 AND END_MINUTES > 600;`)[0][0];
  const c = count(rs);
  R.check('FN-CONC-04', { area: 'Oda', role: 'Çalışan ×10', type: 'Eşzamanlılık', scenario: '10 kullanıcı aynı odanın çakışan saatlerine (10:00-11:00 / 10:30-11:30) aynı anda istek', expected: 'Tam 1 başarı, DB\'de çakışma yok' },
    c[200] === 1 && db === '1' && !c[500], `yanıtlar ${JSON.stringify(c)}, DB çakışan aktif=${db}`);
}
{
  // Check-in ile admin iptali yarisi: bugune ait rezervasyon dogrudan DB'ye (bugun Pazar olabilir; kural motoru disi) eklenir
  const today = istanbulDayKey(0);
  let raced = 0; let bad = 0; const samples = [];
  for (let i = 0; i < 15; i++) {
    const u = await newUser();
    const desk = (await freeDesks(u.c, week[4]))[i];
    const id = `race${tag}${i}`.slice(0, 32);
    sql(`INSERT INTO OFIS360_PERF.DESK_RESERVATION (ID, DESK_ID, USER_ID, RESERVATION_DATE, STATUS, SOURCE, ACTIVE_DESK_KEY, ACTIVE_USER_DAY_KEY, CREATED_AT, UPDATED_AT)
         VALUES ('${id}', '${desk.id}', '${u.id}', DATE '${today}', 'BOOKED', 'EMPLOYEE', '${desk.id}#${today}', '${u.id}#${today}', SYSTIMESTAMP, SYSTIMESTAMP);
         COMMIT;`);
    const [ci, ca] = await Promise.all([u.c.post(`/desk-reservations/${id}/check-in`), adm.post(`/desk-reservations/${id}/cancel`, { reason: 'yarış' })]);
    const row = sql(`SELECT STATUS, NVL(ACTIVE_DESK_KEY,'NULL') FROM OFIS360_PERF.DESK_RESERVATION WHERE ID='${id}';`)[0];
    if (ci.status === 200 && ca.status === 200) raced++;
    if (row[0] === 'CHECKED_IN' && row[1] === 'NULL') { bad++; samples.push(`${ci.status}/${ca.status}->${row.join(',')}`); }
  }
  R.check('FN-CONC-05', { area: 'Masa', role: 'Çalışan + Admin', type: 'Eşzamanlılık', scenario: 'Aynı rezervasyona eşzamanlı check-in ve admin iptali (15 deneme)', expected: 'Son durum tutarlı: CANCELLED veya CHECKED_IN+aktif anahtar; ikisi birden 200 olmaz' },
    bad === 0, `ikisi de 200: ${raced}/15; CHECKED_IN ama aktif anahtar NULL: ${bad}/15 ${samples.slice(0, 3).join(' ')}`);
}

// ---------- Admin akislari ve veri butunlugu ----------
{
  const u = await newUser();
  const desk = (await freeDesks(u.c, week[1]))[3];
  await u.c.post('/desk-reservations', { deskId: desk.id, date: week[1] });
  const note = 'Bakım: ' + 'ş'.repeat(225);
  const r = await adm.post('/admin/closures', { scope: 'DESK', targetId: desk.id, startDayKey: week[1], endDayKey: week[1], reason: 'MAINTENANCE', note });
  const after = sql(`SELECT STATUS FROM OFIS360_PERF.DESK_RESERVATION WHERE USER_ID='${u.id}' AND RESERVATION_DATE=DATE '${week[1]}';`)[0]?.[0];
  R.check('FN-ADM-01', { area: 'Kapatma', role: 'Admin', type: 'Fonksiyonel/API', scenario: 'Etkilenen rezervasyonu olan masaya 232 karakterlik notla kapatma (validator 240\'a izin verir)', expected: '200, rezervasyon iptal' },
    r.status === 200, `HTTP ${r.status} ${r.json?.error?.code ?? ''}; rezervasyon durumu=${after}`);
  if (r.status === 200) await adm.put(`/admin/closures/${r.json.id}`, { active: false });
}
{
  const u = await newUser();
  const desk = (await freeDesks(u.c, week[2]))[4];
  const cl = await adm.post('/admin/closures', { scope: 'DESK', targetId: desk.id, startDayKey: week[0], endDayKey: week[0], reason: 'MAINTENANCE', note: 'kısa kapatma' });
  const res = await u.c.post('/desk-reservations', { deskId: desk.id, date: week[2] });
  const ext = await adm.put(`/admin/closures/${cl.json.id}`, { endDayKey: week[2] });
  const st = sql(`SELECT STATUS FROM OFIS360_PERF.DESK_RESERVATION WHERE ID='${res.json.id}';`)[0][0];
  R.check('FN-ADM-02', { area: 'Kapatma', role: 'Admin', type: 'Veri bütünlüğü', scenario: 'Kapatma bitiş tarihi uzatılınca yeni kapsanan rezervasyon iptal edilmeli', expected: 'CANCELLED' },
    st === 'CANCELLED', `uzatma=${ext.status}, rezervasyon=${res.status}/${st}`);
  const bad = await adm.put(`/admin/closures/${cl.json.id}`, { endDayKey: istanbulDayKey(-30) });
  R.check('FN-ADM-03', { area: 'Kapatma', role: 'Admin', type: 'Doğrulama', scenario: 'Kapatma bitişi başlangıçtan önceye çekilir', expected: '400' }, bad.status === 400, `HTTP ${bad.status} ${bad.json?.error?.code ?? ''}`);
  await adm.put(`/admin/closures/${cl.json.id}`, { active: false });
}
{
  const u = await newUser();
  const desk = (await freeDesks(u.c, week[3]))[5];
  const res = await u.c.post('/desk-reservations', { deskId: desk.id, date: week[3] });
  await adm.put(`/admin/users/${u.id}`, { active: false });
  const st = sql(`SELECT STATUS FROM OFIS360_PERF.DESK_RESERVATION WHERE ID='${res.json.id}';`)[0][0];
  const other = await newUser();
  const plan = await other.c.get(`/floors/${desk.floorId}/plan?date=${week[3]}`);
  const shown = plan.json.desks.find((d) => d.id === desk.id);
  R.check('FN-ADM-04', { area: 'Kullanıcı', role: 'Admin', type: 'Veri bütünlüğü', scenario: 'Pasifleştirilen çalışanın gelecekteki masa rezervasyonu serbest bırakılmalı', expected: 'CANCELLED; masa diğerlerine açık' },
    st === 'CANCELLED' && shown?.selectable, `rezervasyon=${st}, masa diğerine durum=${shown?.state}`);
}
{
  const floors = await adm.get('/admin/floors');
  const list = floors.json.items ?? floors.json;
  const f = list.find((x) => x.buildingCode === 'DEMO') ?? list[list.length - 1];
  const before = await adm.get(`/admin/floors/${f.id}`);
  const orig = before.json.floor ?? before.json;
  const r1 = await adm.put(`/admin/floors/${f.id}`, { planWidth: -5 });
  const r2 = await adm.put(`/admin/floors/${f.id}`, { name: '' });
  await adm.put(`/admin/floors/${f.id}`, { planWidth: orig.planWidth, name: orig.name });
  const zone = (orig.zones ?? before.json.zones ?? [])[0];
  const z = zone ? await adm.put(`/admin/zones/${zone.id}`, { kind: 'HACK' }) : null;
  R.check('FN-ADM-05', { area: 'Kat', role: 'Admin', type: 'Doğrulama', scenario: 'Kat güncellemesinde planWidth=-5 / boş ad; bölgede geçersiz tür', expected: 'Hepsi 400' },
    r1.status === 400 && r2.status === 400 && (!z || z.status === 400), `planWidth=-5 → ${r1.status}; ad='' → ${r2.status}; zone.kind=HACK → ${z?.status ?? 'bölge yok'} (değerler geri yüklendi)`);
}
{
  const rules = await adm.get('/admin/rules');
  const list = rules.json.items ?? rules.json.rules ?? rules.json;
  const wh = list.find((x) => x.key === 'ROOM_WORKING_HOURS');
  const r = await adm.put('/admin/rules/ROOM_WORKING_HOURS', { intValue: 1200 });
  const after = (await adm.get('/admin/rules')).json;
  const wh2 = (after.items ?? after.rules ?? after).find((x) => x.key === 'ROOM_WORKING_HOURS');
  await adm.put('/admin/rules/ROOM_WORKING_HOURS', { intValue: wh.intValue, secondValue: wh.secondValue });
  R.check('FN-ADM-06', { area: 'Kural', role: 'Admin', type: 'Doğrulama', scenario: 'Çalışma saati başlangıcı tek başına 20:00 yapılır (bitiş 18:00 iken)', expected: '400 (başlangıç < bitiş)' },
    r.status === 400, `HTTP ${r.status}; kayıt=${wh2?.intValue}-${wh2?.secondValue} (önce ${wh.intValue}-${wh.secondValue}, geri yüklendi)`);
}
{
  const av = await adm.get(`/rooms/availability?date=${week[4]}&start=900&end=930&minCapacity=2`);
  const room = av.json.rooms.find((x) => x.availableForRequest && !x.closed);
  const r = await adm.post('/room-reservations', { roomId: room.id, date: week[4], startMinutes: 900, endMinutes: 930, title: 'Hayalet', purpose: 'yok', attendeeCount: 2, targetUserId: 'boyle-bir-kullanici-yok' });
  R.check('FN-ADM-07', { area: 'Oda', role: 'Admin', type: 'Doğrulama', scenario: 'Admin var olmayan kullanıcı adına oda rezervasyonu', expected: '404' }, r.status === 404, `HTTP ${r.status} ${r.json?.error?.code ?? ''}`);
  const em = `Dup.${tag}@Ofis360.Local`;
  const a = await adm.post('/admin/users', { fullName: 'Tekil A', email: em, title: 'Uzman', role: 'EMPLOYEE', departmentId: deptId });
  const b = await adm.post('/admin/users', { fullName: 'Tekil B', email: em.toLowerCase(), title: 'Uzman', role: 'EMPLOYEE', departmentId: deptId });
  const c = await adm.post('/admin/users', { fullName: 'Tekil C', email: em, title: 'Uzman', role: 'EMPLOYEE', departmentId: deptId });
  R.check('FN-ADM-08', { area: 'Kullanıcı', role: 'Admin', type: 'Doğrulama', scenario: 'Aynı e-posta (büyük/küçük harf farkı ve birebir) ile ikinci kullanıcı', expected: '409/400 (500 değil), harf duyarsız tekil' },
    a.status === 200 && b.status >= 400 && b.status < 500 && c.status >= 400 && c.status < 500, `ilk=${a.status} ${a.json?.error?.message ?? ''}, küçük harf=${b.status}, birebir=${c.status} ${c.json?.error?.code ?? ''}`);
  for (const x of [a, b]) if (x.json?.id) await adm.put(`/admin/users/${x.json.id}`, { active: false });
}

// ---------- Raporlama ve saat dilimi ----------
{
  const home = await (await newUser()).c.get('/panel/home');
  const utcDay = new Date().toISOString().slice(0, 10);
  R.add({ id: 'FN-TZ-01', area: 'Saat dilimi', role: 'Çalışan', type: 'Fonksiyonel/API', scenario: '"Bugün" Europe/Istanbul\'a göre hesaplanır', expected: istanbulDayKey(0),
    actual: `today=${home.json.today} (UTC tarihi ${utcDay})`, status: home.json.today === istanbulDayKey(0) ? 'Geçti' : 'Kaldı' });
}
{
  // Saatlik giris grafigi: 09:15 Istanbul (06:15 UTC) check-in "09:00" kovasina dusmeli
  const day = istanbulDayKey(-3);
  const row = sql(`SELECT ID FROM OFIS360_PERF.DESK_RESERVATION WHERE RESERVATION_DATE=DATE '${day}' AND STATUS IN ('CHECKED_IN','COMPLETED') AND ROWNUM=1;`)[0];
  const mgr = await loginAs(managerId);
  const q = `/analytics?from=${day}&to=${day}`;
  const bucket = (j, h) => (j.hourlyDensity ?? j.analytics?.hourlyDensity ?? []).find((x) => x.hour === h)?.arrivals ?? 0;
  if (!row) {
    R.add({ id: 'FN-REP-01', area: 'Analitik', role: 'Yönetici', type: 'Fonksiyonel/API', scenario: 'Saatlik giriş grafiği yerel saate göre', expected: '-', actual: `${day} için check-in kaydı yok`, status: 'Engellendi' });
  } else {
    const id = row[0];
    const orig = sql(`SELECT TO_CHAR(CHECKED_IN_AT, 'YYYY-MM-DD"T"HH24:MI:SS.FF3TZH:TZM') FROM OFIS360_PERF.DESK_RESERVATION WHERE ID='${id}';`)[0]?.[0];
    sql(`UPDATE OFIS360_PERF.DESK_RESERVATION SET CHECKED_IN_AT = NULL WHERE ID='${id}';
COMMIT;`);
    const b0 = (await mgr.get(q)).json;
    sql(`UPDATE OFIS360_PERF.DESK_RESERVATION SET CHECKED_IN_AT = FROM_TZ(TIMESTAMP '${day} 09:15:00', 'Europe/Istanbul') WHERE ID='${id}';
COMMIT;`);
    const b1 = (await mgr.get(q)).json;
    sql(`UPDATE OFIS360_PERF.DESK_RESERVATION SET CHECKED_IN_AT = ${orig ? `TO_TIMESTAMP_TZ('${orig}', 'YYYY-MM-DD"T"HH24:MI:SS.FF3TZH:TZM')` : 'NULL'} WHERE ID='${id}';
COMMIT;`);
    const d9 = bucket(b1, '09:00') - bucket(b0, '09:00');
    R.check('FN-REP-01', { area: 'Analitik', role: 'Yönetici', type: 'Fonksiyonel/API', scenario: 'İstanbul 09:15\'te yapılan check-in saatlik grafikte 09:00 kovasına düşer', expected: '09:00 kovası +1' },
      d9 === 1, `09:00 kovası farkı=${d9}; 08:00 farkı=${bucket(b1, '08:00') - bucket(b0, '08:00')} (kayıt geri yüklendi)`);
  }
}
{
  const mgr = await loginAs(managerId);
  const huge = await mgr.get(`/analytics?from=2000-01-01&to=2030-12-31`);
  // fetch().text() BOM'u yutar; ham baytlara bakilir
  const t0 = performance.now();
  const raw = await fetch(`${BASE}/analytics/export?type=adil-kullanim&from=${istanbulDayKey(-60)}&to=${istanbulDayKey(0)}`, { headers: { cookie: mgr.cookie } });
  const bytes = new Uint8Array(await raw.arrayBuffer());
  const exp = { status: raw.status, headers: raw.headers, ms: performance.now() - t0, bom: bytes[0] === 0xef && bytes[1] === 0xbb && bytes[2] === 0xbf };
  R.check('FN-REP-02', { area: 'Analitik', role: 'Yönetici', type: 'Fonksiyonel/API', scenario: '30 yıllık aralık reddedilir; CSV UTF-8 BOM + ; ayraçlı iner', expected: '400; CSV BOM, text/csv' },
    huge.status === 400 && exp.bom && /text\/csv/.test(exp.headers.get('content-type')), `aralık=${huge.status}; csv=${exp.status} ${exp.headers.get('content-type')} BOM=${exp.bom} ${exp.ms.toFixed(0)}ms`);
  const audit = await adm.get('/admin/audit?take=5&action=REPORT_EXPORT');
  R.check('FN-REP-03', { area: 'Denetim', role: 'Yönetici', type: 'Fonksiyonel/API', scenario: 'Rapor dışa aktarımı denetime yazılır', expected: 'REPORT_EXPORT kaydı' }, (audit.json.items ?? []).length > 0, `REPORT_EXPORT kayıt=${audit.json.total ?? audit.json.items?.length}`);
}

R.save();
