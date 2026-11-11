// Production Angular paketini (web/dist/web/browser) sunar, /api isteklerini izole perf API'sine (5211) iletir.
// Kullanim: node serve-prod.mjs   -> http://localhost:4211  (yalnizca yerel test)
import http from 'node:http';
import { readFile } from 'node:fs/promises';
import { join, extname, normalize } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = fileURLToPath(new URL('../../../web/dist/web/browser/', import.meta.url));
const API = { host: 'localhost', port: Number(process.env.API_PORT ?? 5211) };
const PORT = Number(process.env.PORT ?? 4211);
const types = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript', '.css': 'text/css', '.svg': 'image/svg+xml', '.ico': 'image/x-icon', '.png': 'image/png', '.woff2': 'font/woff2', '.json': 'application/json' };

http.createServer(async (req, res) => {
  if (req.url.startsWith('/api')) {
    const p = http.request({ ...API, path: req.url, method: req.method, headers: { ...req.headers, host: `${API.host}:${API.port}` } }, (r) => { res.writeHead(r.statusCode, r.headers); r.pipe(res); });
    p.on('error', () => { res.writeHead(502); res.end(); });
    req.pipe(p);
    return;
  }
  const clean = normalize(decodeURIComponent(req.url.split('?')[0])).replace(/^([\\/]\.\.)+/, '');
  let file = join(ROOT, clean);
  if (!file.startsWith(ROOT)) { res.writeHead(403); return res.end(); }
  try {
    const data = await readFile(file);
    res.writeHead(200, { 'content-type': types[extname(file)] ?? 'application/octet-stream' });
    res.end(data);
  } catch {
    const html = await readFile(join(ROOT, 'index.html'));
    res.writeHead(200, { 'content-type': types['.html'] });
    res.end(html);
  }
}).listen(PORT, 'localhost', () => console.log(`prod paket http://localhost:${PORT} -> api :${API.port}`));
