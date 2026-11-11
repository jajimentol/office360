// Ortak yardimcilar: cerezli istemci, sonuc kaydi. Yalnizca yerel/izole ortam icin (BASE varsayilani perf ornegi 5211).
import { writeFileSync, mkdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

export const BASE = process.env.OFIS360_BASE ?? 'http://localhost:5211/api';
export const RESULTS_DIR = join(dirname(fileURLToPath(import.meta.url)), '..', 'results');

export class Client {
  constructor(cookie = null) { this.cookie = cookie; }
  async req(method, path, body, extraHeaders = {}) {
    const headers = { ...extraHeaders };
    if (this.cookie) headers.cookie = this.cookie;
    let payload;
    if (body !== undefined) {
      if (typeof body === 'string') payload = body;
      else { payload = JSON.stringify(body); headers['content-type'] ??= 'application/json'; }
    }
    const t0 = performance.now();
    const res = await fetch(BASE + path, { method, headers, body: payload, redirect: 'manual' });
    const text = await res.text(); // govdeyi her zaman oku (baglanti havuzu)
    const ms = performance.now() - t0;
    const setCookie = res.headers.get('set-cookie');
    if (setCookie && setCookie.startsWith('ofis360_session=')) {
      const v = setCookie.split(';')[0];
      this.cookie = v === 'ofis360_session=' ? null : v;
    }
    let json = null;
    try { json = text ? JSON.parse(text) : null; } catch { /* metin */ }
    return { status: res.status, headers: res.headers, text, json, ms };
  }
  get(p, h) { return this.req('GET', p, undefined, h); }
  post(p, b, h) { return this.req('POST', p, b ?? {}, h); }
  put(p, b, h) { return this.req('PUT', p, b ?? {}, h); }
  del(p, h) { return this.req('DELETE', p, undefined, h); }
}

export async function loginAs(userId) {
  const c = new Client();
  const r = await c.post('/auth/login', { userId });
  if (r.status !== 200) throw new Error(`login ${userId} -> ${r.status} ${r.text}`);
  return c;
}

export async function demoAccounts() {
  const r = await new Client().get('/auth/demo-accounts');
  return r.json;
}

export function mask(s) {
  return String(s).replace(/(Password=)[^;"]+/gi, '$1****').replace(/(ofis360_session=)([0-9a-z]{4})[0-9a-z]+/gi, '$1$2…');
}

export class Recorder {
  constructor(name) { this.name = name; this.rows = []; }
  add(row) {
    // row: {id, area, role, type, scenario, expected, actual, status: Geçti|Kaldı|Engellendi|Çalıştırılmadı|Uygulanamaz, evidence}
    row.actual = mask(row.actual ?? ''); row.evidence = mask(row.evidence ?? '');
    this.rows.push(row);
    const icon = { 'Geçti': 'PASS', 'Kaldı': 'FAIL', 'Engellendi': 'BLOCK', 'Çalıştırılmadı': 'SKIP', 'Uygulanamaz': 'N/A' }[row.status] ?? row.status;
    console.log(`[${icon}] ${row.id} ${row.scenario} -> ${row.actual}`);
  }
  check(id, meta, ok, actual, evidence = '') {
    this.add({ id, ...meta, actual, status: ok ? 'Geçti' : 'Kaldı', evidence });
  }
  save() {
    mkdirSync(RESULTS_DIR, { recursive: true });
    const file = join(RESULTS_DIR, `${this.name}.json`);
    writeFileSync(file, JSON.stringify({ base: BASE, ranAt: new Date().toISOString(), rows: this.rows }, null, 2));
    const s = this.rows.reduce((a, r) => ((a[r.status] = (a[r.status] ?? 0) + 1), a), {});
    console.log(`\n${this.name}: ${JSON.stringify(s)} -> ${file}`);
  }
}

export const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

/** Istanbul saatine gore YYYY-MM-DD (uygulama is gunu anahtari). */
export function istanbulDayKey(offsetDays = 0) {
  const d = new Date(Date.now() + offsetDays * 86400000);
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'Europe/Istanbul' }).format(d);
}

import { execFileSync } from 'node:child_process';
/** Perf semasinda SQL (docker exec sqlplus, SYSTEM; yalnizca izole OFIS360_PERF semasi). Satirlari '|' ayracli dondurur. */
export function sql(statement, container = process.env.OFIS360_ORACLE_CONTAINER ?? 'ofis360-oracle') {
  // Yerel (bequeath) baglanti: dinleyici/surec limiti doldugunda da calisir (bkz. ORA-12516 bulgusu).
  const script = `SET HEADING OFF FEEDBACK OFF PAGESIZE 0 LINESIZE 32767 TRIMSPOOL ON COLSEP '|'\nWHENEVER SQLERROR EXIT 1\nALTER SESSION SET CONTAINER = FREEPDB1;\n${statement}\nEXIT\n`;
  const out = execFileSync('docker', ['exec', '-i', container, 'bash', '-c', 'sqlplus -s / as sysdba'], { input: script, encoding: 'utf8' });
  return out.split(/\r?\n/).map((l) => l.trim()).filter(Boolean).map((l) => l.split('|').map((c) => c.trim()));
}
