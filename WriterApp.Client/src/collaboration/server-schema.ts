// Node's validator is bundled from the same schema/contract implementation as both editors.
import { getSchema } from "@tiptap/core";
import { prosemirrorJSONToYDoc, yDocToProsemirrorJSON } from "@tiptap/y-tiptap";
import { richExtensions } from "../editor-schema";
import { prepare, validateJson } from "../editor-content";
import { DOMSerializer } from "@tiptap/pm/model";
import { getSupport } from "./session";
import * as Y from "yjs";
import { validateDocumentRoots } from './document-roots';

const schema = getSchema(richExtensions(true));
export { getSupport };
export function parseSource(content: string, format: string, dom: Document) {
    if (!['Html', 'LegacyJson', 'LegacyText'].includes(format) || content.length > 16 * 1024 * 1024 || content.includes('<!--'))
        throw new Error('collaboration_source_unsupported');
    const json = prepare(content, format, dom); validateJson(json); const node = schema.nodeFromJSON(json); node.check();
    if (format === 'Html') {
        const original = dom.createElement('div'), rendered = dom.createElement('div'); original.innerHTML = content;
        rendered.appendChild(DOMSerializer.fromSchema(schema).serializeFragment(node.content, { document: dom }));
        // Contract v2 has cell widths, but no independent table/column style state. Refuse any such loss.
        for (const tag of ['table', 'col']) {
            const before = [...original.querySelectorAll(tag)], after = [...rendered.querySelectorAll(tag)];
            if (before.some((element, index) => element.hasAttribute('style') && (element as HTMLElement).style.cssText !== (after[index] as HTMLElement | undefined)?.style.cssText))
                throw new Error('collaboration_source_formatting_loss');
        }
        for (const element of original.querySelectorAll('[data-indent-level]')) {
            const margin = (element as HTMLElement).style.marginLeft;
            if (margin && Number.parseFloat(margin) !== Number(element.getAttribute('data-indent-level')) * 2) throw new Error('collaboration_source_formatting_loss');
        }
    }
    return json;
}
export function projectDoc(doc: Y.Doc, dom: Document) {
    validateCommittedDoc(doc);
    const json = yDocToProsemirrorJSON(doc, 'default'), node = schema.nodeFromJSON(json);
    const wrap = dom.createElement('div');
    wrap.appendChild(DOMSerializer.fromSchema(schema).serializeFragment(node.content, { document: dom }));
    // Round-trip the HTML with the very same schema before accepting a projection.
    if (!schema.nodeFromJSON(parseSource(wrap.innerHTML, 'Html', dom)).eq(node)) throw new Error('collaboration_projection_loss');
    return { html: wrap.innerHTML, json, text: node.textBetween(0, node.content.size, '\n') };
}
export function validateCommittedDoc(doc: Y.Doc) {
    validateDocumentRoots(doc);
    const json = yDocToProsemirrorJSON(doc, "default");
    validateJson(json); schema.nodeFromJSON(json).check();
}
export function createSeed(json: any) {
    validateJson(json); schema.nodeFromJSON(json).check();
    const doc = prosemirrorJSONToYDoc(schema, json, "default");
    try { validateCommittedDoc(doc); return Y.encodeStateAsUpdate(doc); } finally { doc.destroy(); }
}
