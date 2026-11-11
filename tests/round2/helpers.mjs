import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import {spawnSync} from 'node:child_process';
import {put, readJournal} from '../../scripts/round2/journal.mjs';
import {siteHome} from '../../scripts/round2/site.mjs';
export function fixture(t) {
  const root=fs.mkdtempSync(path.join(os.tmpdir(),'round two offline '));
  t.after(()=>fs.rmSync(root,{recursive:true,force:true}));
  const home=path.join(root,'private'),journal=path.join(root,'public','journal.jsonl');
  fs.mkdirSync(home);
  const ctx={home,journal,site:'owner',slot:'gemini'};
  fs.mkdirSync(path.join(root,'vpub'));
  fs.writeFileSync(path.join(home,'site.json'),JSON.stringify({site:'owner',vpub_home:path.join(root,'vpub')}));
  const env={...process.env,ROUND2_HOME:home,VPUB_HOME:path.join(root,'vpub'),BILI_CLI_HOME:path.join(root,'bili')};
  return {...ctx,root,ctx,env};
}
export function approve(ctx,job='job-1',count=1,scope='one_write'){
  return put(ctx,'GO_AHEAD',{job,scope,count,by:'owner',words_sha256:'a'.repeat(64)});
}
export function setup(ctx,jobs=[{id:'job-1',due_at:'2020-01-01T00:00:00.000Z',class:'write',run_class:'test_post',platforms:['youtube']}]) {
  if(!readJournal(ctx.journal).some(r=>r.type==='PROTOCOL_SEAL'))put(ctx,'PROTOCOL_SEAL',{
    protocol_sha256:'a'.repeat(64),lab_commit:'b'.repeat(40),crosscast_commit:'c'.repeat(40),phase:'rehearsal',
    crosscast_digest:null,plan_sha256:null,analysis_sha256:null,rules_card_sha256:null,vpub_home_sha256:siteHome(ctx).digest});
  put(ctx,'HANDOFF',{from:'owner',to:'gemini',job:null,by:'owner',words_sha256:'b'.repeat(64)});
  fs.writeFileSync(path.join(ctx.home,'operator.json'),JSON.stringify({operator:'gemini'}));
  fs.writeFileSync(path.join(ctx.home,'jobs.json'),JSON.stringify(jobs));
  return jobs;
}
export function cli(f,script,args=[],input) {
  return spawnSync(process.execPath,[path.resolve('scripts/round2/'+script+'.mjs'),...args],
    {env:f.env,encoding:'utf8',input,timeout:15000,windowsHide:true});
}
export const flags=f=>['--site','owner','--slot','gemini','--journal',f.journal];
export const last=(ctx,type)=>readJournal(ctx.journal).filter(r=>r.type===type).at(-1);
