import { Editor, Extension, getSchema } from "@tiptap/core";
import { DOMParser as ProseMirrorDOMParser, DOMSerializer, Fragment, Slice, type Node as ProseMirrorNode } from "@tiptap/pm/model";
import { TextSelection } from "@tiptap/pm/state";
import StarterKit from "@tiptap/starter-kit";
import { annotationExtension, setAnnotations, navigateToAnnotation, selectedAnnotationText, type TextAnnotation } from "./device-annotations";
import { qualityExtension, qualityRange, qualityBlockSeparator, setQualityHighlights, navigateToQuality, type QualityHighlight } from "./device-quality";
import { targetedRevisionTransaction } from "./targeted-revision";
import { consistencyPassageExtension, highlightConsistencyPassage } from "./device-consistency";
import TextAlign from "@tiptap/extension-text-align";
import { Table, TableRow, TableHeader, TableCell } from "@tiptap/extension-table";
import { EditorImage, IndentExtension } from "./editor-rich-extensions";
import { toggleBold, toggleItalic, setParagraph, setHeading, toggleBulletList,
    toggleOrderedList, toggleBlockquote, undo, redo } from "./tiptap-commands";
import "./device-editor.css";
import contract from "../../WriterApp.Shared/Editor/content-contract.json";

const tags = new Set(contract.tags.map(tag => tag.toUpperCase()));
const nodes = new Set(contract.nodes);
const marks = new Set(contract.marks);
const safeLink = (value: string) => {
    if (/[\u0000-\u001f\u007f]/.test(value)) return false;
    try { return contract.linkSchemes.includes(new URL(value.trim()).protocol.slice(0, -1)); }
    catch { return false; }
};
const safeImage = (value: string) => {
    if (/[\u0000-\u001f\u007f]/.test(value)) return false;
    if (/^data:image\/(png|jpeg|gif|webp);base64,[A-Za-z0-9+/]+={0,2}$/.test(value)) return true;
    if (value.startsWith("/") && !value.startsWith("//") && !value.includes("\\")) return true;
    try { const url = new URL(value); return ["http:", "https:"].includes(url.protocol) && !url.username && !url.password; }
    catch { return false; }
};
function safeStyle(tag: string, value: string) {
    return value.split(";").filter(part => part.trim()).every(part => {
        const pair = part.split(":").map(value => value.trim().toLowerCase());
        if (pair.length !== 2) return false;
        const [property, setting] = pair;
        if (/^(p|h[1-6])$/.test(tag)) return property === "text-align" && ["left", "center", "right", "justify"].includes(setting)
            || property === "margin-left" && /^(0|2|4|6|8|10|12|14|16)em$/.test(setting);
        if (["td", "th"].includes(tag)) return property === "text-align" && ["left", "center", "right", "justify"].includes(setting);
        return ["table", "col"].includes(tag) && ["width", "min-width"].includes(property) && /^\d+(\.\d+)?px$/.test(setting);
    });
}
const richExtensions = () => [StarterKit.configure({ link: { openOnClick: false, autolink: false, linkOnPaste: false }, trailingNode: false }),
    TextAlign.configure({ types: ["heading", "paragraph"] }), EditorImage, IndentExtension,
    Table.configure({ resizable: true, allowTableNodeSelection: true }), TableRow, TableHeader, TableCell];

function validateHtml(value: string) {
    const template = document.createElement("template");
    template.innerHTML = value;
    for (const element of template.content.querySelectorAll("*")) {
        if (!tags.has(element.tagName)) throw new Error("This page contains unsupported elements. Its saved source is unchanged.");
        const tag = element.localName;
        const children = [...element.children];
        const inline = contract.inlineTags;
        const invalidChildren = ((["p", "h1", "h2", "h3", "h4", "h5", "h6"].includes(tag) || inline.includes(tag)) && children.some(c => !inline.includes(c.localName)))
            || (["ul", "ol"].includes(tag) && (children.some(c => c.localName !== "li") || [...element.childNodes].some(c => c.nodeType === Node.TEXT_NODE && c.textContent?.trim())))
            || (tag === "li" && !["ul", "ol"].includes(element.parentElement?.localName || ""))
            || (tag === "pre" && (children.length !== 1 || children[0].localName !== "code" || [...element.childNodes].some(c => c.nodeType === Node.TEXT_NODE && c.textContent)))
            || (tag === "code" && (children.length > 0 || inline.includes(element.parentElement?.localName || "")));
        if (invalidChildren) throw new Error("This page has unsupported content structure. Its saved source is unchanged.");
        if (tag === "code" && element.attributes.length && element.parentElement?.localName !== "pre") throw new Error("Unsupported inline code attributes.");
        if (tag === "img" && !safeImage(element.getAttribute("src") || "")) throw new Error("Unsupported image address.");
        const parent = element.parentElement?.localName || "";
        if (tag === "table" && children.some(c => !["colgroup", "thead", "tbody", "tfoot", "tr"].includes(c.localName))
            || ["thead", "tbody", "tfoot"].includes(tag) && (parent !== "table" || children.some(c => c.localName !== "tr"))
            || tag === "tr" && (!["table", "thead", "tbody", "tfoot"].includes(parent) || !children.length || children.some(c => !["td", "th"].includes(c.localName)))
            || ["td", "th"].includes(tag) && parent !== "tr"
            || tag === "colgroup" && (parent !== "table" || children.some(c => c.localName !== "col"))
            || tag === "col" && parent !== "colgroup") throw new Error("Unsupported table structure.");
        for (const attr of element.attributes) {
            const allowed = (contract.attributes[element.localName] || []).includes(attr.name)
                && (element.tagName !== "OL" || /^-?\d+$/.test(attr.value) && Number.isSafeInteger(Number(attr.value)) && Number(attr.value) >= -2147483648 && Number(attr.value) <= 2147483647)
                && (element.tagName !== "CODE" || /^language-[\w-]+$/.test(attr.value))
                && (!["src", "data-asset-url"].includes(attr.name) || safeImage(attr.value))
                && (attr.name !== "style" || safeStyle(tag, attr.value))
                && (attr.name !== "data-indent-level" || /^[0-8]$/.test(attr.value))
                && (!["colspan", "rowspan", "width"].includes(attr.name) || /^\d+$/.test(attr.value) && Number(attr.value) > 0 && Number(attr.value) <= 10000)
                && (attr.name !== "colwidth" || attr.value.split(",").every(v => /^\d+$/.test(v) && Number(v) <= 10000))
                && (attr.name !== "align" || ["left", "center", "right", "justify"].includes(attr.value));
            if (!allowed) throw new Error("This page contains formatting this editor cannot preserve yet. Its saved source is unchanged.");
        }
        if (element.tagName === "A" && element.hasAttribute("href") && !safeLink(element.getAttribute("href")!))
            throw new Error("This page contains an unsupported link. Its saved source is unchanged.");
    }
    return value;
}

