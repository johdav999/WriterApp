import { defineConfig } from "vite";

export default defineConfig({
  build: {
    outDir: "wwwroot/js",
    emptyOutDir: false,
    lib: {
      entry: "src/tiptap-editor.ts",
      name: "WriterAppTipTap",
      formats: ["iife"],
      fileName: () => "tiptap-editor.bundle.js"
    },
    target: "es2020"
  }
});
