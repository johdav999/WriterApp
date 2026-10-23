import {readFile,writeFile,mkdir,stat} from 'node:fs/promises';
import {resolve,relative,sep} from 'node:path';
import {createHash} from 'node:crypto';

// Evidence aggregation cannot authorize activation or substitute for physical/hosted acceptance.
const directory=resolve(process.argv[2]??''),allowed=resolve('artifacts/realtime-acceptance')+sep;
if(!directory.startsWith(allowed))throw Error('Pass an existing acceptance run directory under artifacts/realtime-acceptance');
const input=async path=>{const bytes=await readFile(path);return {path:relative(process.cwd(),path).replaceAll('\\','/'),sha256:createHash('sha256').update(bytes).digest('hex'),modifiedAtUtc:(await stat(path)).mtime.toISOString(),value:JSON.parse(bytes.toString())};};
const acceptance=await input(resolve(directory,'manifest.json')),editor=await input(resolve('artifacts/realtime-p10/editor-performance.json')),packages=await input(resolve('artifacts/realtime-p10/package-builds.json'));
const providers={};
for(const provider of['sqlite','sqlserver']){
 const load=await input(resolve(`artifacts/realtime-p10/load-${provider}.json`)),manuscript=await input(resolve(`artifacts/realtime-p10/manuscript-${provider}.json`));
 providers[provider]={load,manuscript,correctnessPassed:load.value.stockWrites===0&&load.value.duplicateReplayPreserved===true&&load.value.memory.after50.loadedRooms===0&&load.value.durableReceiptMs.samples===100&&manuscript.value.pages===100&&manuscript.value.words===100000,
  provisionalBudgetsPassed:Object.values(load.value.provisionalBudgets).every(value=>typeof value!=='boolean'||value),remoteVisibilityP95Ms:load.value.remoteVisibilityMs.p95};
}
const paint=editor.value.results.filter(r=>r.keystrokeToTwoAnimationFramesMs).map(r=>({host:r.host,fixture:r.fixture,p95Ms:r.keystrokeToTwoAnimationFramesMs.p95,targetMs:50,passed:r.keystrokeToTwoAnimationFramesMs.p95<=50}));
const currentDeviceBundleSha256=createHash('sha256').update(await readFile('WriterApp.Device.Shared/wwwroot/editor/device-editor.js')).digest('hex');
const latencyGates=[];
if(Object.values(providers).some(p=>!p.provisionalBudgetsPassed))latencyGates.push('Transport opening, receipt or 300 ms remote visibility budgets');
if(paint.length!==6||paint.some(p=>!p.passed))latencyGates.push('50 ms editor paint budget');
const report={generatedAtUtc:new Date().toISOString(),acceptance,providers,editor,paint,packages,
 packageEvidenceMatchesCurrentDeviceBundle:packages.value.android.packagedEditorSha256===currentDeviceBundleSha256,currentDeviceBundleSha256,
 automatedCorrectnessPassed:acceptance.value.automatedChecksPassed&&Object.values(providers).every(p=>p.correctnessPassed),
 localLatencyBudgetsPassed:latencyGates.length===0,
 outstandingGates:[...latencyGates,'Simultaneous room-open stampede/admission policy','4-core/16-GB reference hardware and physical mobile measurements','Installed Windows package, physical Android and actual iOS user journeys','Hosted identity/TLS/proxy and cross-host database/assets/key-ring recovery','10,000 offline updates versus current 4,096 pending-envelope safeguard'],
 historicalFailures:['artifacts/realtime-p10-load-burst-failure.log','artifacts/realtime-p10-load-idle-failure.log','artifacts/realtime-p10/load-sqlite-failure.json','artifacts/realtime-p10/load-sqlite-intermediate-latency.json','artifacts/realtime-p10/load-sqlite-polling-latency.json'],
 pilotReady:false,activation:'blocked',note:'Correctness and capacity results are separate. No automation in this report enables the hard product editor gate.'};
await mkdir('artifacts/realtime-p10',{recursive:true});await writeFile('artifacts/realtime-p10/readiness.json',JSON.stringify(report,null,2));
console.log(JSON.stringify({automatedCorrectnessPassed:report.automatedCorrectnessPassed,localLatencyBudgetsPassed:report.localLatencyBudgetsPassed,pilotReady:false,activation:'blocked'}));
