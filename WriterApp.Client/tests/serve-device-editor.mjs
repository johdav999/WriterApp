import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';

// Serve only the test harness and the two shipped assets, never the repository or user documents.
const routes = new Map([
  ['/WriterApp.Client/tests/device-editor.html', [new URL('./device-editor.html', import.meta.url), 'text/html']],
  ['/WriterApp.Device.Shared/wwwroot/editor/device-editor.js', [new URL('../../WriterApp.Device.Shared/wwwroot/editor/device-editor.js', import.meta.url), 'text/javascript']],
  ['/WriterApp.Device.Shared/wwwroot/editor/device-editor.css', [new URL('../../WriterApp.Device.Shared/wwwroot/editor/device-editor.css', import.meta.url), 'text/css']]
]);
createServer(async (request, response) => {
  const route = routes.get(new URL(request.url, 'http://localhost').pathname);
  if (!route) { response.writeHead(404); response.end(); return; }
  try { const bytes = await readFile(route[0]); response.writeHead(200, { 'Content-Type': route[1], 'Cache-Control': 'no-store' }); response.end(bytes); }
  catch { response.writeHead(500); response.end('Run npm run build first.'); }
}).listen(5179, '127.0.0.1', () => console.log('Open http://127.0.0.1:5179/WriterApp.Client/tests/device-editor.html'));
