// Yuk ureticisi (Node 24, harici bagimlilik yok). Kapali model: her sanal kullanici (VU) ayri oturumla
// gercekci bir is akisi dongusu calistirir, adimlar arasinda dusunme suresi bekler.
// Kullanim: node load.mjs --name=S2-peak --vus=150 --duration=300 --ramp=60 --think=2000
//   --think: ortalama dusunme suresi (ms, ustel dagilim; 0 = durmaksizin)
//   --users=500 kullanici havuzu (perfu0001..0200 + seed calisanlari)
import { writeFileSync, mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { BASE, RESULTS_DIR, istanbulDayKey, sql } from './lib.mjs';

const arg = (k, d) => { const a = process.argv.find((x) => x.startsWith(`--${k}=`)); return a ? a.split('=')[1] : d; };
const NAME = arg('name', 'load');
const VUS = Number(arg('vus', 50));
const DURATION = Number(arg('duration', 120)) * 1000;
const RAMP = Number(arg('ramp', 30)) * 1000;
const THINK = Number(arg('think', 3000));
const WRITE_RATIO = Number(arg('writes', 1)); // yazma olasiliklarini olcekler

// ---------- Kullanici havuzu ----------
const users = sql(`SELECT ID, ROLE FROM OFIS360_PERF.APP_USER WHERE ACTIVE = TRUE ORDER BY ID;`).map(([id, role]) => ({ id, role }));
const employees = users.filter((u) => u.role === 'EMPLOYEE');
const managers = users.filter((u) => u.role === 'MANAGER');
const admins = users.filter((u) => u.role === 'ADMIN');

// ---------- Olcum ----------
const stats = new Map(); // endpoint -> { lat:[], ok, biz, err, codes }
const timeline = []; // her saniye: { t, rps, err, p95 }
let secBucket = { n: 0, err: 0, lat: [] };
const biz = { deskOk: 0, deskRejected: 0, roomOk: 0, roomRejected: 0, cancelOk: 0 };
function record(ep, status, ms, netErr) {
  let s = stats.get(ep);
  if (!s) stats.set(ep, (s = { lat: [], ok: 0, biz: 0, err: 0, codes: {} }));
  s.lat.push(ms);
  s.codes[status] = (s.codes[status] ?? 0) + 1;
  if (netErr || status >= 500 || status === 0) s.err++;
  else if (status >= 400) s.biz++;
  else s.ok++;
  secBucket.n++; secBucket.lat.push(ms); if (netErr || status >= 500) secBucket.err++;
}
async function call(vu, ep, method, path, body) {
  const t0 = performance.now();
  try {
    const headers = { cookie: vu.cookie };
    if (body) headers['content-type'] = 'application/json';
    const r = await fetch(BASE + path, { method, headers, body: body ? JSON.stringify(body) : undefined, signal: AbortSignal.timeout(30000) });
    const text = await r.text();
    record(ep, r.status, performance.now() - t0, false);
    let json = null; try { json = JSON.parse(text); } catch {}
    return { status: r.status, json };
  } catch (e) {
    record(ep, 0, performance.now() - t0, true);
    return { status: 0, json: null, error: String(e) };
  }
}
const think = (scale = 1) => THINK <= 0 ? Promise.resolve() : new Promise((r) => setTimeout(r, -Math.log(1 - Math.random()) * THINK * scale));
const pick = (a) => a[Math.floor(Math.random() * a.length)];
const chance = (p) => Math.random() < p * WRITE_RATIO;
const weekdays = []; for (let o = 1; weekdays.length < 10 && o < 20; o++) { const k = istanbulDayKey(o); const wd = new Date(`${k}T12:00:00Z`).getUTCDay(); if (wd && wd !== 6) weekdays.push(k); }
const createdIds = [];

async function login(vu) {
  const t0 = performance.now();
  const r = await fetch(BASE + '/auth/login', { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ userId: vu.user.id }) });
  await r.text();
  record('POST /auth/login', r.status, performance.now() - t0, false);
  vu.cookie = (r.headers.get('set-cookie') ?? '').split(';')[0];
}

