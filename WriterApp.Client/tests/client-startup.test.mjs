import assert from "node:assert/strict";
import { test } from "node:test";
import { createBootResourceLoader } from "../wwwroot/js/client-startup.js";

test("runtime modules retain the framework module loader", () => {
    const loader = createBootResourceLoader(() => { throw new Error("Unexpected fetch"); });
    assert.equal(loader("dotnetjs", "dotnet.js", "/app/_framework/dotnet.js", "sha256-test"), null);
});

test("boot downloads revalidate the cache and preserve integrity", async () => {
    const requests = [];
    const response = new Response("asset");
    const loader = createBootResourceLoader(async (...args) => { requests.push(args); return response; });
    assert.equal(await loader("assembly", "Shared.wasm", "/app/_framework/Shared.wasm", "sha256-test"), response);
    assert.deepEqual(requests, [["/app/_framework/Shared.wasm", { cache: "no-cache", integrity: "sha256-test" }]]);
});

test("a stale cached download retries once with a fresh URL and intact integrity", async () => {
    const requests = [];
    const loader = createBootResourceLoader(async (...args) => {
        requests.push(args);
        if (requests.length === 1) throw new TypeError("Failed to fetch");
        return new Response("asset");
    });
    await loader("pdb", "Shared.pdb", "/app/_framework/Shared.pdb?existing=1", "sha256-test");
    assert.deepEqual(requests[1], ["/app/_framework/Shared.pdb?existing=1&boot-retry=1", { cache: "reload", integrity: "sha256-test" }]);
});

test("persistent failures stop after one retry", async () => {
    let requests = 0;
    const loader = createBootResourceLoader(async () => { requests++; return new Response("missing", { status: 404 }); });
    await assert.rejects(loader("assembly", "Shared.wasm", "/app/_framework/Shared.wasm", "sha256-test"), /404/);
    assert.equal(requests, 2);
});
