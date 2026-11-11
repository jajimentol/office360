// Dayaniklilik testleri: Oracle yanit vermez (docker pause) ve tamamen durur (docker stop) senaryolari.
// UYARI: ofis360-oracle konteynerini gecici olarak durdurur — ayni konteyneri kullanan gelistirme ornegi (5210) da etkilenir.
// Calistirma: node resilience.mjs
import { execFileSync } from 'node:child_process';
import { Recorder, BASE, loginAs, demoAccounts } from './lib.mjs';

const R = new Recorder('resilience');
const docker = (...a) => execFileSync('docker', a, { encoding: 'utf8' }).trim();
async function timed(path, cookie, timeoutMs = 60000) {
  const t0 = performance.now();
  try {
    const r = await fetch(BASE + path, { headers: cookie ? { cookie } : {}, signal: AbortSignal.timeout(timeoutMs) });
    const text = await r.text();
    return { status: r.status, ms: Math.round(performance.now() - t0), body: text.slice(0, 160) };
  } catch (e) { return { status: 0, ms: Math.round(performance.now() - t0), body: String(e.name ?? e).slice(0, 80) }; }
}
async function waitHealthy(maxS) {
  const t0 = Date.now();
  while (Date.now() - t0 < maxS * 1000) {
    const h = await timed('/health', null, 5000);
    if (h.status === 200 && /"connected":true/.test(h.body)) return Math.round((Date.now() - t0) / 1000);
    await new Promise((r) => setTimeout(r, 2000));
  }
  return null;
}

const accounts = await demoAccounts();
const emp = await loginAs(accounts.find((a) => a.role === 'EMPLOYEE').id);
const base = await timed('/panel/home', emp.cookie);

// ---- 1) Veritabani yanit vermiyor (ag/sunucu donmasi) ----
docker('pause', 'ofis360-oracle');
let hung, health1;
try {
  [hung, health1] = await Promise.all([timed('/panel/home', emp.cookie, 70000), timed('/health', null, 70000)]);
} finally { docker('unpause', 'ofis360-oracle'); }
const rec1 = await waitHealthy(120);
const after1 = await timed('/panel/home', emp.cookie);
R.check('RES-01', { area: 'Dayanıklılık', role: 'Çalışan', type: 'Dayanıklılık', scenario: 'Oracle yanıt vermiyor (docker pause): istek sınırlı sürede anlaşılır hatayla dönmeli', expected: '≤ 30 sn içinde 503/500 + mesaj' },
  hung.status >= 500 && hung.ms <= 30000, `panel/home → ${hung.status || 'zaman aşımı(istemci 70sn)'} ${hung.ms}ms; health → ${health1.status || 'zaman aşımı'} ${health1.ms}ms "${health1.body.slice(0, 60)}"`);
R.check('RES-02', { area: 'Dayanıklılık', role: 'Çalışan', type: 'Dayanıklılık', scenario: 'Oracle geri gelince uygulama yeniden başlatılmadan toparlanır', expected: 'health 200, istek 200' },
  rec1 !== null && after1.status === 200, `toparlanma=${rec1}sn, sonraki istek=${after1.status} ${after1.ms}ms (öncesi ${base.ms}ms)`);

// ---- 2) Veritabani tamamen kapali ----
docker('stop', '-t', '30', 'ofis360-oracle');
let down, health2;
try {
  [down, health2] = await Promise.all([timed('/panel/home', emp.cookie, 70000), timed('/health', null, 70000)]);
} finally { docker('start', 'ofis360-oracle'); }
const t0 = Date.now();
const rec2 = await waitHealthy(420);
const after2 = await timed('/panel/home', emp.cookie);
R.check('RES-03', { area: 'Dayanıklılık', role: 'Çalışan', type: 'Dayanıklılık', scenario: 'Oracle kapalı: API hızlı ve sızıntısız hata döner; sağlık ucu 503', expected: 'panel 500/503 hızlı, health 503' },
  down.status >= 500 && down.ms < 15000 && health2.status === 503 && !/ORA-|Exception/.test(down.body), `panel/home → ${down.status} ${down.ms}ms "${down.body.slice(0, 70)}"; health → ${health2.status} ${health2.ms}ms`);
R.check('RES-04', { area: 'Dayanıklılık', role: 'Çalışan', type: 'Dayanıklılık', scenario: 'Oracle yeniden başlayınca API kendiliğinden toparlanır (bayat havuz bağlantıları)', expected: 'health 200, ilk istek 200' },
  rec2 !== null && after2.status === 200, `Oracle başlat→sağlıklı=${rec2 ?? '>' + Math.round((Date.now() - t0) / 1000)}sn, ilk istek=${after2.status} ${after2.ms}ms`);
const st = docker('inspect', '-f', '{{.State.Health.Status}} restart={{.HostConfig.RestartPolicy.Name}}', 'ofis360-oracle');
R.add({ id: 'RES-05', area: 'İşletim', role: '-', type: 'Dayanıklılık/Yapılandırma', scenario: 'Veritabanı konteyneri yeniden başlatma politikası ve sağlık durumu', expected: 'restart politikası; healthy', actual: st, status: /restart=no/.test(st) ? 'Kaldı' : 'Geçti', evidence: 'docker/oracle/compose.yml' });
R.save();