function validateJson(node: any): void {
    if (!node || !nodes.has(node.type)) throw new Error("This legacy document contains unsupported content.");
    if (Object.keys(node).some(key => !["type", "attrs", "content", "marks", "text"].includes(key))) throw new Error("Unsupported legacy metadata.");
    const permitted = contract.nodeAttributes[node.type] || [];
    if (Object.keys(node.attrs || {}).some(key => !permitted.includes(key))) throw new Error("Unsupported legacy attributes.");
    if (node.type === "heading" && !contract.headingLevels.includes(node.attrs?.level)) throw new Error("Unsupported heading level.");
    if (node.attrs?.start !== undefined && (!Number.isInteger(node.attrs.start) || node.attrs.start < -2147483648 || node.attrs.start > 2147483647)) throw new Error("Unsupported list start.");
    if (node.attrs?.language != null && !/^[\w-]+$/.test(node.attrs.language)) throw new Error("Unsupported code language.");
    for (const key of ["textAlign", "align"]) if (node.attrs?.[key] != null && !["left", "center", "right", "justify"].includes(node.attrs[key])) throw new Error("Unsupported alignment.");
    if (node.attrs?.indentLevel != null && (!Number.isInteger(node.attrs.indentLevel) || node.attrs.indentLevel < 0 || node.attrs.indentLevel > 8)) throw new Error("Unsupported indentation.");
    for (const key of ["colspan", "rowspan"]) if (node.attrs?.[key] != null && (!Number.isInteger(node.attrs[key]) || node.attrs[key] < 1 || node.attrs[key] > 10000)) throw new Error("Unsupported table span.");
    if (node.attrs?.colwidth != null && (!Array.isArray(node.attrs.colwidth) || node.attrs.colwidth.some(v => !Number.isInteger(v) || v < 0 || v > 10000))) throw new Error("Unsupported table width.");
    if (node.type === "image") {
        if (typeof node.attrs?.src !== "string" || !safeImage(node.attrs.src)) throw new Error("Unsupported image.");
        if (node.attrs.assetUrl != null && !safeImage(node.attrs.assetUrl)) throw new Error("Unsupported image asset URL.");
        if (node.attrs.width != null && (!/^\d+$/.test(String(node.attrs.width)) || Number(node.attrs.width) < 1 || Number(node.attrs.width) > 10000)) throw new Error("Unsupported image width.");
    }
    for (const mark of node.marks || []) {
        if (!marks.has(mark.type)) throw new Error("Unsupported legacy formatting.");
        if (Object.keys(mark).some(key => !["type", "attrs"].includes(key))) throw new Error("Unsupported legacy mark metadata.");
        if (Object.keys(mark.attrs || {}).some(key => mark.type !== "link" || !contract.attributes.a.includes(key)))
            throw new Error("Unsupported legacy formatting attributes.");
        if (mark.type === "link" && !safeLink(mark.attrs?.href || "")) throw new Error("Unsupported legacy link.");
    }
    for (const child of node.content || []) validateJson(child);
}

function prepare(content: string, format: string) {
    if (format === "Html") {
        const container = document.createElement("div"); container.innerHTML = validateHtml(content);
        // Table's generated tbody/colgroup wrappers are HTML structure, not schema nodes.
        // Validate our vocabulary first, then check the parsed document rather than TipTap's catch-all HTML checker.
        const doc = ProseMirrorDOMParser.fromSchema(getSchema(richExtensions())).parse(container, { preserveWhitespace: "full" });
        doc.check(); return doc.toJSON();
    }
    if (format === "LegacyJson") {
        const value = JSON.parse(content);
        if (value.type !== "doc") throw new Error("The legacy JSON is not a TipTap document.");
        validateJson(value);
        getSchema(richExtensions()).nodeFromJSON(value).check();
        return value;
    }
    // Convert plain text to TipTap text nodes, never interpret it as HTML.
    return { type: "doc", content: content.split(/\r?\n/).map(text => ({ type: "paragraph", content: text ? [{ type: "text", text }] : [] })) };
}

// Page-local text markers retain the complete schema tree, including marks and block attributes.
// Translation never interprets provider text as HTML or lets it alter the tree.
export function captureTranslation(content: string, format: string) {
    if (content.length > 500000) throw new Error("This page exceeds the translation source limit. Translate a smaller selection.");
    if (format === "Html" && /<!--/.test(content)) throw new Error("Translation cannot preserve embedded HTML comments. Use selection translation; the original is unchanged.");
    if (format === "Html") {
        // Inspect inert source before schema parsing: a block image nested in a paragraph
        // can otherwise be discarded by the parser before the node-level refusal runs.
        const source = document.createElement("template"); source.innerHTML = content;
        if (source.content.querySelector("img,pre,code"))
            throw new Error("Translation cannot preserve image captions or code semantics yet. Translate a text-only section or selection; the original is unchanged.");
    }
    const json = prepare(content, format);
    const runs: { id: string, text: string }[] = [];
    function visit(node: any, path: number[]) {
        if (["image", "codeBlock"].includes(node.type) || (node.marks || []).some((mark: any) => mark.type === "code"))
            throw new Error("Translation cannot preserve image captions or code semantics yet. Translate a text-only section or selection; the original is unchanged.");
        if (node.type === "text") {
            if (/[\r\n\0]/.test(node.text)) throw new Error("Translation needs explicit paragraph or hard-break structure. Normalize line breaks in this page or translate a selection.");
            runs.push({ id: path.join("."), text: node.text });
        }
        (node.content || []).forEach((child: any, index: number) => visit(child, [...path, index]));
    }
    visit(json, []);
    return { runs };
}

