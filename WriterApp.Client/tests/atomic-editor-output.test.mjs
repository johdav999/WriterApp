import assert from "node:assert/strict";
import { mkdtemp, readFile, readdir, rm, stat, utimes, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { test } from "node:test";
import { spawnSync } from "node:child_process";
import { atomicEditorOutput, publishAsset } from "../atomic-editor-output.mjs";

test("publishes chunks and binary assets, preserves unchanged files, and cleans temporary files", async () => {
  const directory = await mkdtemp(join(tmpdir(), "writerapp-assets-"));
  try {
    const destination = join(directory, "device-editor.js");
    await publishAsset(destination, "old bundle");
    await atomicEditorOutput(directory, join(directory, "retired")).writeBundle({}, {
      js: { type: "chunk", fileName: "device-editor.js", code: "new bundle" },
      css: { type: "asset", fileName: "device-editor.css", source: new Uint8Array([0, 128, 255]) }
    });
    assert.equal(await readFile(destination, "utf8"), "new bundle");
    assert.deepEqual(await readFile(join(directory, "device-editor.css")), Buffer.from([0, 128, 255]));
    await utimes(destination, new Date(1000000000000), new Date(1000000000000));
    const before = await stat(destination);
    await publishAsset(destination, "new bundle");
    assert.equal((await stat(destination)).mtimeMs, before.mtimeMs);
    assert.deepEqual((await readdir(directory)).sort(), ["device-editor.css", "device-editor.js", "retired"]);
    assert.deepEqual(await readdir(join(directory, "retired")), []);
  } finally {
    await rm(directory, { recursive: true, force: true });
  }
});

test("invalid destination preserves existing files", async () => {
  const directory = await mkdtemp(join(tmpdir(), "writerapp-assets-"));
  try {
    const blocker = join(directory, "file");
    await writeFile(blocker, "preserve me");
    await assert.rejects(publishAsset(join(blocker, "nested.js"), "bundle"));
    assert.equal(await readFile(blocker, "utf8"), "preserve me");
    assert.deepEqual(await readdir(directory), ["file"]);
  } finally {
    await rm(directory, { recursive: true, force: true });
  }
});

test("Windows publishes a changed memory-mapped file and later cleans the retired file", { skip: process.platform !== "win32" }, async () => {
  const directory = await mkdtemp(join(tmpdir(), "writerapp-mapped-"));
  const destination = join(directory, "editor.js");
  const retirementDirectory = join(directory, "retired");
  try {
    await writeFile(destination, "old bundle");
    const result = spawnSync("powershell.exe", ["-NoProfile", "-Command", `
      $ErrorActionPreference = 'Stop'
      $stream = [IO.File]::Open($env:WRITERAPP_TEST_ASSET, [IO.FileMode]::Open, [IO.FileAccess]::Read, ([IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete))
      $mapping = $null
      $view = $null
      try {
        $mapping = [IO.MemoryMappedFiles.MemoryMappedFile]::CreateFromFile($stream, ('WriterAppTest-' + [guid]::NewGuid()), 0, [IO.MemoryMappedFiles.MemoryMappedFileAccess]::Read, [IO.HandleInheritability]::None, $true)
        $view = $mapping.CreateViewAccessor(0, 0, [IO.MemoryMappedFiles.MemoryMappedFileAccess]::Read)
        $blocked = $false
        try { [IO.File]::WriteAllText($env:WRITERAPP_TEST_ASSET, 'new bundle') } catch { $blocked = $true }
        if (-not $blocked) { throw 'Expected direct overwrite to fail.' }
        & $env:WRITERAPP_TEST_NODE --input-type=module -e $env:WRITERAPP_TEST_SCRIPT
        if ($LASTEXITCODE -ne 0) { throw 'Mapped publication failed.' }
      } finally {
        if ($view) { $view.Dispose() }
        if ($mapping) { $mapping.Dispose() }
        $stream.Dispose()
      }
    `], {
      encoding: "utf8",
      timeout: 30000,
      env: {
        ...process.env,
        WRITERAPP_TEST_ASSET: destination,
        WRITERAPP_TEST_NODE: process.execPath,
        WRITERAPP_TEST_MODULE: new URL("../atomic-editor-output.mjs", import.meta.url).href,
        WRITERAPP_TEST_RETIRED: retirementDirectory,
        WRITERAPP_TEST_SCRIPT: "const { publishAsset } = await import(process.env.WRITERAPP_TEST_MODULE); await publishAsset(process.env.WRITERAPP_TEST_ASSET, 'new bundle', process.env.WRITERAPP_TEST_RETIRED);"
      }
    });
    assert.equal(result.status, 0, result.error?.message ?? result.stderr);
    assert.equal(await readFile(destination, "utf8"), "new bundle");
    assert.equal((await readdir(retirementDirectory)).length, 1);
    await atomicEditorOutput(directory, retirementDirectory).writeBundle({}, {});
    assert.deepEqual(await readdir(retirementDirectory), []);
  } finally {
    await rm(directory, { recursive: true, force: true });
  }
});
