import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {begin,step,end,closeJob} from '../../scripts/round2/gate-run.mjs';
import {put,readJournal,clearGuard,verifyJournal} from '../../scripts/round2/journal.mjs';
import {grantScope,limits} from '../../scripts/round2/policy.mjs';
import {fixture,setup,approve,last,cli,flags} from './helpers.mjs';
const command=[process.execPath,'-e','console.log(JSON.stringify({state:"DELETED_VERIFIED"}))'];
const sf={job:'job-1',role:'delete',platform:['youtube']};
const observe=(f,seen='absent')=>put(f.ctx,'OBSERVATION',{job:'job-1',ref:null,about:'post',by:'owner',client:'phone_app',seen,fields_match:null,minutes_after:10,words_sha256:'a'.repeat(64),shot_sha256:null});
function admitted(t){const f=fixture(t);setup(f.ctx);approve(f.ctx,'job-1',5,'listed_test_posts');f.ctx.run=begin(f.ctx,{}).run;return f;}
test('listed approval cannot pay for a delete; one scoped unit pays once',async t=>{
 const f=admitted(t);
 assert.equal(await step(f.ctx,sf,command),5);assert.equal(last(f.ctx,'STEP'),undefined);
 approve(f.ctx,'job-1',1,'one_write');
 assert.equal(await step(f.ctx,sf,command),0);
 assert.equal(await step(f.ctx,sf,command),5);
 assert.equal(readJournal(f.journal).filter(r=>r.type==='STEP').length,1);
 assert.equal(JSON.parse(fs.readFileSync(path.join(f.home,'reservations.json')))[0].scope,'one_write');
});
test('ordinary sequence consumes listed units first; unknown role cannot spend them',async t=>{
 const f=admitted(t);approve(f.ctx,'job-1',1,'one_write');
 assert.equal(await step(f.ctx,{...sf,role:'verify'},command),0);
 const saved=JSON.parse(fs.readFileSync(path.join(f.home,'reservations.json')));
 assert.equal(saved[0].scope,'listed_test_posts');
 assert.equal(grantScope(readJournal(f.journal),'job-1','write',saved,'delete'),'one_write');
 assert.equal(grantScope(readJournal(f.journal),'job-1','write',[...saved,{job:'job-1',class:'write',scope:'one_write'}],'other'),null);
});
test('uncertain legacy reservation never creates new deletion credit',t=>{
 const f=admitted(t);approve(f.ctx,'job-1',1,'one_write');
 assert.equal(grantScope(readJournal(f.journal),'job-1','write',[{job:'job-1',class:'write'}],'delete'),null);
});
test('one job spans sessions with a refusal and later readback; only JOB_END closes',async t=>{
 const f=admitted(t);
 assert.equal(await step(f.ctx,{...sf,role:'publish'},[process.execPath,'-e','process.exit(2)']),2);
 assert.equal(end(f.ctx).record.d.state,'completed');
 f.ctx.run=begin(f.ctx,{}).run;
 approve(f.ctx,'job-1',1,'one_write');assert.equal(await step(f.ctx,sf,command),0);
 assert.equal(await step(f.ctx,{...sf,role:'delete_verify',tool:'other',class:'read'},['unused']),127);
 // A failed spawn cannot establish successful readback.
 assert.throws(()=>closeJob(f.ctx,{job:'job-1',reason:'done'}),/evidence/);
 // Run a known Node stand-in (write-class by default) with explicit one-write;
 // the synthetic STEP below represents the existing CLI's read-only classify.
 put(f.ctx,'STEP',{job:'job-1',class:'read',tool:'vpub',action:'delete',role:'delete_verify',platforms:['youtube'],exit:0,ms:1,capture_sha256:'b'.repeat(64),result:'ok',code:null,refs:[],fault_fired:null},f.ctx.run);
 assert.equal(closeJob(f.ctx,{job:'job-1',reason:'done'}).record.type,'JOB_END');
 assert.equal(await step(f.ctx,{...sf,role:'verify'},command),5);
 end(f.ctx);assert.equal(begin(f.ctx,{}).jobs,undefined);
 assert.equal(last(f.ctx,'GATE_DECISION').d.jobs[0].verdict,'completed');
 assert.throws(()=>closeJob(f.ctx,{job:'job-1',reason:'done'}),/job_closed/);
});
test('test closure requires evidence for its exact job and chosen reason',async t=>{
 const f=admitted(t);
 assert.throws(()=>closeJob(f.ctx,{job:'job-1',reason:'nothing_posted'}),/evidence/);
 observe(f);await step(f.ctx,{...sf,role:'verify'},command);
 assert.throws(()=>closeJob(f.ctx,{job:'job-1',reason:'nothing_posted'}),/evidence/);
 observe(f);assert.equal(closeJob(f.ctx,{job:'job-1',reason:'nothing_posted'}).record.d.reason,'nothing_posted');
});
test('manual cleanup closure requires its manual note',t=>{
 const f=admitted(t);assert.throws(()=>closeJob(f.ctx,{job:'job-1',reason:'removed_by_hand'}));
 put(f.ctx,'NOTE',{code:'manual_step',platform:null,job:'job-1',agent:null,minutes_ago:null});
 assert.equal(closeJob(f.ctx,{job:'job-1',reason:'removed_by_hand'}).exit,0);
});
test('abandon requires private words, while a real release can close explicitly',t=>{
 const f=admitted(t);
 let r=cli(f,'gate-run',['close-job',...flags(f),'--job','job-1','--reason','abandoned'],'');assert.equal(r.status,70);
 r=cli(f,'gate-run',['close-job',...flags(f),'--job','job-1','--reason','abandoned','--by','owner'],'PRIVATE_CLOSE_CANARY');
 assert.equal(r.status,0,r.stderr);assert.equal(last(f.ctx,'JOB_END').d.by,'owner');
 assert.ok(!fs.readFileSync(f.journal,'utf8').includes('PRIVATE_CLOSE_CANARY'));
});
test('real-release readbacks remain available after clean end',async t=>{
 const f=fixture(t);setup(f.ctx,[{id:'job-1',due_at:'2020-01-01',class:'write',run_class:'real_slot',platforms:['youtube']}]);approve(f.ctx,'job-1',4);
 f.ctx.run=begin(f.ctx,{}).run;await step(f.ctx,{...sf,role:'publish'},command);end(f.ctx);
 f.ctx.run=begin(f.ctx,{}).run;await step(f.ctx,{...sf,role:'verify'},command);end(f.ctx);
 assert.equal(closeJob(f.ctx,{job:'job-1',reason:'done'}).exit,0);
 assert.equal(begin(f.ctx,{}).exit,3);
});
test('clear-guard rejects live, ambiguous, corrupt, and unauthorised recovery',t=>{
 const f=admitted(t),g=path.join(f.home,'operation.guard');
 fs.writeFileSync(g,JSON.stringify({pid:process.pid}));assert.throws(()=>clearGuard(f.ctx,'owner',Buffer.from('yes')),/not_dead/);
 fs.writeFileSync(g,'{}');assert.throws(()=>clearGuard(f.ctx,'owner',Buffer.from('yes')));
 fs.writeFileSync(g,JSON.stringify({pid:2147483647}));assert.throws(()=>clearGuard(f.ctx,'gemini',Buffer.from('yes')));
 assert.throws(()=>clearGuard(f.ctx,'owner',Buffer.alloc(0)));
 fs.appendFileSync(f.journal,'broken');assert.throws(()=>clearGuard(f.ctx,'owner',Buffer.from('yes')),/broken/);
 assert.equal(fs.existsSync(g),true);
});
test('explicit dead-guard recovery records digest and preserves control evidence',t=>{
 const f=admitted(t),g=path.join(f.home,'operation.guard'),lock=path.join(f.home,'run.lock');
 const saved=fs.readFileSync(lock);fs.writeFileSync(path.join(f.home,'STOP'),'keep');
 fs.writeFileSync(g,JSON.stringify({pid:2147483647}));fs.writeFileSync(f.journal+'.lock',JSON.stringify({pid:2147483647}));
 const r=clearGuard(f.ctx,'owner',Buffer.from('PRIVATE_RECOVERY_CANARY'));
 assert.equal(r.type,'NOTE');assert.equal(r.d.code,'guard_cleared');assert.match(r.d.words_sha256,/^[a-f0-9]{64}$/);
 assert.equal(fs.existsSync(g),false);assert.deepEqual(fs.readFileSync(lock),saved);assert.equal(fs.readFileSync(path.join(f.home,'STOP'),'utf8'),'keep');
 assert.ok(!fs.readFileSync(f.journal,'utf8').includes('PRIVATE_RECOVERY_CANARY'));assert.equal(verifyJournal(f.journal).ok,true);
});

