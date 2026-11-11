// Guvenlik testleri (OWASP ASVS 4.0.3 L1/L2 hedefli, WSTG v4.2 rehberli).
// Izole perf ornegine (varsayilan http://localhost:5211/api, ASPNETCORE_ENVIRONMENT=Production) karsi calisir.
// Calistirma: node security.mjs   (OFIS360_BASE ile hedef degistirilebilir). Sentetik veri olusturur (perfu* kullanicilari).
import { Client, loginAs, demoAccounts, Recorder, istanbulDayKey, BASE } from './lib.mjs';
import http from 'node:http';
const rawStatus = (method, path) => new Promise((res) => { const u = new URL(BASE + path); const q = http.request({ method, hostname: u.hostname, port: u.port, path: u.pathname }, (r) => { r.resume(); res(r.statusCode); }); q.on('error', () => res(0)); q.end(); });

const R = new Recorder('security');
// Tekrar calistirilabilirlik: her kosu farkli sentetik kullanicilar kullanir (gunde bir masa / haftada uc gun kurallari).
const RUN = Number(process.env.SEC_RUN ?? Math.floor(Math.random() * 18));
const U = (k) => `perfu${String(10 + RUN * 10 + k).padStart(4, '0')}`;
const accounts = await demoAccounts();
const adminId = accounts.find((a) => a.role === 'ADMIN').id;
const managerId = accounts.find((a) => a.role === 'MANAGER').id;
const anon = new Client();
const okAny = (s, ...codes) => codes.includes(s);

// ---------- Kimlik dogrulama / oturum (ASVS V2, V3; WSTG-ATHN, WSTG-SESS) ----------
{
  const r = await anon.get('/auth/demo-accounts');
  R.check('SEC-AUTH-01', { area: 'Kimlik', role: 'Anonim', type: 'Güvenlik/API', scenario: 'Production ortamında oturumsuz kullanıcı, hesap listesini (yönetici ID dahil) alamamalı', expected: '401/404' },
    r.status !== 200, `HTTP ${r.status}, ${Array.isArray(r.json) ? r.json.length : 0} hesap döndü, ADMIN kimliği yanıtta: ${r.text.includes(adminId)}`, 'GET /api/auth/demo-accounts');
}
{
  const c = new Client();
  const r = await c.post('/auth/login', { userId: adminId });
  R.check('SEC-AUTH-02', { area: 'Kimlik', role: 'Anonim', type: 'Güvenlik/API', scenario: 'Parola/SSO olmadan yalnızca kullanıcı ID ile yönetici oturumu açılamamalı', expected: '401' },
    r.status !== 200, `HTTP ${r.status} rol=${r.json?.role}`, 'POST /api/auth/login {userId:<admin>}');
}
{
  const forged = new Client(`ofis360_session=${adminId}`);
  const r = await forged.get('/admin/users?take=1');
  R.check('SEC-AUTH-03', { area: 'Oturum', role: 'Anonim', type: 'Güvenlik/API', scenario: 'Elle üretilmiş çerez (değer = kullanıcı ID, imzasız) ile yönetici uçlarına erişilememeli', expected: '401' },
    r.status === 401, `HTTP ${r.status}`, 'Cookie: ofis360_session=<adminId> -> GET /api/admin/users');
}
{
  // Calisanin kat planindan baska kullanicilarin ID'lerini gorebilmesi (F1 zinciri)
  const e = await loginAs(U(10));
  const pday = istanbulDayKey(-2) ; const fl = await e.get(`/floors/summaries?date=${pday}`); let occ = 0; let sample = '';
  let ids = 0;
  for (const f of fl.json) {
    const p = await e.get(`/floors/${f.id}/plan?date=${pday}`);
    const o = p.json.desks.filter((d) => d.occupant);
    occ += o.length; if (o[0] && !sample) sample = JSON.stringify(o[0].occupant);
    ids += o.filter((d) => d.occupant.userId || d.occupant.id).length;
  }
  R.check('SEC-AUTH-05', { area: 'Gizlilik', role: 'Çalışan', type: 'Güvenlik/API', scenario: 'Çalışan kat planında diğer çalışanların kullanıcı kimliklerini görmemeli (oturum çerezi = kimlik)', expected: 'occupant içinde userId yok' },
    occ > 0 && ids === 0, `${pday}: ${occ} dolu masanın ${ids} tanesinde kullanıcı kimliği açık; örnek occupant=${sample}`, 'GET /api/floors/{id}/plan -> desks[].occupant');
}
{
  const c = await loginAs(U(2));
  const old = c.cookie;
  await c.post('/auth/logout');
  const replay = new Client(old);
  const r = await replay.get('/me/context');
  R.check('SEC-AUTH-04', { area: 'Oturum', role: 'Çalışan', type: 'Güvenlik/API', scenario: 'Çıkıştan sonra eski çerez tekrar kullanılamamalı (sunucu tarafı oturum iptali)', expected: '401' },
    r.status === 401, `HTTP ${r.status}`, 'logout sonrası aynı çerez -> GET /api/me/context');
}
{
  const c = new Client();
  const r = await c.req('POST', '/auth/login', { userId: U(3) });
  const sc = r.headers.get('set-cookie') ?? '';
  const ok = /httponly/i.test(sc) && /secure/i.test(sc) && /samesite=(lax|strict)/i.test(sc);
  R.check('SEC-SESS-01', { area: 'Oturum', role: 'Çalışan', type: 'Güvenlik/Yapılandırma', scenario: 'Production çerezi HttpOnly + Secure + SameSite', expected: 'HttpOnly; Secure; SameSite=Lax/Strict' }, ok, sc);
  const maxAge = /max-age=(\d+)/i.exec(sc)?.[1];
  R.add({ id: 'SEC-SESS-02', area: 'Oturum', role: 'Çalışan', type: 'Güvenlik/Yapılandırma', scenario: 'Mutlak ve boşta kalma zaman aşımı sunucuda zorlanmalı (ASVS 3.3.1/3.3.2)', expected: 'Sunucu tarafı oturum kaydı, boşta zaman aşımı',
    actual: `max-age=${maxAge}s yalnızca tarayıcıya söylenir; çerez değeri sabit kullanıcı ID olduğundan süre sunucuda zorlanamaz (SEC-AUTH-03)`, status: 'Kaldı', evidence: 'DemoIdentityProvider.SignInAsync' });
}
{
  const admin = await loginAs(adminId);
  const victim = await loginAs(U(4));
  const before = await victim.get('/me/context');
  const off = await admin.put(`/admin/users/${U(4)}`, { active: false });
  const after = await victim.get('/me/context');
  await admin.put(`/admin/users/${U(4)}`, { active: true });
  R.check('SEC-SESS-03', { area: 'Oturum', role: 'Admin→Çalışan', type: 'Güvenlik/API', scenario: 'Pasifleştirilen kullanıcının açık oturumu hemen geçersiz olmalı', expected: '401' },
    before.status === 200 && off.status === 200 && after.status === 401, `önce ${before.status}, pasifleştirme ${off.status}, sonra ${after.status}`);
}

