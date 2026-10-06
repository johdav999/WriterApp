const database = 'writerapp.web-ai-history.v1';
const store = 'operations';
async function open() {
  return await new Promise((resolve, reject) => {
    const request = indexedDB.open(database, 1);
    request.onupgradeneeded = () => request.result.createObjectStore(store, { keyPath: 'key' });
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(new Error('History storage is unavailable. Keep the draft; no AI save was approved.'));
    request.onblocked = () => reject(new Error('Close an older tab to upgrade history storage.'));
  });
}
async function transaction(work) {
  const db = await open();
  try { return await new Promise((resolve,reject) => {
    const tx = db.transaction(store, 'readwrite'); let result;
    tx.oncomplete = () => resolve(result);
    tx.onerror = tx.onabort = () => reject(tx.error || new Error('History storage failed; intent is retained when possible.'));
    work(tx.objectStore(store), value => { result = value; }, tx);
  }); } finally { db.close(); }
}
export async function prepare(scope,json) {
  const intent = JSON.parse(json);
  if (intent.version !== 1 || !intent.operationId || !intent.source?.documentId || json.length > 2000000 || !scope) throw new Error('Invalid history intent.');
  return await transaction((table, done, tx) => {
    const key = scope + '/' + intent.operationId, get = table.get(key);
    get.onsuccess = () => {
      if (get.result) {
        if (get.result.json !== json) { tx.abort(); return; }
        done(get.result); return;
      }
      const all = table.getAll();
      all.onsuccess = () => {
        const rows = all.result.filter(row => row.scope === scope);
        if (rows.length >= 1000 || rows.reduce((n,row)=>n+row.json.length,0)+json.length > 16000000) { tx.abort(); return; }
        const row = {key, scope, documentId:intent.source.documentId, operationId:intent.operationId, json, status:'Prepared', message:'Approved intent retained; Save is not confirmed.', receipt:null};
        table.add(row); done(row);
      };
    };
  });
}
export async function list(scope,documentId) {
  return JSON.stringify(await transaction((table,done) => {
    const request = table.getAll();
    request.onsuccess = () => done(request.result.filter(row=>row.scope===scope && row.documentId===documentId));
  }));
}
export async function mark(scope,operationId,status,message,receipt) {
  if (!['Prepared','Pending','Confirmed','Rejected','Uncertain'].includes(status)) throw new Error('Invalid history delivery state.');
  await transaction((table,done,tx) => {
    const request=table.get(scope+'/'+operationId);
    request.onsuccess=()=>{
      const row=request.result;
      if (!row) { tx.abort(); return; }
      if (row.status==='Confirmed') { done(); return; }
      table.put({...row,status,message,receipt:receipt ?? row.receipt});done();
    };
  });
}
