// Actual shipped editor comparison and static production Razor renders from real C# handler tests.
// These are fixtures, not signed-in browser/native or live provider acceptance.
import assert from 'node:assert/strict';
import {createServer} from 'node:http';
import {readFile,writeFile,mkdir} from 'node:fs/promises';
import {resolve,dirname} from 'node:path';
import {createRequire} from 'node:module';
const {chromium}=createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE);
const directory=dirname(process.env.WRITERAPP_BRAINSTORMING_CONTEXT_HTML);
const evidence=resolve(process.env.BRAINSTORMING_EVIDENCE);await mkdir(evidence,{recursive:true});
const fixtures=JSON.parse(await readFile(resolve(directory,'server-projection.json'),'utf8'));
const allowed=new Set(['WriterApp.Client/tests/device-editor.html','WriterApp.Client/wwwroot/js/tiptap-editor.bundle.js','WriterApp.Client/wwwroot/js/tiptap-editor-patch.js','WriterApp.Client/wwwroot/js/web-ai-history-outbox.js','WriterApp.Device.Shared/wwwroot/editor/device-editor.js','WriterApp.Device.Shared/wwwroot/editor/device-editor.css','WriterApp.Device.Shared/wwwroot/app.css','WriterApp.UI.Shared/wwwroot/design-tokens.css','WriterApp.UI.Shared/wwwroot/editor.css','WriterApp.UI.Shared/BrainstormingConversationPanel.razor.css']);
const server=createServer(async(req,res)=>{
    const name=new URL(req.url,'http://localhost').pathname.slice(1);res.setHeader('Cache-Control','no-store');
    if(['web','device'].includes(name)){res.setHeader('Content-Type','text/html');res.end('<!doctype html><meta charset="utf-8"><meta name="viewport" content="width=device-width"><link rel="stylesheet" href="/WriterApp.UI.Shared/BrainstormingConversationPanel.razor.css"><style>body{font:16px system-ui;margin:16px}</style>'+await readFile(resolve(directory,`conversation-${name}.html`),'utf8'));return;}
    if(!allowed.has(name)){res.writeHead(404);res.end();return;}res.setHeader('Content-Type',name.endsWith('.js')?'text/javascript':name.endsWith('.css')?'text/css':'text/html');res.end(await readFile(name));
});
await new Promise(done=>server.listen(0,'127.0.0.1',done));const browser=await chromium.launch({channel:'msedge',headless:true});const page=await browser.newPage();const errors=[];page.on('pageerror',e=>errors.push(e.message));const base=`http://127.0.0.1:${server.address().port}`;const checks=[];
try{
    await page.goto(base+'/WriterApp.Client/tests/device-editor.html');await page.waitForFunction(()=>/tests (passed|failed)/.test(document.title));
    const results=await page.evaluate(async fixtures=>{const device=await import('/WriterApp.Device.Shared/wwwroot/editor/device-editor.js');return fixtures.map(f=>({content:f.content,expected:f.expected,web:window.tiptapEditor.brainstormingPlainText(f.content,'Html'),device:device.qualityPlainText(f.content,'Html')}));},fixtures);
    for(const row of results){assert.equal(row.web,row.expected,JSON.stringify(row));assert.equal(row.device,row.expected,JSON.stringify(row));}checks.push(`${results.length} server projections match actual web/device bundles, including Unicode, tables, empty blocks, whitespace, images and rules`);
    for(const surface of ['web','device']){
        await page.goto(base+'/'+surface);assert.equal(await page.locator('script,img').count(),0);assert((await page.locator('body').innerText()).includes('<img src=x onerror=evil()>'));assert(!await page.evaluate(()=>window.evil));
        assert.equal(await page.getByRole('button',{name:'Send message',exact:true}).count(),1);assert.equal(await page.getByRole('button',{name:'Stop generation',exact:true}).count(),0);assert.equal(await page.locator('.brainstorm-audio').getAttribute('open'),null);
        await page.setViewportSize({width:390,height:844});assert(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth+1));await page.getByRole('textbox').focus();await page.keyboard.press('Tab');assert(await page.locator('.brainstorm-composer').evaluate(e=>e.contains(document.activeElement) && document.activeElement.tagName!=='TEXTAREA'));
        await page.screenshot({path:resolve(evidence,`conversation-${surface}-narrow.png`),fullPage:true});checks.push(`${surface} real Razor handler render: encoded conversation, contextual actions, collapsed audio, 390px layout and keyboard focus`);
    }
    assert.deepEqual(errors,[]);const report={scope:'Server/editor projection parity and static actual C# handler renders; no live authenticated browser/native/provider claim',browser:browser.version(),checks,errors};await writeFile(resolve(evidence,'conversation-result.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report,null,2));
}finally{await browser.close();await new Promise(done=>server.close(done));}
