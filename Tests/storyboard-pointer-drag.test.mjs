import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const source = await readFile(new URL('../WriterApp.UI.Shared/wwwroot/storyboard.js', import.meta.url), 'utf8');
const { attachPointerDrag, detachPointerDrag, pointerPlacement } = await import(`data:text/javascript;base64,${Buffer.from(source).toString('base64')}`);

function fixture() {
    const classes = () => {
        const values = new Set();
        return { add: n => values.add(n), remove: n => values.delete(n), contains: n => values.has(n) };
    };
    const target = () => ({ listeners: new Map(),
        addEventListener(name, fn) { this.listeners.set(name, fn); },
        removeEventListener(name) { this.listeners.delete(name); } });
    const doc = target(), win = target(), root = target(), captures = new Set(), frames = new Map();
    let frameId = 0, hit, wait = false, finish;
    globalThis.requestAnimationFrame = fn => { frames.set(++frameId, fn); return frameId; };
    globalThis.cancelAnimationFrame = id => frames.delete(id);
    const list = { dataset: { storyboardChapter: 'chapter' }, classList: classes(),
        contains: el => el === list || el === a || el === b || el === gap || el === end,
        closest: selector => selector === '[data-storyboard-chapter]' ? list : null };
    const card = id => {
        const el = { dataset: { storyboardScene: id }, classList: classes(),
            getBoundingClientRect: () => ({ top: 100, height: 80 }),
            closest: selector => selector === '[data-storyboard-scene]' ? el : selector === '[data-storyboard-chapter]' ? list : null };
        return el;
    };
    const a = card('a'), b = card('b');
    const makeGap = before => {
        const el = { dataset: { storyboardBefore: before }, classList: classes(),
            closest: selector => selector === '[data-storyboard-before]' ? el : selector === '[data-storyboard-chapter]' ? list : null };
        return el;
    };
    const gap = makeGap('b'), end = makeGap('');
    a.nextElementSibling = gap; b.nextElementSibling = end;
    doc.defaultView = win; doc.elementFromPoint = () => hit;
    Object.assign(root, { dataset: { storyboardBusy: 'false' }, classList: classes(), ownerDocument: doc,
        contains: list.contains, hasPointerCapture: id => captures.has(id),
        setPointerCapture: id => captures.add(id), releasePointerCapture: id => captures.delete(id) });
    const calls = [], reference = { invokeMethodAsync(...args) {
        calls.push(args); return wait ? new Promise(resolve => finish = resolve) : Promise.resolve();
    } };
    const send = (surface, name, el = a, extra = {}) => {
        hit = el;
        const e = { target: el, pointerId: 1, button: 0, clientX: 10, clientY: 110,
            prevented: false, stopped: false, preventDefault() { this.prevented = true; },
            stopImmediatePropagation() { this.stopped = true; }, ...extra };
        return { event: e, result: surface.listeners.get(name)?.(e) };
    };
    const start = () => { send(root, 'pointerdown'); send(doc, 'pointermove', b, { clientX: 100 }); };
    attachPointerDrag(root, reference);
    return { root, doc, win, a, b, gap, end, list, calls, captures, frames, reference, send, start,
        hold: () => wait = true, finish: () => finish() };
}

test('Gaps and both halves of a card preserve before/append placement', () => {
    const f = fixture();
    assert.equal(pointerPlacement(f.list, f.gap, 0).before, 'b');
    assert.equal(pointerPlacement(f.list, f.a, 110).before, 'a');
    assert.equal(pointerPlacement(f.list, f.a, 170).before, 'b');
    assert.equal(pointerPlacement(f.list, f.b, 170).before, null);
    assert.equal(pointerPlacement(f.list, f.list, 0).before, null);
    detachPointerDrag(f.root);
});

test('A click keeps normal selection; a drag captures and invokes exactly one move', async () => {
    const f = fixture();
    f.send(f.root, 'pointerdown'); f.send(f.doc, 'pointermove', f.a, { clientX: 12 });
    await f.send(f.doc, 'pointerup').result;
    assert.equal(f.calls.length, 0); assert.equal(f.captures.size, 0);
    assert.equal(f.send(f.root, 'click').event.prevented, false);
    f.start(); assert.equal(f.captures.size, 1);
    await f.send(f.doc, 'pointerup', f.b).result;
    assert.deepEqual(f.calls, [['DropStoryboardSceneAsync', 'a', 'chapter', 'b']]);
    assert.equal(f.frames.size, 0); assert.equal(f.captures.size, 0);
    assert.equal(f.send(f.root, 'click').event.stopped, true);
    f.send(f.root, 'pointerdown');
    assert.equal(f.send(f.root, 'click').event.stopped, false);
    detachPointerDrag(f.root);
});

test('Escape, cancellation, outside releases, and self drops cannot save or strand the next drag', async () => {
    const f = fixture();
    for (const [surface, event, extra] of [[f.doc, 'keydown', { key: 'Escape' }],
        [f.doc, 'pointercancel', {}], [f.root, 'lostpointercapture', {}], [f.win, 'blur', {}]]) {
        f.start(); f.send(surface, event, f.b, extra);
        await f.send(f.doc, 'pointerup', f.b).result;
        assert.equal(f.calls.length, 0); assert.equal(f.captures.size, 0); assert.equal(f.frames.size, 0);
    }
    f.send(f.root, 'pointerdown');
    await f.send(f.doc, 'pointerup', { closest: () => null }).result;
    f.start(); await f.send(f.doc, 'pointerup', f.a).result;
    assert.equal(f.calls.length, 0);
    f.start(); await f.send(f.doc, 'pointerup', f.b).result;
    assert.equal(f.calls.length, 1);
    detachPointerDrag(f.root);
});

test('Editing controls and a busy board reject drag initiation', async () => {
    const f = fixture();
    const control = { closest: selector => selector.startsWith('button,') ? control : f.a };
    f.send(f.root, 'pointerdown', control); f.send(f.doc, 'pointermove', f.b, { clientX: 100 });
    await f.send(f.doc, 'pointerup', f.b).result;
    f.root.dataset.storyboardBusy = 'true'; f.start();
    await f.send(f.doc, 'pointerup', f.b).result;
    assert.equal(f.calls.length, 0);
    f.root.dataset.storyboardBusy = 'false'; f.start();
    f.root.dataset.storyboardBusy = 'true';
    await f.send(f.doc, 'pointerup', f.b).result;
    assert.equal(f.calls.length, 0); assert.equal(f.frames.size, 0);
    detachPointerDrag(f.root);
});

test('Pending saves block duplicate moves; repeated attachment and teardown remove document listeners', async () => {
    const f = fixture();
    const listener = f.doc.listeners.get('pointerup');
    attachPointerDrag(f.root, f.reference); assert.equal(f.doc.listeners.get('pointerup'), listener);
    f.hold(); f.start(); const saving = f.send(f.doc, 'pointerup', f.b).result;
    f.start(); await f.send(f.doc, 'pointerup', f.b).result;
    assert.equal(f.calls.length, 1); f.finish(); await saving;
    detachPointerDrag(f.root);
    assert.equal(f.root.listeners.size, 0); assert.equal(f.doc.listeners.size, 0); assert.equal(f.win.listeners.size, 0);
});
