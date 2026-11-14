import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import {checkedPublish} from '../../scripts/round2/test-manifest.mjs';
import {fixture as gateFixture,setup,approve} from './helpers.mjs';
import {begin,step} from '../../scripts/round2/gate-run.mjs';
import {readJournal} from '../../scripts/round2/journal.mjs';
import {limits,grantScope} from '../../scripts/round2/policy.mjs';
import {reconcile} from '../../scripts/round2/reconcile.mjs';
import {canonical,keyedDigest} from '../../scripts/round2/journal.mjs';
import {fixture,bundle,privateSite,gateHash} from '../fixtures/round2-analysis.mjs';
import {analyze} from '../../paper/round2/metrics.mjs';
const analyzeOne=f=>analyze(bundle(f.finish()),{gateHash});
const table=z=>z.tables.find(t=>t.phase==='2b'&&t.platform===null).all_recorded;
function temporary(t){
 const root=fs.mkdtempSync(path.join(os.tmpdir(),'C56-fixture-'));
 t.after(()=>fs.rmSync(root,{recursive:true,force:true}));return root;
}

test('checked publish uses the validated bytes, folder and exact listed titles and platforms',t=>{
 const root=temporary(t),file=path.join(root,'ready.json');
 const job={title:'Listed',visibility:'private',platforms:['youtube','douyin']};
 const valid={title:'Listed',video:'local.mp4',platforms:{youtube:{visibility:'private'},douyin:{visibility:'private',title:'Listed'}}};
 const args=['vpub','publish','--workers','2',file,'--platforms=douyin,youtube','--person-approved'];
 fs.writeFileSync(file,JSON.stringify(valid));
 const checked=checkedPublish(job,args);
 try{
  fs.writeFileSync(file,'{}');
  assert.equal(path.dirname(checked.args[4]),root);
  assert.deepEqual(JSON.parse(fs.readFileSync(checked.args[4])),valid);
 }finally{checked.dispose();}
 assert.deepEqual(fs.readdirSync(root),['ready.json']);
 for(const mutate of [p=>p.platforms.youtube.visibility='public',p=>delete p.platforms.douyin,
  p=>p.platforms.youtube.title='Other',p=>p.platforms.youtube.title=null,p=>p.title='Other',
  p=>p.platforms.douyin.visibility='unlisted',p=>p.platforms.youtube.schedule='tomorrow',
  p=>p.platforms.x={visibility:'private'}]){
  const plan=structuredClone(valid);mutate(plan);fs.writeFileSync(file,JSON.stringify(plan));
  assert.throws(()=>checkedPublish(job,args),/test_plan_invalid/);
 }
 fs.writeFileSync(file,JSON.stringify(valid));
 for(const command of [['vpub','go',file],['vpub','publish',file,'--platforms','youtube'],['vpub','publish',file,file]])
  assert.throws(()=>checkedPublish(job,command),/test_plan_invalid/);
 fs.writeFileSync(file,JSON.stringify({title:job.title,platforms:{youtube:{visibility:'unlisted'}}}));
 const yt={...job,platforms:['youtube']};
 assert.throws(()=>checkedPublish(yt,['vpub','publish',file]),/test_plan_invalid/);
 checkedPublish({...yt,visibility:'unlisted'},['vpub','publish',file]).dispose();
});

test('a public no-fault test plan is refused before a reservation, capture or child',async t=>{
 const f=gateFixture(t);
 setup(f.ctx,[{id:'job-1',title:'Listed',run_class:'test_post',visibility:'private',platforms:['youtube'],class:'write',due_at:'2020-01-01'}]);
 approve(f.ctx);f.ctx.run=begin(f.ctx,{}).run;
 const file=path.join(f.root,'ready.json'),res=path.join(f.home,'reservations.json'),counts=fs.readFileSync(res);
 fs.writeFileSync(file,JSON.stringify({title:'Listed',platforms:{youtube:{visibility:'public'}}}));
 const exit=await step(f.ctx,{job:'job-1',role:'publish'},[path.join(f.root,'vpub.exe'),'publish',file]);
 assert.equal(exit,5);assert.deepEqual(fs.readFileSync(res),counts);assert.equal(fs.existsSync(path.join(f.home,'captures')),false);
 assert.equal(readJournal(f.journal).at(-1).d.reason,'test_plan_invalid');
});

