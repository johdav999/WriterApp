import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const source = await readFile(new URL('../WriterApp.Device.Shared/wwwroot/library-drop.js', import.meta.url), 'utf8');
const { attach, detach, attachProjects, detachProjects } = await import(`data:text/javascript;base64,${Buffer.from(source).toString('base64')}`);

function fixture() {
    const zone = new EventTarget();
    const classes = new Set();
    zone.classList = { add: value => classes.add(value), remove: value => classes.delete(value) };
    const input = new EventTarget();
    let changes = 0;
    input.addEventListener('change', () => changes++);
    zone.querySelector = () => input;
    const errors = [];
    const receiver = { invokeMethodAsync: async (_method, message) => errors.push(message) };
    const dispatch = (name, files) => {
        const event = new Event(name, { cancelable: true });
        event.dataTransfer = { types: ['Files'], files };
        zone.dispatchEvent(event);
        return event;
    };
    attach(zone, receiver);
    return { zone, input, classes, errors, receiver, dispatch, changes: () => changes };
}

test('one dropped file reaches the existing import input exactly once, including after reattachment', () => {
    const f = fixture();
    attach(f.zone, f.receiver);
    const files = [{ name: 'draft.docx', size: 1024 }];
    assert.equal(f.dispatch('drop', files).defaultPrevented, true);
    assert.equal(f.input.files, files);
    assert.equal(f.changes(), 1);
    assert.deepEqual(f.errors, []);
});

test('multiple and oversized files are rejected before an import can create documents', () => {
    const f = fixture();
    f.dispatch('drop', [{ size: 1 }, { size: 1 }]);
    f.dispatch('drop', [{ size: 5 * 1024 * 1024 + 1 }]);
    assert.equal(f.changes(), 0);
    assert.equal(f.errors.length, 2);
    assert.match(f.errors[0], /one document/);
    assert.match(f.errors[1], /5 MB/);
    f.dispatch('drop', [{ size: 5 * 1024 * 1024 }]);
    assert.equal(f.changes(), 1);
});

test('nested drag events retain the highlight and disposal removes all import handlers', () => {
    const f = fixture();
    f.dispatch('dragenter', []);
    f.dispatch('dragenter', []);
    f.dispatch('dragleave', []);
    assert.equal(f.classes.has('is-dragging'), true);
    f.dispatch('dragleave', []);
    assert.equal(f.classes.has('is-dragging'), false);
    f.dispatch('dragenter', []);
    detach(f.zone);
    assert.equal(f.classes.has('is-dragging'), false);
    assert.equal(f.dispatch('drop', [{ size: 10 }]).defaultPrevented, false);
    assert.equal(f.changes(), 0);
});

function projectFixture() {
    const collection = new EventTarget();
    collection.dataset = { libraryDisabled: 'false' };
    const row = (dataset, draggable = false) => {
        const classes = new Set();
        return { dataset, draggable, classes,
            classList: { add: name => classes.add(name), remove: name => classes.delete(name) },
            closest(selector) {
                return selector === '[data-library-project-id]' && dataset.libraryProjectId
                    || selector === '[data-standalone-document-id]' && dataset.standaloneDocumentId ? this : null;
            },
            contains: element => element === null ? false : element === undefined ? false : element.dataset === dataset };
    };
    const document = row({ standaloneDocumentId: 'document-id' }, true);
    const project = row({ libraryProjectId: 'project-id' });
    const other = row({});
    collection.contains = element => [document, project, other].includes(element);
    const calls = [];
    let finish;
    const receiver = { invokeMethodAsync: (method, ...args) => {
        calls.push([method, ...args]);
        return new Promise(resolve => { finish = resolve; });
    } };
    const dataTransfer = { data: {}, setData(type, value) { this.data[type] = value; } };
    const dispatch = (name, target = document, relatedTarget = null) => {
        const event = new Event(name, { cancelable: true });
        Object.defineProperty(event, 'target', { value: target });
        event.dataTransfer = dataTransfer;
        event.relatedTarget = relatedTarget;
        collection.dispatchEvent(event);
        return event;
    };
    attachProjects(collection, receiver);
    return { collection, document, project, other, calls, dataTransfer, dispatch, receiver, finish: () => finish?.() };
}

test('standalone document drops invoke one persisted move and block further drops while saving', async () => {
    const f = projectFixture();
    attachProjects(f.collection, f.receiver);
    f.dispatch('dragstart');
    assert.equal(f.dataTransfer.effectAllowed, 'move');
    assert.equal(f.document.classes.has('is-document-dragging'), true);
    assert.equal(f.dispatch('dragover', f.project).defaultPrevented, true);
    assert.equal(f.project.classes.has('is-project-drop-target'), true);
    assert.equal(f.dispatch('drop', f.project).defaultPrevented, true);
    assert.deepEqual(f.calls, [['MoveDocumentToProject', 'document-id', 'project-id']]);
    assert.equal(f.project.classes.size, 0);
    assert.equal(f.document.classes.size, 0);
    assert.equal(f.dispatch('dragstart').defaultPrevented, true);
    f.dispatch('drop', f.project);
    assert.equal(f.calls.length, 1);
    f.finish();
    await new Promise(resolve => setImmediate(resolve));
    assert.equal(f.dispatch('dragstart').defaultPrevented, false);
});

test('only an internal standalone drag over an enabled project is accepted', () => {
    const f = projectFixture();
    assert.equal(f.dispatch('drop', f.project).defaultPrevented, false);
    assert.equal(f.dispatch('dragstart', f.project).defaultPrevented, true);
    f.dispatch('dragstart');
    assert.equal(f.dispatch('dragover', f.other).defaultPrevented, false);
    f.dispatch('drop', f.other);
    assert.equal(f.calls.length, 0);
    f.collection.dataset.libraryDisabled = 'true';
    assert.equal(f.dispatch('dragstart').defaultPrevented, true);
    assert.equal(f.dispatch('dragover', f.project).defaultPrevented, false);
    f.dispatch('drop', f.project);
    assert.equal(f.calls.length, 0);
});

test('nested target transitions retain highlighting; cancellation and detach clean up', () => {
    const f = projectFixture();
    f.dispatch('dragstart');
    f.dispatch('dragenter', f.project);
    f.dispatch('dragleave', f.project, f.project);
    assert.equal(f.project.classes.has('is-project-drop-target'), true);
    f.dispatch('dragleave', f.project, f.other);
    assert.equal(f.project.classes.has('is-project-drop-target'), false);
    f.dispatch('dragover', f.project);
    f.dispatch('dragend');
    assert.equal(f.project.classes.size, 0);
    f.dispatch('dragstart');
    f.dispatch('dragover', f.project);
    detachProjects(f.collection);
    assert.equal(f.document.classes.size, 0);
    assert.equal(f.project.classes.size, 0);
    assert.equal(f.dispatch('drop', f.project).defaultPrevented, false);
    assert.equal(f.calls.length, 0);
});
