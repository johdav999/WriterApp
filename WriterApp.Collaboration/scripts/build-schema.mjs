import { build } from '../../WriterApp.Client/node_modules/vite/dist/node/index.js';
import { fileURLToPath } from 'node:url';
import { mkdir, writeFile, rename, unlink } from 'node:fs/promises';
import { randomUUID } from 'node:crypto';

const root = fileURLToPath(new URL('../', import.meta.url));
const result = await build({ root, configFile: false, ssr: { noExternal: true, external: ['yjs', 'y-protocols'] },
  build: { ssr: '../WriterApp.Client/src/collaboration/server-schema.ts', target: 'node24', outDir: 'src/generated',
    write: false, emptyOutDir: false, minify: false, rollupOptions: { output: { entryFileNames: 'schema.mjs' } } } });
const output = (Array.isArray(result) ? result : [result]).flatMap(bundle => bundle.output);
if (output.length !== 1 || output[0].type !== 'chunk' || output[0].fileName !== 'schema.mjs') throw Error('Unexpected shared schema output');
const destination = new URL('../src/generated/schema.mjs', import.meta.url), temporary = new URL('../src/generated/schema.' + randomUUID() + '.tmp', import.meta.url);
await mkdir(new URL('../src/generated/', import.meta.url), { recursive: true });
try { await writeFile(temporary, output[0].code, { flag: 'wx' }); await rename(temporary, destination); }
finally { await unlink(temporary).catch(error => { if (error.code !== 'ENOENT') throw error; }); }