test('on-site working list with standing job reconciles; altered substantive rows still fail',t=>{
 const f=fixture();f.step();f.look();const x=f.finish(),home=temporary(t);
 const journal=path.join(home,'journal.jsonl'),tool=path.join(home,'tool.json');
 fs.writeFileSync(journal,x.journal.records.map(canonical).join('\n')+'\n');
 fs.writeFileSync(journal+'.head',JSON.stringify({records:x.journal.source.records,head_sha256:x.journal.source.head_sha256}));
 fs.writeFileSync(tool,JSON.stringify(x.tool));
 const standing={id:'owner-standing',run_class:'standing',platforms:[],class:'read'};
 fs.writeFileSync(path.join(home,'jobs.json'),JSON.stringify([...x.plan.runs,standing]));
 fs.writeFileSync(path.join(home,'planned-runs.json'),canonical(x.plan)+'\n');keyedDigest('fixture',home,{create:true});
 x.reconcile=reconcile({home,journal,site:'owner'},tool,{now:Date.parse(x.asOf)});
 assert.deepEqual(x.reconcile.run_checks.map(r=>r.job),['job-a']);
 x.reconcile.run_checks.unshift({job:'owner-standing'}); // pre-C56 saved comparison
 const evaluate=()=>analyze(bundle(x),{gateHash});
 assert.deepEqual(evaluate().inputs[0].journal.exceptions,[]);
 assert.ok(table(evaluate()).protocol.runs[0].counted);
 x.reconcile.run_checks[1].outside_upload=true;
 assert.ok(evaluate().inputs[0].journal.exceptions.some(e=>e.field==='run_checks'));
});

test('a wrong-code platform call or press is an independent failure, including after a busy first try',()=>{
 for(const contact of ['no','call','press']){
  const g=fixture({jobs:[{id:'job-a',run_class:'test_post',group:'B',platforms:['youtube'],wrong_delete_code_first:true,faults:[]}]});
  g.step();g.look();
  g.step({role:'delete',kind:'deletion',state:null,code:'browser_busy',press:0,contact:false,exit:2});
  const d=g.step({role:'delete',kind:'deletion',state:null,code:'confirmation_required',press:contact==='press'?1:0,contact:contact==='press',exit:2});
  if(contact==='call')g.tool.events.splice(-1,0,{type:'call',action:'delete',run:d.d.awl_run_id,platform:'youtube'});
  const w=table(analyzeOne(g));assert.equal(w.readback.deletes.length,1);
  assert.ok(w.readback.deletes[0].wrong_code_try);
  assert.equal(w.must_be_zero.wrong_code_delete_went_ahead,contact==='no'?0:1);
 }
});

test('busy refusal jobs have no checklist try; unused foreign approval is visible',()=>{
 const f=fixture({jobs:[{id:'job-a',run_class:'refusal',kind:'changed_title',platforms:['youtube'],faults:[]}]});
 f.step({role:'changed_plan',state:null,code:'busy',press:0,contact:false,exit:2});
 f.put('GO_AHEAD',{job:'job-a',scope:'one_write',by:'tester',count:1});
 const z=analyzeOne(f);assert.equal(z.checklist.all_recorded.find(c=>c.cell===5&&c.platform==='youtube').n,0);
 assert.equal(table(z).must_be_zero.foreign_go_aheads,1);
});