// ---------- Yetkilendirme matrisi (ASVS V4; WSTG-ATHZ) ----------
const getEndpoints = {
  employee: ['/panel/home', '/floors/summaries', '/me/context', '/me/reservations', '/me/usage', '/me/notifications', '/me/notifications/unread-count', '/auth/me'],
  manager: ['/analytics', '/analytics/context', '/analytics/export?type=ozet'],
  admin: ['/admin/users?take=1', '/admin/users/stats', '/admin/departments', '/admin/rules', '/admin/closures', '/admin/settings', '/admin/audit?take=1',
    '/admin/buildings', '/admin/floors', '/admin/floors/capacity', '/admin/desks?take=1', '/admin/rooms', '/admin/desk-features', '/admin/room-features',
    '/admin/desk-reservations?take=1', '/admin/room-reservations?take=1'],
};
const emp = await loginAs(U(5));
const mgr = await loginAs(managerId);
const adm = await loginAs(adminId);
{
  const all = [...getEndpoints.employee, ...getEndpoints.manager, ...getEndpoints.admin];
  const leaks = [];
  for (const p of all) { const r = await anon.get(p); if (r.status !== 401) leaks.push(`${p}=${r.status}`); }
  R.check('SEC-AUTHZ-01', { area: 'Yetki', role: 'Anonim', type: 'Güvenlik/API', scenario: `Oturumsuz erişim: ${all.length} korumalı GET ucu`, expected: 'Hepsi 401' }, leaks.length === 0, leaks.length ? leaks.join(', ') : `${all.length}/${all.length} uç 401`);
}
{
  const bad = [];
  for (const p of [...getEndpoints.manager, ...getEndpoints.admin]) { const r = await emp.get(p); if (r.status !== 403) bad.push(`${p}=${r.status}`); }
  R.check('SEC-AUTHZ-02', { area: 'Yetki', role: 'Çalışan', type: 'Güvenlik/API', scenario: `Çalışan ${getEndpoints.manager.length + getEndpoints.admin.length} yönetici/admin GET ucuna erişemez`, expected: 'Hepsi 403' }, bad.length === 0, bad.length ? bad.join(', ') : 'tamamı 403');
}
{
  const bad = [];
  for (const p of getEndpoints.admin) { const r = await mgr.get(p); if (r.status !== 403) bad.push(`${p}=${r.status}`); }
  for (const p of getEndpoints.manager) { const r = await mgr.get(p); if (r.status !== 200) bad.push(`${p}=${r.status}`); }
  R.check('SEC-AUTHZ-03', { area: 'Yetki', role: 'Yönetici', type: 'Güvenlik/API', scenario: 'Yönetici admin uçlarına erişemez, analitik uçlarına erişir', expected: 'admin 403, analitik 200' }, bad.length === 0, bad.join(', ') || 'beklendiği gibi');
}
{
  const writes = [
    ['POST', '/admin/buildings', { code: 'HACK', name: 'x' }], ['PUT', '/admin/rules/DESK_WEEKLY_LIMIT', { enabled: false }],
    ['PUT', '/admin/settings/app.name', { value: 'x' }], ['POST', '/admin/users', { fullName: 'x', email: 'x@x.x', title: 'x', role: 'ADMIN', departmentId: 'x' }],
    ['PUT', `/admin/users/${U(5)}`, { role: 'ADMIN' }], ['POST', '/admin/closures', {}], ['DELETE', '/admin/desks/x'], ['DELETE', '/admin/rooms/x'],
    ['PUT', '/admin/floors/x/plan', {}], ['POST', '/admin/floors', {}], ['PUT', '/admin/buildings/x', { active: false }],
  ];
  const bad = []; const pre400 = [];
  for (const [label, who] of [['Çalışan', emp], ['Yönetici', mgr]]) {
    for (const [m, p, b] of writes) { const r = await who.req(m, p, b); if (r.status === 400) pre400.push(`${label} ${m} ${p}`); else if (r.status !== 403) bad.push(`${label} ${m} ${p}=${r.status}`); }
  }
  // Gecerli govdeyle ayni uclar: 400 dogrulamasi rol kontrolunden once mi calisiyor?
  const validClosure = { scope: 'FLOOR', targetId: 'x', startDayKey: istanbulDayKey(20), endDayKey: istanbulDayKey(21), reason: 'OTHER', note: 'yetki testi' };
  const vc = await emp.post('/admin/closures', validClosure);
  if (vc.status !== 403) bad.push(`Çalışan POST /admin/closures (geçerli gövde)=${vc.status}`);
  const me = await emp.get('/auth/me');
  R.check('SEC-AUTHZ-04', { area: 'Yetki', role: 'Çalışan, Yönetici', type: 'Güvenlik/API', scenario: 'Arayüzde gizli admin yazma işlemleri doğrudan API ile yapılamaz (rol yükseltme dahil)', expected: 'Hepsi 403; rol değişmez' },
    bad.length === 0 && me.json.user.role === 'EMPLOYEE', (bad.join(', ') || `${writes.length * 2 + 1} çağrının hiçbiri işlenmedi (403/400)`) + `; çalışan rolü=${me.json.user.role}`);
  R.add({ id: 'SEC-AUTHZ-05', area: 'Yetki', role: 'Çalışan, Yönetici', type: 'Güvenlik/API', scenario: 'Yetkisiz kullanıcıya doğrulama hatası yerine 403 dönmeli (yetki, model doğrulamasından önce)', expected: '403',
    actual: pre400.length ? `${pre400.length} çağrıda 403 yerine 400: ${pre400.join(', ')} (işlem yapılmadı; bilgi sızıntısı düşük)` : 'hepsi 403', status: pre400.length ? 'Kaldı' : 'Geçti', evidence: 'RequireRole filtresi model bağlamadan sonra çalışıyor' });
}

