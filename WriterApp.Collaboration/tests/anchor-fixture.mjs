import * as Y from 'yjs';
const chunks=[];for await(const chunk of process.stdin)chunks.push(chunk);
const doc=new Y.Doc();Y.applyUpdate(doc,Buffer.from(JSON.parse(Buffer.concat(chunks).toString()).state,'base64'));
const root=doc.getXmlFragment('default');let text;
function visit(node){if(node instanceof Y.XmlText&&!text&&node.length>2)text=node;else if(node.toArray)node.toArray().forEach(visit);}visit(root);
const encode=p=>Buffer.from(Y.encodeRelativePosition(p)).toString('base64');
const anchor={start:encode(Y.createRelativePositionFromTypeIndex(text,0)),end:encode(Y.createRelativePositionFromTypeIndex(text,2))};
const vector=Y.encodeStateVector(doc);text.delete(0,2);const deletion=Buffer.from(Y.encodeStateAsUpdate(doc,vector)).toString('base64');
process.stdout.write(JSON.stringify({anchor,deletion}));doc.destroy();
