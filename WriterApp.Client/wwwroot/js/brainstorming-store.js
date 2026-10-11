const database = 'writerapp.brainstorming.v1';
const bytes = value => new TextEncoder().encode(value).length;
const id = value => String(value).toLowerCase();
function key(partition, document, session) {
    if (!/^[a-f0-9]{64}$/i.test(partition) || !/^[a-f0-9-]{36}$/i.test(String(document)) || !/^[a-f0-9-]{36}$/i.test(String(session))) throw new Error('Invalid conversation identity.');
    return partition + '/' + id(document) + '/' + id(session);
}
async function transaction(work) {
    const db = await new Promise((resolve, reject) => {
        const request = indexedDB.open(database, 1);
        request.onupgradeneeded = () => {
            const sessions = request.result.createObjectStore('sessions', {keyPath: 'key'});
            sessions.createIndex('document', 'document');
            const rejected = request.result.createObjectStore('rejected', {keyPath: 'key'});
            rejected.createIndex('session', 'session');
            request.result.createObjectStore('identity', {keyPath: 'key'});
        };
        request.onsuccess = () => resolve(request.result);
        request.onerror = request.onblocked = () => reject(new Error('Conversation storage unavailable. Existing data was retained.'));
    });
    try { return await new Promise((resolve, reject) => {
        const tx = db.transaction(['sessions', 'rejected', 'identity'], 'readwrite'); let result;
        tx.oncomplete = () => resolve(result);
        tx.onerror = tx.onabort = () => reject(tx.error || new Error('Conversation transaction failed; existing data was retained.'));
        work(tx, value => { result = value; });
    }); } finally { db.close(); }
}
export async function read(partition, document, session) {
    return await transaction((tx, done) => { const request = tx.objectStore('sessions').get(key(partition, document, session)); request.onsuccess = () => done(request.result?.json ?? null); });
}
function retain(tx, session, json) {
    if (bytes(json) > 8001000) throw new Error('Conversation conflict too large.');
    const value = JSON.parse(json); if (value.version !== 1 || !value.operationId) throw new Error('Invalid conversation conflict.');
    const table = tx.objectStore('rejected'), k = session + '/' + id(value.operationId), request = table.get(k);
    request.onsuccess = () => {
        if (request.result) {
            const previous = JSON.parse(request.result.json);
            if (JSON.stringify({...previous,capturedAt:null}) !== JSON.stringify({...value,capturedAt:null})) tx.abort(); return;
        }
        const count = table.index('session').count(session);
        count.onsuccess = () => { if (count.result >= 10) {tx.abort(); return;} table.add({key:k,session,json}); };
    };
}
export async function retainRejected(partition, document, session, json) {
    await transaction(tx => retain(tx, key(partition,document,session), json));
}
export async function write(partition, document, session, json, expected, operation, rejectedJson) {
    if (bytes(json) > 8000000) throw new Error('Conversation cache too large.');
    const incoming = JSON.parse(json), k = key(partition,document,session);
    if (incoming.version !== 1 || incoming.cacheRevision !== expected + 1 || incoming.session?.version !== 1 || id(incoming.session.scope.sessionId) !== id(session)
        || id(incoming.session.scope.documentId) !== id(document)) throw new Error('Invalid conversation envelope.');
    return await transaction((tx, done) => {
        const table = tx.objectStore('sessions'), request = table.get(k);
        request.onsuccess = () => {
            const current = request.result ? JSON.parse(request.result.json) : null;
            if (current && current.version !== 1) {tx.abort(); return;}
            const old = current?.localReceipts.find(r => id(r.operationId) === id(operation));
            const receipt = incoming.localReceipts.find(r => id(r.operationId) === id(operation));
            if (!receipt) {tx.abort(); return;}
            if (old) { if (old.payloadHash !== receipt.payloadHash) {tx.abort(); return;} done({conflict:false,json:request.result.json}); return; }
            if ((current?.cacheRevision ?? 0) !== expected) { retain(tx, k, rejectedJson); done({conflict:true,json:request.result.json}); return; }
            const s = incoming.session;
            const summary = JSON.stringify({scope:s.scope,revision:s.revision,state:s.state,updatedAt:s.updatedAt});
            table.put({key:k,document:partition+'/'+id(document),sessionId:id(session),json,summary}); done({conflict:false,json});
        };
    });
}
export async function list(partition, document, after, limit) {
    if (limit < 1 || limit > 50) throw new Error('Invalid conversation page size.');
    return await transaction((tx, done) => {
        const values = []; const request = tx.objectStore('sessions').index('document').openCursor(IDBKeyRange.only(partition+'/'+id(document)));
        request.onsuccess = () => { const cursor = request.result;
            if (!cursor || values.length === limit) {done(values); return;}
            if (!after || cursor.value.sessionId > id(after)) values.push(cursor.value.summary);
            cursor.continue();
        };
    });
}
export async function readRejected(partition, document, session) {
    return await transaction((tx, done) => { const request = tx.objectStore('rejected').index('session').getAll(key(partition,document,session), 10); request.onsuccess = () => done(request.result.map(r => r.json)); });
}
export async function reviewIdentity(backend) {
    const reviewed = sessionStorage.getItem(database + '/' + backend);
    if (!reviewed) return null;
    return await transaction((tx, done) => { const request = tx.objectStore('identity').get(backend); request.onsuccess = () => done(request.result?.account === reviewed ? reviewed : null); });
}
export async function rememberIdentity(backend, account) {
    if (!/^[a-f0-9]{64}$/i.test(backend) || account !== null && !/^[a-f0-9]{64}$/i.test(account)) throw new Error('Invalid review identity.');
    // Revoke this tab's offline review grant before asynchronous durable cleanup.
    if (account === null) sessionStorage.removeItem(database + '/' + backend);
    await transaction(tx => { const table = tx.objectStore('identity'); if (account === null) table.delete(backend); else table.put({key:backend,account}); });
    if (account !== null) sessionStorage.setItem(database + '/' + backend, account);
}
export function isOffline() { return navigator.onLine === false; }