// ---------- Is akislari ----------
async function employeeIteration(vu) {
  const home = await call(vu, 'GET /panel/home', 'GET', '/panel/home');
  await call(vu, 'GET /me/notifications/unread-count', 'GET', '/me/notifications/unread-count');
  await think();
  if (Math.random() < 0.7) {
    const day = pick(weekdays);
    const floors = await call(vu, 'GET /floors/summaries', 'GET', `/floors/summaries?date=${day}`);
    const fl = (floors.json ?? []).filter((f) => f.reservable);
    if (fl.length) {
      const f = pick(fl);
      const plan = await call(vu, 'GET /floors/{id}/plan', 'GET', `/floors/${f.id}/plan?date=${day}`);
      await think(0.5);
      const free = (plan.json?.desks ?? []).filter((d) => d.selectable);
      if (free.length && chance(0.25)) {
        const r = await call(vu, 'POST /desk-reservations', 'POST', '/desk-reservations', { deskId: pick(free).id, date: day });
        if (r.status === 200) { biz.deskOk++; createdIds.push(r.json.id); vu.mine.push(r.json.id); } else if (r.status >= 400 && r.status < 500) biz.deskRejected++;
      }
    }
  }
  await think();
  if (Math.random() < 0.4) await call(vu, 'GET /me/reservations', 'GET', '/me/reservations');
  if (Math.random() < 0.2) {
    const day = pick(weekdays); const start = 540 + 30 * Math.floor(Math.random() * 14);
    const av = await call(vu, 'GET /rooms/availability', 'GET', `/rooms/availability?date=${day}&start=${start}&end=${start + 60}&minCapacity=2`);
    const rooms = (av.json?.rooms ?? []).filter((r) => r.availableForRequest && !r.closed);
    if (rooms.length && chance(0.3)) {
      const r = await call(vu, 'POST /room-reservations', 'POST', '/room-reservations', { roomId: pick(rooms).id, date: day, startMinutes: start, endMinutes: start + 60, title: 'Yük testi', purpose: 'Planlama', attendeeCount: 2 });
      if (r.status === 200) biz.roomOk++; else if (r.status >= 400 && r.status < 500) biz.roomRejected++;
    }
  }
  if (vu.mine.length && chance(0.08)) {
    const id = vu.mine.shift();
    const r = await call(vu, 'POST /desk-reservations/{id}/cancel', 'POST', `/desk-reservations/${id}/cancel`, { reason: 'Plan değişti' });
    if (r.status === 200) biz.cancelOk++;
  }
  if (Math.random() < 0.1) await call(vu, 'GET /me/usage', 'GET', '/me/usage');
  void home;
}
async function managerIteration(vu) {
  await call(vu, 'GET /panel/home', 'GET', '/panel/home');
  await think();
  await call(vu, 'GET /analytics (30g)', 'GET', `/analytics?from=${istanbulDayKey(-30)}&to=${istanbulDayKey(0)}`);
  await think(2);
  if (Math.random() < 0.15) await call(vu, 'GET /analytics/export', 'GET', `/analytics/export?type=${pick(['ozet', 'masa', 'departman', 'adil-kullanim'])}&from=${istanbulDayKey(-60)}&to=${istanbulDayKey(0)}`);
  await employeeIteration(vu);
}
async function adminIteration(vu) {
  await call(vu, 'GET /admin/desk-reservations', 'GET', `/admin/desk-reservations?take=50&from=${istanbulDayKey(0)}`);
  await think();
  await call(vu, 'GET /admin/users', 'GET', '/admin/users?take=50');
  await think();
  await call(vu, 'GET /admin/audit', 'GET', '/admin/audit?take=50');
  await think();
  await call(vu, 'GET /admin/floors', 'GET', '/admin/floors');
}

