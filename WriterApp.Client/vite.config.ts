import { defineConfig } from "vite";
import { fileURLToPath } from "node:url";
import { atomicEditorOutput } from "./atomic-editor-output.mjs";

export default defineConfig({
  plugins: [atomicEditorOutput(
    fileURLToPath(new URL("wwwroot/js/", import.meta.url)),
    fileURLToPath(new URL("dist/web-retired/", import.meta.url))
  )],
  // Nested ProseMirror copies must share a single plugin registry in both hosts.
  resolve: { dedupe: ["prosemirror-state", "prosemirror-model", "prosemirror-view", "prosemirror-transform", "prosemirror-keymap"] },
  build: {
    outDir: "dist/web-editor",
    emptyOutDir: true,
    lib: {
      entry: "src/tiptap-editor.ts",
      name: "WriterAppTipTap",
      formats: ["iife"],
      fileName: () => "tiptap-editor.bundle.js"
    },
    target: "es2020"
  }
});