test('daily cap counts distinct first publishes, never repeats or resumes',()=>{
 const now='2026-11-12T10:00:00.000Z',job={id:'next',run_class:'test_post'};
 const rows=['publish','publish','repeat_same','resume','repeat_same'].map(role=>({job:'one',role,at:'2026-11-12T08:00:00.000Z',run_class:'test_post',platforms:['youtube'],started:true}));
 assert.equal(limits([],job,'publish',['youtube'],now,rows),null);
 for(let i=2;i<=4;i++)rows.push({...rows[0],job:'job-'+i});
 assert.equal(limits([],job,'publish',['youtube'],now,rows),'test_post_daily_cap');
});

test('wrapper recording failure marks failed but keeps the spent reservation',async t=>{
 const f=admitted(t),original=fs.writeFileSync;
 const spy=t.mock.method(fs,'writeFileSync',function(file,data,...rest){
   if(typeof data==='string'&&data.includes('"type":"STEP"'))throw new Error('offline_record_failure');
   return original.call(this,file,data,...rest);
 });
 await assert.rejects(step(f.ctx,{...sf,role:'verify'},command),/offline_record_failure/);spy.mock.restore();
 assert.equal(end(f.ctx).record.d.state,'failed');
 assert.equal(JSON.parse(fs.readFileSync(path.join(f.home,'reservations.json'))).length,1);
});
