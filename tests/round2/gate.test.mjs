import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {begin, step, end} from '../../scripts/round2/gate-run.mjs';
import {readJournal, put, verifyJournal} from '../../scripts/round2/journal.mjs';
import {classify, limits, outcome} from '../../scripts/round2/policy.mjs';
import {fixture, setup, approve, last, cli, flags} from './helpers.mjs';

function admitted(t,count=1,jobs) {
  const f=fixture(t);setup(f.ctx,jobs);approve(f.ctx,'job-1',count);
  const result=begin(f.ctx,{});assert.equal(result.exit,0);
  f.ctx.run=result.run;return f;
}
const child=(f)=>[process.execPath,'-e','require("node:fs").writeFileSync('+JSON.stringify(path.join(f.root,'sentinel'))+',"ran");console.log(JSON.stringify({ok:true,data:{state:"SUBMISSION_VERIFIED"}}))'];
const stepFlags={job:'job-1',role:'publish',platform:['youtube']};

test('not-owner does not take another operators lock',t=>{
  const f=fixture(t);setup(f.ctx);approve(f.ctx);
  const r=begin({...f.ctx,slot:'claude'},{});assert.equal(r.exit,3);assert.equal(r.reason,'not_owner');
  assert.equal(fs.existsSync(path.join(f.home,'run.lock')),false);
});
test('stop gate refuses with durable skipped end',t=>{
  const f=fixture(t);setup(f.ctx);approve(f.ctx);fs.writeFileSync(path.join(f.home,'STOP'),'');
  assert.equal(begin(f.ctx,{}).reason,'stop_flag');assert.equal(last(f.ctx,'RUN_END').d.state,'skipped');
});
test('active run cannot be displaced by another begin',t=>{
  const f=admitted(t);
  assert.equal(begin(f.ctx,{}).reason,'already_running');
  assert.equal(JSON.parse(fs.readFileSync(path.join(f.home,'run.lock'))).run,f.ctx.run);
});
test('not due and unauthorized jobs are explicitly counted',t=>{
  const f=fixture(t);setup(f.ctx,[{id:'job-1',due_at:'2099-01-01',class:'read'},{id:'job-2',due_at:'2020-01-01',class:'write'}]);
  approve(f.ctx,'job-1',0,'read_only');const r=begin(f.ctx,{});
  assert.equal(r.exit,3);assert.equal(r.decision,'no_work_due');
  assert.deepEqual(last(f.ctx,'GATE_DECISION').d.jobs.map(j=>j.verdict),['not_due','not_authorized']);
});
test('invalid job list leaves GATE_ERROR and no run lock',t=>{
  const f=fixture(t);setup(f.ctx);fs.writeFileSync(path.join(f.home,'jobs.json'),'[{}]');
  assert.equal(begin(f.ctx,{}).exit,4);assert.equal(last(f.ctx,'GATE_ERROR').d.error,'bad_job_list');
  assert.equal(fs.existsSync(path.join(f.home,'run.lock')),false);
});
test('invalid clock is a gate error',t=>{
  const f=fixture(t);setup(f.ctx);assert.equal(begin(f.ctx,{now:'bad'}).exit,4);
  assert.equal(last(f.ctx,'GATE_ERROR').d.error,'invalid_time');
});
test('unrecorded operator changes fail closed',t=>{
  const f=fixture(t);setup(f.ctx);approve(f.ctx);
  fs.writeFileSync(path.join(f.home,'operator.json'),'{"operator":"claude"}');
  assert.equal(begin({...f.ctx,slot:'claude'},{}).exit,4);
});
test('dead session is ended interrupted before new admission',t=>{
  const f=admitted(t),file=path.join(f.home,'run.lock');
  const old=JSON.parse(fs.readFileSync(file));old.owner_pid=2147483647;fs.writeFileSync(file,JSON.stringify(old));
  const r=begin(f.ctx,{});assert.equal(r.exit,0);assert.notEqual(r.run,old.run);
  assert.equal(readJournal(f.journal).find(r=>r.type==='RUN_END'&&r.run===old.run).d.state,'interrupted');
});
test('step without an admitted run starts no child',async t=>{
  const f=fixture(t);setup(f.ctx);approve(f.ctx);f.ctx.run='c'.repeat(32);
  assert.equal(await step(f.ctx,stepFlags,child(f)),5);assert.equal(fs.existsSync(path.join(f.root,'sentinel')),false);
});
test('step refuses changed owner and leaves command untouched',async t=>{
  const f=admitted(t);put(f.ctx,'HANDOFF',{from:'gemini',to:'claude',job:null,by:'owner',words_sha256:'b'.repeat(64)});
  fs.writeFileSync(path.join(f.home,'operator.json'),'{"operator":"claude"}');
  assert.equal(await step(f.ctx,stepFlags,child(f)),5);assert.equal(fs.existsSync(path.join(f.root,'sentinel')),false);
});
test('stop after admission and unknown job refuse before spawn',async t=>{
  const f=admitted(t);assert.equal(await step(f.ctx,{...stepFlags,job:'not-admitted'},child(f)),5);
  fs.writeFileSync(path.join(f.home,'STOP'),'');assert.equal(await step(f.ctx,stepFlags,child(f)),5);
  assert.equal(fs.existsSync(path.join(f.root,'sentinel')),false);
});
test('damaged or unwritable journal prevents child, even after begin',async t=>{
  const f=admitted(t);fs.writeFileSync(f.journal+'.lock','held');
  await assert.rejects(step(f.ctx,stepFlags,child(f)));
  assert.equal(fs.existsSync(path.join(f.root,'sentinel')),false);fs.unlinkSync(f.journal+'.lock');
  fs.appendFileSync(f.journal,'broken');await assert.rejects(step(f.ctx,stepFlags,child(f)));
  assert.equal(fs.existsSync(path.join(f.root,'sentinel')),false);
});
test('one child consumes approval, records capture, and preserves stdout/exit',async t=>{
  const f=admitted(t);
  assert.equal(await step(f.ctx,stepFlags,child(f)),0);
  assert.equal(fs.readFileSync(path.join(f.root,'sentinel'),'utf8'),'ran');
  const record=last(f.ctx,'STEP');
  assert.equal(record.d.result,'ok');assert.equal(record.d.fault_fired,null);
  assert.equal(record.d.class,'write');assert.match(record.d.capture_sha256,/^[a-f0-9]{64}$/);
  assert.equal(await step(f.ctx,{...stepFlags,role:'repeat_same'},child(f)),5);
  assert.equal(end(f.ctx).record.d.state,'completed');assert.equal(verifyJournal(f.journal).ok,true);
});
test('performed jobs remain open across completed sessions',async t=>{
  const f=fixture(t);setup(f.ctx,[{id:'job-1',due_at:'2020-01-01',class:'write'},{id:'job-2',due_at:'2020-01-01',class:'write'}]);
  approve(f.ctx,'job-1',2);approve(f.ctx,'job-2',1);
  f.ctx.run=begin(f.ctx,{}).run;
  await step(f.ctx,{job:'job-1',role:'other'},child(f));end(f.ctx);
  const r=begin(f.ctx,{});assert.deepEqual(r.due,['job-1','job-2']);
});
test('child nonzero code is returned but explicit session end is completed',async t=>{
  const f=admitted(t);
  assert.equal(await step(f.ctx,stepFlags,[process.execPath,'-e','process.stderr.write("private stderr");process.exit(4)']),4);
  assert.equal(last(f.ctx,'STEP').d.result,'unknown');assert.equal(last(f.ctx,'STEP').d.answered,false);assert.equal(end(f.ctx).record.d.state,'completed');
  assert.equal(fs.readFileSync(f.journal,'utf8').includes('private stderr'),false);
});
test('spawn failure consumes approval without pretending a child ran',async t=>{
  const f=admitted(t);
  assert.equal(await step(f.ctx,stepFlags,[path.join(f.root,'missing-executable')]),127);
  const reservation=JSON.parse(fs.readFileSync(path.join(f.home,'reservations.json')))[0];
  assert.equal(reservation.started,false);assert.equal(last(f.ctx,'STEP').d.exit,127);
});
test('Bilibili delete and unsupported test platform refuse regardless of consent',async t=>{
  const f=admitted(t,9,[{id:'job-1',due_at:'2020-01-01',class:'write',run_class:'real_slot',platforms:['bilibili']}]);
  assert.equal(await step(f.ctx,{job:'job-1',role:'delete',platform:['bilibili']},child(f)),5);
  assert.equal(fs.existsSync(path.join(f.root,'sentinel')),false);
  const job={id:'job-1',run_class:'test_post'};
  assert.equal(limits([],job,'publish',['x'],new Date().toISOString()),'test_platform_forbidden');
});
test('real child transcript relays bytes and private lines with timestamps',t=>{
  const f=admitted(t),r=cli(f,'gate-run',['step',...flags(f),'--run',f.ctx.run,'--job','job-1','--role','other',
    '--',process.execPath,'-e','process.stdout.write("A\\nB");process.stderr.write("C\\nD");']);
  assert.equal(r.status,0,r.stderr);assert.equal(r.stdout,'A\nB');assert.equal(r.stderr,'C\nD');
  const captures=fs.readdirSync(path.join(f.home,'captures'));
  const lines=fs.readFileSync(path.join(f.home,'captures',captures[0]),'utf8').trimEnd().split('\n').map(JSON.parse);
  assert.ok(lines.every(x=>/Z$/.test(x.at)));assert.equal(lines.length,4);
});
test('classification does not accept false read labels or disguised delete role',()=>{
  assert.equal(classify(['vpub','verify','secret'],{}).class,'write');
  assert.equal(classify(['vpub','delete','secret','--verify-only'],{}).class,'read');
  assert.equal(classify(['vpub','delete','secret','--dry-run'],{}).class,'local');
  assert.equal(classify(['vpub','delete','secret'],{}).class,'write');
  assert.throws(()=>classify(['vpub','publish','plan'],{tool:'other',class:'local'}));
  assert.equal(classify(['unknown-command'],{tool:'other',class:'read'}).class,'read');
  assert.equal(classify(['unknown-command'],{}).class,'write');
});
test('outcome sanitizes unknown message/code and preserves uncertainty',()=>{
  const x=outcome(4,JSON.stringify({error:{code:'secret-private-code',message:'CANARY'},data:{state:'UNKNOWN'}}));
  assert.equal(x.code,null);assert.equal(x.result,'in_doubt');
});

