// Kaynak izleyici: API sureci (CPU, calisma kumesi, is parcacigi, tutamac), Oracle konteyneri (docker stats),
// OFIS360_PERF oturum sayisi ve kilit bekleyen oturumlar. Kullanim: node monitor.mjs --name=S2 --seconds=330 --port=5211
import { execFileSync } from 'node:child_process';
import { writeFileSync, mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { RESULTS_DIR, sql } from './lib.mjs';

const arg = (k, d) => { const a = process.argv.find((x) => x.startsWith(`--${k}=`)); return a ? a.split('=')[1] : d; };
const NAME = arg('name', 'monitor');
const SECONDS = Number(arg('seconds', 120));
const PORT = arg('port', '5211');
const INTERVAL = 5000;

const ps = (cmd) => execFileSync('powershell', ['-NoProfile', '-Command', cmd], { encoding: 'utf8' }).trim();
const pid = ps(`(Get-NetTCPConnection -LocalPort ${PORT} -State Listen).OwningProcess`).split(/\s+/)[0];
const cores = Number(ps('(Get-CimInstance Win32_Processor).NumberOfLogicalProcessors'));
const samples = [];
let prevCpu = null; let prevT = null;
const t0 = Date.now();
while (Date.now() - t0 < SECONDS * 1000) {
  const tick = Date.now();
  try {
    const [cpuS, ws, threads, handles, freeMb] = ps(`$p=Get-Process -Id ${pid}; $o=Get-CimInstance Win32_OperatingSystem; "$($p.CPU)|$($p.WorkingSet64)|$($p.Threads.Count)|$($p.HandleCount)|$([math]::Round($o.FreePhysicalMemory/1024))"`).split('|');
    const cpu = Number(cpuS.replace(',', '.'));
    const apiCpuPct = prevCpu == null ? null : +((100 * (cpu - prevCpu)) / ((tick - prevT) / 1000) / cores).toFixed(1);
    prevCpu = cpu; prevT = tick;
    const ds = execFileSync('docker', ['stats', '--no-stream', '--format', '{{.CPUPerc}}|{{.MemUsage}}', 'ofis360-oracle'], { encoding: 'utf8' }).trim().split('|');
    const db = sql(`SELECT COUNT(*), SUM(CASE WHEN STATUS='ACTIVE' THEN 1 ELSE 0 END), SUM(CASE WHEN BLOCKING_SESSION IS NOT NULL THEN 1 ELSE 0 END) FROM V$SESSION WHERE USERNAME='OFIS360_PERF';`)[0];
    samples.push({ t: Math.round((tick - t0) / 1000), apiCpuPct, apiWsMb: Math.round(Number(ws) / 1048576), threads: Number(threads), handles: Number(handles), freeRamMb: Number(freeMb),
      oracleCpu: ds[0], oracleMem: ds[1], dbSessions: Number(db[0]), dbActive: Number(db[1]), dbBlocked: Number(db[2]) });
  } catch (e) { samples.push({ t: Math.round((tick - t0) / 1000), error: String(e).slice(0, 200) }); }
  const wait = INTERVAL - (Date.now() - tick);
  if (wait > 0) await new Promise((r) => setTimeout(r, wait));
}
mkdirSync(RESULTS_DIR, { recursive: true });
writeFileSync(join(RESULTS_DIR, `monitor-${NAME}.json`), JSON.stringify({ name: NAME, pid, cores, samples }, null, 2));
const ok = samples.filter((s) => !s.error);
const max = (k) => Math.max(...ok.map((s) => s[k] ?? 0));
console.log(`${NAME}: örnek=${ok.length} API CPU maks=%${max('apiCpuPct')} (${cores} mantıksal çekirdeğe göre) WS maks=${max('apiWsMb')}MB iş parçacığı maks=${max('threads')} tutamaç maks=${max('handles')} DB oturum maks=${max('dbSessions')} aktif maks=${max('dbActive')} bloklu maks=${max('dbBlocked')} boş RAM min=${Math.min(...ok.map((s) => s.freeRamMb))}MB`);