export function previewTranslation(content: string, format: string, translated: { id: string, text: string }[]) {
    const original = captureTranslation(content, format).runs;
    if (original.length !== translated.length || original.some((run, index) => run.id !== translated[index]?.id)
        || translated.some(run => typeof run.text !== "string" || !run.text.length || /[\r\n\0]/.test(run.text)))
        throw new Error("Translation text markers do not match this complete page.");
    const json = prepare(content, format);
    for (const run of translated) {
        const path = run.id.split(".").map(Number);
        let node = json;
        for (const index of path) node = node.content[index];
        if (node.type !== "text") throw new Error("Invalid translation text marker.");
        node.text = run.text;
    }
    const schema = getSchema(richExtensions()); const doc = schema.nodeFromJSON(json); doc.check();
    const container = document.createElement("div");
    container.appendChild(DOMSerializer.fromSchema(schema).serializeFragment(doc.content));
    return container.innerHTML;
}

// Build a reviewed replacement without touching the live editor or its selection.
// Uses the same schema and content checks as the writing surface, including legacy conversion.
export function previewConsistencyRevision(content: string, format: string, original: string, proposed: string, exactStart?: number, quality = false) {
    const editor = new Editor({ element: document.createElement("div"), extensions: richExtensions(),
        content: prepare(content, format), editable: true });
    try {
        const doc = editor.state.doc;
        const separator = quality ? qualityBlockSeparator : "\n";
        const plain = doc.textBetween(0, doc.content.size, separator, "\n");
        const start = exactStart ?? plain.indexOf(original);
        if (!original.trim() || !Number.isSafeInteger(start) || start < 0 || plain.slice(start, start + original.length) !== original
            || exactStart === undefined && plain.indexOf(original, start + 1) >= 0)
            throw new Error("The affected writing could not be matched uniquely. Run the consistency check again.");
        if (proposed === original || proposed.length > 0 && !proposed.trim()) throw new Error("This suggestion makes no change to the writing.");
        let from: number | undefined, to: number | undefined;
        doc.descendants((node, pos) => {
            if (!node.isText) return;
            const prefix = doc.textBetween(0, pos, separator, "\n").length;
            if (start >= prefix && start < prefix + node.nodeSize) from = pos + start - prefix;
            const end = start + original.length;
            if (end > prefix && end <= prefix + node.nodeSize) to = pos + end - prefix;
        });
        if (from === undefined || to === undefined || doc.textBetween(from, to, separator, "\n") !== original)
            throw new Error("The affected writing cannot be replaced safely. Run the consistency check again.");
        // Provider prose stays inert, and a phrase replacement retains the source's inline marks.
        const lines = proposed.replace(/\r\n?/g, "\n").split("\n");
        const sourceMarks = doc.nodeAt(from)?.marks ?? doc.resolve(from).marks();
        const replacement = proposed.length === 0 ? Slice.empty : lines.length === 1
            ? new Slice(Fragment.from(editor.schema.text(proposed, sourceMarks)), 0, 0)
            : new Slice(Fragment.fromArray(lines.map(line => (quality ? doc.resolve(from!).parent.type : editor.schema.nodes.paragraph).create(
                quality ? doc.resolve(from!).parent.attrs : null,
                line ? editor.schema.text(line, sourceMarks) : undefined))), 1, 1);
        const rangeFrom = from, rangeTo = to;
        if (!editor.commands.command(({ tr, dispatch }) => {
            if (dispatch) tr.replaceRange(rangeFrom, rangeTo, replacement);
            return true;
        }))
            throw new Error("The suggested revision could not be applied.");
        return editor.getHTML();
    } finally { editor.destroy(); }
}

export function validateQualityRange(content: string, format: string, from: number, to: number, expected: string, quality = true, allowMixedMarks = false) {
    if (!Number.isSafeInteger(to) || to !== from + expected.length) throw new Error("The quality range is invalid.");
    const editor = new Editor({ element: document.createElement("div"), extensions: richExtensions(), content: prepare(content, format), editable: false });
    try {
        const doc = editor.state.doc;
        const separator = quality ? qualityBlockSeparator : "\n";
        const plain = doc.textBetween(0, doc.content.size, separator, "\n");
        const word = (character: string) => /[\p{L}\p{N}]/u.test(character);
        const splitsCharacter = (at: number) => at > 0 && /[\uD800-\uDBFF]/.test(plain[at-1]) && /[\uDC00-\uDFFF]/.test(plain[at] ?? "");
        if (splitsCharacter(from) || splitsCharacter(to)
            || from > 0 && word(plain[from-1]) && word(plain[from] ?? "")
            || to < plain.length && word(plain[to-1] ?? "") && word(plain[to]))
            throw new Error("Select a complete passage or whole words before reviewing this fix.");
        const range = qualityRange(doc, { issueKey: "", from, to, expectedText: expected, severity: "info" }, separator);
        if (!range) throw new Error("The quality passage no longer matches the writing. Check again.");
        const start = doc.resolve(range.from), end = doc.resolve(range.to);
        const sourceMarks = JSON.stringify(doc.nodeAt(range.from)?.marks ?? start.marks());
        let uniform = start.sameParent(end);
        doc.nodesBetween(range.from, range.to, node => {
            if (!allowMixedMarks && node.isText && JSON.stringify(node.marks) !== sourceMarks || node.isLeaf && !node.isText) uniform = false;
        });
        if (!uniform) throw new Error("This passage spans different formatting, blocks or embedded content. Revise it manually to preserve that structure.");
    } finally { editor.destroy(); }
}
export function previewQualityRevision(content: string, format: string, from: number, to: number, expected: string, proposed: string) {
    validateQualityRange(content, format, from, to, expected);
    return previewConsistencyRevision(content, format, expected, proposed, from, true);
}

