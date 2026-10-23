import * as Y from 'yjs';

// y-tiptap 3.0.10 records these mappings when a text block changes type.
// They preserve cursor/selection anchors; they are not manuscript content.
const positions = '__tiptap_position_migrations:root:default';
const fail = (): never => { throw new Error('collaboration_position_metadata_unsupported'); };
const integer = (n: unknown) => typeof n === 'number' && Number.isSafeInteger(n) && n >= 0;
const keys = (v: any, allowed: string[]) => v && typeof v === 'object' && !Array.isArray(v)
    && Object.keys(v).every(k => allowed.includes(k));
const id = (v: any) => keys(v,['client','clock']) && integer(v.client) && integer(v.clock);
const boundary = (v: any) => v === undefined || v === 'start' || v === 'end';

export function validateDocumentRoots(doc: Y.Doc) {
    if(doc.store.pendingStructs || doc.store.pendingDs) throw new Error('collaboration_state_incomplete');
    if([...doc.share.keys()].some(key => key !== 'default' && key !== positions)) throw new Error('collaboration_state_unsupported');
    if(!doc.share.has(positions))return;
    const map = doc.getMap(positions);
    if(map.size > 16384)fail();
    let transfers = 0, bytes = 0;
    for(const [key,value] of map.entries()) {
        const v:any=value;
        if(!keys(v,['version','client','clock','seen','transfers']) || v.version!==1 || !integer(v.client) || !integer(v.clock)
            || key!==`${v.client}:${v.clock}` || !Array.isArray(v.seen) || v.seen.length>4096 || !Array.isArray(v.transfers))fail();
        bytes+=JSON.stringify(v).length;
        transfers+=v.transfers.length;
        if(bytes>8*1024*1024 || transfers>131072)fail();
        if(v.seen.some((pair:any)=>!Array.isArray(pair)||pair.length!==2||!pair.every(integer)))fail();
        for(const t of v.transfers) {
            if(!keys(t,['source','target']) || !keys(t.source,['client','clock','length','boundary'])
                || !integer(t.source.client)||!integer(t.source.clock)||!integer(t.source.length)||t.source.length<1
                || !Number.isSafeInteger(t.source.clock+t.source.length)||!boundary(t.source.boundary))fail();
            if(t.target===null)continue;
            const to=t.target;
            if(!keys(to,['client','clock','type','tname','assoc','boundary']) || !integer(to.client)||!integer(to.clock)
                || !(to.type===null||id(to.type)) || !(to.tname===undefined||to.tname==='default')
                || !(to.assoc===undefined||Number.isSafeInteger(to.assoc))||!boundary(to.boundary))fail();
        }
    }
}
