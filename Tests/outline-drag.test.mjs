import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const source = await readFile(new URL('../WriterApp.Device.Shared/wwwroot/outline-drag.js', import.meta.url), 'utf8');
const { attach, detach, dropPlacement } = await import(`data:text/javascript;base64,${Buffer.from(source).toString('base64')}`);

function fixture() {
    const classes = () => {
        const values = new Set();
        return { add: (...names) => names.forEach(n => values.add(n)), remove: (...names) => names.forEach(n => values.delete(n)), contains: n => values.has(n) };
    };
    const rows = [];
    const row = (id, type, parent = '') => {
        const item = { dataset: { outlineId: id, outlineType: type, outlineParent: parent }, classList: classes(),
            closest: selector => selector === '[data-outline-id]' ? item : null,
            getBoundingClientRect: () => ({ top: 0, height: 40 }) };
        rows.push(item); return item;
    };
    const part = row('part', 'part'), chapter = row('chapter', 'chapter', 'part'), empty = row('empty', 'chapter'),
        a = row('a', 'scene', 'chapter'), b = row('b', 'scene', 'chapter');
    const rootTarget = { classList: classes(), closest: selector => selector === '[data-outline-root]' ? rootTarget : null };
    const listeners = new Map(), calls = [], transfer = { effectAllowed: '', dropEffect: '', setData(type, value) { this.type = type; this.value = value; } };
    const root = { dataset: { outlineDisabled: 'false' }, classList: classes(), querySelectorAll: () => rows,
        contains: node => rows.includes(node) || node === rootTarget,
        addEventListener: (name, fn) => listeners.set(name, fn), removeEventListener: name => listeners.delete(name) };
    let resolveDrop;
    let wait = false;
    const reference = { invokeMethodAsync(...args) { calls.push(args); return wait ? new Promise(resolve => { resolveDrop = resolve; }) : Promise.resolve(); } };
    const send = (name, target, y = 20, extra = {}) => {
        const event = { target, clientY: y, dataTransfer: transfer, prevented: false, preventDefault() { this.prevented = true; }, stopPropagation() {}, ...extra };
        const result = listeners.get(name)?.(event);
        return { event, result };
    };
    attach(root, reference);
    return { root, part, chapter, empty, a, b, rootTarget, transfer, listeners, calls, reference, send,
        hold: () => { wait = true; }, finish: () => resolveDrop() };
}

test('Edges reorder siblings while the center chooses only a legal parent', () => {
    assert.equal(dropPlacement('scene', 'scene', 'chapter', .1), 'before');
    assert.equal(dropPlacement('scene', 'scene', 'chapter', .9), 'after');
    assert.equal(dropPlacement('scene', 'chapter', 'part', .5), 'inside');
    assert.equal(dropPlacement('chapter', 'part', null, .5), 'inside');
    assert.equal(dropPlacement('chapter', 'chapter', 'part', .9), 'after');
    assert.equal(dropPlacement('scene', 'part', null, .5), null);
    assert.equal(dropPlacement('part', 'scene', 'chapter', .5), null);
});

test('Native drag data, indicator, drop callback and cleanup carry the chosen position', async () => {
    const f = fixture();
    f.send('dragstart', f.a);
    assert.equal(f.transfer.effectAllowed, 'move');
    assert.equal(f.transfer.value, 'a');
    assert.ok(f.a.classList.contains('outline-is-dragging'));
    assert.ok(f.send('dragover', f.b, 39).event.prevented);
    assert.ok(f.b.classList.contains('outline-drop-after'));
    await f.send('drop', f.b, 39).result;
    assert.deepEqual(f.calls, [['DropOutlineAsync', 'a', 'b', 'after']]);
    assert.ok(!f.b.classList.contains('outline-drop-after'));
    assert.ok(!f.root.classList.contains('outline-drag-active'));
    detach(f.root);
    assert.equal(f.listeners.size, 0);
});

test('Drop into an empty chapter and onto root respects scene versus chapter placement', async () => {
    const f = fixture();
    f.send('dragstart', f.a);
    f.send('dragover', f.empty);
    assert.ok(f.empty.classList.contains('outline-drop-inside'));
    await f.send('drop', f.empty).result;
    f.send('dragstart', f.chapter);
    assert.ok(f.send('dragover', f.rootTarget).event.prevented);
    await f.send('drop', f.rootTarget).result;
    assert.deepEqual(f.calls, [['DropOutlineAsync', 'a', 'empty', 'inside'], ['DropOutlineAsync', 'chapter', null, 'inside']]);
    f.send('dragstart', f.b);
    assert.ok(!f.send('dragover', f.rootTarget).event.prevented);
    await f.send('drop', f.rootTarget).result;
    assert.equal(f.calls.length, 2);
});

test('Self drops, external drags, disabled outline and cancellation cannot write', async () => {
    const f = fixture();
    await f.send('drop', f.b).result;
    f.root.dataset.outlineDisabled = 'true';
    assert.ok(f.send('dragstart', f.a).event.prevented);
    f.root.dataset.outlineDisabled = 'false';
    f.send('dragstart', f.a);
    assert.ok(!f.send('dragover', f.a).event.prevented);
    f.send('dragover', f.b);
    f.send('keydown', f.a, 0, { key: 'Escape' });
    await f.send('drop', f.b).result;
    assert.equal(f.calls.length, 0);
    assert.ok(!f.b.classList.contains('outline-drop-after'));
    assert.ok(!f.root.classList.contains('outline-drag-active'));
});

test('Rerender attachment is idempotent and pending save blocks additional drags', async () => {
    const f = fixture();
    const initial = f.listeners.get('drop');
    attach(f.root, f.reference);
    assert.equal(f.listeners.get('drop'), initial);
    f.hold();
    f.send('dragstart', f.a);
    const saving = f.send('drop', f.b).result;
    f.send('dragend', f.a);
    assert.ok(f.send('dragstart', f.b).event.prevented);
    await f.send('drop', f.empty).result;
    assert.equal(f.calls.length, 1);
    f.finish(); await saving;
    assert.ok(!f.send('dragstart', f.b).event.prevented);
});

test('Leaving the outline clears the indicator and a newly busy host rejects the drop', async () => {
    const f = fixture();
    f.send('dragstart', f.a);
    f.send('dragover', f.b, 1);
    assert.ok(f.b.classList.contains('outline-drop-before'));
    f.send('dragleave', f.b, 1, { relatedTarget: null });
    assert.ok(!f.b.classList.contains('outline-drop-before'));
    f.root.dataset.outlineDisabled = 'true';
    await f.send('drop', f.b).result;
    assert.equal(f.calls.length, 0);
});
