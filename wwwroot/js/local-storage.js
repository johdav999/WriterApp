window.writerAppStorage = window.writerAppStorage || {};
window.writerAppStorage.loadLegacyDocuments = function (storagePrefix, indexKey, autosavePrefix, migratedPrefix) {
    var results = [];
    try {
        var length = localStorage.length;
        for (var i = 0; i < length; i++) {
            var key = localStorage.key(i);
            if (!key || key.indexOf(storagePrefix) !== 0) {
                continue;
            }

            if (key === indexKey || key.indexOf(autosavePrefix) === 0 || key.indexOf(migratedPrefix) === 0) {
                continue;
            }

            var id = key.substring(storagePrefix.length);
            var json = localStorage.getItem(key);
            var migratedValue = localStorage.getItem(migratedPrefix + id);
            var migrated = migratedValue === "1" || migratedValue === "true";
            results.push({ id: id, json: json, migrated: migrated });
        }
    } catch (e) {
        return results;
    }

    return results;
};

window.writerAppStorage.listLegacyDocumentIds = function (storagePrefix, indexKey, autosavePrefix, migrationPrefix) {
    var result = { items: [], error: null };
    try {
        var length = localStorage.length;
        for (var i = 0; i < length; i++) {
            var key = localStorage.key(i);
            if (!key || key.indexOf(storagePrefix) !== 0) {
                continue;
            }

            if (key === indexKey || key.indexOf(autosavePrefix) === 0 || key.indexOf(migrationPrefix) === 0) {
                continue;
            }

            var id = key.substring(storagePrefix.length);
            var migratedValue = localStorage.getItem(migrationPrefix + id);
            var migrated = migratedValue === "1" || migratedValue === "true";
            result.items.push({ id: id, migrated: migrated });
        }

        if (window.__writerAppDebugStorage) {
            console.debug("[writerAppStorage] listLegacyDocumentIds:", result.items.length);
        }
    } catch (e) {
        result.error = e && e.message ? e.message : "localStorage unavailable";
    }

    return result;
};

window.writerAppStorage.loadLegacyDocumentJson = function (storagePrefix, id) {
    var result = { id: id, json: null, error: null };
    try {
        var key = storagePrefix + id;
        result.json = localStorage.getItem(key);
        if (window.__writerAppDebugStorage) {
            console.debug("[writerAppStorage] loadLegacyDocumentJson:", key, result.json ? result.json.length : 0);
        }
    } catch (e) {
        result.error = e && e.message ? e.message : "localStorage unavailable";
    }

    return result;
};

window.writerAppStorage.getOrigin = function () {
    return window.location.origin;
};

window.writerAppStorage.countKeysWithPrefix = function (prefix) {
    var count = 0;
    try {
        for (var i = 0; i < localStorage.length; i++) {
            var key = localStorage.key(i);
            if (key && key.indexOf(prefix) === 0) {
                count++;
            }
        }
    } catch (e) {
        return 0;
    }

    return count;
};

window.writerAppStorage.diagnostics = function (storagePrefix, indexKey, autosavePrefix, migrationPrefix) {
    var result = {
        existsWriterAppStorage: !!window.writerAppStorage,
        existsLoadLegacyDocuments: !!(window.writerAppStorage && window.writerAppStorage.loadLegacyDocuments),
        origin: window.location.origin,
        keyCount: 0,
        indexKeyExists: false,
        indexValueLength: 0,
        sampleKeys: [],
        matchedDocKeysCount: 0,
        matchedAutosaveKeysCount: 0,
        matchedMigratedKeysCount: 0,
        error: null
    };

    try {
        result.keyCount = localStorage.length;
        var indexValue = localStorage.getItem(indexKey);
        result.indexKeyExists = indexValue !== null;
        result.indexValueLength = indexValue ? indexValue.length : 0;

        var sample = [];
        for (var i = 0; i < localStorage.length; i++) {
            var key = localStorage.key(i);
            if (!key) {
                continue;
            }

            if (key.indexOf(storagePrefix) === 0) {
                result.matchedDocKeysCount++;
            }
            if (key.indexOf(autosavePrefix) === 0) {
                result.matchedAutosaveKeysCount++;
            }
            if (key.indexOf(migrationPrefix) === 0) {
                result.matchedMigratedKeysCount++;
            }
            if (key === indexKey) {
                result.matchedDocKeysCount = result.matchedDocKeysCount;
            }

            if (sample.length < 50) {
                if (key.indexOf(storagePrefix) === 0 || key.indexOf(autosavePrefix) === 0 || key.indexOf(migrationPrefix) === 0 || key === indexKey) {
                    sample.push(key);
                }
            }
        }

        result.sampleKeys = sample;
    } catch (e) {
        result.error = e && e.message ? e.message : "localStorage unavailable";
    }

    return result;
};
