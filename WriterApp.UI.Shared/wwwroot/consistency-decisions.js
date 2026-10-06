function storageKey(scope, document, key) {
    if (!/^[a-f0-9]{64}$/i.test(scope) || !/^[a-f0-9-]{36}$/i.test(document) || !/^[a-f0-9]{64}$/i.test(key)) {
        throw new Error('Invalid consistency decision identity.');
    }
    return `prosa.consistency.intentional.${scope}.${document}.${key}`;
}
export function isIntentional(scope, document, key) {
    return localStorage.getItem(storageKey(scope, document, key)) === 'true';
}
export function setIntentional(scope, document, key, intentional) {
    const name = storageKey(scope, document, key);
    if (intentional) localStorage.setItem(name, 'true');
    else localStorage.removeItem(name);
}