// ---------- Calistirma ----------
const startedAt = Date.now();
const endAt = startedAt + DURATION;
let active = 0;
const sampler = setInterval(() => {
  const l = secBucket.lat.sort((a, b) => a - b);
  timeline.push({ t: Math.round((Date.now() - startedAt) / 1000), vus: active, rps: secBucket.n, err: secBucket.err, p95: l.length ? Math.round(l[Math.floor(l.length * 0.95)]) : null });
  secBucket = { n: 0, err: 0, lat: [] };
}, 1000);

async function runVu(i) {
  await new Promise((r) => setTimeout(r, RAMP * (i / VUS)));
  if (Date.now() >= endAt) return;
  // Rol dagilimi: %92 calisan, %6 yonetici, %2 admin
  const roll = i % 50;
  const role = roll === 0 ? 'ADMIN' : roll <= 3 ? 'MANAGER' : 'EMPLOYEE';
  const pool = role === 'ADMIN' ? admins : role === 'MANAGER' ? managers : employees;
  const vu = { user: pool[i % pool.length], cookie: '', mine: [] };
  active++;
  await login(vu);
  await think(0.3);
  while (Date.now() < endAt) {
    if (role === 'ADMIN') await adminIteration(vu); else if (role === 'MANAGER') await managerIteration(vu); else await employeeIteration(vu);
    await think();
  }
  active--;
}
const cpu0 = process.cpuUsage();
await Promise.all(Array.from({ length: VUS }, (_, i) => runVu(i)));
clearInterval(sampler);
const cpu = process.cpuUsage(cpu0);
const elapsed = (Date.now() - startedAt) / 1000;

// ---------- Raporlama ----------
const pct = (a, p) => (a.length ? a[Math.min(a.length - 1, Math.floor(a.length * p))] : null);
const endpoints = [...stats.entries()].map(([ep, s]) => {
  const l = s.lat.sort((a, b) => a - b);
  return { ep, n: l.length, ok: s.ok, biz4xx: s.biz, err: s.err, p50: Math.round(pct(l, 0.5)), p95: Math.round(pct(l, 0.95)), p99: Math.round(pct(l, 0.99)), max: Math.round(l[l.length - 1]), codes: s.codes };
}).sort((a, b) => b.n - a.n);
const allLat = [...stats.values()].flatMap((s) => s.lat).sort((a, b) => a - b);
const total = allLat.length;
const errors = [...stats.values()].reduce((a, s) => a + s.err, 0);
const summary = {
  name: NAME, base: BASE, vus: VUS, durationS: DURATION / 1000, rampS: RAMP / 1000, thinkMs: THINK, elapsedS: elapsed,
  requests: total, rps: +(total / elapsed).toFixed(1), errors, errorRate: +(100 * errors / Math.max(1, total)).toFixed(3),
  p50: Math.round(pct(allLat, 0.5)), p95: Math.round(pct(allLat, 0.95)), p99: Math.round(pct(allLat, 0.99)), max: Math.round(allLat[total - 1]),
  business: biz, generatorCpuS: +((cpu.user + cpu.system) / 1e6).toFixed(1), endpoints, timeline,
};
mkdirSync(RESULTS_DIR, { recursive: true });
writeFileSync(join(RESULTS_DIR, `load-${NAME}.json`), JSON.stringify(summary, null, 2));
console.log(`${NAME}: VU=${VUS} süre=${elapsed.toFixed(0)}s istek=${total} rps=${summary.rps} hata=${errors} (%${summary.errorRate}) p50=${summary.p50} p95=${summary.p95} p99=${summary.p99} max=${summary.max} ms`);
console.log(`iş: ${JSON.stringify(biz)}  üretici CPU=${summary.generatorCpuS}s`);
for (const e of endpoints) console.log(`  ${e.ep.padEnd(38)} n=${String(e.n).padStart(6)} p50=${String(e.p50).padStart(5)} p95=${String(e.p95).padStart(5)} p99=${String(e.p99).padStart(5)} err=${e.err} 4xx=${e.biz4xx}`);
