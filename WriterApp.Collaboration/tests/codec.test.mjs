import test from 'node:test';
import assert from 'node:assert/strict';
import { convert } from '../src/codec.mjs';
import * as Y from 'yjs';

test('formatting position migrations survive projection but arbitrary shared metadata is rejected', async () => {
  const seed=await convert({operation:'seed',content:'<p>Retained text</p>',format:'Html'});
  const valid={version:1,client:7,clock:9,seen:[[7,9]],transfers:[{source:{client:7,clock:1,length:1},target:{client:7,clock:8,type:null,boundary:'end'}}]};
  async function project(field,value,key='7:9') {
    const doc=new Y.Doc();
    try {
      Y.applyUpdate(doc,Buffer.from(seed.state,'base64'));
      doc.getMap(field).set(key,value);
      return await convert({operation:'project',updates:[Buffer.from(Y.encodeStateAsUpdate(doc)).toString('base64')]});
    } finally { doc.destroy(); }
  }
  const field='__tiptap_position_migrations:root:default';
  assert.equal((await project(field,valid)).text,'Retained text');
  await assert.rejects(project('private-data',valid));
  await assert.rejects(project('__tiptap_position_migrations:root:another',valid));
  await assert.rejects(project(field,valid,'different-key'));
  for(const bad of [{...valid,version:2},{...valid,unexpected:'content'},{...valid,seen:[[1,-1]]},
    {...valid,transfers:[{source:{client:7,clock:1,length:-1},target:null}]},
    {...valid,transfers:[{source:{client:7,clock:1,length:1},target:{client:7,clock:8,type:null,tname:'another'}}]}])
    await assert.rejects(project(field,bad));
});

test('migration and projection retain contract formatting, Unicode, links and asset identities', async () => {
  const html = '<h6 style="text-align:center;margin-left:4em" data-indent-level="2">日本語 🧭</h6>'
    + '<p><strong>B</strong><em>I</em><u>U</u><s>S</s><code>C</code><br><a href="https://example.test" target="_blank" rel="noopener" class="reference">L</a></p>'
    + '<blockquote><p>Quote</p></blockquote><ol start="3"><li><p>List</p></li></ol><ul><li><p>Bullet</p></li></ul><hr>'
    + '<pre><code class="language-typescript">let x = 1;</code></pre>'
    + '<img src="/api/assets/test" alt="Alt" title="Title" width="240" data-asset-id="asset-identity" data-asset-url="/api/assets/test">'
    + '<table><tbody><tr><th colspan="2" rowspan="1" colwidth="120,180" align="center"><p>Header</p></th></tr>'
    + '<tr><td align="right"><p>A</p></td><td style="text-align:justify"><p>B</p></td></tr></tbody></table>';
  const seeded = await convert({ operation: 'seed', content: html, format: 'Html' });
  const projected = await convert({ operation: 'project', updates: [seeded.state] });
  assert.deepEqual(projected.json, seeded.json); assert.equal(projected.sha256, seeded.sha256);
  assert.match(projected.html, /data-asset-id="asset-identity"/);
  assert.match(projected.html, /align="right"/); assert.match(projected.html, /align="justify"/);
  assert.match(projected.html, /colwidth="120,180"/); assert.match(projected.html, /language-typescript/);
  assert.match(projected.text, /日本語 🧭/);
});

test('unsupported and lossy source refuses conversion instead of stripping original writing', async () => {
  for (const content of ['<p style="font-size:48px">text</p>', '<p><span>unknown</span></p>', '<script>throw 1</script>',
    '<p><!--comment-->text</p>', '<p><a href="javascript:alert(1)">unsafe</a></p>',
    '<table style="width:300px"><tr><td><p>width has no contract state</p></td></tr></table>',
    '<p data-indent-level="2" style="margin-left:2em">conflicting indent</p>']) {
    await assert.rejects(convert({ operation: 'seed', content, format: 'Html' }));
  }
  await assert.rejects(convert({ operation: 'project', updates: ['////'] }));
});

test('legacy text stays literal and legacy JSON validates before conversion', async () => {
  const plain = await convert({ operation: 'seed', content: 'literal <script> & 日本語\nline two', format: 'LegacyText' });
  assert.match(plain.html, /&lt;script&gt;/); assert.match(plain.text, /日本語/);
  const json = await convert({ operation: 'seed', content: JSON.stringify({ type: 'doc', content: [{ type: 'paragraph', content: [{ type: 'text', text: 'JSON' }] }] }), format: 'LegacyJson' });
  assert.equal(json.text, 'JSON');
  await assert.rejects(convert({ operation: 'seed', content: '{"type":"unknown"}', format: 'LegacyJson' }));
});
