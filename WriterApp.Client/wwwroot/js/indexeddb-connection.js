// Recover connections before a transaction starts. Never replay an uncertain write.
export function indexedDbConnection(name, version, upgrade) {
    let opening = null, current = null, closed = false;
    const retire = db => {
        if (current === db) { current = null; opening = null; }
        db.close();
    };
    async function open() {
        for (let attempt = 0; ; attempt++) {
            if (closed) throw new DOMException('Local storage connection was closed.', 'InvalidStateError');
            try {
                return await new Promise((resolve, reject) => {
                    const request = indexedDB.open(name, version); let rejected = false;
                    request.onupgradeneeded = () => upgrade(request.result, request.transaction);
                    request.onblocked = () => {
                        rejected = true;
                        reject(new DOMException('Another tab is blocking local storage. Close other Prosa tabs and retry; retained data is unchanged.', 'InvalidStateError'));
                    };
                    request.onerror = () => { rejected = true; reject(request.error ?? new DOMException('Local storage could not be opened.', 'UnknownError')); };
                    request.onsuccess = () => {
                        const db = request.result;
                        if (rejected || closed) { db.close(); reject(new DOMException('Local storage connection was closed.', 'InvalidStateError')); return; }
                        current = db;
                        db.onversionchange = () => retire(db);
                        db.onclose = () => { if (current === db) { current = null; opening = null; } };
                        resolve(db);
                    };
                });
            } catch (error) {
                // The browser has not started a transaction: reopening cannot duplicate a save.
                if (closed || attempt >= 2 || !['AbortError', 'UnknownError'].includes(error?.name)) throw error;
                await new Promise(resolve => setTimeout(resolve, 50 * (attempt + 1)));
            }
        }
    }
    async function get() {
        if (closed) throw new DOMException('Local storage connection was closed.', 'InvalidStateError');
        if (!opening) {
            const pending = open(); opening = pending;
            pending.catch(() => { if (opening === pending) opening = null; });
        }
        return opening;
    }
    return {
        get,
        async transaction(stores, mode = 'readonly', options) {
            for (let attempt = 0; ; attempt++) {
                const db = await get();
                try { return db.transaction(stores, mode, options); }
                catch (error) {
                    // A closed-connection exception is synchronous: no transaction exists yet.
                    if (attempt || closed || error?.name !== 'InvalidStateError') throw error;
                    retire(db);
                }
            }
        },
        async close() {
            closed = true;
            const pending = opening; opening = null; current = null;
            try { (await pending)?.close(); } catch { /* Failed opens have no connection to close. */ }
        }
    };
}
