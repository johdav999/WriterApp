import { Editor, Extension } from "@tiptap/core";
import StarterKit from "@tiptap/starter-kit";
import { toggleBold, toggleItalic, setParagraph, setHeading, toggleBulletList,
    toggleOrderedList, toggleBlockquote, undo, redo } from "./tiptap-commands";
import "./device-editor.css";

const tags = new Set(["P", "H1", "H2", "H3", "H4", "H5", "H6", "STRONG", "B", "EM", "I", "S", "DEL", "U",
    "CODE", "PRE", "BLOCKQUOTE", "UL", "OL", "LI", "BR", "HR", "A"]);
const nodes = new Set(["doc", "paragraph", "heading", "text", "blockquote", "bulletList", "orderedList", "listItem", "hardBreak", "horizontalRule", "codeBlock"]);
const marks = new Set(["bold", "italic", "strike", "underline", "code", "link"]);
const safeLink = (value: string) => /^(https?:\/\/|mailto:)/i.test(value.trim());

function validateHtml(value: string) {
    const template = document.createElement("template");
    template.innerHTML = value;
    for (const element of template.content.querySelectorAll("*")) {
        if (!tags.has(element.tagName)) throw new Error("This page contains unsupported elements. Its saved source is unchanged.");
        for (const attr of element.attributes) {
            const allowed = element.tagName === "A" && ["href", "target", "rel", "class"].includes(attr.name)
                || element.tagName === "OL" && attr.name === "start"
                || element.tagName === "CODE" && attr.name === "class" && /^language-[\w-]+$/.test(attr.value);
            if (!allowed) throw new Error("This page contains formatting this editor cannot preserve yet. Its saved source is unchanged.");
        }
        if (element.tagName === "A" && element.getAttribute("href") && !safeLink(element.getAttribute("href")!))
            throw new Error("This page contains an unsupported link. Its saved source is unchanged.");
    }
    return value;
}

function validateJson(node: any): void {
    if (!node || !nodes.has(node.type)) throw new Error("This legacy document contains unsupported content.");
    const permitted = node.type === "heading" ? ["level"] : node.type === "orderedList" ? ["start"] : node.type === "codeBlock" ? ["language"] : [];
    if (Object.keys(node.attrs || {}).some(key => !permitted.includes(key))) throw new Error("Unsupported legacy attributes.");
    for (const mark of node.marks || []) {
        if (!marks.has(mark.type)) throw new Error("Unsupported legacy formatting.");
        if (Object.keys(mark.attrs || {}).some(key => mark.type !== "link" || !["href", "target", "rel", "class"].includes(key)))
            throw new Error("Unsupported legacy formatting attributes.");
        if (mark.type === "link" && !safeLink(mark.attrs?.href || "")) throw new Error("Unsupported legacy link.");
    }
    for (const child of node.content || []) validateJson(child);
}

function prepare(content: string, format: string) {
    if (format === "Html") return validateHtml(content);
    if (format === "LegacyJson") {
        const value = JSON.parse(content);
        if (value.type !== "doc") throw new Error("The legacy JSON is not a TipTap document.");
        validateJson(value);
        return value;
    }
    // Convert plain text to TipTap text nodes, never interpret it as HTML.
    return { type: "doc", content: content.split(/\r?\n/).map(text => ({ type: "paragraph", content: text ? [{ type: "text", text }] : [] })) };
}

export function create(host: HTMLElement, content: string, format: string, receiver: any) {
    const initial = prepare(content, format);
    const toolbar = document.createElement("div");
    toolbar.className = "device-editor-toolbar";
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
    host.replaceChildren(toolbar, linkPanel, notice, canvas);
    let version = 0;
    let disposed = false;
    let linkSelection = { from: 1, to: 1 };
    const buttons: { button: HTMLButtonElement; active?: () => boolean; enabled?: () => boolean }[] = [];
    const notify = (method: string, ...args: any[]) => {
        if (!disposed) receiver.invokeMethodAsync(method, ...args).catch(() => {
            if (!disposed) notice.textContent = "The editor could not notify the app. Use Save before leaving this page.";
        });
    };
    const openLink = () => {
        if (!editor.isEditable) return;
        linkSelection = { from: editor.state.selection.from, to: editor.state.selection.to };
        linkInput.value = editor.getAttributes("link").href || "";
        linkPanel.hidden = false;
        linkInput.focus();
    };
    const shortcuts = Extension.create({
        name: "deviceShortcuts",
        addKeyboardShortcuts() { return {
            "Mod-s": () => { notify("OnSaveRequested"); return true; },
            "Mod-k": () => { openLink(); return true; }
        }; }
    });
    const editor = new Editor({
        element: canvas,
        extensions: [StarterKit.configure({ link: { openOnClick: false, autolink: false, linkOnPaste: false }, trailingNode: false }), shortcuts],
        content: initial,
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
        onUpdate: () => { version++; notify("OnContentChanged", editor.getHTML(), version); },
        onTransaction: () => {
            for (const entry of buttons) {
                if (entry.active) entry.button.setAttribute("aria-pressed", String(entry.active()));
                entry.button.disabled = !editor.isEditable || (entry.enabled ? !entry.enabled() : false);
            }
        }
    });
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
    for (const level of [1, 2, 3]) button(`Heading ${level}`, () => setHeading(editor, level), () => editor.isActive("heading", { level }));
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
        setEditable(value: boolean) {
            editor.setEditable(value, false);
            host.querySelectorAll<HTMLButtonElement | HTMLInputElement>("button,input").forEach(control => { control.disabled = !value; });
            for (const entry of buttons) entry.button.disabled = !value || (entry.enabled ? !entry.enabled() : false);
        },
        snapshot: () => ({ html: editor.getHTML(), version }),
        // Only apply explicit external updates. Ordinary .NET rerenders never reset content or selection.
        setContent(html: string) {
            validateHtml(html);
            if (html === editor.getHTML()) return;
            const selection = editor.state.selection;
            editor.commands.setContent(html, { emitUpdate: false, errorOnInvalidContent: true });
            const max = editor.state.doc.content.size;
            editor.commands.setTextSelection({ from: Math.min(selection.from, max), to: Math.min(selection.to, max) });
        },
        destroy() { disposed = true; editor.destroy(); host.replaceChildren(); }
    };
}
