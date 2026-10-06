import { Extension } from "@tiptap/core";
import { Plugin, PluginKey } from "@tiptap/pm/state";
import { Decoration, DecorationSet } from "@tiptap/pm/view";
import type { Node } from "@tiptap/pm/model";

export type TextAnnotation = { id: string; quote: string; kind: string; status: string; content: string };
const annotationsKey = new PluginKey<{ items: TextAnnotation[]; decorations: DecorationSet }>("deviceAnnotations");
const blocks = new Set(["paragraph", "heading", "listItem", "blockquote", "codeBlock", "tableCell", "tableHeader", "hardBreak", "horizontalRule"]);

// Match LocalDocumentPreview.PlainText, including block separators, while retaining
// ProseMirror positions across formatting marks, lists, tables and hard breaks.
function textMap(doc: Node) {
    let text = "";
    const positions: (number | undefined)[] = [];
    function append(node: Node, pos: number) {
        if (node.isText) {
            text += node.text;
            for (let i = 0; i < node.text!.length; i++) positions.push(pos + i);
        } else {
            node.forEach((child, offset) => append(child, pos + offset + (node.type.name === "doc" ? 0 : 1)));
            if (blocks.has(node.type.name)) { text += "\n"; positions.push(undefined); }
        }
    }
    append(doc, 0);
    return { text, positions };
}

function uniqueRange(map: ReturnType<typeof textMap>, quote: string) {
    if (!quote) return null;
    const index = map.text.indexOf(quote);
    if (index < 0 || map.text.indexOf(quote, index + 1) >= 0) return null;
    const matched = map.positions.slice(index, index + quote.length).filter((pos): pos is number => pos !== undefined);
    return matched.length ? { from: matched[0], to: matched[matched.length - 1] + 1 } : null;
}

function decorate(doc: Node, items: TextAnnotation[]) {
    if (!items.length) return DecorationSet.empty;
    const map = textMap(doc);
    const decorations: Decoration[] = [];
    for (const item of items) {
        const range = uniqueRange(map, item.quote);
        if (!range) continue;
        const kind = ["comment", "todo", "highlight"].includes(item.kind) ? item.kind : "comment";
        const status = item.status === "resolved" ? "resolved" : "open";
        decorations.push(Decoration.inline(range.from, range.to, {
            class: `wa-annotation wa-annotation-${kind} wa-annotation-${status}`,
            "data-annotation-id": item.id,
            title: item.content
        }));
    }
    return DecorationSet.create(doc, decorations);
}

export function annotationExtension(clicked: (id: string) => void) {
    return Extension.create({
        name: "deviceAnnotations",
        addProseMirrorPlugins() {
            return [new Plugin({
                key: annotationsKey,
                state: {
                    init: () => ({ items: [], decorations: DecorationSet.empty }),
                    apply(tr, previous) {
                        const items: TextAnnotation[] = tr.getMeta(annotationsKey) ?? previous.items;
                        return tr.docChanged || items !== previous.items
                            ? { items, decorations: decorate(tr.doc, items) } : previous;
                    }
                },
                props: {
                    decorations: state => annotationsKey.getState(state)?.decorations,
                    handleDOMEvents: {
                        click(view, event) {
                            const element = event.target instanceof Element ? event.target.closest("[data-annotation-id]") : null;
                            if (element && view.dom.contains(element)) clicked(element.getAttribute("data-annotation-id")!);
                            return false;
                        }
                    }
                }
            })];
        }
    });
}

export function setAnnotations(editor: any, items: TextAnnotation[]) {
    editor.view.dispatch(editor.state.tr.setMeta(annotationsKey, items).setMeta("addToHistory", false));
}

export function navigateToAnnotation(editor: any, id: string) {
    const item = annotationsKey.getState(editor.state)?.items.find(item => item.id === id);
    const range = item && uniqueRange(textMap(editor.state.doc), item.quote);
    if (!range) return false;
    // Selection scrolling targets the end of long quotes. Focus synchronously
    // and reveal the first decoration instead, including nested editor viewports.
    if (!editor.commands.setTextSelection(range)) return false;
    editor.view.focus();
    const spans: NodeListOf<HTMLElement> = editor.view.dom.querySelectorAll("[data-annotation-id]");
    const target = Array.from(spans)
        .find(element => element.getAttribute("data-annotation-id") === id);
    if (!target) return false;
    target.scrollIntoView({ block: "start", inline: "nearest", behavior: "instant" });
    return true;
}

export function selectedAnnotationText(editor: any) {
    const { from, to, empty } = editor.state.selection;
    if (empty) return "";
    const map = textMap(editor.state.doc);
    let start = -1, end = -1;
    map.positions.forEach((pos, index) => {
        if (pos !== undefined && pos >= from && pos < to) {
            if (start < 0) start = index;
            end = index + 1;
        }
    });
    return start < 0 ? "" : map.text.slice(start, end);
}
