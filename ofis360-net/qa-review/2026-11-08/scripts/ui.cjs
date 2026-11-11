// Arayuz testleri: Playwright (Chromium) + axe-core. Production Angular paketi (serve-prod.mjs, 4211) + izole perf API.
// Calistirma: node ui.cjs   (PW_PATH / AXE_PATH ile modul yollari degistirilebilir)
const path = require('node:path');
const fs = require('node:fs');
const { chromium } = require(process.env.PW_PATH ?? 'C:/Users/USER/belge-sozlesme-platformu/node_modules/playwright');
const AXE = fs.readFileSync(process.env.AXE_PATH ?? 'C:/Users/USER/ofis360/node_modules/axe-core/axe.min.js', 'utf8');
const APP = process.env.OFIS360_UI ?? 'http://localhost:4211';
const API = process.env.OFIS360_BASE ?? 'http://localhost:5211/api';
const OUT = path.join(__dirname, '..', 'results');
const SHOTS = path.join(OUT, 'screenshots');
fs.mkdirSync(SHOTS, { recursive: true });

const rows = [];
function add(r) { rows.push(r); console.log(`[${r.status === 'Geçti' ? 'PASS' : r.status === 'Kaldı' ? 'FAIL' : r.status}] ${r.id} ${r.scenario} -> ${r.actual}`); }
const check = (id, meta, ok, actual, evidence = '') => add({ id, ...meta, actual, status: ok ? 'Geçti' : 'Kaldı', evidence });

