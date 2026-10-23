// Verify automatic preparation on the retained Android conflict manuscript.
// Does not resolve conflicts, apply proposals, or change authored writing.
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
const mode = process.argv[2] ?? 'verify';
process.argv[2] = 'automatic-sync';
process.env.PROSA_ADB ??= 'C:/Program Files (x86)/Android/android-sdk/platform-tools/adb.exe';
process.env.PROSA_ANDROID_EVIDENCE ??= 'artifacts/android-automatic-sync';
const { connect, adb, capture } = await import('./android-native-smoke.mjs');
const output = resolve(process.env.PROSA_ANDROID_EVIDENCE);
mkdirSync(output, { recursive: true });
const delay = ms => new Promise(r => setTimeout(r, ms));
const c = await connect();
const metadata = new Set(['serverDocumentId', 'serverProjectId', 'serverVersion', 'localRevision', 'syncState',
    'lastSyncedAtUtc', 'updatedAtUtc', 'serverSectionId', 'serverPageId', 'metadataRevision',
    'serverMetadataRevision', 'metadataDirty', 'serverPrimaryDocumentId']);
function authored(value) {
    if (Array.isArray(value)) return value.map(authored);
    if (value && typeof value === 'object') return Object.fromEntries(Object.entries(value)
        .filter(([k]) => !metadata.has(k)).map(([k, v]) => [k, authored(v)]));
    return value;
}
async function snapshot() {
    for (let i = 0; i < 50 && !await c.evaluate(`!!document.querySelector('.ProseMirror')`); i++) await delay(200);
    const files = adb('shell', 'run-as', 'com.prosa.writer.android', 'find', 'files/development/documents', '-maxdepth', '1', '-type', 'f')
        .split(/\r?\n/).filter(p => p.endsWith('.json')).sort();
    const documents = Object.fromEntries(files.map(path => [path, createHash('sha256')
        .update(JSON.stringify(authored(JSON.parse(adb('shell', 'run-as', 'com.prosa.writer.android', 'cat', path))))).digest('hex')]));
    const writing = await c.evaluate(`document.querySelector('.ProseMirror')?.innerHTML`);
    assert.ok(writing, 'Open the retained conflict manuscript before running verification');
    return { route: await c.evaluate('location.pathname'), documents,
        writing: createHash('sha256').update(writing).digest('hex') };
}
try {
    if (mode === 'baseline') {
        writeFileSync(resolve(output, 'baseline.json'), JSON.stringify(await snapshot(), null, 2));
        console.log('Recorded authored writing/planning hashes before updating the Android APK.');
    } else {
        const baseline = JSON.parse(readFileSync(resolve(output, 'baseline.json'), 'utf8'));
        assert.deepEqual(await snapshot(), baseline);
        await c.evaluate(`document.querySelector('[data-panel-action="right"]')?.click()`);
        await delay(200);
        await c.evaluate(`(()=>{
            const b=[...document.querySelectorAll('button')].find(b=>b.textContent.trim()==='Consistency Coach');
            if(b)b.click();
        })()`);
        for (let i = 0; i < 30 && !await c.evaluate(`!!document.querySelector('.consistency-run')`); i++) await delay(200);
        assert.ok(await c.evaluate(`document.body.textContent.includes('saves and syncs your latest writing automatically')`));
        await c.evaluate(`document.querySelector('.consistency-run').click()`);
        let result;
        for (let i = 0; i < 60; i++) {
            result = await c.evaluate(`(()=>{const p=document.querySelector('.consistency-workspace .ai-response');
                return {error:p?.querySelector('[role=alert]')?.textContent,
                    review:p?.querySelector('a[href="/sync/conflicts"]')?.textContent,
                    busy:!!p?.querySelector('button'), result:!!p?.querySelector('section[aria-label="Review AI result"]')};})()`);
            if (result.error?.includes('cloud sync conflict') && result.review) break;
            await delay(500);
        }
        assert.ok(result.error?.includes('cloud sync conflict'), JSON.stringify(result));
        assert.equal(result.review, 'Review both versions');
        assert.equal(result.busy, false); assert.equal(result.result, false);
        assert.ok(!result.error.includes('Save and synchronize'));
        await c.evaluate(`document.querySelector('.consistency-workspace .ai-response').scrollIntoView({block:'center'})`);
        await delay(200);
        result.visible = await c.evaluate(`(()=>{const r=document.querySelector('.consistency-workspace .ai-response [role=alert]').getBoundingClientRect();return r.top>=0&&r.bottom<=innerHeight;})()`);
        assert.ok(result.visible);
        assert.deepEqual(await snapshot(), baseline);
        capture('consistency-preparation-conflict');
        writeFileSync(resolve(output, 'results.json'), JSON.stringify(result, null, 2));
        console.log('PASS: preparation reports the actual sync conflict with review navigation; all authored writing and planning retained.', result);
    }
} finally { c.close(); }
