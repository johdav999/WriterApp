import { defineConfig } from "vite";

export default defineConfig({
  // The existing lockfile has nested ProseMirror copies; a single plugin registry is required.
  resolve: { dedupe: ["prosemirror-state", "prosemirror-model", "prosemirror-view", "prosemirror-transform", "prosemirror-keymap"] },
  build: {
    outDir: "../WriterApp.Device.Shared/wwwroot/editor",
    emptyOutDir: false,
    lib: { entry: "src/device-editor.ts", formats: ["es"], fileName: () => "device-editor.js", cssFileName: "device-editor" },
    target: "es2020"
  }
});
