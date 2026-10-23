import { getSchema } from "@tiptap/core";
import { DOMParser as ProseMirrorDOMParser } from "@tiptap/pm/model";
import { richExtensions } from "./editor-schema";
import contract from "../../WriterApp.Shared/Editor/content-contract.json";

const tags = new Set(contract.tags.map(tag => tag.toUpperCase()));
const nodes = new Set(contract.nodes);
const marks = new Set(contract.marks);
export const safeLink = (value: string) => {
    if (/[\u0000-\u001f\u007f]/.test(value)) return false;
    try { return contract.linkSchemes.includes(new URL(value.trim()).protocol.slice(0, -1)); }
    catch { return false; }
};
export const safeImage = (value: string) => {
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
export function validateHtml(value: string, dom: Document = document) {
    const template = dom.createElement("template");
    template.innerHTML = value;
    for (const element of template.content.querySelectorAll("*")) {
        if (!tags.has(element.tagName)) throw new Error("This page contains unsupported elements. Its saved source is unchanged.");
        const tag = element.localName;
        const children = [...element.children];
        const inline = contract.inlineTags;
        const invalidChildren = ((["p", "h1", "h2", "h3", "h4", "h5", "h6"].includes(tag) || inline.includes(tag)) && children.some(c => !inline.includes(c.localName)))
            || (["ul", "ol"].includes(tag) && (children.some(c => c.localName !== "li") || [...element.childNodes].some(c => c.nodeType === 3 && c.textContent?.trim())))
            || (tag === "li" && !["ul", "ol"].includes(element.parentElement?.localName || ""))
            || (tag === "pre" && (children.length !== 1 || children[0].localName !== "code" || [...element.childNodes].some(c => c.nodeType === 3 && c.textContent)))
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

export function validateJson(node: any): void {
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

export function prepare(content: string, format: string, dom: Document = document) {
    if (format === "Html") {
        const container = dom.createElement("div"); container.innerHTML = validateHtml(content, dom);
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