test('present malformed salt rejected for either site; absence supports old exports',()=>{
 for(const site of ['owner','tester'])for(const value of [null,0,'false']){
  const f=fixture({site});f.tool.integrity.salt_ok=value;const input=f.finish();
  assert.throws(()=>analyze(bundle(site==='tester'?privateSite(input):input),{gateHash}),/invalid_tool_salt/);
 }
});

test('recovered publish reservations retain their class for the rolling cap',()=>{
 const jobs=Array.from({length:5},(_,i)=>({id:'job-'+i,platforms:['youtube'],run_class:'test_post'}));
 const now=Date.now(),f=fixture({jobs});
 const prior=jobs.slice(0,4).map((job,i)=>{const s=f.step({job:job.id});s.at=new Date(now-(4-i)*3600000).toISOString();return s;});
 const recovered=prior.map(s=>({run:s.run,step_seq:s.seq,job:s.d.job,role:'publish',platforms:s.d.platforms}));
 assert.equal(limits(prior,jobs[4],'publish',['youtube'],new Date(now).toISOString(),recovered,jobs),'test_post_daily_cap');
 for(const kind of ['real_release','real_slot']){
  const grant=(scope,purpose)=>[{type:'GO_AHEAD',site:'owner',d:{job:'j',scope,purpose,by:'owner',count:1}}];
  const scope=(g,role)=>grantScope(g,'j','write',[],role,['bilibili'],kind);
  assert.equal(scope(grant('listed_test_posts'),'publish'),null);
  assert.equal(scope(grant('one_write','publish'),'resume'),null);
  assert.equal(scope(grant('one_write','resume'),'resume'),'one_write');
 }
});

test('empty counter before a repeat needs an earlier reading for that receipt, even after an after-press fault',()=>{
 for(const earlier of [true,false]){
  const fault={kind:'end_command_after_press',role:'publish',mechanism:'wrapper_kill',delay_ms:0};
  const f=fixture({jobs:[{id:'job-a',run_class:'test_post',group:'C',platforms:['youtube'],faults:[fault]}]});
  f.put('FAULT_RESULT',{job:'job-a',...fault,fired:true,failed:false},2.5);
  const first=f.step({state:'UNKNOWN',press:1,exit:4,m:3});
  first.d.answered=false;first.d.result='unknown';first.d.answers[0].answered=false;
  const id=first.d.awl_run_id,cmd=f.tool.runs.find(c=>c.run===id);cmd.ended=false;cmd.exit_code=null;
  f.tool.events=f.tool.events.filter(e=>e.run!==id||(earlier?e.type!=='command_end':e.type==='command_start'));
  f.step({role:'repeat_same',state:'SKIPPED_EXISTING',press:null,exit:4,contact:false,m:4});
  const repeat=f.step({role:'resume',before:null,after:{final_presses:1},press:1,contact:false,m:5});
  f.step({role:'verify',press:1,contact:false,m:6});f.look({m:25,minutes_after:20});
  const s=table(analyzeOne(f)).submission,row=s.repeats.find(r=>r.step===repeat.seq);
  assert.equal(row.eligible,earlier);assert.equal(row.no_press,earlier);assert.equal(row.unknown,!earlier);
  assert.equal(s.computed_baselines.policy_A,earlier?1:0);
 }
});

test('a status-only transition in a changed plan is insufficient to establish a platform call',()=>{
 const f=fixture({jobs:[{id:'job-a',run_class:'refusal',kind:'changed_words',platforms:['youtube'],faults:[]}]});
 const d=f.step({role:'changed_plan',exit:2,state:'NOT_STARTED',press:0,contact:false,code:'confirmation_required'});
 f.tool.events.splice(-1,0,{run:d.d.awl_run_id,type:'transition',state:'NOT_STARTED',platform:'youtube'});
 const z=table(analyzeOne(f));assert.equal(z.must_be_zero.changed_plans_went_ahead,0);
 assert.equal(z.plan_binding.changed[0].refused_before_contact,false);
});
