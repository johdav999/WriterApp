import type { Editor } from "@tiptap/core";
import { Fragment, Slice } from "@tiptap/pm/model";

const word = (text: string) => /[\p{L}\p{N}\p{M}_]/u.test(text);
const before = (text: string, at: number) => text.slice(0, at).match(/.$/u)?.[0] ?? "";
const after = (text: string, at: number) => at < text.length ? String.fromCodePoint(text.codePointAt(at)!) : "";
function boundaries(text: string): Set<number> {
    const result = new Set<number>([0, text.length]);
    // Both shipped editors run on engines with grapheme segmentation. Never split
    // surrogate pairs, combining sequences or joined emoji when assigning marks.
    const segmenter = new (Intl as any).Segmenter(undefined, { granularity: "grapheme" });
    for (const segment of segmenter.segment(text)) result.add(segment.index);
    return result;
}
function safeBoundary(text: string, at: number, graphemes: Set<number>) {
    return graphemes.has(at) && !(word(before(text, at)) && word(after(text, at)));
}
function validUnicode(text: string) {
    return !/[\u0000-\u0008\u000B\u000C\u000E-\u001F]/.test(text)
        && !/[\uD800-\uDBFF](?![\uDC00-\uDFFF])|(?<![\uD800-\uDBFF])[\uDC00-\uDFFF]/u.test(text);
}

// A schema-owned, undispatched transaction shared by the richer web schema and
// the device preview. The caller must bind the range to its checked source.
export function validateTargetedRevisionRange(editor: Editor, from: number, to: number, expected: string, separator = "\n\n") {
    const doc = editor.state.doc;
    if (!Number.isSafeInteger(from) || !Number.isSafeInteger(to) || from < 1 || to < from || to > doc.content.size
        || typeof expected !== "string" || !validUnicode(expected))
        throw new Error("Invalid targeted revision. Check the writing again.");
    const start = doc.resolve(from), end = doc.resolve(to);
    if (!start.parent.isTextblock || !start.sameParent(end) || /[\r\n]/.test(expected)
        || doc.textBetween(from, to, separator, "\n") !== expected)
        throw new Error("This passage spans blocks, line breaks or changed source. Review a smaller passage.");
    let embedded = false;
    doc.nodesBetween(from, to, node => { if (node.isLeaf && !node.isText) embedded = true; });
    if (embedded) throw new Error("This passage contains embedded content. Revise it manually.");
    const parentText = start.parent.textBetween(0, start.parent.content.size, "", "\uFFFC");
    const parentBoundaries = boundaries(parentText);
    if (!safeBoundary(parentText, start.parentOffset, parentBoundaries) || !safeBoundary(parentText, end.parentOffset, parentBoundaries))
        throw new Error("Select whole words and complete Unicode characters before applying this fix.");
}

export function targetedRevisionTransaction(editor: Editor, from: number, to: number, expected: string, proposed: string, separator = "\n\n") {
    validateTargetedRevisionRange(editor, from, to, expected, separator);
    if (typeof proposed !== "string" || expected === proposed || proposed.length > 20000
        || proposed.length > 0 && !proposed.trim() && from !== to || !validUnicode(proposed) || /\r/.test(proposed))
        throw new Error("Invalid targeted revision. Check the writing again.");
    const doc = editor.state.doc, start = doc.resolve(from);

    const plain = doc.textBetween(0, doc.content.size, separator, "\n");
    const offset = doc.textBetween(0, from, separator, "\n").length;
    const verify = (transaction: ReturnType<typeof editor.state.tr.replaceWith>) => {
        transaction.doc.check();
        if (transaction.doc.textBetween(0, transaction.doc.content.size, separator, "\n") !== plain.slice(0, offset) + proposed + plain.slice(offset + expected.length))
            throw new Error("The targeted revision could not preserve the surrounding writing.");
        return transaction;
    };
    // The deterministic paragraph fix may split one uniformly formatted paragraph.
    // Structural output stays host-owned and must reproduce the complete text.
    if (proposed.includes("\n")) {
        const lines = proposed.split(separator);
        const marks = doc.nodeAt(from)?.marks ?? start.marks();
        let uniform = true;
        doc.nodesBetween(from, to, node => { if (node.isText && JSON.stringify(node.marks) !== JSON.stringify(marks)) uniform = false; });
        if (start.parent.type.name !== "paragraph" || !uniform || lines.some(line => /[\r\n]/.test(line)))
            throw new Error("Review a uniformly formatted paragraph split manually to preserve its structure.");
        const replacement = new Slice(Fragment.fromArray(lines.map(line => start.parent.type.create(start.parent.attrs,
            line ? editor.schema.text(line, marks) : undefined))), 1, 1);
        return verify(editor.state.tr.replaceRange(from, to, replacement));
    }
    let prefix = 0, suffix = 0;
    while (prefix < expected.length && prefix < proposed.length && expected[prefix] === proposed[prefix]) prefix++;
    const originalBoundaries = boundaries(expected), proposedBoundaries = boundaries(proposed);
    while (prefix > 0 && (!safeBoundary(expected, prefix, originalBoundaries) || !safeBoundary(proposed, prefix, proposedBoundaries))) prefix--;
    while (suffix < expected.length - prefix && suffix < proposed.length - prefix
        && expected[expected.length - suffix - 1] === proposed[proposed.length - suffix - 1]) suffix++;
    while (suffix > 0 && (!safeBoundary(expected, expected.length - suffix, originalBoundaries)
        || !safeBoundary(proposed, proposed.length - suffix, proposedBoundaries))) suffix--;
    const changeFrom = from + prefix, changeTo = to - suffix;
    const text = proposed.slice(prefix, proposed.length - suffix);
    const marks = changeTo > changeFrom ? doc.nodeAt(changeFrom)?.marks ?? doc.resolve(changeFrom).marks() : doc.resolve(changeFrom).marks();
    const sameMarks = (candidate: any) => JSON.stringify(candidate) === JSON.stringify(marks);
    let uniform = true;
    doc.nodesBetween(changeFrom, changeTo, node => { if (node.isText && !sameMarks(node.marks)) uniform = false; });
    // Insertions at a formatting boundary have no unambiguous source run.
    if (text && changeFrom === changeTo) {
        const position = doc.resolve(changeFrom);
        if (position.nodeBefore?.isText && position.nodeAfter?.isText
            && JSON.stringify(position.nodeBefore.marks) !== JSON.stringify(position.nodeAfter.marks)) uniform = false;
    }
    if (text && !uniform) throw new Error("This changed wording spans mixed formatting. Review a smaller passage or revise it manually.");
    const transaction = editor.state.tr.replaceWith(changeFrom, changeTo, text ? editor.schema.text(text, marks) : Fragment.empty);
    return verify(transaction);
}
