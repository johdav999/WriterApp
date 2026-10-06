import {createRequire} from 'node:module';
import {readFile,writeFile} from 'node:fs/promises';
import {resolve,join} from 'node:path';
import assert from 'node:assert/strict';
const {chromium}=createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE);
const evidence=resolve(process.argv[2]||'artifacts/ai-parity-p14'),fixture=JSON.parse(await readFile(join(evidence,'acceptance-fixture.json'),'utf8'));
const host='http://127.0.0.1:5396',browser=await chromium.launch({channel:'msedge',headless:true});
const context=await browser.newContext({viewport:{width:1280,height:900}}),page=await context.newPage(),checks=[],errors=[];
page.on('pageerror',e=>errors.push(e.message));
async function pages(){let rows=[];for(const id of fixture.sections){const r=await context.request.get(`${host}/api/sections/${id}/pages`);assert.equal(r.status(),200);rows.push(...await r.json());}return rows;}
async function card(){const r=await context.request.get(`${host}/api/sections/${fixture.sections[0]}/scene-card`);assert.equal(r.status(),200);return r.json();}
function record(name){checks.push(name);console.log('PASS '+name);}
async function dismissWalkthrough(){await page.waitForLoadState('networkidle');const skip=page.getByRole('button',{name:'Skip',exact:true});if(await skip.isVisible())await skip.click();}
const replayResponse=()=>page.waitForResponse(r=>r.url().endsWith('/api/ai/actions/history/recovery/replay'));
const fields=['summary','status','narrativePurpose','narrativeRole','narrativeIntent','emotionalBeat','keyEvents','openQuestions','povCharacterId','placeId','timelineEventId','timeRef','tags','references','subplotTags'];
function values(c){return Object.fromEntries(fields.map(f=>[f,c[f]]));}
try{
 await page.goto(`${host}/app/documents/${fixture.document}/sections/${fixture.sections[0]}`);
 await page.locator('.ProseMirror[contenteditable=true]').first().waitFor({timeout:60000});
 await dismissWalkthrough();
 // A repeat against this synthetic fixture resets the prior approved probe using
 // the same visible recovery action, never by overwriting database state.
 if((await card()).narrativeIntent?.startsWith('Clarify what this scene')) {
   await page.getByRole('tab',{name:'Writing',exact:true}).first().click();
   await page.getByRole('tab',{name:'Writing tools',exact:true}).click();
   const [reset]=await Promise.all([replayResponse(),page.getByRole('button',{name:'Undo change',exact:true}).first().click()]);
   assert.equal(reset.status(),200,await reset.text());
   await page.getByRole('button',{name:'Redo change',exact:true}).first().waitFor();
   assert(!((await card()).narrativeIntent));
 }
 await page.getByRole('tab',{name:'Story',exact:true}).click();
 const beforePages=await pages(),before=await card();
 await page.getByRole('button',{name:'Suggest from text',exact:true}).click();
 const fieldsReview=page.locator('section[aria-label="Review scene fields"]');await fieldsReview.waitFor({timeout:30000});
 assert((await fieldsReview.getByRole('checkbox').count())>1);assert.deepEqual(await pages(),beforePages);
 assert(await page.getByRole('button',{name:'Apply approved fields',exact:true}).isDisabled());
 await fieldsReview.getByRole('button',{name:'Approve all changed fields',exact:true}).click();
 await fieldsReview.getByRole('button',{name:'Clear selection',exact:true}).click();assert(await page.getByRole('button',{name:'Apply approved fields',exact:true}).isDisabled());
 await fieldsReview.getByRole('checkbox',{name:'Approve Narrative Intent',exact:true}).check();
 await page.screenshot({path:join(evidence,'web-screenshots','scene-subset-review.png'),fullPage:true});
 record('Real scene review allows clearing and approving only Narrative Intent');
 const saved=page.waitForResponse(r=>r.url().endsWith(`/api/sections/${fixture.sections[0]}/scene-card`)&&r.request().method()==='PUT'&&r.status()===200);
 await page.getByRole('button',{name:'Apply approved fields',exact:true}).click();await saved;
 const approved=await card();assert.notEqual(approved.narrativeIntent,before.narrativeIntent);
 // The DTO's legacy purpose is a projection of role/intent; raw unselected stored fields are checked by the endpoint suite.
 assert.equal(approved.narrativePurpose,approved.narrativeRole||approved.narrativeIntent);
 for(const f of fields.filter(f=>f!=='narrativeIntent'&&f!=='narrativePurpose'))assert.deepEqual(approved[f],before[f],f);
 assert.deepEqual(await pages(),beforePages);record('One approved field persists; every unselected field and manuscript page remains unchanged');
 await page.reload();await page.locator('.ProseMirror[contenteditable=true]').first().waitFor({timeout:60000});
 await dismissWalkthrough();
 await page.getByRole('tab',{name:'Writing',exact:true}).first().click();
 await page.getByRole('tab',{name:'Writing tools',exact:true}).click();
 const undo=page.getByRole('button',{name:'Undo change',exact:true}).first();await undo.waitFor();
 let [replay]=await Promise.all([replayResponse(),undo.click()]);assert.equal(replay.status(),200,await replay.text());
 assert.deepEqual(values(await card()),values(before));assert.deepEqual(await pages(),beforePages);record('Reachable scoped history Undo after reload restores the exact planning snapshot');
 const redo=page.getByRole('button',{name:'Redo change',exact:true}).first();await redo.waitFor();
 [replay]=await Promise.all([replayResponse(),redo.click()]);assert.equal(replay.status(),200,await replay.text());
 assert.deepEqual(values(await card()),values(approved));assert.deepEqual(await pages(),beforePages);record('Scoped Redo restores only the approved field with manuscript content unchanged');
 // Wait for the host reload and recovery-panel refresh as well as the committed response.
 await page.getByRole('button',{name:'Undo change',exact:true}).first().click({trial:true});
 await page.getByText('All changes saved',{exact:true}).waitFor();
 assert.deepEqual(await pages(),beforePages);
 await page.screenshot({path:join(evidence,'web-screenshots','scene-scoped-recovery.png'),fullPage:true});
 assert.deepEqual(errors,[]);await writeFile(join(evidence,'planning-browser.json'),JSON.stringify({passed:true,checks,errors,before:values(before),approved:values(approved),scope:'Actual client scene menu, existing mock-text backend, partial checked save, reload, reachable server-scoped snapshot Undo/Redo. Synthetic isolated graph; no live/native/deployed acceptance.'},null,2));
}catch(error){await page.screenshot({path:join(evidence,'web-screenshots','planning-failure.png'),fullPage:true}).catch(()=>{});await writeFile(join(evidence,'planning-browser-failure.json'),JSON.stringify({checks,errors,error:String(error),text:await page.locator('body').innerText().catch(()=>'')},null,2));throw error;}
finally{await browser.close();}
