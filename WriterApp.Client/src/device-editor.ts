import { Editor, Extension, getSchema } from "@tiptap/core";
import { DOMParser as ProseMirrorDOMParser } from "@tiptap/pm/model";
import StarterKit from "@tiptap/starter-kit";
import { annotationExtension, setAnnotations, navigateToAnnotation, selectedAnnotationText, type TextAnnotation } from "./device-annotations";
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
        extensions: [...richExtensions(), shortcuts, annotationExtension(id => notify("OnAnnotationClicked", id))],
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
        captureAi(wholePage = false, currentSelectionOnly = false) {
            const doc = editor.state.doc;
            const range = !editor.state.selection.empty
                ? editor.state.selection
                : currentSelectionOnly ? null : lastAiSelection;
            const from = wholePage ? 0 : range?.from ?? editor.state.selection.from;
            const to = wholePage ? doc.content.size : range?.to ?? editor.state.selection.to;
            const text = (start: number, end: number) => doc.textBetween(start, end, "\n", "\n");
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
        restoreAi(expectedHtml: string, originalHtml: string) {
            if (!editor.isEditable || editor.getHTML() !== expectedHtml)
                throw new Error("The writing changed after AI was applied. Restore the original as a separate copy instead.");
            validateHtml(originalHtml);
            editor.commands.setContent(prepare(originalHtml, "Html"), { emitUpdate: true, errorOnInvalidContent: true });
            return { html: editor.getHTML(), version };
        },
        // Only apply explicit external updates. Ordinary .NET rerenders never reset content or selection.
        setContent(html: string) {
            validateHtml(html);
            if (html === editor.getHTML()) return;
            const selection = editor.state.selection;
            editor.commands.setContent(prepare(html, "Html"), { emitUpdate: false, errorOnInvalidContent: true });
            const max = editor.state.doc.content.size;
            editor.commands.setTextSelection({ from: Math.min(selection.from, max), to: Math.min(selection.to, max) });
        },
        destroy() { disposed = true; editor.destroy(); host.replaceChildren(); }
    };
}