// ---------- IDOR / is akisi atlama ----------
async function freeDesk(client, day) {
  const fl = await client.get(`/floors/summaries?date=${day}`);
  for (const f of fl.json.filter((x) => x.reservable)) {
    const plan = await client.get(`/floors/${f.id}/plan?date=${day}`);
    const d = plan.json.desks.find((x) => x.selectable);
    if (d) return d;
  }
  return null;
}
const day = istanbulDayKey(3);
const victim = await loginAs(U(6));
const attacker = await loginAs(U(7));
{
  const desk = await freeDesk(victim, day);
  const vres = await victim.post('/desk-reservations', { deskId: desk.id, date: day, note: 'kurban' });
  const r = await attacker.post(`/desk-reservations/${vres.json.id}/cancel`, { reason: 'idor' });
  R.check('SEC-IDOR-01', { area: 'Rezervasyon', role: 'Çalışan', type: 'Güvenlik/API', scenario: 'Başka çalışanın masa rezervasyonunu ID ile iptal edemez', expected: '403/404' }, vres.status === 200 && okAny(r.status, 403, 404), `oluşturma ${vres.status}, saldırı HTTP ${r.status} ${r.json?.error?.code ?? ''}`);
  const c = await attacker.post(`/desk-reservations/${vres.json.id}/check-in`);
  R.check('SEC-IDOR-02', { area: 'Rezervasyon', role: 'Çalışan', type: 'Güvenlik/API', scenario: 'Başka çalışanın rezervasyonuna check-in yapamaz', expected: '403/404' }, okAny(c.status, 403, 404), `HTTP ${c.status} ${c.json?.error?.code ?? ''}`);
  const mc = await mgr.post(`/desk-reservations/${vres.json.id}/cancel`, { reason: 'idor' });
  R.check('SEC-IDOR-03', { area: 'Rezervasyon', role: 'Yönetici', type: 'Güvenlik/API', scenario: 'Yönetici başkasının rezervasyonunu iptal edemez (yalnızca admin)', expected: '403/404' }, okAny(mc.status, 403, 404), `HTTP ${mc.status}`);
}
{
  const d2 = await freeDesk(attacker, istanbulDayKey(4));
  const r = await attacker.post('/desk-reservations', { deskId: d2.id, date: istanbulDayKey(4), targetUserId: U(6) });
  R.check('SEC-IDOR-04', { area: 'Rezervasyon', role: 'Çalışan', type: 'Güvenlik/API', scenario: 'Çalışan targetUserId ile başkası adına rezervasyon yapamaz', expected: '403' }, r.status === 403, `HTTP ${r.status}`);
  const m = await mgr.post('/desk-reservations', { deskId: d2.id, date: istanbulDayKey(4), targetUserId: U(6) });
  R.check('SEC-IDOR-05', { area: 'Rezervasyon', role: 'Yönetici', type: 'Güvenlik/API', scenario: 'Yönetici targetUserId ile başkası adına rezervasyon yapamaz', expected: '403' }, m.status === 403, `HTTP ${m.status}`);
}
{
  const rooms = await victim.get(`/rooms/availability?date=${day}&start=840&end=900&minCapacity=2`);
  const room = rooms.json.rooms.find((x) => x.availableForRequest && !x.closed);
  const rr = await victim.post('/room-reservations', { roomId: room.id, date: day, startMinutes: 840, endMinutes: 900, title: 'Kurban toplantısı', purpose: 'test', attendeeCount: 2 });
  const r = await attacker.post(`/room-reservations/${rr.json?.id}/cancel`, { reason: 'idor' });
  R.check('SEC-IDOR-06', { area: 'Oda', role: 'Çalışan', type: 'Güvenlik/API', scenario: 'Başkasının oda rezervasyonunu iptal edemez', expected: '403/404' }, rr.status === 200 && okAny(r.status, 403, 404), `oluşturma ${rr.status}, saldırı ${r.status}`);
  const av = await attacker.get(`/rooms/availability?date=${day}&start=840&end=900&minCapacity=2`);
  const busy = av.json.rooms.find((x) => x.id === room.id)?.busySlots ?? [];
  const exposesOwner = JSON.stringify(busy).includes(U(6));
  R.check('SEC-AUTH-06', { area: 'Gizlilik', role: 'Çalışan', type: 'Güvenlik/API', scenario: 'Oda uygunluk yanıtı başka kullanıcının kimliğini (ownerId) açmamalı', expected: 'ownerId yok' }, !exposesOwner, `busySlots kurban kimliğini içeriyor: ${exposesOwner}`, 'GET /api/rooms/availability');
}
{
  const n = await victim.get('/me/notifications');
  const id = n.json.items.find((x) => !x.readAt)?.id ?? n.json.items[0]?.id;
  const r = id ? await attacker.post(`/me/notifications/${id}/read`) : null;
  const after = await victim.get('/me/notifications');
  const stillUnread = after.json.items.find((x) => x.id === id)?.readAt == null;
  R.add({ id: 'SEC-IDOR-07', area: 'Bildirim', role: 'Çalışan', type: 'Güvenlik/API', scenario: 'Başkasının bildirimini okundu yapamaz', expected: '403/404 veya etkisiz; kurbanda okunmamış kalır',
    actual: id ? `HTTP ${r.status}, kurbanda okunmamış: ${stillUnread}` : 'kurbanın bildirimi yok', status: !id ? 'Engellendi' : stillUnread ? 'Geçti' : 'Kaldı' });
}
{
  const d3 = await freeDesk(attacker, istanbulDayKey(5));
  const r = await attacker.post('/desk-reservations', { deskId: d3.id, date: istanbulDayKey(5), status: 'CHECKED_IN', userId: U(6), source: 'ADMIN', checkedInAt: new Date().toISOString() });
  const res = r.json?.reservation;
  const uid = res?.userId ?? res?.user?.id;
  R.check('SEC-MASS-01', { area: 'Rezervasyon', role: 'Çalışan', type: 'Güvenlik/API', scenario: 'Fazla alanlar (status, userId, source, checkedInAt) yok sayılır', expected: 'status=BOOKED, kullanıcı=kendisi' },
    r.status === 200 && res?.status === 'BOOKED' && uid === U(7), `HTTP ${r.status} status=${res?.status} user=${uid} source=${res?.source}`);
  const p = await attacker.put('/me/preferences', { role: 'ADMIN', accessibilityApproved: true, prefersQuietZone: true });
  const ctx = await attacker.get('/auth/me');
  R.check('SEC-MASS-02', { area: 'Profil', role: 'Çalışan', type: 'Güvenlik/API', scenario: 'Tercih güncellemesiyle rol/erişilebilirlik onayı yükseltilemez', expected: 'role=EMPLOYEE, accessibilityApproved=false' },
    ctx.json.user.role === 'EMPLOYEE' && ctx.json.user.accessibilityApproved === false, `HTTP ${p.status} role=${ctx.json.user.role} accessibilityApproved=${ctx.json.user.accessibilityApproved}`);
}

