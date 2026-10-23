// Verify the installed emulator's conflict feedback without resolving copies or applying AI.
import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
const mode = process.argv[2] ?? 'verify';
process.argv[2] = 'writing-feedback'; // Import connection helpers without another smoke mode.
process.env.PROSA_ADB ??= 'C:/Program Files (x86)/Android/android-sdk/platform-tools/adb.exe';
const { connect, adb, capture } = await import('./android-native-smoke.mjs');
const output = resolve('artifacts/android-writing-feedback');
mkdirSync(output, { recursive: true });
const c = await connect();
const evaluate = c.evaluate;
async function snapshot() {
    const files = adb('shell', 'run-as', 'com.prosa.writer.android', 'find', 'files/development/documents', '-maxdepth', '1', '-type', 'f').split(/\r?\n/).filter(p => p.endsWith('.json')).sort();
    return {
        route: await evaluate('location.pathname'),
        writing: createHash('sha256').update(await evaluate(`document.querySelector('.ProseMirror').innerHTML`)).digest('hex'),
        documents: Object.fromEntries(files.map(p => [p, adb('shell', 'run-as', 'com.prosa.writer.android', 'sha256sum', p).split(' ')[0]]))
    };
}
try {
    if (mode === 'baseline') {
        writeFileSync(resolve(output, 'baseline.json'), JSON.stringify(await snapshot(), null, 2));
        console.log('Recorded emulator manuscript hashes before APK update.');
    } else {
        const baseline = JSON.parse(readFileSync(resolve(output, 'baseline.json'), 'utf8'));
        if (!await evaluate(`!!document.querySelector('.ProseMirror')`)) {
            await evaluate(`document.querySelector('a[href="${baseline.route}"]').click()`);
            for (let i = 0; i < 40 && !await evaluate(`!!document.querySelector('.ProseMirror')`); i++) await new Promise(r => setTimeout(r, 500));
        }
        assert.deepEqual(await snapshot(), baseline);
        await evaluate(`document.querySelector('[data-panel-action="right"]').click()`);
        await new Promise(r => setTimeout(r, 300));
        await evaluate(`(()=>{const s=document.querySelector('select[aria-label="Writing target"]');s.value='Section';s.dispatchEvent(new Event('change',{bubbles:true}));})()`);
        await new Promise(r => setTimeout(r, 200));
        await evaluate(`document.querySelector('button[aria-label="Change tone section"]').click()`);
        await new Promise(r => setTimeout(r, 200));
        await evaluate(`(()=>{const s=document.querySelector('select[aria-label="Tone"]');s.value='Casual';s.dispatchEvent(new Event('change',{bubbles:true}));})()`);
        await new Promise(r => setTimeout(r, 200));
        await evaluate(`(()=>{const e=document.querySelector('.writing-preview');e.scrollIntoView({block:'center'});e.click();})()`);
        let feedback;
        for (let attempt = 0; attempt < 60; attempt++) {
            feedback = await evaluate(`(()=>{const f=document.querySelector('.writing-feedback');const a=f?.querySelector('[role=alert]');const r=a?.getBoundingClientRect();return {text:a?.textContent,visible:!!r&&r.top>=0&&r.bottom<=innerHeight,link:f?.querySelector('a')?.getAttribute('href'),adjacent:document.querySelector('.writing-preview')?.nextElementSibling===f};})()`);
            if (feedback.text?.includes('cloud sync conflict')) break;
            await new Promise(r => setTimeout(r, 500));
        }
        assert.ok(feedback.text?.includes('cloud sync conflict'));
        assert.ok(feedback.visible, 'Failure must be in the Android viewport');
        assert.ok(feedback.adjacent);
        assert.equal(feedback.link, '/sync/conflicts');
        assert.deepEqual(await snapshot(), baseline);
        writeFileSync(resolve(output, 'results.json'), JSON.stringify(feedback, null, 2));
        capture('tone-conflict-visible');
        console.log('PASS: tone conflict is visible beside preview; recovery link and manuscript hashes verified.');
    }
} finally { c.close(); }