test('lock refusals retain their closed code without exposing messages',()=>{
 for(const code of ['lock_marker_unreadable','browser_busy']){
  const result=outcome(2,JSON.stringify({error:{code,message:'PRIVATE'}}));
  assert.equal(result.code,code);assert.equal(result.result,'refused');
  assert.ok(!JSON.stringify(result).includes('PRIVATE'));
 }
});
const now='2026-11-11T12:00:00.000Z',job={id:'job-1',run_class:'test_post'};
for(const [reason,entries] of [
  ['test_post_daily_cap',Array.from({length:4},()=>({at:'2026-11-11T10:00:00.000Z',platforms:['youtube'],role:'publish',run_class:'test_post'}))],
  ['publish_spacing',[{at:'2026-11-11T11:45:00.000Z',platforms:['youtube'],role:'publish',run_class:'real_slot'}]]
]){
  test(reason+' refuses a publish',()=>assert.equal(limits([],job,'publish',['youtube'],now,entries),reason));
}
test('outside upload spacing uses observed upload time',()=>{
  const record={type:'NOTE',at:now,d:{code:'outside_upload',platform:'youtube',minutes_ago:10}};
  assert.equal(limits([record],job,'publish',['youtube'],now),'outside_upload_spacing');
  record.d.minutes_ago=21;assert.equal(limits([record],job,'publish',['youtube'],now),null);
});
test('48-hour warning hold expires only outside window',()=>{
  const record={type:'NOTE',at:'2026-11-09T13:00:00.000Z',d:{code:'human_check',platform:'youtube'}};
  assert.equal(limits([record],job,'publish',['youtube'],now),'platform_warning_hold');
  record.at='2026-11-09T11:59:00.000Z';assert.equal(limits([record],job,'publish',['youtube'],now),null);
});
