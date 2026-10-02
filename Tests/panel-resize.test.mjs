import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const source = await readFile(new URL('../WriterApp.UI.Shared/wwwroot/panel-resize.js', import.meta.url), 'utf8');
const { attach, detach, widthBounds } = await import(`data:text/javascript;base64,${Buffer.from(source).toString('base64')}`);

function fixture(side, stored = null, storageBlocked = false) {
    const values = new Map(stored === null ? [] : [[`prosa.panel-width.${side}`, String(stored)]]);
    globalThis.localStorage = {
        getItem: key => { if (storageBlocked) throw Error('Storage unavailable'); return values.get(key) ?? null; },
        setItem: (key, value) => { if (storageBlocked) throw Error('Storage unavailable'); values.set(key, value); }
    };
    globalThis.window = { innerWidth: 1280, addEventListener() {}, removeEventListener() {} };
    globalThis.ResizeObserver = class { observe() {} disconnect() {} };
    const styles = new Map(), attrs = new Map(), events = new Map(), captures = new Set();
    const classes = { add() {}, remove() {} };
    const variable = side === 'left' ? '--app-nav-width' : '--context-panel-width';
    const panel = { getBoundingClientRect: () => ({ width: parseFloat(styles.get(variable) ?? (side === 'left' ? '180' : '360')) }) };
    const owner = { clientWidth: 1280, style: { setProperty: (key, value) => styles.set(key, value) }, classList: classes,
        querySelector: selector => selector === '.editor-workspace' ? { clientWidth: 1052 } : panel,
        closest: () => null };
    const handle = {
        closest: () => owner, setAttribute: (key, value) => attrs.set(key, value), classList: classes, focus() {},
        hasPointerCapture: id => captures.has(id), setPointerCapture: id => captures.add(id), releasePointerCapture: id => captures.delete(id),
        addEventListener: (key, value) => events.set(key, value), removeEventListener: key => events.delete(key)
    };
    const send = (name, props = {}) => events.get(name)?.({ preventDefault() {}, ...props });
    attach(handle, side);
    return { handle, send, values, attrs, events, styles, variable };
}

test('Bounds reserve manuscript space and cap excessively wide saved panels', () => {
    assert.deepEqual(widthBounds('right', 1000), { min: 280, max: 580 });
    assert.deepEqual(widthBounds('left', 1280, 680), { min: 140, max: 140 });
    assert.deepEqual(widthBounds('right', 2000), { min: 280, max: 680 });
    const f = fixture('right', 20000);
    assert.equal(f.attrs.get('aria-valuenow'), '632');
    detach(f.handle);
});

test('Right drag and arrow keys use opposite direction and persist the resulting width', () => {
    const f = fixture('right');
    f.send('pointerdown', { button: 0, pointerId: 1, clientX: 800 });
    f.send('pointermove', { pointerId: 1, clientX: 700 });
    f.send('pointerup');
    assert.equal(f.attrs.get('aria-valuenow'), '460');
    assert.equal(f.values.get('prosa.panel-width.right'), '460');
    f.send('keydown', { key: 'ArrowRight' });
    assert.equal(f.attrs.get('aria-valuenow'), '444');
    detach(f.handle);
    assert.equal(f.events.size, 0);
});

test('Left dragging grows the navigation and cancellation restores its previous width', () => {
    const f = fixture('left');
    f.send('pointerdown', { button: 0, pointerId: 2, clientX: 180 });
    f.send('pointermove', { pointerId: 2, clientX: 240 });
    assert.equal(f.attrs.get('aria-valuenow'), '240');
    f.send('pointercancel');
    assert.equal(f.attrs.get('aria-valuenow'), '180');
    assert.equal(f.values.has('prosa.panel-width.left'), false);
    detach(f.handle);
});

test('Resizing and keyboard limits remain usable when preference storage is unavailable', () => {
    const f = fixture('right', null, true);
    f.send('keydown', { key: 'End' });
    assert.equal(f.attrs.get('aria-valuenow'), '632');
    f.send('keydown', { key: 'Home' });
    assert.equal(f.attrs.get('aria-valuenow'), '280');
    f.send('dblclick');
    assert.equal(f.attrs.get('aria-valuenow'), '360');
    detach(f.handle);
});

test('Reattaching after a host style update restores width without duplicating listeners', () => {
    const f = fixture('right', 504);
    const listener = f.events.get('keydown');
    f.styles.delete(f.variable);
    attach(f.handle, 'right');
    assert.equal(f.styles.get(f.variable), '504px');
    assert.equal(f.events.get('keydown'), listener);
    f.send('keydown', { key: 'ArrowLeft' });
    assert.equal(f.attrs.get('aria-valuenow'), '520');
    detach(f.handle);
});