// ---------- Girdi kaynakli riskler (ASVS V5; WSTG-INPV) ----------
{
  const payloads = ["' OR '1'='1", "'; DROP TABLE APP_USER;--", '%', '_', '\\', "') UNION SELECT NULL FROM DUAL--", 'İ%_\\'];
  const bad = [];
  for (const q of payloads) {
    for (const p of [`/admin/users?search=${encodeURIComponent(q)}`, `/admin/audit?search=${encodeURIComponent(q)}`, `/admin/desks?search=${encodeURIComponent(q)}`]) {
      const r = await adm.get(p);
      if (r.status !== 200 || /ORA-|Exception|stack/i.test(r.text) || (r.json?.total ?? 0) > 0) bad.push(`${p} -> ${r.status} total=${r.json?.total}`);
    }
  }
  R.check('SEC-INJ-01', { area: 'Arama', role: 'Admin', type: 'Güvenlik/API', scenario: `SQL enjeksiyonu ve LIKE joker yükleri (${payloads.length} yük × 3 uç)`, expected: '200, 0 sonuç, hata/ORA sızıntısı yok' }, bad.length === 0, bad.join(' | ') || 'tümü 200, 0 eşleşme, sızıntı yok');
}
{
  const r1 = await emp.req('POST', '/desk-reservations', '{"deskId":', { 'content-type': 'application/json' });
  const r2 = await emp.req('POST', '/desk-reservations', 'deskId=x&date=2026-01-01', { 'content-type': 'application/x-www-form-urlencoded' });
  const r3 = await emp.req('POST', '/desk-reservations', '{"deskId":"x","date":"2026-01-01"}', { 'content-type': 'text/plain' });
  const leak = [r1, r2, r3].some((r) => /Exception|at Ofis360|ORA-|System\./.test(r.text));
  R.check('SEC-INJ-02', { area: 'API', role: 'Çalışan', type: 'Güvenlik/API', scenario: 'Bozuk JSON 400; form/text gövdeleri reddedilir (CSRF basit istek koruması); hata ayrıntısı sızmaz', expected: '400 INVALID_JSON, 415, 415' },
    r1.status === 400 && r2.status === 415 && r3.status === 415 && !leak, `bozuk=${r1.status} ${r1.json?.error?.code}; form=${r2.status}; text=${r3.status}; sızıntı=${leak}`);
}
{
  const long = 'A'.repeat(20000);
  const d4 = await freeDesk(emp, istanbulDayKey(6));
  const r = await emp.post('/desk-reservations', { deskId: d4.id, date: istanbulDayKey(6), note: long });
  R.check('SEC-INJ-03', { area: 'Rezervasyon', role: 'Çalışan', type: 'Doğrulama', scenario: '20.000 karakterlik not', expected: '400 VALIDATION_ERROR (500 değil)' }, r.status === 400, `HTTP ${r.status} ${r.json?.error?.code ?? ''}`);
  const dates = ['2026-02-30', 'abc', '9999-12-31', '2026-13-01', '0001-01-01', "2026-01-01' OR 1=1"];
  const out = [];
  for (const dt of dates) { const x = await emp.post('/desk-reservations', { deskId: d4.id, date: dt }); out.push(`${dt}=${x.status}`); }
  R.check('SEC-INJ-04', { area: 'Rezervasyon', role: 'Çalışan', type: 'Doğrulama', scenario: 'Geçersiz/sınır tarihler', expected: 'Hepsi 4xx' }, out.every((s) => /=4\d\d$/.test(s)), out.join(', '));
  const takes = [];
  for (const t of ['100000', '-1', 'abc', '2147483648']) { const x = await adm.get(`/admin/audit?take=${t}`); takes.push(`take=${t}->${x.status}/${x.json?.items?.length ?? '-'}/${x.json?.take ?? '-'}`); }
  const unbounded = takes.some((s) => /take=100000->200\/\d+\/100000/.test(s));
  R.check('SEC-INJ-05', { area: 'Sayfalama', role: 'Admin', type: 'Doğrulama', scenario: 'Aşırı/negatif/geçersiz take', expected: '400 veya üst sınıra kırpılmış; 500 yok' }, !takes.some((s) => /->5\d\d/.test(s)) && !unbounded, takes.join(', '));
  const bad = [];
  for (const p of ['/admin/desk-reservations?from=abc', '/admin/room-reservations?to=2026-99-99', '/me/notifications?take=-5']) { const x = await (p.startsWith('/me') ? emp : adm).get(p); bad.push(`${p}=${x.status}`); }
  R.check('SEC-INJ-07', { area: 'Liste', role: 'Admin', type: 'Doğrulama', scenario: 'Geçersiz from/to sorgu parametreleri', expected: '400 (500 değil)' }, bad.every((s) => !/=5\d\d$/.test(s)), bad.join(', '));
}
{
  const xss = '<img src=x onerror=alert(1)><script>alert(2)</script>';
  const d5 = await freeDesk(victim, istanbulDayKey(7));
  const r = await victim.post('/desk-reservations', { deskId: d5?.id, date: istanbulDayKey(7), note: xss });
  const list = await victim.get('/me/reservations');
  const stored = JSON.stringify(list.json).includes('<img src=x');
  const ct = list.headers.get('content-type');
  R.add({ id: 'SEC-XSS-01', area: 'Rezervasyon', role: 'Çalışan', type: 'Güvenlik/API', scenario: 'Saklanan XSS yükü API\'de JSON olarak döner; arayüz kaçışı UI testinde (UI-XSS-01)', expected: 'application/json',
    actual: `oluşturma ${r.status}, saklandı=${stored}, content-type=${ct}`, status: r.status === 200 && /application\/json/.test(ct) ? 'Geçti' : 'Kaldı' });
}
{
  const r = await adm.get('/admin/floors/..%2F..%2Fadmin%2Fusers');
  const r2 = await adm.get('/admin/floors/%00');
  R.check('SEC-INJ-06', { area: 'API', role: 'Admin', type: 'Güvenlik/API', scenario: 'Yol geçişi / null bayt içeren ID', expected: '404/400, sızıntı yok' }, [r.status, r2.status].every((s) => s === 404 || s === 400) && !/Exception|ORA-/.test(r.text + r2.text), `${r.status}, ${r2.status}`);
}
{
  // CSV/formul enjeksiyonu: admin serbest metni (oda adi) rapora gider
  const floors = await adm.get('/admin/floors');
  const floor = (floors.json.items ?? floors.json).find((f) => f.buildingCode === 'DEMO' || f.number === 1) ?? (floors.json.items ?? floors.json)[0];
  const code = `CSV${Date.now() % 100000}`;
  const mk = await adm.post('/admin/rooms', { code, name: '=HYPERLINK("http://example.invalid","Oda")', floorId: floor.id, capacity: 4, x: 10, y: 10, width: 60, height: 40 });
  const found = [];
  for (const t of ['ozet', 'doluluk', 'kat', 'masa', 'oda', 'departman', 'adil-kullanim']) {
    const x = await mgr.get(`/analytics/export?type=${t}&from=${istanbulDayKey(-60)}&to=${istanbulDayKey(14)}`);
    const line = x.text.split(/\r?\n/).find((l) => /(^|;)"?=HYPERLINK/.test(l));
    if (line) found.push(`${t}: ${line.slice(0, 80)}`);
  }
  R.add({ id: 'SEC-CSV-01', area: 'Rapor', role: 'Admin→Yönetici', type: 'Güvenlik/API', scenario: 'Kullanıcı kontrollü metin CSV hücresine formül olarak yazılmamalı (=,+,-,@ önekli)', expected: "Hücre ' önekiyle etkisizleştirilmeli",
    actual: mk.status !== 200 ? `oda oluşturulamadı ${mk.status} ${mk.text.slice(0, 100)}` : found.length ? `formül hücresi → ${found.join(' | ')}` : 'oda raporlarda formül önekli hücre olarak görünmedi',
    status: mk.status !== 200 ? 'Engellendi' : found.length ? 'Kaldı' : 'Geçti', evidence: 'POST /api/admin/rooms + GET /api/analytics/export' });
  const rid = mk.json?.id ?? mk.json?.room?.id;
  if (rid) await adm.put(`/admin/rooms/${rid}`, { active: false });
}

// ---------- Basliklar, CORS, bilgi sizdirma (ASVS V14; WSTG-CONF) ----------
{
  const r = await emp.get('/me/reservations');
  const want = ['content-security-policy', 'x-content-type-options', 'x-frame-options', 'referrer-policy', 'strict-transport-security', 'cache-control'];
  const missing = want.filter((h) => !r.headers.get(h));
  R.check('SEC-HDR-01', { area: 'HTTP', role: 'Çalışan', type: 'Güvenlik/Yapılandırma', scenario: 'Güvenlik başlıkları ve kişisel veride Cache-Control: no-store', expected: want.join(', ') }, missing.length === 0, `eksik: ${missing.join(', ') || '-'}; Server=${r.headers.get('server')}`);
}
{
  const evil = await emp.get('/me/context', { origin: 'https://evil.example' });
  const acao = evil.headers.get('access-control-allow-origin');
  const pre = await fetch(BASE + '/desk-reservations', { method: 'OPTIONS', headers: { origin: 'https://evil.example', 'access-control-request-method': 'POST', 'access-control-request-headers': 'content-type' } });
  await pre.text();
  R.check('SEC-CORS-01', { area: 'HTTP', role: 'Anonim', type: 'Güvenlik/Yapılandırma', scenario: 'Yabancı origin için CORS izni verilmez', expected: 'ACAO yok' }, !acao && !pre.headers.get('access-control-allow-origin'), `GET ACAO=${acao ?? '-'}, preflight ${pre.status} ACAO=${pre.headers.get('access-control-allow-origin') ?? '-'}`);
  const good = await emp.get('/me/context', { origin: 'http://localhost:4210' });
  const a = good.headers.get('access-control-allow-origin');
  R.add({ id: 'SEC-CORS-02', area: 'HTTP', role: '-', type: 'Güvenlik/Yapılandırma', scenario: 'Production izinli origin listesi yapılandırmadan gelmeli', expected: 'Yalnızca kurumsal alan adı (https)',
    actual: `Production'da yapılandırma yokken http://localhost:4210 kimlik bilgisiyle kabul ediliyor: ACAO=${a}`, status: a ? 'Kaldı' : 'Geçti', evidence: 'Program.cs AddCors varsayılanı' });
}
{
  const h = await anon.get('/health');
  R.add({ id: 'SEC-INFO-01', area: 'HTTP', role: 'Anonim', type: 'Güvenlik/API', scenario: 'Oturumsuz sağlık ucu sürüm/şema/zaman bilgisi', expected: 'Yalnızca durum; ayrıntı iç ağda', actual: `HTTP ${h.status}: ${h.text.slice(0, 150)}`, status: /schemaVersion/.test(h.text) ? 'Kaldı' : 'Geçti', evidence: 'GET /api/health' });
  const nf = await anon.get('/does-not-exist');
  const tr = { status: await rawStatus('TRACE', '/health') };
  R.check('SEC-INFO-02', { area: 'HTTP', role: 'Anonim', type: 'Güvenlik/API', scenario: 'Bilinmeyen yol ve TRACE', expected: '404/405, ayrıntı yok' }, okAny(nf.status, 404, 401) && tr.status >= 400 && !/Exception/.test(nf.text), `yol=${nf.status}, TRACE=${tr.status}`);
}

// ---------- Hiz sinirlama (ASVS 2.2.1, 11.1.4) ----------
{
  const N = 300;
  const t0 = performance.now();
  const rs = await Promise.all(Array.from({ length: N }, () => new Client().post('/auth/login', { userId: 'yok-boyle-biri' })));
  const s = rs.reduce((a, r) => ((a[r.status] = (a[r.status] ?? 0) + 1), a), {});
  R.check('SEC-RATE-01', { area: 'Kimlik', role: 'Anonim', type: 'Güvenlik/API', scenario: `${N} eşzamanlı başarısız giriş denemesi`, expected: 'Bir noktadan sonra 429' }, !!s[429], `${JSON.stringify(s)} ${(performance.now() - t0).toFixed(0)} ms`);
  const M = 20;
  const t1 = performance.now();
  const ex = await Promise.all(Array.from({ length: M }, () => mgr.get(`/analytics/export?type=masa&from=${istanbulDayKey(-60)}&to=${istanbulDayKey(14)}`)));
  const es = ex.reduce((a, r) => ((a[r.status] = (a[r.status] ?? 0) + 1), a), {});
  R.check('SEC-RATE-02', { area: 'Rapor', role: 'Yönetici', type: 'Güvenlik/API', scenario: `${M} eşzamanlı 74 günlük CSV dışa aktarımı`, expected: 'Kısıtlama (429) veya kuyruk' }, !!es[429], `${JSON.stringify(es)}; toplam ${(performance.now() - t1).toFixed(0)} ms, en yavaş ${Math.max(...ex.map((r) => r.ms)).toFixed(0)} ms`);
}

// ---------- Denetim kaydi (ASVS V7) ----------
{
  const a = await adm.get('/admin/audit?take=200');
  const actions = new Set(a.json.items.map((i) => i.action));
  const hasActor = a.json.items.every((i) => i.actorName && i.createdAt);
  R.check('SEC-AUD-01', { area: 'Denetim', role: 'Admin', type: 'Güvenlik/API', scenario: 'Kritik işlemler kimin/ne zaman/hangi kayıt bilgisiyle loglanır', expected: 'LOGIN, kullanıcı güncelleme, iptal; aktör+zaman+kayıt' },
    hasActor && [...actions].some((x) => /LOGIN/.test(x)) && [...actions].some((x) => /USER/.test(x)), `son 200 kayıttaki eylemler: ${[...actions].join(', ')}`);
  const failed = a.json.items.filter((i) => /FAIL|DENIED|FORBIDDEN/i.test(i.action)).length;
  R.add({ id: 'SEC-AUD-02', area: 'Denetim', role: 'Anonim', type: 'Güvenlik/API', scenario: 'Başarısız giriş ve yetkisiz erişim (403) denemeleri loglanır', expected: 'Güvenlik olayı kaydı', actual: `300 başarısız giriş + ~60 adet 403 sonrası denetim kaydı: ${failed}`, status: failed ? 'Geçti' : 'Kaldı', evidence: 'GET /api/admin/audit' });
}

R.save();
