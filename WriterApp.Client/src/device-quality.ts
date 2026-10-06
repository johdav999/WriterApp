import { Extension, type Editor } from "@tiptap/core";
import { Plugin, PluginKey, TextSelection } from "@tiptap/pm/state";
import { Decoration, DecorationSet } from "@tiptap/pm/view";
import type { Node } from "@tiptap/pm/model";

export type QualityHighlight = { issueKey: string; from: number; to: number; expectedText: string; severity: string };
export const qualityBlockSeparator = "\n\n";
const key = new PluginKey<DecorationSet>("deviceQuality");
export function qualityRange(doc: Node, item: QualityHighlight, separator = qualityBlockSeparator) {
    const plain = doc.textBetween(0, doc.content.size, separator, "\n");
    if (!Number.isSafeInteger(item.from) || !Number.isSafeInteger(item.to) || item.from < 0 || item.to <= item.from
        || item.to > plain.length || !item.expectedText || plain.slice(item.from, item.to) !== item.expectedText) return null;
    let from: number | undefined, to: number | undefined;
    doc.descendants((node, pos) => {
        if (!node.isText) return;
        const prefix = doc.textBetween(0, pos, separator, "\n").length;
        if (item.from >= prefix && item.from < prefix + node.nodeSize) from = pos + item.from - prefix;
        if (item.to > prefix && item.to <= prefix + node.nodeSize) to = pos + item.to - prefix;
    });
    return from !== undefined && to !== undefined && doc.textBetween(from, to, separator, "\n") === item.expectedText
        ? { from, to } : null;
}
export const qualityExtension = Extension.create({
    name: "deviceQuality",
    addProseMirrorPlugins() {
        return [new Plugin({
            key,
            state: {
                init: () => DecorationSet.empty,
                apply(tr, previous) { return tr.getMeta(key) ?? (tr.docChanged ? DecorationSet.empty : previous); }
            },
            props: { decorations(state) { return key.getState(state); } }
        })];
    }
});
export function setQualityHighlights(editor: Editor, plain: string, items: QualityHighlight[], selected: string | null) {
    const decorations: Decoration[] = [];
    if (editor.state.doc.textBetween(0, editor.state.doc.content.size, qualityBlockSeparator, "\n") === plain)
        for (const item of items.slice(0, 200)) {
            const range = qualityRange(editor.state.doc, item);
            if (!range) continue;
            const severity = ["info", "warning", "error"].includes(item.severity) ? item.severity : "info";
            decorations.push(Decoration.inline(range.from, range.to, {
                class: "wa-quality wa-quality-" + severity + (item.issueKey === selected ? " wa-quality-selected" : ""),
                "data-quality-key": item.issueKey
            }));
        }
    editor.view.dispatch(editor.state.tr.setMeta(key, DecorationSet.create(editor.state.doc, decorations)));
}
export function navigateToQuality(editor: Editor, plain: string, item: QualityHighlight) {
    if (editor.state.doc.textBetween(0, editor.state.doc.content.size, qualityBlockSeparator, "\n") !== plain) return false;
    const range = qualityRange(editor.state.doc, item);
    if (!range) return false;
    editor.view.dispatch(editor.state.tr.setSelection(TextSelection.create(editor.state.doc, range.from, range.to)).scrollIntoView());
    editor.view.focus();
    return true;
}

