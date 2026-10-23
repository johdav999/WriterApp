import * as Y from 'yjs';

export function decodePosition(encoded) {
  if (typeof encoded !== 'string' || encoded.length < 4 || encoded.length > 1024) throw Error('collaboration_anchor_invalid');
  const bytes = Buffer.from(encoded, 'base64');
  if (bytes.toString('base64') !== encoded) throw Error('collaboration_anchor_invalid');
  const position = Y.decodeRelativePosition(bytes);
  if (!Buffer.from(Y.encodeRelativePosition(position)).equals(bytes)) throw Error('collaboration_anchor_invalid');
  return position;
}
export function resolveAnchor(doc, anchor, allowCollapsed = false) {
  if (!anchor || Object.keys(anchor).sort().join(',') !== 'end,start') throw Error('collaboration_anchor_invalid');
  const root = doc.getXmlFragment('default');
  const resolve = encoded => {
    const position = Y.createAbsolutePositionFromRelativePosition(decodePosition(encoded), doc);
    if (!position) return null;
    let type = position.type; while (type.parent) type = type.parent;
    if (type !== root) throw Error('collaboration_anchor_invalid');
    return position;
  };
  const start = resolve(anchor.start), end = resolve(anchor.end);
  const path = position => {
    const values = [position.index * 2]; let type = position.type;
    while (type.parent) { const parent = type.parent; values.unshift(parent.toArray().indexOf(type) * 2 + 1); type = parent; }
    return values;
  };
  let order = 0;
  if (start && end) {
    const a = path(start), b = path(end);
    for (let index = 0; index < Math.max(a.length,b.length); index++) {
      const difference = (a[index] ?? -1) - (b[index] ?? -1); if (difference) { order = Math.sign(difference); break; }
    }
  }
  // An originally selected passage whose two ends collapse after deletion is detached.
  return { detached: !start || !end || order > 0 || !allowCollapsed && order === 0 };
}
