import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {begin,step,end} from '../../scripts/round2/gate-run.mjs';
import {put,readJournal,validateRecord} from '../../scripts/round2/journal.mjs';
import {reconcile} from '../../scripts/round2/reconcile.mjs';
import {exportData} from '../../scripts/round2/export.mjs';
import {fixture,setup,approve} from './helpers.mjs';

const id='e'.repeat(32);
function ready(t){const f=fixture(t);setup(f.ctx);approve(f.ctx,'job-1',8);f.ctx.run=begin(f.ctx,{}).run;return f;}
function recorded(f,changes={}){
 return put(f.ctx,'STEP',{job:'job-1',class:'write',tool:'vpub',action:'publish',role:'publish',platforms:['youtube'],
  exit:0,ms:1,capture_sha256:'b'.repeat(64),result:'ok',code:null,refs:[],fault_fired:null,awl_run_id:id,...changes},f.ctx.run);
}
const command=(changes={})=>({run:'a'.repeat(32),command:'publish',awl_run_id:id,flags:{},chain_ok:true,...changes});
function check(f,runs=[command()],changes={}){
 const file=path.join(f.root,'audit.json');fs.writeFileSync(file,JSON.stringify({schema:'crosscast-audit/1',times:'relative',integrity:{chain_ok:true},runs,...changes}));
 return reconcile(f.ctx,file);
}

test('child id equals reserved id and STEP id; siblings differ and inherited id stays in parent',async t=>{
 const f=ready(t),previous=process.env.AWL_RUN_ID;process.env.AWL_RUN_ID='PRIVATE_PARENT';
 try{
  for(let i=0;i<2;i++){
   const out=path.join(f.root,'child-'+i+'.json');
   const code="require('node:fs').writeFileSync(process.argv[1],JSON.stringify({id:process.env.AWL_RUN_ID}))";
   assert.equal(await step(f.ctx,{job:'job-1',role:'other'},[process.execPath,'-e',code,out]),0);
  }
  const steps=readJournal(f.journal).filter(r=>r.type==='STEP'),reserved=JSON.parse(fs.readFileSync(path.join(f.home,'reservations.json')));
  assert.equal(steps.length,2);assert.notEqual(steps[0].d.awl_run_id,steps[1].d.awl_run_id);
  for(let i=0;i<2;i++){
   const child=JSON.parse(fs.readFileSync(path.join(f.root,'child-'+i+'.json')));
   assert.match(child.id,/^[a-f0-9]{32}$/);assert.equal(child.id,reserved[i].id);assert.equal(child.id,steps[i].d.awl_run_id);
   assert.equal(reserved[i].step_seq,steps[i].seq);assert.equal(reserved[i].started,true);
  }
  assert.equal(process.env.AWL_RUN_ID,'PRIVATE_PARENT');assert.ok(!fs.readFileSync(f.journal,'utf8').includes('PRIVATE_PARENT'));
 }finally{if(previous===undefined)delete process.env.AWL_RUN_ID;else process.env.AWL_RUN_ID=previous;}
});

test('known unique id matches a step in an open session without absolute times',t=>{
 const f=ready(t),s=recorded(f),r=check(f);
 assert.equal(r.command_binding,'shared_id');assert.equal(r.ungated_writes,0);assert.equal(r.matched_write_commands,1);
 assert.equal(r.write_bindings[0].step_seq,s.seq);
 end(f.ctx);assert.equal(check(f,[command({t_start:'1999-01-01T00:00:00.000Z'})],{times:'exact'}).ungated_writes,0);
});

test('missing and unknown ids are outside wrapper even inside a closed admitted time window',t=>{
 const f=ready(t);recorded(f);const at=new Date().toISOString();end(f.ctx);
 const r=check(f,[command({awl_run_id:null,t_start:at}),command({run:'b'.repeat(32),awl_run_id:'f'.repeat(32),t_start:at})],{times:'exact'});
 assert.equal(r.ungated_writes,2);assert.deepEqual(r.write_bindings.map(b=>b.status),['missing_id','unknown_id']);
});

test('reused tool id cannot credit two commands to one step',t=>{
 const f=ready(t);recorded(f);const r=check(f,[command(),command({run:'b'.repeat(32)})]);
 assert.equal(r.ungated_writes,2);assert.ok(r.write_bindings.every(b=>b.status==='ambiguous_id'));
});

test('duplicate journal step ids are ambiguous',t=>{
 const f=ready(t);recorded(f);recorded(f);assert.equal(check(f).write_bindings[0].status,'ambiguous_id');
});

test('read/local/failed-spawn steps cannot cover a write command',t=>{
 for(const changes of [{class:'read'},{class:'local'},{exit:127}]){
  const f=ready(t);recorded(f,changes);assert.equal(check(f).write_bindings[0].status,'ineligible_step');
 }
});

test('unadmitted session and wrong job cannot cover a write command',t=>{
 for(const what of ['session','job']){
  const f=ready(t);if(what==='session')f.ctx.run='f'.repeat(32);
  recorded(f,what==='job'?{job:'not-admitted'}:{});assert.equal(check(f).write_bindings[0].status,'ineligible_step');
 }
});

test('private reservation without completed STEP stays unbound',t=>{
 const f=ready(t);fs.writeFileSync(path.join(f.home,'reservations.json'),JSON.stringify([{id,run:f.ctx.run,step_seq:null,started:true}]));
 assert.equal(check(f).write_bindings[0].status,'unknown_id');
});

test('broken global or command chain yields unavailable, never a safe zero',t=>{
 const f=ready(t);recorded(f);
 for(const r of [check(f,[command()],{integrity:{chain_ok:false}}),check(f,[command({chain_ok:false})])]){
  assert.equal(r.command_binding,'unavailable');assert.equal(r.ungated_writes,null);assert.equal(r.matched_write_commands,null);
 }
});

test('legacy missing STEP field stays valid; malformed new field refuses',t=>{
 const f=ready(t),s=recorded(f);delete s.d.awl_run_id;assert.deepEqual(validateRecord(s),[]);
 for(const bad of [null,'PRIVATE_CANARY','e'.repeat(32)+'\n',true]){
  assert.ok(validateRecord({...s,d:{...s.d,awl_run_id:bad}}).length);
  assert.throws(()=>check(f,[command({awl_run_id:bad??'PRIVATE_CANARY'})]),/invalid_crosscast_shared_id/);
 }
});

test('read-only command excluded; plain verify stays a write needing a binding',t=>{
 const f=ready(t),r=check(f,[command({command:'delete',flags:{verify_only:true},awl_run_id:null}),command({run:'b'.repeat(32),command:'verify',awl_run_id:null})]);
 assert.equal(r.write_bindings.length,1);assert.equal(r.ungated_writes,1);
});

test('Lab export preserves the optional id without inventing ids for old steps',t=>{
 const f=ready(t);recorded(f);const s=recorded(f,{awl_run_id:'c'.repeat(32)});
 const d={...s.d};delete d.awl_run_id;put(f.ctx,'STEP',d,f.ctx.run);
 const data=exportData(f.ctx),steps=data.records.filter(r=>r.type==='STEP');
 assert.equal(steps[0].d.awl_run_id,id);assert.equal(steps[2].d.awl_run_id,undefined);
});