async function api(method, p, body, cookie) {
  const r = await fetch(API + p, { method, headers: { 'content-type': 'application/json', ...(cookie ? { cookie } : {}) }, body: body ? JSON.stringify(body) : undefined });
  const text = await r.text();
  return { status: r.status, json: text ? JSON.parse(text) : null, cookie: (r.headers.get('set-cookie') ?? '').split(';')[0] };
}
async function sessionFor(userId) { return (await api('POST', '/auth/login', { userId })).cookie; }
async function contextWith(browser, cookie, viewport) {
  const ctx = await browser.newContext({ viewport, locale: 'tr-TR', timezoneId: 'Europe/Istanbul' });
  if (cookie) { const [name, value] = cookie.split('='); await ctx.addCookies([{ name, value, domain: 'localhost', path: '/', httpOnly: true, secure: true, sameSite: 'Lax' }]); }
  return ctx;
}
function instrument(page) {
  const log = { console: [], failed: [], dialogs: 0 };
  page.on('console', (m) => { if (m.type() === 'error') log.console.push(m.text().slice(0, 160)); });
  page.on('pageerror', (e) => log.console.push('pageerror: ' + String(e).slice(0, 160)));
  page.on('response', (r) => { if (r.status() >= 500) log.failed.push(`${r.status()} ${r.url().replace(APP, '')}`); });
  page.on('requestfailed', (r) => log.failed.push(`FAILED ${r.url().replace(APP, '')} ${r.failure()?.errorText}`));
  page.on('dialog', async (d) => { log.dialogs++; await d.dismiss(); });
  return log;
}
async function axeScan(page) {
  await page.addScriptTag({ content: AXE });
  return page.evaluate(async () => {
    const r = await window.axe.run(document, { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'] } });
    return r.violations.map((v) => ({ id: v.id, impact: v.impact, nodes: v.nodes.length, help: v.help }));
  });
}
const overflow = (page) => page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
async function settle(page) { await page.waitForLoadState('networkidle', { timeout: 15000 }).catch(() => {}); await page.waitForTimeout(400); }

(async () => {
  const browser = await chromium.launch();
  const accounts = (await api('GET', '/auth/demo-accounts')).json;
  const emp = accounts.find((a) => a.role === 'EMPLOYEE');
  const mgr = accounts.find((a) => a.role === 'MANAGER');
  const adm = accounts.find((a) => a.role === 'ADMIN');
  const viewports = { mobil: { width: 375, height: 812 }, tablet: { width: 768, height: 1024 }, masaustu: { width: 1440, height: 900 } };

  // ---- UI-LOGIN: gercek tiklama ile giris + klavye ----
  {
    const ctx = await contextWith(browser, null, viewports.masaustu);
    const page = await ctx.newPage(); const log = instrument(page);
    await page.goto(APP + '/giris'); await settle(page);
    const tabs = [];
    for (let i = 0; i < 10; i++) {
      await page.keyboard.press('Tab');
      tabs.push(await page.evaluate(() => {
        const el = document.activeElement; if (!el || el === document.body) return 'body (sayfa sonu)';
        const s = getComputedStyle(el); const lbl = el.closest('label');
        const ring = s.outlineStyle !== 'none' && s.outlineWidth !== '0px' || s.boxShadow !== 'none' || (lbl && getComputedStyle(lbl).boxShadow !== 'none');
        return `${el.tagName.toLowerCase()}${el.getAttribute('type') ? '[' + el.getAttribute('type') + ']' : ''}:${(el.getAttribute('aria-label') || (el.type === 'radio' ? (el.closest('label')?.innerText || '') : el.textContent) || '').trim().replace(/\s+/g, ' ').slice(0, 22)}${ring ? '' : '(odak görünmez?)'}`;
      }));
    }
    await page.keyboard.press('ArrowDown'); // radyo grubunda gezinme
    const a11y = await axeScan(page);
    await page.screenshot({ path: path.join(SHOTS, 'giris-masaustu.png') });
    check('UI-A11Y-01', { area: 'Giriş', role: 'Anonim', type: 'Erişilebilirlik', scenario: 'Giriş ekranı axe-core 4.13 WCAG 2.1 A/AA taraması', expected: '0 ihlal' }, a11y.length === 0, a11y.map((v) => `${v.id}(${v.impact},${v.nodes})`).join(', ') || '0 ihlal');
    add({ id: 'UI-KBD-01', area: 'Giriş', role: 'Anonim', type: 'Erişilebilirlik', scenario: 'Klavye ile odak sırası (10 × Tab)', expected: 'Mantıklı sıra, odak görünür', actual: tabs.join(' → '), status: tabs.some((t) => t.includes('odak görünmez')) ? 'Kaldı' : 'Geçti' });
    await page.locator('label', { hasText: emp.fullName }).click();
    await page.getByRole('button', { name: /olarak devam et/ }).click();
    await page.waitForURL(/\/panel/, { timeout: 10000 }).catch(() => {});
    await settle(page);
    check('UI-LOGIN-01', { area: 'Giriş', role: 'Çalışan', type: 'Uçtan uca/UI', scenario: 'Demo hesabı seçip giriş → çalışan paneli', expected: '/panel, konsol hatası yok' }, /\/panel/.test(page.url()) && log.console.length === 0, `url=${page.url().replace(APP, '')}, konsol=${log.console.length} ${log.console[0] ?? ''}`);
    // cikis + geri tusu
    const logout = page.getByRole('button', { name: /Çıkış/i }).first();
    if (await logout.count()) { await logout.click(); } else { await page.evaluate(() => fetch('/api/auth/logout', { method: 'POST' })); await page.goto(APP + '/giris'); }
    await page.waitForURL(/\/giris/, { timeout: 8000 }).catch(() => {});
    await page.goBack(); await settle(page);
    const leaked = await page.locator('text=/Rezervasyonlarım|Bugün/').count();
    check('UI-SESS-01', { area: 'Oturum', role: 'Çalışan', type: 'Uçtan uca/UI', scenario: 'Çıkıştan sonra geri tuşu korumalı sayfayı göstermez', expected: '/giris’e yönlenir' }, /\/giris/.test(page.url()), `geri sonrası url=${page.url().replace(APP, '')} (korumalı metin sayısı=${leaked})`);
    await ctx.close();
  }

  // ---- UI-PAGES: rol x ekran x boyut ----
  const sessions = { emp: await sessionFor(emp.id), mgr: await sessionFor(mgr.id), adm: await sessionFor(adm.id) };
  const pages = {
    emp: ['/panel', '/panel/masa', '/panel/toplanti', '/panel/rezervasyonlarim', '/panel/kullanimim', '/panel/bildirimler', '/panel/profil'],
    mgr: ['/yonetim'],
    adm: ['/admin', '/admin/katlar', '/admin/masalar', '/admin/odalar', '/admin/rezervasyonlar', '/admin/kullanicilar', '/admin/kurallar', '/admin/kapatmalar', '/admin/audit', '/admin/ayarlar'],
  };
  const roleName = { emp: 'Çalışan', mgr: 'Yönetici', adm: 'Admin' };
  const a11ySummary = {}; const perPage = [];
  for (const [vpName, vp] of Object.entries(viewports)) {
    for (const [role, list] of Object.entries(pages)) {
      const ctx = await contextWith(browser, sessions[role], vp);
      const page = await ctx.newPage(); const log = instrument(page);
      for (const p of list) {
        const t0 = Date.now();
        await page.goto(APP + p); await settle(page);
        const ms = Date.now() - t0;
        const ov = await overflow(page);
        const redirected = !page.url().endsWith(p);
        let a11y = [];
        if (vpName !== 'tablet') { a11y = await axeScan(page); for (const v of a11y) { a11ySummary[v.id] ??= { impact: v.impact, help: v.help, pages: new Set() }; a11ySummary[v.id].pages.add(`${p}@${vpName}`); } }
        if (vpName === 'mobil' || p === '/panel/masa' || p === '/admin/katlar') await page.screenshot({ path: path.join(SHOTS, `${p.replace(/\//g, '_').slice(1)}-${vpName}.png`), fullPage: false });
        perPage.push({ vp: vpName, role, p, ms, ov, redirected, a11y: a11y.length, consoleErr: log.console.length, failed: log.failed.length });
      }
      const mine = perPage.filter((x) => x.vp === vpName && x.role === role);
      check(`UI-PAGE-${role}-${vpName}`, { area: 'Ekranlar', role: roleName[role], type: 'Uçtan uca/UI', scenario: `${roleName[role]} ${list.length} ekran, ${vpName} ${vp.width}×${vp.height}: açılış, yatay taşma, konsol/ağ hatası`, expected: 'yönlenme yok, taşma 0px, hata 0' },
        mine.every((x) => !x.redirected && x.ov <= 0) && log.console.length === 0 && log.failed.length === 0,
        `taşma>0: ${mine.filter((x) => x.ov > 0).map((x) => `${x.p}(${x.ov}px)`).join(',') || '-'}; yönlenen: ${mine.filter((x) => x.redirected).map((x) => x.p).join(',') || '-'}; konsol=${log.console.length} ${log.console.slice(0, 2).join(' | ')}; ağ=${log.failed.length} ${log.failed.slice(0, 2).join(' | ')}; maks açılış=${Math.max(...mine.map((x) => x.ms))}ms`);
      await ctx.close();
    }
  }
  const a11yList = Object.entries(a11ySummary).map(([id, v]) => `${id}[${v.impact}] ${v.pages.size} ekran`);
  add({ id: 'UI-A11Y-02', area: 'Ekranlar', role: 'Tümü', type: 'Erişilebilirlik', scenario: `axe-core WCAG 2.1 A/AA — ${Object.values(pages).flat().length} ekran × (mobil, masaüstü)`, expected: '0 ihlal', actual: a11yList.join('; ') || '0 ihlal', status: a11yList.length ? 'Kaldı' : 'Geçti', evidence: 'results/ui-a11y.json' });
  fs.writeFileSync(path.join(OUT, 'ui-a11y.json'), JSON.stringify(Object.fromEntries(Object.entries(a11ySummary).map(([k, v]) => [k, { ...v, pages: [...v.pages] }])), null, 2));
  fs.writeFileSync(path.join(OUT, 'ui-pages.json'), JSON.stringify(perPage, null, 2));

  // ---- UI-XSS: saklanan yuk metin olarak gorunur, betik calismaz ----
  {
    const ctx = await contextWith(browser, sessions.emp, viewports.masaustu);
    const page = await ctx.newPage(); const log = instrument(page);
    const days = []; for (let o = 1; days.length < 8; o++) { const k = new Intl.DateTimeFormat('en-CA', { timeZone: 'Europe/Istanbul' }).format(new Date(Date.now() + o * 864e5)); const wd = new Date(k + 'T12:00:00Z').getUTCDay(); if (wd && wd !== 6) days.push(k); }
    let created = null;
    for (const day of days) {
      const fl = (await api('GET', `/floors/summaries?date=${day}`, null, sessions.emp)).json.filter((f) => f.reservable);
      const plan = (await api('GET', `/floors/${fl[0].id}/plan?date=${day}`, null, sessions.emp)).json;
      const d = plan.desks.find((x) => x.selectable);
      if (!d) continue;
      const r = await api('POST', '/desk-reservations', { deskId: d.id, date: day, note: '<img src=x onerror=alert(1)><b>kalın</b>' }, sessions.emp);
      if (r.status === 200) { created = r.json.id; break; }
    }
    await page.goto(APP + '/panel/rezervasyonlarim'); await settle(page);
    const literal = await page.locator('text=<img src=x').count();
    const injected = await page.locator('img[src="x"]').count();
    check('UI-XSS-01', { area: 'Rezervasyon', role: 'Çalışan', type: 'Güvenlik/UI', scenario: 'Notta saklanan HTML/JS yükü arayüzde çalışmaz', expected: 'dialog yok, <img> DOM\'a eklenmez' }, !!created && log.dialogs === 0 && injected === 0,
      `rezervasyon=${created ? 'oluştu' : 'oluşamadı'}, dialog=${log.dialogs}, enjekte img=${injected}, metin olarak görünen=${literal}`);
    if (created) await api('POST', `/desk-reservations/${created}/cancel`, { reason: 'xss testi' }, sessions.emp);
    await ctx.close();
  }

  // ---- UI-ERR: sunucu hatasi, ag kesintisi, bos durum ----
  {
    const ctx = await contextWith(browser, sessions.emp, viewports.masaustu);
    const page = await ctx.newPage(); instrument(page);
    await page.route('**/api/panel/home', (r) => r.fulfill({ status: 500, contentType: 'application/json', body: JSON.stringify({ error: { code: 'INTERNAL_ERROR', message: 'Beklenmeyen sunucu hatası.' } }) }));
    await page.goto(APP + '/panel'); await settle(page);
    const t500 = (await page.locator('main').innerText().catch(() => '')).slice(0, 300);
    const has500 = /hata|ulaşılamadı|tekrar dene|yüklenemedi/i.test(t500);
    await page.screenshot({ path: path.join(SHOTS, 'hata-500-panel.png') });
    await page.unroute('**/api/panel/home');
    await page.route('**/api/me/reservations', (r) => r.abort('internetdisconnected'));
    await page.goto(APP + '/panel/rezervasyonlarim'); await settle(page);
    const tNet = (await page.locator('main').innerText().catch(() => '')).slice(0, 300);
    const hasNet = /hata|ulaşılamadı|bağlantı|tekrar dene|yüklenemedi/i.test(tNet);
    await page.screenshot({ path: path.join(SHOTS, 'hata-ag-rezervasyonlarim.png') });
    const retry = await page.getByRole('button', { name: /tekrar|yeniden/i }).count();
    check('UI-ERR-01', { area: 'Hata durumları', role: 'Çalışan', type: 'Dayanıklılık/UI', scenario: 'Sunucu 500 ve ağ kesintisinde kullanıcıya anlaşılır mesaj (boş ekran değil)', expected: 'Hata mesajı + yeniden dene' },
      has500 && hasNet, `500: "${t500.replace(/\s+/g, ' ').slice(0, 90)}"; ağ: "${tNet.replace(/\s+/g, ' ').slice(0, 90)}"; yeniden dene düğmesi=${retry}`);
    await ctx.close();
  }
  {
    const fresh = await api('POST', '/admin/users', { fullName: `UI Boş Durum ${Date.now() % 1e5}`, email: `ui.bos.${Date.now()}@ofis360.local`, title: 'Uzman', role: 'EMPLOYEE', departmentId: emp.departmentId }, sessions.adm);
    const ctx = await contextWith(browser, await sessionFor(fresh.json.id), viewports.mobil);
    const page = await ctx.newPage(); instrument(page);
    await page.goto(APP + '/panel/rezervasyonlarim'); await settle(page);
    const t = (await page.locator('main').innerText()).replace(/\s+/g, ' ');
    await page.screenshot({ path: path.join(SHOTS, 'bos-durum-rezervasyonlarim-mobil.png') });
    check('UI-EMPTY-01', { area: 'Boş durum', role: 'Çalışan (yeni)', type: 'Kullanılabilirlik/UI', scenario: 'Hiç rezervasyonu olmayan kullanıcı Rezervasyonlarım (mobil)', expected: 'Açıklayıcı boş durum + yönlendirme' }, /henüz|yok|bulunmuyor/i.test(t), t.slice(0, 160));
    await ctx.close();
    await api('PUT', `/admin/users/${fresh.json.id}`, { active: false }, sessions.adm);
  }

  // ---- UI-ROLE: calisan yonetim/admin URL'lerine girerse ----
  {
    const ctx = await contextWith(browser, sessions.emp, viewports.masaustu);
    const page = await ctx.newPage(); instrument(page);
    const out = [];
    for (const p of ['/yonetim', '/admin', '/admin/kullanicilar']) { await page.goto(APP + p); await settle(page); out.push(`${p}→${page.url().replace(APP, '')}`); }
    check('UI-ROLE-01', { area: 'Yetki', role: 'Çalışan', type: 'Güvenlik/UI', scenario: 'Çalışan yönetim/admin URL\'lerini doğrudan açar', expected: '/yetkisiz veya panele yönlenir' }, out.every((s) => /→\/(yetkisiz|panel)/.test(s)), out.join(', '));
    await ctx.close();
  }

  await browser.close();
  fs.writeFileSync(path.join(OUT, 'ui.json'), JSON.stringify({ app: APP, browser: 'chromium (Playwright 1.49.1)', ranAt: new Date().toISOString(), rows }, null, 2));
  const s = rows.reduce((a, r) => ((a[r.status] = (a[r.status] ?? 0) + 1), a), {});
  console.log('\nui:', JSON.stringify(s));
})().catch((e) => { console.error(e); process.exit(1); });
