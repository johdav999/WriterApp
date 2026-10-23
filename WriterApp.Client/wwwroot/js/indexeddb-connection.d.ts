export function indexedDbConnection(name: string, version: number, upgrade: (db: IDBDatabase, transaction: IDBTransaction | null) => void): {
    get(): Promise<IDBDatabase>;
    transaction(stores: string | string[], mode?: IDBTransactionMode, options?: IDBTransactionOptions): Promise<IDBTransaction>;
    close(): Promise<void>;
};
