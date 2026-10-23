import assert from 'node:assert/strict';
import {mkdirSync,writeFileSync} from 'node:fs';
import {resolve} from 'node:path';
const mode=process.argv[2] || 'verify';process.argv[2]='annotation-acceptance';
process.env.PROSA_ADB ??='C:/Program Files (x86)/Android/android-sdk/platform-tools/adb.exe';
process.env.PROSA_ANDROID_EVIDENCE='artifacts/android-annotations';
const {connect,adb,capture}=await import('./android-native-smoke.mjs');
const c=await connect(), e=c.evaluate;
const pause=()=>new Promise(r=>setTimeout(r,350));
const click=async text=>{await e(`(()=>{const b=[...document.querySelectorAll('button')].find(b=>b.textContent.trim()===${JSON.stringify(text)}&&!b.closest('[hidden]'));if(!b)throw Error('Missing '+${JSON.stringify(text)});b.click();})()`);await pause();};
const text='Android annotation acceptance. The blue boat crossed the quiet lake. A second passage needs a task.';
try {
 for(let i=0;i<30 && !await e(`!!document.querySelector('.ProseMirror') && document.body.innerText.includes('Android annotations UAT')`);i++) await pause();
 assert.ok(await e(`document.body.innerText.includes('Android annotations UAT')`),'Open the dedicated Android annotations UAT project');
 if(mode==='seed') {
  assert.equal((await e(`document.querySelector('.ProseMirror').innerText`)).trim(),'');
  await e(`document.querySelector('.ProseMirror').focus()`);await c.send('Input.insertText',{text});await pause();
  await click('Save on this device');
 } else {
  assert.equal(await e(`document.querySelectorAll('.wa-annotation').length`),0,'Use a fresh dedicated test manuscript');
  const select=async quote=>{await e(`(()=>{const root=document.querySelector('.ProseMirror');const walker=document.createTreeWalker(root,NodeFilter.SHOW_TEXT);let n;while(n=walker.nextNode()){const i=n.textContent.indexOf(${JSON.stringify(quote)});if(i<0)continue;root.focus();const r=document.createRange();r.setStart(n,i);r.setEnd(n,i+${quote.length});getSelection().removeAllRanges();getSelection().addRange(r);document.dispatchEvent(new Event('selectionchange'));return;}throw Error('Quote missing');})()`);await pause();};
  await select('The blue boat crossed the quiet lake.');
  await click('Add comment / task');
  const checks=[];
  const both=async()=>{
   const state=await e(`(()=>{const main=document.querySelector('.editor-main'),panel=document.querySelector('.editor-context-host'),m=main.getBoundingClientRect(),p=panel.getBoundingClientRect();return {inert:main.inert,modal:panel.getAttribute('aria-modal'),mt:m.top,mb:m.bottom,pt:p.top,pb:p.bottom,height:window.visualViewport?.height??innerHeight};})()`);
   assert.equal(state.inert,false);assert.equal(state.modal,null);assert.ok(state.mb<=state.pt+1);assert.ok(state.pb<=state.height+1);assert.ok(state.mb-state.mt>100);return state;
  };
  await both();
  assert.equal(await e(`document.querySelectorAll('.annotation-composer textarea')[1].value`),'The blue boat crossed the quiet lake.');
  const enter=async value=>{
   await e(`(()=>{const t=document.querySelector('.annotation-composer textarea');t.scrollIntoView({block:'nearest'});})()`);
   const pt=await e(`(()=>{const r=document.querySelector('.annotation-composer textarea').getBoundingClientRect();return{x:r.x+20,y:r.y+20}})()`);
   await c.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[pt]});await c.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});await pause();
   await c.send('Input.insertText',{text:value});await pause();
  };
  await enter('Check the boat colour.');
  assert.ok(adb('shell','dumpsys','input_method').includes('mInputShown=true'),'Native keyboard opens');
  await both();capture('comment-keyboard');
  adb('shell','input','keyevent','KEYCODE_BACK');await pause();
  assert.equal(await e(`document.querySelector('[data-responsive-panels]').dataset.openDrawer`),'right');
  await click('Add comment');await both();
  assert.equal(await e(`document.querySelectorAll('.wa-annotation-comment').length`),1);
  assert.equal(await e(`document.querySelector('.ProseMirror').innerText`),text);
  checks.push('Selected quote captured; native keyboard and first Back retain both panes; comment immediately highlights text');
  await select('A second passage needs a task.');await click('New');
  await e(`(()=>{const s=document.querySelector('.annotation-composer select');s.value='todo';s.dispatchEvent(new Event('change',{bubbles:true}));})()`);await pause();
  await enter('Check the second passage.');adb('shell','input','keyevent','KEYCODE_BACK');await pause();await click('Add task');
  assert.equal(await e(`document.querySelectorAll('.wa-annotation-todo').length`),1);
  await click('Next');await both();
  if(adb('shell','dumpsys','input_method').includes('mInputShown=true')) { adb('shell','input','keyevent','KEYCODE_BACK');await pause(); }
  capture('text-and-annotation');
  await click('Resolve');await pause();
  await e(`(()=>{const s=document.querySelector('.annotation-filter select');s.value='resolved';s.dispatchEvent(new Event('change',{bubbles:true}));})()`);await pause();await click('Reopen');
  await e(`(()=>{const s=document.querySelector('.annotation-filter select');s.value='open';s.dispatchEvent(new Event('change',{bubbles:true}));})()`);await pause();
  const beforeDrag=await both();
  const grip=await e(`(()=>{const r=document.querySelector('.annotation-resize-grip').getBoundingClientRect();return{x:r.x+r.width/2,y:r.y+r.height/2}})()`);
  await c.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[grip]});
  await c.send('Input.dispatchTouchEvent',{type:'touchMove',touchPoints:[{x:grip.x,y:grip.y-40}]});
  await c.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});await pause();
  assert.ok((await both()).pt<beforeDrag.pt);
  await click('More space');await both();await click('Less space');
  checks.push('Task highlights, previous/next navigation, resolve/reopen and panel resize work with manuscript interactive');
  await e(`(()=>{const t=document.querySelector('.ProseMirror');t.focus();getSelection().selectAllChildren(t);getSelection().collapseToEnd();document.dispatchEvent(new Event('selectionchange'));})()`);await pause();
  await click('New');
  await e(`(()=>{const s=document.querySelector('.annotation-composer select');s.value='comment';s.dispatchEvent(new Event('change',{bubbles:true}));})()`);await pause();
  await enter('Link this scene note.');adb('shell','input','keyevent','KEYCODE_BACK');await pause();await click('Add comment');
  await click('All');await select('Android annotation acceptance.');await click('Link to selected text');
  assert.equal(await e(`document.querySelectorAll('.wa-annotation-comment').length`),2);
  capture('note-relinked');checks.push('Scene note relinks to text selected while annotation panel stays open');
  const writing=await e(`document.querySelector('.ProseMirror').innerText`);assert.equal(writing,text);
  const beforeBackRoute=await e('location.pathname');
  for(let back=0;back<2 && await e(`document.querySelector('[data-responsive-panels]')?.dataset.openDrawer==='right'`);back++) {
   adb('shell','input','keyevent','KEYCODE_BACK');await pause();
   assert.equal(await e('location.pathname'),beforeBackRoute);
  }
  assert.equal(await e(`document.querySelector('[data-responsive-panels]').dataset.openDrawer`),'');
  await e(`document.querySelector('.wa-annotation-comment').click()`);await pause();await both();
  assert.equal(await e(`document.querySelector('[data-responsive-panels]').dataset.openDrawer`),'right');
  capture('highlight-opens-panel');checks.push('Native Back closes annotation panel; tapping highlight reopens it beside text');
  await e(`(()=>{const root=document.querySelector('.ProseMirror');root.focus();getSelection().selectAllChildren(root);getSelection().collapseToEnd();})()`);
  await c.send('Input.insertText',{text:' Edited while annotations are visible.'});await pause();await both();
  assert.ok((await e(`document.querySelector('.ProseMirror').innerText`)).endsWith(' Edited while annotations are visible.'));
  await c.send('Input.dispatchKeyEvent',{type:'keyDown',key:'z',code:'KeyZ',modifiers:2,windowsVirtualKeyCode:90});
  await c.send('Input.dispatchKeyEvent',{type:'keyUp',key:'z',code:'KeyZ',modifiers:2,windowsVirtualKeyCode:90});await pause();
  assert.equal(await e(`document.querySelector('.ProseMirror').innerText`),text);
  checks.push('Text can be edited and undone while annotations remain visible');
  const route=await e('location.pathname');
  writeFileSync(resolve('artifacts/android-annotations/results.json'),JSON.stringify({route,checks,layout:await both(),writingUnchanged:true},null,2));
  console.log(JSON.stringify({checks},null,2));

 }
}finally{c.close();}