// Targeted findings include sentence context. Its unchanged marks need not match;
// the replacement below must still fit wholly inside one formatting run.
export function validateTargetedQualityRange(content: string, format: string, from: number, to: number, expected: string) {
    validateQualityRange(content, format, from, to, expected, true, true);
}
export function previewTargetedQualityRevision(content: string, format: string, from: number, to: number, expected: string, proposed: string) {
    validateTargetedQualityRange(content, format, from, to, expected);
    // Paragraph splits retain the established stricter block/mark validation.
    if (/[\r\n]/.test(proposed)) return previewQualityRevision(content, format, from, to, expected, proposed);
    if (proposed === expected || proposed.length > 0 && !proposed.trim()) throw new Error("This suggestion makes no change to the writing.");
    const editor = new Editor({ element: document.createElement("div"), extensions: richExtensions(), content: prepare(content, format), editable: false });
    try {
        const doc = editor.state.doc;
        const range = qualityRange(doc, { issueKey: "", from, to, expectedText: expected, severity: "info" })!;
        const transaction = targetedRevisionTransaction(editor, range.from, range.to, expected, proposed, qualityBlockSeparator);
        const container = document.createElement("div");
        container.appendChild(DOMSerializer.fromSchema(editor.schema).serializeFragment(transaction.doc.content));
        return container.innerHTML;
    } finally { editor.destroy(); }
}

export function consistencyPlainText(content: string, format: string) {
    const doc = getSchema(richExtensions()).nodeFromJSON(prepare(content, format));
    return doc.textBetween(0, doc.content.size, "\n", "\n");
}
export function qualityPlainText(content: string, format: string) {
    const doc = getSchema(richExtensions()).nodeFromJSON(prepare(content, format));
    return doc.textBetween(0, doc.content.size, qualityBlockSeparator, "\n");
}
// Continuation owns a new paragraph; no source node is replaced or interpreted as provider markup.
export function previewContinuation(content: string, format: string, paragraph: string) {
    if (!paragraph.trim() || paragraph.length > 20000 || /[\r\n\0]/.test(paragraph)) throw new Error("Review exactly one new paragraph.");
    const schema = getSchema(richExtensions()); const original = schema.nodeFromJSON(prepare(content, format)); original.check();
    const added = schema.nodes.paragraph.create(null, schema.text(paragraph));
    const revised = original.copy(original.content.append(added.content.size ? Fragment.from(added) : Fragment.empty)); revised.check();
    const container = document.createElement("div"); container.appendChild(DOMSerializer.fromSchema(schema).serializeFragment(revised.content));
    return container.innerHTML;
}
export function previewSafeConsistencyRevision(content: string, format: string, from: number, expected: string, proposed: string) {
    validateQualityRange(content, format, from, from + expected.length, expected, false);
    if (/[\r\n]/.test(proposed)) throw new Error("Review a single-block revision manually to preserve its structure.");
    return previewConsistencyRevision(content, format, expected, proposed, from);
}

export type StyleQualityEdit = { original: string; replacement: string };
function styleQualityTransaction(editor: Editor, baseHtml: string, sourceFrom: number, source: string, edits: StyleQualityEdit[]) {
    const doc = editor.state.doc;
    const plain = doc.textBetween(0, doc.content.size, "\n", "\n");
    if (editor.getHTML() !== baseHtml || !Number.isSafeInteger(sourceFrom) || sourceFrom < 0
        || !source || plain.slice(sourceFrom, sourceFrom + source.length) !== source
        || !Array.isArray(edits) || edits.length === 0 || edits.length > 24)
        throw new Error("The style review no longer matches the writing. Run the coach again.");
    const patches: { from: number; to: number; text: string; marks: any }[] = [];
    const anchors: { from: number; to: number }[] = [];
    for (const edit of edits) {
        if (!edit || typeof edit.original !== "string" || typeof edit.replacement !== "string"
            || !edit.original.trim() || !edit.replacement.trim() || edit.original === edit.replacement || edit.replacement.length > 20000)
            throw new Error("Invalid reviewed style change.");
        const offset = source.indexOf(edit.original);
        if (offset < 0 || source.indexOf(edit.original, offset + 1) >= 0
            || anchors.some(a => offset < a.to && offset + edit.original.length > a.from))
            throw new Error("Ambiguous or overlapping style changes. Review a shorter selection.");
        anchors.push({ from: offset, to: offset + edit.original.length });
        const range = qualityRange(doc, { issueKey: "", from: sourceFrom + offset, to: sourceFrom + offset + edit.original.length,
            expectedText: edit.original, severity: "info" }, "\n");
        if (!range || !doc.resolve(range.from).sameParent(doc.resolve(range.to)))
            throw new Error("Review a single paragraph or phrase to preserve its structure.");
        let prefix = 0, suffix = 0;
        while (prefix < edit.original.length && prefix < edit.replacement.length && edit.original[prefix] === edit.replacement[prefix]) prefix++;
        if (prefix > 0 && /[\uD800-\uDBFF]/.test(edit.original[prefix - 1])) prefix--;
        while (suffix < edit.original.length - prefix && suffix < edit.replacement.length - prefix
            && edit.original[edit.original.length - suffix - 1] === edit.replacement[edit.replacement.length - suffix - 1]) suffix++;
        if (suffix > 0 && /[\uDC00-\uDFFF]/.test(edit.original[edit.original.length - suffix])) suffix--;
        const word = (c: string) => /[\p{L}\p{N}]/u.test(c);
        while (prefix > 0 && word(edit.original[prefix - 1]) && word(edit.original[prefix] ?? "")) prefix--;
        while (suffix > 0 && word(edit.original[edit.original.length - suffix - 1] ?? "") && word(edit.original[edit.original.length - suffix])) suffix--;
        const from = range.from + prefix, to = range.to - suffix;
        const text = edit.replacement.slice(prefix, edit.replacement.length - suffix);
        const before = edit.original.slice(prefix, edit.original.length - suffix);
        if (/[\r\n]/.test(text) || /[\r\n]/.test(before) || doc.textBetween(from, to, "\n", "\n") !== before)
            throw new Error("This style change crosses a line break. Review a shorter passage.");
        const marks = to > from ? doc.nodeAt(from)?.marks ?? doc.resolve(from).marks() : doc.resolve(from).marks();
        let uniform = true;
        if (to > from) doc.nodesBetween(from, to, node => {
            if (node.isText && JSON.stringify(node.marks) !== JSON.stringify(marks) || node.isLeaf && !node.isText) uniform = false;
        });
        if (!uniform) throw new Error("This changed wording spans mixed formatting or embedded content. Review a smaller passage.");
        patches.push({ from, to, text, marks });
    }
    const transaction = editor.state.tr;
    for (const patch of patches.sort((a, b) => b.from - a.from))
        transaction.replaceWith(patch.from, patch.to, patch.text ? doc.type.schema.text(patch.text, patch.marks) : Fragment.empty);
    transaction.doc.check();
    return transaction;
}
export function previewStyleQualityRevision(content: string, sourceFrom: number, source: string, edits: StyleQualityEdit[]) {
    const editor = new Editor({ element: document.createElement("div"), extensions: richExtensions(), content: prepare(content, "Html"), editable: false });
    try {
        const revised = styleQualityTransaction(editor, content, sourceFrom, source, edits).doc;
        const container = document.createElement("div");
        container.appendChild(DOMSerializer.fromSchema(editor.schema).serializeFragment(revised.content));
        return container.innerHTML;
    } finally { editor.destroy(); }
}

