import { Editor } from "https://esm.sh/@tiptap/core@2.1.13?bundle";
import StarterKit from "https://esm.sh/@tiptap/starter-kit@2.1.13?bundle";

window.tiptapEditor = {
    create: function (elementId, initialContent, dotNetRef) {
        const editor = new Editor({
            element: document.getElementById(elementId),
            extensions: [
                StarterKit
            ],
            content: initialContent,
            editorProps: {
                attributes: {
                    class: "ProseMirror tiptap-content",
                    spellcheck: "true",
                    style: "white-space: pre-wrap;"
                }
            },
            onUpdate({ editor }) {
                dotNetRef.invokeMethodAsync(
                    "OnEditorContentChanged",
                    editor.getHTML()
                );
            }
        });

        return editor;
    },

    setContent: function (editor, content) {
        editor.commands.setContent(content, false);
    },

    destroy: function (editor) {
        editor.destroy();
    }
};
