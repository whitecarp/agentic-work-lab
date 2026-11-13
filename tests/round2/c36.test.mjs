import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {fixture,setup,approve,flags,cli,last} from './helpers.mjs';
import {begin,step} from '../../scripts/round2/gate-run.mjs';
import {reconcile} from '../../scripts/round2/reconcile.mjs';
import {faultController} from '../../scripts/round2/planned-faults.mjs';
import {configureSetup} from '../../scripts/round2/fault-setup.mjs';
import {put,readJournal} from '../../scripts/round2/journal.mjs';
const captured=new URL('./fixtures/c36-cli-repeat.json',import.meta.url);
const audit=JSON.parse(fs.readFileSync(captured));
function admitted(t){const f=fixture(t);setup(f.ctx);approve(f.ctx,'job-1',20);f.ctx.run=begin(f.ctx,{}).run;return f;}
function record(f,options){return put(f.ctx,'STEP',{job:'job-1',class:'write',tool:'vpub',action:'publish',role:'publish',platforms:['youtube'],exit:4,ms:0,capture_sha256:'a'.repeat(64),result:'in_doubt',code:null,refs:[],fault_fired:null,...options},f.ctx.run);}
const verify={role:'verify',action:'verify',exit:0,result:'ok',confirmed_state:'SUBMISSION_VERIFIED'};
test('captured span without before counter remains unproven; later reads can settle',t=>{
 const f=admitted(t);record(f,{});const repeat=record(f,{role:'repeat_same',exit:0,result:'ok',awl_run_id:audit.runs[0].awl_run_id});
 const early=record(f,verify);let c=reconcile(f.ctx,captured).in_doubt[0];assert.equal(c.settled_at_seq,early.seq);assert.deepEqual(c.ignored_no_press_steps,[]);
 assert.equal(c.closed_strict,false);
 const res=record(f,{role:'resume',action:'resume',exit:0,result:'ok'});
 c=reconcile(f.ctx,captured).in_doubt[0];assert.equal(c.closed,false);assert.equal(c.last_may_press,res.seq);
 const final=record(f,verify);assert.equal(reconcile(f.ctx,captured).in_doubt[0].settled_at_seq,final.seq);
});
test('a started command cut off without a JSON reply opens the case before a busy repeat',async t=>{
 const f=admitted(t);await step(f.ctx,{job:'job-1',role:'publish'},[process.execPath,'-e','process.exit(1)']);
 const s=last(f.ctx,'STEP');assert.equal(s.d.answered,false);assert.equal(s.d.started,true);assert.equal(s.d.result,'unknown');
 record(f,{role:'repeat_same',exit:1,result:'failed',answered:true,code:'busy'});
 assert.equal(reconcile(f.ctx).in_doubt[0].step,s.seq);assert.equal(reconcile(f.ctx).cutoff_press_flag_unavailable,1);
});
test('cutoff cases separate a recorded false press marker from absent and positive evidence',t=>{
 const f=admitted(t),cmd=audit.runs[0],id=cmd.awl_run_id;
 record(f,{started:true,answered:false,result:'unknown',exit:1,awl_run_id:id});
 const d=structuredClone(audit);d.runs[0].ended=false;d.runs[0].exit_code=null;
 d.events=[d.events[0],{run:cmd.run,type:'transition',platform:'youtube',final_submit_attempted:false,final_presses:0}];
 const file=path.join(f.root,'cutoff.json');fs.writeFileSync(file,JSON.stringify(d));
 assert.equal(reconcile(f.ctx,file).cutoff_without_press_flag,1);
 d.events[1].final_submit_attempted=true;fs.writeFileSync(file,JSON.stringify(d));
 assert.equal(reconcile(f.ctx,file).in_doubt[0].cutoff_press_flag,true);
 d.runs[0].sequence_gap=true;fs.writeFileSync(file,JSON.stringify(d));
 assert.equal(reconcile(f.ctx,file).cutoff_press_flag_unavailable,1);
});
test('a controller failure is distinct from a capture failure and preserves the started step',async t=>{
 const f=admitted(t);const controllerFactory=()=>({attach(){},receive(){},async finish(){throw new Error('offline process snapshot unavailable');}});
 await step(f.ctx,{job:'job-1',role:'publish'},[process.execPath,'-e','process.exit(1)'],{controllerFactory});
 const s=last(f.ctx,'STEP');assert.equal(s.d.fault_fired,null);assert.equal(s.d.started,true);assert.equal(s.d.answered,false);
 assert.ok(fs.existsSync(path.join(f.home,'captures')));
});
test('post-action identity evidence controls firing even with taskkill exit 128',async t=>{
 const f=admitted(t),root={pid:233,parent:7,born:'19'},child={pid:244,parent:233,born:'20'};
 const fault={job:'job-1',kind:'end_tree_after_press',role:'publish',armed_on:'youtube_private',delay_ms:0};
 let gone=false;const c=faultController(f.ctx,fault,{snapshot:async()=>gone?[]:[root,child],kill:async()=>{gone=true;return 128;}});
 c.attach({pid:233,exitCode:null,signalCode:null,once:(event,callback)=>{if(event==='spawn')callback();}});c.line(JSON.stringify({event:'browser_progress',platform:'youtube',stage:'waiting for platform confirmation'}));
 assert.equal((await c.finish('')).fired,true);const r=last(f.ctx,'FAULT_RESULT').d;assert.equal(r.kill_exit,128);assert.equal(r.processes.length,2);assert.ok(r.processes.every(p=>p.ended));
});
test('an agent clears its own stop, another agent cannot; owner clearance commits words',t=>{
 const f=fixture(t);setup(f.ctx);const cmd=(sub,slot,extra=[],input='')=>cli(f,'journal',[sub,'--site','owner','--slot',slot,'--journal',f.journal,...extra],input).status;
 assert.equal(cmd('stop','codex'),0);assert.equal(cmd('clear-stop','gemini'),70);assert.equal(cmd('clear-stop','codex'),0);
 put(f.ctx,'STOP_SET',{by:'scheduler'});assert.equal(cmd('stop','gemini'),0);assert.equal(cmd('clear-stop','gemini'),0);assert.ok(fs.existsSync(path.join(f.home,'STOP')));
 assert.equal(cmd('clear-stop','gemini',['--by','owner']),70);assert.equal(cmd('clear-stop','gemini',['--by','owner'],'synthetic owner words'),0);
 const r=last(f.ctx,'STOP_CLEARED');assert.equal(r.d.by,'owner');assert.match(r.d.words_sha256,/^[0-9a-f]{64}$/);
});
test('different registered stop or wrong operator leaves config, journal and backups untouched',t=>{
 const f=fixture(t);setup(f.ctx);const config=path.join(f.root,'vpub','config.json'),bytes='{"external_stop_file":"other-stop"}';fs.writeFileSync(config,bytes);
 const before=fs.readFileSync(f.journal);const statement=()=>{throw new Error('must not consume words');};
 assert.throws(()=>configureSetup(f.ctx,{enabled:true,by:'owner'},statement),/external_stop_already_registered/);
 assert.throws(()=>configureSetup({...f.ctx,slot:'codex'},{enabled:true,by:'owner'},statement),/not_operator/);
 assert.equal(fs.readFileSync(config,'utf8'),bytes);assert.deepEqual(fs.readFileSync(f.journal),before);assert.ok(!fs.existsSync(path.join(f.home,'fault-config-original.json')));
});
