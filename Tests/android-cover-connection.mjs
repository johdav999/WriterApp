// Installed Android WebView acceptance. Generates only in the explicit generate mode;
// never saves a concept or changes manuscript content. Requires the original-form
// capture in the ignored evidence directory so an app update retains the user's brief.
// Open Cover studio, run this script with capture, then failure or generate.
import assert from 'node:assert/strict';
import {readFileSync, writeFileSync} from 'node:fs';
import {createServer} from 'node:net';
process.env.PROSA_ADB ??= 'C:/Program Files (x86)/Android/android-sdk/platform-tools/adb.exe';
const mode=process.argv[2]??'inspect';
process.argv[2]='cover-connection';
const {connect,adb,capture}=await import('./android-native-smoke.mjs');
let c=await connect();
const delay=ms=>new Promise(resolve=>setTimeout(resolve,ms));
const wait=async(expression,seconds=30)=>{
    for(let i=0;i<seconds*4;i++){const result=await c.evaluate(expression);if(result)return result;await delay(250);}
    throw new Error('Timed out waiting for cover state.');
};
if(mode==='capture'){
    const form=await c.evaluate(`({route:location.pathname,fields:[...document.querySelectorAll('.cover-brief textarea,.cover-brief select')].map(x=>x.value)})`);
    assert.equal(form.fields.length,5,'Open Cover studio before capturing its brief.');
    writeFileSync('artifacts/cover-connection/original-form.json',JSON.stringify(form,null,2));
    c.close();console.log('Captured the current cover brief locally.');process.exit(0);
}
const original=JSON.parse(readFileSync('artifacts/cover-connection/original-form.json','utf8'));
const restoreFields=async()=>{
    for(let i=0;i<original.fields.length;i++){
        await c.evaluate(`(()=>{const e=document.querySelectorAll('.cover-brief textarea,.cover-brief select')[${i}];e.value=${JSON.stringify(original.fields[i])};e.dispatchEvent(new Event(e.tagName==='TEXTAREA'?'input':'change',{bubbles:true}));})()`);
        await delay(50);
    }
};
try {
    if(await c.evaluate('location.pathname')!==original.route){
        await c.evaluate(`(()=>{const a=document.createElement('a');a.href=${JSON.stringify(original.route)};document.body.append(a);a.click();a.remove();})()`);
        await wait(`!!document.querySelector('.cover-brief')`);
    }
    await wait(`!document.querySelector('.cover-brief').disabled`);
    await restoreFields();
    const refresh=`[...document.querySelectorAll('button')].find(b=>b.textContent.trim()==='Refresh image-edit availability')`;
    if(mode==='failure'){
        // Drop an HTTP connection before response headers, reproducing Android's
        // unexpected-end-of-stream path. Do not inspect or record request contents.
        const sockets=new Set();
        const server=createServer(socket=>{sockets.add(socket);socket.on('data',()=>socket.destroy());socket.on('close',()=>sockets.delete(socket));});
        await new Promise(resolve=>server.listen(15387,'127.0.0.1',resolve));
        try {
            adb('reverse','tcp:5387','tcp:15387');
            // Existing .NET pooled sockets survive changes to adb forwarding.
            // Relaunch to ensure this probe opens a connection to the fixture.
            c.close();adb('shell','am','force-stop','com.prosa.writer.android');
            adb('shell','monkey','-p','com.prosa.writer.android','-c','android.intent.category.LAUNCHER','1');
            c=await connect();
            await c.evaluate(`(()=>{const a=document.createElement('a');a.href=${JSON.stringify(original.route)};document.body.append(a);a.click();a.remove();})()`);
            await wait(`!!document.querySelector('.cover-brief') && !document.querySelector('.cover-brief').disabled`);
            await delay(750);
            assert.equal(await c.evaluate(`${refresh}.disabled`),false,'Refresh must be available');
            await c.evaluate(`${refresh}.click()`);
            const text=await wait(`(()=>{const text=document.querySelector('.desktop-cover-studio [role=alert]')?.textContent;return text?.includes('backend is running') && text;})()`);
            assert(!text.includes('okhttp'));assert(text.includes('cached concepts are preserved'));
            await c.evaluate(`document.querySelector('.desktop-cover-studio [role=alert]')?.scrollIntoView({block:'center'})`);
            capture('cover-connection-interrupted');
            writeFileSync('artifacts/cover-connection/native-failure.json',JSON.stringify({route:original.route,message:text,automaticGenerationRetry:false},null,2));
        } finally {
            adb('reverse','tcp:5387','tcp:5387');
            for(const socket of sockets)socket.destroy();
            await new Promise(resolve=>server.close(resolve));
        }
        c.close();adb('shell','am','force-stop','com.prosa.writer.android');
        adb('shell','monkey','-p','com.prosa.writer.android','-c','android.intent.category.LAUNCHER','1');
        c=await connect();
        await c.evaluate(`(()=>{const a=document.createElement('a');a.href=${JSON.stringify(original.route)};document.body.append(a);a.click();a.remove();})()`);
        await wait(`!!document.querySelector('.cover-brief')`);
        await delay(1000);
        await wait(`!${refresh}.disabled`);
        const error=await c.evaluate(`document.querySelector('.desktop-cover-studio [role=alert]')?.textContent`);
        assert(!error,error);
        await restoreFields();
        capture('cover-connection-recovered');
        console.log('PASS: native interrupted connection shows recovery guidance; restored backend succeeds.');
    } else if(mode==='generate'){
        await c.evaluate(`document.querySelector('.studio-actions button').click()`);
        await delay(500);
        await wait(`!document.querySelector('.studio-actions button').disabled`,200);
        const state=await c.evaluate(`({error:document.querySelector('.desktop-cover-studio [role=alert]')?.textContent,concepts:document.querySelectorAll('.concept-grid img').length,status:[...document.querySelectorAll('.desktop-cover-studio [role=status]')].map(x=>x.textContent)})`);
        writeFileSync('artifacts/cover-connection/native-generation.json',JSON.stringify(state,null,2));
        capture('cover-connection-generated');console.log(state);assert(!state.error,state.error);assert(state.concepts>0,'No cover concepts returned');
    } else {
        console.log(await c.evaluate(`({route:location.pathname,error:document.querySelector('.desktop-cover-studio [role=alert]')?.textContent,generateDisabled:document.querySelector('.studio-actions button')?.disabled})`));
    }
} finally {c.close();}
