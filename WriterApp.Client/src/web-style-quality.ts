import type { Editor } from "@tiptap/core";
import type { Node } from "@tiptap/pm/model";
import { targetedRevisionTransaction } from "./targeted-revision";

type Edit = { original: string; replacement: string };
type Source = { html: string; plain: string; from: number; text: string };
function project(doc: Node) {
    let plain = "", first = true;
    const runs: { start: number; end: number; pos: number }[] = [];
    // Match the saved HTML mapper: at most two adjacent separators and no trailing ones.
    const breaks = (count: number) => {
        const trailing = plain.endsWith("\n\n") ? 2 : plain.endsWith("\n") ? 1 : 0;
        plain += "\n".repeat(Math.min(count, 2 - trailing));
    };
    doc.descendants((block, pos) => {
        if (!block.isTextblock) return;
        if (!first) breaks(2);
        first = false;
        block.descendants((node, offset) => {
            if (node.isText) {
                runs.push({ start: plain.length, end: plain.length + node.text!.length, pos: pos + 1 + offset });
                plain += node.text;
            }
            else if (node.type.name === "hardBreak") breaks(1);
        });
        return false;
    });
    return { plain: plain.replace(/\n+$/, ""), runs };
}
export function captureStyleQuality(editor: Editor, wholePage: boolean): Source {
    if (!editor?.view) throw new Error("The editor is not ready.");
    const { plain, runs } = project(editor.state.doc);
    let from = 0, end = plain.length;
    if (!wholePage) {
        const selection = editor.state.selection;
        if (selection.empty) throw new Error("Select current writing before requesting a selection review.");
        const selected = runs.filter(r => r.pos < selection.to && r.pos + r.end - r.start > selection.from);
        if (!selected.length) throw new Error("Select writing rather than embedded content.");
        const first = selected[0], last = selected[selected.length - 1];
        from = first.start + Math.max(0, selection.from - first.pos);
        end = last.start + Math.min(last.end - last.start, selection.to - last.pos);
    }
    const text = plain.slice(from, end), html = editor.getHTML();
    if (!text.trim() || plain.length > 100000 || html.length > 750000)
        throw new Error("Review nonempty writing on a page of up to 100,000 characters.");
    return { html, plain, from, text };
}
function transaction(editor: Editor, source: Source, edits: Edit[]) {
    const { plain, runs } = project(editor.state.doc);
    if (editor.getHTML() !== source.html || plain !== source.plain || !Number.isSafeInteger(source.from) || source.from < 0
        || !source.text?.trim() || plain.slice(source.from, source.from + source.text.length) !== source.text
        || plain.length > 100000 || !Array.isArray(edits) || edits.length > 24)
        throw new Error("The style review no longer matches the writing. Run it again.");
    const spans: { start: number; end: number; edit: Edit }[] = [];
    for (const edit of edits) {
        if (!edit?.original?.trim() || !edit.replacement?.trim() || edit.original === edit.replacement || edit.replacement.length > 20000
            || /[\r\n]/.test(edit.original) || /[\r\n]/.test(edit.replacement))
            throw new Error("Invalid reviewed style change.");
        const start = source.text.indexOf(edit.original), end = start + edit.original.length;
        if (start < 0 || source.text.indexOf(edit.original, start + 1) >= 0 || spans.some(s => start < s.end && end > s.start))
            throw new Error("Ambiguous or overlapping style changes. Review a shorter selection.");
        spans.push({ start, end, edit });
    }
    const tr = editor.state.tr;
    let revised = source.text;
    for (const span of spans.sort((a, b) => b.start - a.start)) {
        const a = source.from + span.start, b = source.from + span.end;
        const first = runs.find(r => a >= r.start && a < r.end), last = runs.find(r => b > r.start && b <= r.end);
        if (!first || !last) throw new Error("This change cannot be mapped to the captured page.");
        const patch = targetedRevisionTransaction(editor, first.pos + a - first.start, last.pos + b - last.start,
            span.edit.original, span.edit.replacement);
        for (const step of patch.steps) tr.step(step);
        revised = revised.slice(0, span.start) + span.edit.replacement + revised.slice(span.end);
    }
    if (project(tr.doc).plain !== plain.slice(0, source.from) + revised + plain.slice(source.from + source.text.length)
        || !editor.state.applyTransaction(tr).state.doc.eq(tr.doc))
        throw new Error("The revision could not preserve the surrounding writing and structure.");
    return tr;
}
export function previewStyleQuality(editor: Editor, source: Source, edits: Edit[]) {
    transaction(editor, source, edits); return true;
}
export function applyStyleQuality(editor: Editor, source: Source, edits: Edit[]) {
    if (!editor.isEditable || !edits.length) throw new Error("Select a change in an editable page before applying.");
    const tr = transaction(editor, source, edits);
    if (!tr.docChanged) throw new Error("No valid style changes selected.");
    // Every edit and the complete resulting document are validated before this single dispatch.
    editor.view.dispatch(tr.scrollIntoView());
    return editor.getHTML();
}
