// One bounded request per owned process. No scripts, resource loading, credentials or HTTP listener.
import { Window } from 'happy-dom';
import * as Y from 'yjs';
import { createHash } from 'node:crypto';
import { createSeed, getSupport, parseSource, projectDoc } from './generated/schema.mjs';
import { resolveAnchor } from './anchors.mjs';

export async function convert(request) {
  const window = new Window({ settings: { enableJavaScriptEvaluation: false, enableImageFileLoading: false, disableJavaScriptEvaluation: true, disableJavaScriptFileLoading: true,
    disableCSSFileLoading: true, disableIframePageLoading: true, disableComputedStyleRendering: true } });
  const doc = new Y.Doc();
  try {
    if (request.operation === 'seed') {
      const json = parseSource(request.content, request.format, window.document);
      Y.applyUpdate(doc, createSeed(json));
    } else if (['project', 'anchors'].includes(request.operation) && Array.isArray(request.updates) && request.updates.length > 0 && request.updates.length <= 4097) {
      let size = 0;
      for (const encoded of request.updates) {
        const bytes = Buffer.from(encoded, 'base64'); size += bytes.length;
        if (size > 16 * 1024 * 1024 || bytes.toString('base64') !== encoded) throw Error();
        Y.applyUpdate(doc, bytes);
      }
    } else throw Error();
    const projection = projectDoc(doc, window.document);
    if (request.operation === 'anchors') {
      if (!Array.isArray(request.anchors) || request.anchors.length > 200) throw Error();
      const support = await getSupport();
      if (request.expectedSupport && Object.keys(support).some(key => support[key] !== request.expectedSupport[key])) throw Error('collaboration_upgrade_required');
      return request.anchors.map(anchor => resolveAnchor(doc, anchor));
    }
    const state = Buffer.from(Y.encodeStateAsUpdate(doc));
    if (state.length > 16 * 1024 * 1024) throw Error();
    return { ...projection, support: await getSupport(), state: state.toString('base64'), sha256: createHash('sha256').update(state).digest('hex') };
  } finally { doc.destroy(); await window.happyDOM.close(); }
}

if (process.argv[1] && import.meta.url === (await import('node:url')).pathToFileURL(process.argv[1]).href) {
  let size = 0; const chunks = [];
  try {
    for await (const chunk of process.stdin) { size += chunk.length; if (size > 24 * 1024 * 1024) throw Error(); chunks.push(chunk); }
    process.stdout.write(JSON.stringify(await convert(JSON.parse(Buffer.concat(chunks).toString('utf8')))));
  } catch { process.stderr.write('collaboration_codec_rejected'); process.exitCode = 1; }
}
