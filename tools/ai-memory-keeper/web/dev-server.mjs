// Local preview of public/ with the same headers Firebase Hosting sends
// (read from firebase.json), so the CSP is tested too. No dependencies.
import http from 'node:http';
import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.join(here, 'public');
const port = Number(process.env.PORT || process.argv[2] || 5178);
const config = JSON.parse(await fs.readFile(path.join(here, 'firebase.json'), 'utf8'));
const headers = Object.fromEntries(config.hosting.headers[0].headers.map((h) => [h.key, h.value]));
const types = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.json': 'application/json; charset=utf-8',
  '.txt': 'text/plain; charset=utf-8',
  '.webp': 'image/webp',
  '.png': 'image/png',
};

http
  .createServer(async (req, res) => {
    const urlPath = decodeURIComponent(new URL(req.url, 'http://x').pathname);
    const file = path.join(root, urlPath.endsWith('/') ? `${urlPath}index.html` : urlPath);
    if (!file.startsWith(root)) return res.writeHead(403).end();
    try {
      const body = await fs.readFile(file);
      res.writeHead(200, { ...headers, 'Content-Type': types[path.extname(file)] || 'application/octet-stream' });
      res.end(body);
    } catch {
      res.writeHead(404, headers).end('Not found');
    }
  })
  .listen(port, '127.0.0.1', () => console.log(`Serving ${root} at http://127.0.0.1:${port}/`));
