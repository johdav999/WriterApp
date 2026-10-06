import { Extension, type Editor } from "@tiptap/core";
import { Plugin, PluginKey } from "@tiptap/pm/state";
import { Decoration, DecorationSet } from "@tiptap/pm/view";

const key = new PluginKey<DecorationSet>("deviceConsistencyPassage");

// View-only highlighting survives focus changes without becoming saved formatting.
export const consistencyPassageExtension = Extension.create({
    name: "deviceConsistencyPassage",
    addProseMirrorPlugins() {
        return [new Plugin({
            key,
            state: {
                init: () => DecorationSet.empty,
                apply(tr, previous) {
                    return tr.getMeta(key) ?? (tr.docChanged ? DecorationSet.empty : previous);
                }
            },
            props: { decorations(state) { return key.getState(state); } }
        })];
    }
});

export function highlightConsistencyPassage(editor: Editor, range: { from: number; to: number } | null) {
    const decorations = range
        ? DecorationSet.create(editor.state.doc, [Decoration.inline(range.from, range.to, { class: "wa-consistency-passage" })])
        : DecorationSet.empty;
    editor.view.dispatch(editor.state.tr.setMeta(key, decorations).setMeta("addToHistory", false));
}