// A mouse drag can stop inside a word. Writing tools review complete words;
// quality findings retain their exact offsets and continue to use strict validation.
function completeWritingWords(doc: ProseMirrorNode, from: number, to: number) {
    const boundary = (position: number, direction: -1 | 1) => {
        const resolved = doc.resolve(position);
        if (!resolved.parent.isTextblock) return position;
        // Inline leaves occupy one document position. A non-word placeholder
        // prevents expansion across images or hard breaks; marks do not split words.
        const text = resolved.parent.textBetween(0, resolved.parent.content.size, "", "\uFFFC");
        let offset = resolved.parentOffset;
        const before = (at: number) => text.slice(Math.max(0, at - 2), at).match(/.$/u)?.[0] ?? "";
        const after = (at: number) => at < text.length ? String.fromCodePoint(text.codePointAt(at)!) : "";
        const word = (character: string) => /[\p{L}\p{N}\p{M}_]/u.test(character);
        const connector = (character: string) => /['’\-]/u.test(character);
        const insideWord = (at: number) => {
            const left = before(at), right = after(at);
            return word(left) && word(right)
                || word(left) && connector(right) && word(after(at + right.length))
                || connector(left) && word(before(at - left.length)) && word(right);
        };
        // UTF-16 selections must also encompass a complete supplementary character.
        if (offset > 0 && /[\uD800-\uDBFF]/.test(text[offset - 1]) && /[\uDC00-\uDFFF]/.test(text[offset] ?? "")) offset += direction;
        while (insideWord(offset)) offset += direction < 0 ? -before(offset).length : after(offset).length;
        return resolved.start() + offset;
    };
    return { from: boundary(from, -1), to: boundary(to, 1) };
}

export function create(host: HTMLElement, content: string, format: string, receiver: any, externalToolbar = false) {
    const initial = prepare(content, format);
    const toolbar = document.createElement("div");
    toolbar.className = "device-editor-toolbar";
    toolbar.hidden = externalToolbar;
    toolbar.setAttribute("role", "group");
    toolbar.setAttribute("aria-label", "Text formatting");
    const canvas = document.createElement("div");
    const linkPanel = document.createElement("div");
    linkPanel.className = "device-editor-link";
    linkPanel.hidden = true;
    const linkInput = document.createElement("input");
    linkInput.type = "url";
    linkInput.setAttribute("aria-label", "Link address (https, http or mailto)");
    linkInput.placeholder = "https://example.com";
    const notice = document.createElement("p");
    notice.className = "device-editor-notice";
    notice.setAttribute("role", "alert");
    const imagePanel = document.createElement("form");
    imagePanel.className = "device-editor-link"; imagePanel.hidden = true;
    imagePanel.setAttribute("aria-label", "Insert image from URL");
    const imageUrl = document.createElement("input"); imageUrl.type = "url"; imageUrl.placeholder = "https://example.com/image.png";
    imageUrl.setAttribute("aria-label", "Image URL");
    const imageAlt = document.createElement("input"); imageAlt.placeholder = "Describe the image"; imageAlt.maxLength = 1000;
    imageAlt.setAttribute("aria-label", "Image description");
    const imageApply = document.createElement("button"); imageApply.type = "submit"; imageApply.textContent = "Insert image";
    const imageCancel = document.createElement("button"); imageCancel.type = "button"; imageCancel.textContent = "Cancel image";
    imagePanel.append(imageUrl, imageAlt, imageApply, imageCancel);
    const imageFile = document.createElement("input"); imageFile.type = "file"; imageFile.hidden = true;
    imageFile.accept = "image/png,image/jpeg,image/gif,image/webp";
    imageFile.setAttribute("aria-label", "Choose image from your device");
    host.replaceChildren(toolbar, linkPanel, imagePanel, imageFile, notice, canvas);
    let version = 0;
    let disposed = false;
    let lastAiSelection: { from: number; to: number } | null = null;
    let linkSelection = { from: 1, to: 1 };
    let imageSelection = { from: 1, to: 1 };
    let imageVersion = 0;
    const buttons: { button: HTMLButtonElement; active?: () => boolean; enabled?: () => boolean }[] = [];
    const notify = (method: string, ...args: any[]) => {
        if (!disposed) receiver.invokeMethodAsync(method, ...args).catch(() => {
            if (!disposed) notice.textContent = "The editor could not notify the app. Use Save before leaving this page.";
        });
    };
    const openLink = () => {
        if (!editor.isEditable) return;
        imagePanel.hidden = true;
        linkSelection = { from: editor.state.selection.from, to: editor.state.selection.to };
        linkInput.value = editor.getAttributes("link").href || "";
        linkPanel.hidden = false;
        linkInput.focus();
    };
    const openImage = (fromFile: boolean) => {
        if (!editor.isEditable) return;
        imageSelection = { from: editor.state.selection.from, to: editor.state.selection.to }; imageVersion = version;
        linkPanel.hidden = true;
        if (fromFile) { imagePanel.hidden = true; imageFile.value = ""; imageFile.click(); }
        else { imageUrl.value = ""; imageAlt.value = ""; imagePanel.hidden = false; imageUrl.focus(); }
    };
    function insertImage(source: string, alt = "", selection = editor.state.selection) {
        if (disposed || !editor.isEditable) return false;
        source = source.trim();
        if (!safeImage(source)) { notice.textContent = "Use an http or https image address, or choose a PNG, JPEG, GIF or WebP file."; return false; }
        // Keep embedded images within the existing per-page sync limit.
        if (editor.getHTML().length + source.length + alt.length * 6 + 200 > 500000) {
            notice.textContent = "This image would exceed the page size limit. Choose a smaller image or use an image URL."; return false;
        }
        editor.chain().focus().setTextSelection({ from: selection.from, to: selection.to }).setImage({ src: source, alt }).run();
        imagePanel.hidden = true; notice.textContent = ""; return true;
    }
    imagePanel.addEventListener("submit", event => {
        event.preventDefault();
        if (version !== imageVersion) { notice.textContent = "The writing changed. Open Image URL again to choose where to insert the image."; return; }
        insertImage(imageUrl.value, imageAlt.value, imageSelection);
    });
    const cancelImage = () => { imagePanel.hidden = true; editor.commands.focus(); };
    imageCancel.addEventListener("click", cancelImage);
    imagePanel.addEventListener("keydown", event => { if (event.key === "Escape") { event.preventDefault(); cancelImage(); } });
    imageFile.addEventListener("change", async () => {
        const file = imageFile.files?.[0]; if (!file || disposed) return;
        const selection = { ...imageSelection }, expectedVersion = imageVersion;
        if (file.size > 300000) { notice.textContent = "Choose an image up to 300 KB, or insert it using an image URL."; return; }
        try {
            const bytes = new Uint8Array(await file.arrayBuffer());
            const matches = (offset: number, signature: number[]) => signature.every((value, index) => bytes[offset + index] === value);
            const mime = matches(0, [137,80,78,71,13,10,26,10]) ? "image/png" : matches(0, [255,216,255]) ? "image/jpeg"
                : matches(0, [71,73,70,56]) ? "image/gif" : matches(0, [82,73,70,70]) && matches(8, [87,69,66,80]) ? "image/webp" : null;
            if (!mime) throw new Error("Choose a PNG, JPEG, GIF or WebP image.");
            if (disposed) return;
            if (version !== expectedVersion) throw new Error("The writing changed. Choose the image again to set its insertion point.");
            let binary = ""; for (const byte of bytes) binary += String.fromCharCode(byte);
            insertImage(`data:${mime};base64,${btoa(binary)}`, file.name, selection);
        } catch (error) { if (!disposed) notice.textContent = error instanceof Error ? error.message : "The image could not be opened."; }
    });
    const shortcuts = Extension.create({
        name: "deviceShortcuts",
        addKeyboardShortcuts() { return {
            "Mod-s": () => { notify("OnSaveRequested"); return true; },
            "Mod-k": () => { openLink(); return true; }
        }; }
    });
    let editorReady = false;
    const editor = new Editor({
        element: canvas,
        extensions: [...richExtensions(), shortcuts, annotationExtension(id => notify("OnAnnotationClicked", id)), qualityExtension, consistencyPassageExtension],
        content: initial,
        parseOptions: { preserveWhitespace: "full" },
        enableContentCheck: true,
        onContentError: ({ error }) => { throw error; },
        editorProps: {
            attributes: { role: "textbox", "aria-label": "Document text", "aria-multiline": "true", spellcheck: "true" },
            // Rich clipboard HTML is checked before insertion. Unsupported paste falls back to plain text.
            handlePaste(view, event) {
                const html = event.clipboardData?.getData("text/html");
                if (html) {
                    try { validateHtml(html); }
                    catch {
                        event.preventDefault();
                        view.dispatch(view.state.tr.insertText(event.clipboardData?.getData("text/plain") || ""));
                        notice.textContent = "Pasted as plain text because the source has unsupported formatting.";
                        return true;
                    }
                }
                return false;
            },
            handleDrop: () => true
        },
        onUpdate: () => { lastAiSelection = null; version++; notify("OnContentChanged", editor.getHTML(), version); },
        onSelectionUpdate: () => {
            const selection = editor.state.selection;
            if (!selection.empty) lastAiSelection = { from: selection.from, to: selection.to };
        },
        onTransaction: () => {
            if (externalToolbar && editorReady) reportFormatting();
            for (const entry of buttons) {
                if (entry.active) entry.button.setAttribute("aria-pressed", String(entry.active()));
                entry.button.disabled = !editor.isEditable || (entry.enabled ? !entry.enabled() : false);
            }
        }
    });
    function reportFormatting() {
        const editable = editor.isEditable;
        const heading = editor.isActive("heading") ? editor.getAttributes("heading").level : null;
        notify("OnFormattingChanged", {
            isBold: editor.isActive("bold"), isItalic: editor.isActive("italic"), isLink: editor.isActive("link"),
            isStrike: editor.isActive("strike"), isCode: editor.isActive("code"),
            canBold: editable && editor.can().toggleBold(), canItalic: editable && editor.can().toggleItalic(),
            canStrike: editable && editor.can().toggleStrike(), canCode: editable && editor.can().toggleCode(),
            canApplyHeading: editable && !editor.isActive("codeBlock"), canToggleList: editable,
            canBlockquote: editable, canHorizontalRule: editable && editor.can().setHorizontalRule(),
            canInsertTable: editable && editor.can().insertTable({ rows: 3, cols: 3, withHeaderRow: true }),
            canInsertImage: editable && !editor.isActive("codeBlock"), isInTable: editor.isActive("table"), isHeaderCell: editor.isActive("tableHeader"),
            canAddTableRowBefore: editable && editor.can().addRowBefore(), canAddTableRowAfter: editable && editor.can().addRowAfter(),
            canDeleteTableRow: editable && editor.can().deleteRow(), canAddTableColumnBefore: editable && editor.can().addColumnBefore(),
            canAddTableColumnAfter: editable && editor.can().addColumnAfter(), canDeleteTableColumn: editable && editor.can().deleteColumn(),
            canToggleTableHeaderRow: editable && editor.can().toggleHeaderRow(), canToggleTableHeaderColumn: editable && editor.can().toggleHeaderColumn(),
            canMergeTableCells: editable && editor.can().mergeCells(), canSplitTableCell: editable && editor.can().splitCell(),
            canDeleteTable: editable && editor.can().deleteTable(),
            canAlign: editable && editor.can().setTextAlign("left"),
            canIncreaseIndent: editable && editor.can().increaseIndent(), canDecreaseIndent: editable && editor.can().decreaseIndent(),
            textAlign: editor.getAttributes(heading ? "heading" : "paragraph").textAlign || "left",
            blockType: heading ? `heading:${heading}` : "paragraph"
        }, editable && editor.can().undo(), editable && editor.can().redo());
    }
    editorReady = true;
    if (externalToolbar) reportFormatting();
    function button(label: string, action: () => void, active?: () => boolean, enabled?: () => boolean) {
        const element = document.createElement("button");
        element.type = "button";
        element.textContent = label;
        // Keep the editor selection when the pointer presses a formatting command; keyboard activation still works.
        element.addEventListener("mousedown", event => event.preventDefault());
        element.addEventListener("click", () => { if (editor.isEditable) action(); });
        if (active) element.setAttribute("aria-pressed", String(active()));
        if (enabled) element.disabled = !enabled();
        toolbar.append(element);
        buttons.push({ button: element, active, enabled });
    }
    button("Paragraph", () => setParagraph(editor), () => editor.isActive("paragraph"));
    for (const level of contract.headingLevels) button(`Heading ${level}`, () => setHeading(editor, level), () => editor.isActive("heading", { level }));
    button("Bold", () => toggleBold(editor), () => editor.isActive("bold"));
    button("Italic", () => toggleItalic(editor), () => editor.isActive("italic"));
    button("Bullets", () => toggleBulletList(editor), () => editor.isActive("bulletList"));
    button("Numbered list", () => toggleOrderedList(editor), () => editor.isActive("orderedList"));
    button("Quote", () => toggleBlockquote(editor), () => editor.isActive("blockquote"));
    button("Link", openLink, () => editor.isActive("link"));
    button("Undo", () => undo(editor), undefined, () => editor.can().undo());
    button("Redo", () => redo(editor), undefined, () => editor.can().redo());
    linkPanel.append(linkInput);
    const linkAction = (label: string, action: () => void) => {
        const control = document.createElement("button"); control.type = "button"; control.textContent = label;
        control.addEventListener("click", action); linkPanel.append(control);
    };
    const applyLink = () => {
        if (!safeLink(linkInput.value)) { notice.textContent = "Use an http, https or mailto address."; return; }
        editor.chain().focus().setTextSelection(linkSelection).extendMarkRange("link").setLink({ href: linkInput.value.trim() }).run();
        linkPanel.hidden = true; notice.textContent = "";
    };
    linkAction("Apply link", applyLink);
    linkAction("Remove link", () => { editor.chain().focus().setTextSelection(linkSelection).extendMarkRange("link").unsetLink().run(); linkPanel.hidden = true; });
    linkAction("Cancel", () => { linkPanel.hidden = true; editor.commands.focus(); });
    linkInput.addEventListener("keydown", event => {
        if (event.key === "Enter") { event.preventDefault(); applyLink(); }
        if (event.key === "Escape") { linkPanel.hidden = true; editor.commands.focus(); }
    });
    return {
        setAnnotations: (items: TextAnnotation[]) => setAnnotations(editor, items),
        setQualityHighlights: (plain: string, items: QualityHighlight[], selected: string | null) => setQualityHighlights(editor, plain, items, selected),
        navigateToQuality: (plain: string, item: QualityHighlight) => navigateToQuality(editor, plain, item),
        clearConsistencyHighlight: () => highlightConsistencyPassage(editor, null),
        navigateToConsistency(plain: string, from: number, expected: string) {
            const doc = editor.state.doc;
            const range = doc.textBetween(0, doc.content.size, "\n", "\n") === plain
                ? qualityRange(doc, { issueKey: "", from, to: from + expected.length, expectedText: expected, severity: "info" }, "\n")
                : null;
            highlightConsistencyPassage(editor, range);
            if (!range) return false;
            editor.view.dispatch(editor.state.tr.setSelection(TextSelection.create(doc, range.from, range.to)).scrollIntoView());
            editor.view.focus(); return true;
        },
        navigateToAnnotation: (id: string) => navigateToAnnotation(editor, id),
        selectedText: () => selectedAnnotationText(editor),
        navigateToText(text: string) {
            if (!text || text.length > 1000) return false;
            let writing = "";
            const positions: number[] = [];
            let lastParent: unknown = null;
            editor.state.doc.descendants((node, pos, parent) => {
                if (node.isText && node.text) {
                    if (lastParent && parent !== lastParent) { writing += "\n"; positions.push(pos); }
                    for (let i = 0; i < node.text.length; i++) positions.push(pos + i);
                    writing += node.text; lastParent = parent;
                } else if (node.type.name === "hardBreak") { writing += "\n"; positions.push(pos); }
            });
            const index = writing.indexOf(text);
            if (index < 0) return false;
            // Selection/scroll transactions do not mutate prose or create history/save events.
            editor.chain().setTextSelection({ from: positions[index], to: positions[index + text.length - 1] + 1 }).scrollIntoView().focus().run();
            return true;
        },
        command(name: string, level?: number) {
            if (!editor.isEditable) return;
            switch (name) {
                case "bold": toggleBold(editor); break;
                case "italic": toggleItalic(editor); break;
                case "bulletList": toggleBulletList(editor); break;
                case "orderedList": toggleOrderedList(editor); break;
                case "blockquote": toggleBlockquote(editor); break;
                case "paragraph": setParagraph(editor); break;
                case "heading": if (level && contract.headingLevels.includes(level)) setHeading(editor, level); break;
                case "link": openLink(); break;
                case "undo": undo(editor); break;
                case "redo": redo(editor); break;
                case "strike": editor.chain().focus().toggleStrike().run(); break;
                case "code": editor.chain().focus().toggleCode().run(); break;
                case "horizontalRule": editor.chain().focus().setHorizontalRule().run(); break;
                case "table": editor.chain().focus().insertTable({ rows: 3, cols: 3, withHeaderRow: true }).run(); break;
                case "image": openImage(true); break;
                case "imageUrl": openImage(false); break;
                case "align:left": case "align:center": case "align:right": editor.chain().focus().setTextAlign(name.slice(6)).run(); break;
                case "increaseIndent": case "decreaseIndent": case "addRowBefore": case "addRowAfter": case "deleteRow":
                case "addColumnBefore": case "addColumnAfter": case "deleteColumn": case "toggleHeaderRow": case "toggleHeaderColumn":
                case "mergeCells": case "splitCell": case "deleteTable": editor.chain().focus()[name]().run(); break;
                default: throw new Error("Unsupported editor command.");
            }
        },
        insertImage,
        setEditable(value: boolean) {
            editor.setEditable(value, false);
            if (externalToolbar) reportFormatting();
            host.querySelectorAll<HTMLButtonElement | HTMLInputElement>("button,input").forEach(control => { control.disabled = !value; });
            for (const entry of buttons) entry.button.disabled = !value || (entry.enabled ? !entry.enabled() : false);
        },
        snapshot: () => ({ html: editor.getHTML(), version }),
        captureAi(wholePage = false, currentSelectionOnly = false, quality = false, completeWords = false) {
            const doc = editor.state.doc;
            const range = !editor.state.selection.empty
                ? editor.state.selection
                : currentSelectionOnly ? null : lastAiSelection;
            let from = wholePage ? 0 : range?.from ?? editor.state.selection.from;
            let to = wholePage ? doc.content.size : range?.to ?? editor.state.selection.to;
            if (quality && !wholePage && from < to) {
                // Dragging through a paragraph's end can include an empty block even
                // though only the prose is visibly selected. Exclude structural
                // boundary positions, retaining all selected text and embedded leaves.
                let first: number | undefined, last: number | undefined;
                doc.nodesBetween(from, to, (node, pos) => {
                    if (!node.isLeaf) return;
                    first ??= Math.max(from, pos);
                    last = Math.min(to, pos + node.nodeSize);
                });
                if (first !== undefined && last !== undefined) { from = first; to = last; }
            }
            if (completeWords && !wholePage && from < to) {
                ({ from, to } = completeWritingWords(doc, from, to));
                // Reflect the reviewed target in the visible selection without editing
                // prose, moving focus out of the panel or creating a save/history event.
                if (from !== editor.state.selection.from || to !== editor.state.selection.to) {
                    const backward = editor.state.selection.anchor > editor.state.selection.head;
                    editor.view.dispatch(editor.state.tr.setSelection(TextSelection.create(doc, backward ? to : from, backward ? from : to)));
                }
            }
            const text = (start: number, end: number) => doc.textBetween(start, end, quality ? qualityBlockSeparator : "\n", "\n");
            const selectedText = text(from, to);
            const selectionStart = text(0, from).length;
            return {
                html: editor.getHTML(), plainText: text(0, doc.content.size),
                selectedText, selectionStart, selectionEnd: selectionStart + selectedText.length,
                from, to, version
            };
        },
        applyAi(baseHtml: string, from: number, to: number, original: string, proposed: string, mode: string) {
            if (!editor.isEditable || editor.getHTML() !== baseHtml)
                throw new Error("The writing changed after the preview. Run the action again.");
            if (!proposed.trim()) throw new Error("The proposed text is empty.");
            const doc = editor.state.doc;
            if (mode === "replace" && (from < 0 || to > doc.content.size || from >= to
                || doc.textBetween(from, to, "\n", "\n") !== original))
                throw new Error("The selection changed after the preview. Run the action again.");
            if (mode !== "replace" && mode !== "append") throw new Error("Unsupported AI apply mode.");
            // JSON text nodes keep provider output inert; an HTML-looking answer is inserted as text.
            const paragraphs = proposed.replace(/\r\n?/g, "\n").split("\n").map(line => ({
                type: "paragraph", content: line ? [{ type: "text", text: line }] : []
            }));
            const position = mode === "replace" ? { from, to } : doc.content.size;
            if (!editor.commands.insertContentAt(position, paragraphs, { updateSelection: true, errorOnInvalidContent: true }))
                throw new Error("The proposal could not be applied. Your writing is unchanged.");
            lastAiSelection = null;
            return { html: editor.getHTML(), version };
        },
        applyStyleQuality(baseHtml: string, sourceFrom: number, source: string, edits: StyleQualityEdit[]) {
            if (!editor.isEditable) throw new Error("The editor is read-only.");
            const transaction = styleQualityTransaction(editor, baseHtml, sourceFrom, source, edits);
            editor.view.dispatch(transaction.scrollIntoView());
            lastAiSelection = null;
            return { html: editor.getHTML(), version };
        },
        restoreAi(expectedHtml: string, originalHtml: string) {
            if (!editor.isEditable || editor.getHTML() !== expectedHtml)
                throw new Error("The writing changed after AI was applied. Restore the original as a separate copy instead.");
            validateHtml(originalHtml);
            editor.commands.setContent(prepare(originalHtml, "Html"), { emitUpdate: true, errorOnInvalidContent: true });
            return { html: editor.getHTML(), version };
        },
        // Only apply explicit external updates. Ordinary .NET rerenders never reset content or selection.
        setContent(content: string, format = "Html") {
            const prepared = prepare(content, format);
            if (editor.schema.nodeFromJSON(prepared).eq(editor.state.doc)) return;
            const selection = editor.state.selection;
            editor.commands.setContent(prepared, { emitUpdate: false, errorOnInvalidContent: true });
            const max = editor.state.doc.content.size;
            editor.commands.setTextSelection({ from: Math.min(selection.from, max), to: Math.min(selection.to, max) });
        },
        destroy() { disposed = true; editor.destroy(); host.replaceChildren(); }
    };
}
