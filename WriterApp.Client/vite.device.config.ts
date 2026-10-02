import { defineConfig } from "vite";
import { fileURLToPath } from "node:url";
import { atomicEditorOutput } from "./atomic-editor-output.mjs";

export default defineConfig({
  plugins: [atomicEditorOutput(
    fileURLToPath(new URL("../WriterApp.Device.Shared/wwwroot/editor/", import.meta.url)),
    fileURLToPath(new URL("dist/device-retired/", import.meta.url))
  )],
  // The existing lockfile has nested ProseMirror copies; a single plugin registry is required.
  resolve: { dedupe: ["prosemirror-state", "prosemirror-model", "prosemirror-view", "prosemirror-transform", "prosemirror-keymap"] },
  build: {
    // Rolldown can safely truncate staging files; published assets may be mapped.
    outDir: "dist/device-editor",
    emptyOutDir: true,
    lib: { entry: "src/device-editor.ts", formats: ["es"], fileName: () => "device-editor.js", cssFileName: "device-editor" },
    target: "es2020"
  }
});
