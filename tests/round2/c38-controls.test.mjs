import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {fixture,setup,approve,flags,cli,last} from './helpers.mjs';
import {begin,end,step} from '../../scripts/round2/gate-run.mjs';
import {protectDigests} from '../../scripts/round2/export.mjs';
import {outcome} from '../../scripts/round2/policy.mjs';
import {reconcile} from '../../scripts/round2/reconcile.mjs';

function admitted(t){
 const f=fixture(t);setup(f.ctx);approve(f.ctx,'job-1',4);
 f.ctx.run=begin(f.ctx,{}).run;return f;
}
test('a lost session lock can be closed only by the admitted operator',t=>{
 const f=admitted(t),before=f.ctx.run;
 assert.equal(cli(f,'journal',['handoff',...flags(f),'--by','owner','--to','codex'],'switch').stderr.trim(),'session_open');
 fs.unlinkSync(path.join(f.home,'run.lock'));
 assert.equal(begin(f.ctx,{}).open_run,before);
 assert.throws(()=>end({...f.ctx,slot:'codex'}),/run_not_admitted/);
 assert.throws(()=>end({...f.ctx,run:'0'.repeat(32)}),/run_not_admitted/);
 assert.equal(end(f.ctx).record.d.state,'interrupted');
 assert.equal(begin(f.ctx,{}).exit,0);
});
test('partial step retains both platform outcomes and fixed code words',()=>{
 const r=outcome(4,JSON.stringify({data:{results:{youtube:{state:'SUBMISSION_VERIFIED'},
  douyin:{state:'NOT_STARTED',code:'page_not_ready'}}}}),{role:'publish',platforms:['youtube','douyin']});
 assert.equal(r.result,'partial');assert.equal(r.answers[1].code,'page_not_ready');
 for(const code of ['layout_changed','page_not_ready','x'.repeat(48),'private data','A1','a'.repeat(49),'C:/file']){
  const r=outcome(4,JSON.stringify({data:{state:'SOME_NEW_STATE',code}}),{role:'publish',platforms:['youtube']});
  assert.equal(r.answers[0].state,'UNLISTED');assert.equal(r.code,/^[a-z][a-z0-9_]{0,47}$/.test(code)?code:null);
 }
});
test('missing export still lists an exit-2 press-capable error',async t=>{
 const f=admitted(t);
 await step(f.ctx,{job:'job-1',role:'edit',platform:['youtube']},[process.execPath,'-e',
  'console.log(JSON.stringify({ok:false,error:{code:"layout_changed"}}));process.exitCode=2']);
 const s=last(f.ctx,'STEP');
 assert.deepEqual(reconcile(f.ctx).error_steps_unclassified,[{step:s.seq,platform:'youtube',exit:2,code:'layout_changed'}]);
});
test('only the seal plan fingerprint is publicly comparable',()=>{
 const digest='d'.repeat(64),mask=()=> 'x';
 const shared=protectDigests({records:[{d:{plan_sha256:digest,capture_sha256:digest}}],unknown:{plan_sha256:digest}},mask);
 assert.equal(shared.records[0].d.plan_sha256,digest);
 assert.equal(shared.records[0].d.capture_sha256,'x');
 assert.equal(shared.unknown.plan_sha256,'x');
});
test('unreadable piped words refuse before creating a commitment',t=>{
 const f=fixture(t);setup(f.ctx);
 const args=['go-ahead',...flags(f),'--job','job-1','--scope','one_write','--count','1'];
 assert.equal(cli(f,'journal',args,' ?? ?  ').stderr.trim(),'words_unreadable');
 assert.equal(last(f.ctx,'GO_AHEAD'),undefined);
 assert.equal(cli(f,'journal',args,'可以?').status,0);
});
test('an error emitted after spawn preserves the started step',async t=>{
 const f=admitted(t);const controllerFactory=()=>({
  attach(child){child.once('spawn',()=>child.emit('error',new Error('synthetic post-spawn signal failure')));},
  receive(){},async finish(){return {fired:null};}
 });
 const code=await step(f.ctx,{job:'job-1',role:'publish'},[process.execPath,'-e','process.exitCode=3'],{controllerFactory});
 assert.equal(code,3);assert.equal(last(f.ctx,'STEP').d.started,true);
});
for(const [name,args] of [['export',[]],['seal',[]],['daily',[]],['reconcile',[]],['plan-runs',['make']],['fault-setup',['configure']]]){
 test(name+' gives a fixed inner refusal',t=>{
  const f=fixture(t);const r=cli(f,name,[...args,'--invalid','yes']);
  assert.equal(r.status,70);assert.equal(r.stderr.trim(),'unknown_flag');
 });
}
