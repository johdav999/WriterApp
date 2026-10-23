import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {mkdir,writeFile} from 'node:fs/promises';
const {chromium}=createRequire(import.meta.url)(process.env.PLAYWRIGHT_MODULE||'playwright');
const browser=await chromium.launch({channel:'msedge',headless:true});
try{
 const page=await browser.newPage({viewport:{width:1152,height:850}});
 await page.goto('http://localhost:5398/collaboration/test');
 await page.getByRole('heading',{name:'Prosa · Local collaboration test'}).waitFor();
 await page.waitForFunction(()=>document.getElementById('status').textContent.includes('Sign in'));
 assert.equal(await page.locator('#workspace').isVisible(),false);
 await mkdir('artifacts/realtime-local',{recursive:true});
 await page.screenshot({path:'artifacts/realtime-local/local-start.png',fullPage:true});
 await page.getByLabel('Prosa account email').fill('johandavidsson@hotmail.se');
 await page.getByRole('button',{name:'Sign in with Prosa',exact:true}).click();
 await page.waitForURL(url=>url.hostname.endsWith('.ciamlogin.com'));
 await page.waitForLoadState('domcontentloaded');
 await page.locator('input[type=password]').waitFor({timeout:20000});
 const body=await page.locator('body').textContent();
 assert.ok(!body.includes('AADSTS'),body.slice(0,400));
 assert.ok(body.toLowerCase().includes('johandavidsson@hotmail.se'));
 assert.ok(!body.includes('farcooperation@gmail.com'));
 await page.screenshot({path:'artifacts/realtime-local/prosa-signin.png',fullPage:true});
 await writeFile('artifacts/realtime-local/login-smoke.json',JSON.stringify({localPageReady:true,workspaceRequiresAuthentication:true,providerHost:new URL(page.url()).hostname,loginFormVisible:true,requestedAccountPasswordForm:true,humanSignInCompleted:false},null,2));
 console.log('PASS: local page requires an account and opens the actual Prosa sign-in form; no credentials entered.');
}finally{await browser.close();}
