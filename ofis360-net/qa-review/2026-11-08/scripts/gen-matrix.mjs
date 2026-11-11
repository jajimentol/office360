// results/*.json (security, functional, integrity, resilience, ui) + elle eklenen satirlardan
// 02-test-kapsam-matrisi.md uretir. Calistirma: node gen-matrix.mjs
import { readFileSync, writeFileSync, existsSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');
const R = (f) => join(ROOT, 'results', f);
const suites = [
  ['security', 'Güvenlik (API)'], ['functional', 'Fonksiyonel, eşzamanlılık, veri bütünlüğü'], ['integrity', 'Yük sonrası veri bütünlüğü (DB)'],
  ['resilience', 'Dayanıklılık'], ['ui', 'Arayüz, erişilebilirlik'],
];
const esc = (s) => String(s ?? '').replace(/\|/g, '\\|').replace(/\r?\n/g, ' ');
let md = '# B. Test kapsamı matrisi\n\n';
md += 'Bu dosya `scripts/gen-matrix.mjs` ile `results/*.json` dosyalarından üretilir; elle düzenlenmez. ';
md += 'Durumlar: **Geçti / Kaldı / Engellendi / Çalıştırılmadı / Uygulanamaz**. "Kanıt" sütunu ilgili uç/sorgu/dosyayı, ham çıktı `results/` altındadır.\n\n';
const totals = {};
for (const [name, title] of suites) {
  if (!existsSync(R(`${name}.json`))) { md += `## ${title}\n\n_Çalıştırılmadı (sonuç dosyası yok)._\n\n`; continue; }
  const j = JSON.parse(readFileSync(R(`${name}.json`), 'utf8'));
  const c = j.rows.reduce((a, r) => ((a[r.status] = (a[r.status] ?? 0) + 1), a), {});
  for (const [k, v] of Object.entries(c)) totals[k] = (totals[k] ?? 0) + v;
  md += `## ${title}\n\nKoşu: ${j.ranAt} · hedef: \`${j.base ?? j.app}\` · ${Object.entries(c).map(([k, v]) => `${k}: ${v}`).join(', ')}\n\n`;
  md += '| ID | Modül/akış | Rol | Test türü | Senaryo | Beklenen | Gerçekleşen | Durum | Kanıt |\n|---|---|---|---|---|---|---|---|---|\n';
  for (const r of j.rows) md += `| ${r.id} | ${esc(r.area)} | ${esc(r.role)} | ${esc(r.type)} | ${esc(r.scenario)} | ${esc(r.expected)} | ${esc(r.actual).slice(0, 400)} | **${r.status}** | ${esc(r.evidence) || `results/${name}.json`} |\n`;
  md += '\n';
}
// Otomasyon disi / toplu satirlar
const extra = JSON.parse(readFileSync(join(ROOT, 'scripts', 'matrix-extra.json'), 'utf8'));
md += '## Diğer test grupları (birim/entegrasyon paketleri, yük, statik analiz, yürütülemeyenler)\n\n';
md += '| ID | Modül/akış | Rol | Test türü | Senaryo | Beklenen | Gerçekleşen | Durum | Kanıt |\n|---|---|---|---|---|---|---|---|---|\n';
for (const r of extra) { md += `| ${r.id} | ${esc(r.area)} | ${esc(r.role)} | ${esc(r.type)} | ${esc(r.scenario)} | ${esc(r.expected)} | ${esc(r.actual)} | **${r.status}** | ${esc(r.evidence)} |\n`; totals[r.status] = (totals[r.status] ?? 0) + 1; }
md += `\n## Toplam\n\n${Object.entries(totals).map(([k, v]) => `- ${k}: ${v}`).join('\n')}\n`;
writeFileSync(join(ROOT, '02-test-kapsam-matrisi.md'), md);
console.log('matris yazıldı', JSON.stringify(totals));
