import { defineConfig } from "vite";

export default defineConfig({
  resolve: {
    alias: {
      "@tiptap/pm/model": "prosemirror-model",
      "@tiptap/pm/state": "prosemirror-state",
      "@tiptap/pm/view": "prosemirror-view",
      "@tiptap/pm/transform": "prosemirror-transform",
      "@tiptap/pm/commands": "prosemirror-commands",
      "@tiptap/pm/schema-list": "prosemirror-schema-list",
      "@tiptap/pm/history": "prosemirror-history",
      "@tiptap/pm/keymap": "prosemirror-keymap",
      "@tiptap/pm/inputrules": "prosemirror-inputrules"
    }
  },
  build: {
    outDir: "wwwroot/js",
    emptyOutDir: false,
    lib: {
      entry: "src/tiptap-editor.ts",
      name: "WriterAppTipTap",
      formats: ["iife"],
      fileName: () => "tiptap-editor.bundle.js"
    },
    rollupOptions: {
      output: {
        inlineDynamicImports: true
      }
    },
    target: "es2020"
  }
});
