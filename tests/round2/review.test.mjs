import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {seal,verifySeals} from '../../scripts/round2/seal.mjs';
import {exportData,exportFile} from '../../scripts/round2/export.mjs';
import {daily} from '../../scripts/round2/daily.mjs';
import {reconcile} from '../../scripts/round2/reconcile.mjs';
import {crosscast} from '../../scripts/round2/crosscast.mjs';
import {put,readJournal,verifyJournal,canonical} from '../../scripts/round2/journal.mjs';
import {sha,atomic} from '../../scripts/round2/io.mjs';
import {begin,end} from '../../scripts/round2/gate-run.mjs';
import {fixture,setup,approve,cli,flags} from './helpers.mjs';
const today=()=>new Date().toISOString().slice(0,10);
const tomorrow=()=>new Date(Date.now()+86400000).toISOString().slice(0,10);
function ready(t){const f=fixture(t);setup(f.ctx);approve(f.ctx,'job-1',8);return f;}
function note(f){return put(f.ctx,'NOTE',{code:'rehearsal',platform:null,job:null,agent:null,minutes_ago:null});}
function step(f,changes={},run=null){
 const capture=Buffer.from('private command result\n');atomic(path.join(f.home,'captures','fixture.jsonl'),capture);
 return put(f.ctx,'STEP',{job:'job-1',class:'write',tool:'vpub',action:'publish',role:'publish',platforms:['youtube'],exit:4,ms:10,capture_sha256:sha(capture),result:'in_doubt',code:'injected_fault',refs:[],fault_fired:true,...changes},run);
}
function observe(f,changes={}){return put(f.ctx,'OBSERVATION',{job:'job-1',ref:null,about:'post',by:'owner',client:'phone_app',seen:'present_private',fields_match:null,minutes_after:15,words_sha256:'c'.repeat(64),shot_sha256:null,...changes});}
function audit(f,runs=[],changes={}){
 const file=path.join(f.root,'crosscast.json');fs.writeFileSync(file,JSON.stringify({schema:'crosscast-audit/1',times:'exact',integrity:{chain_ok:true},runs,...changes}));return file;
}
const run=(changes={})=>({run:'a'.repeat(32),command:'publish',t_start:new Date().toISOString(),t_end:new Date().toISOString(),flags:{},dropped_events:0,sequence_gap:false,loss_complete:false,chain_ok:true,...changes});
const sealFile=f=>path.join(path.dirname(f.journal),'seals',today()+'.json');

test('seal records exact head, types, versions and is byte-idempotent',t=>{
 const f=ready(t),a=seal(f.ctx),bytes=fs.readFileSync(sealFile(f));
 assert.equal(a.records,2);assert.equal(a.head_sha256,verifyJournal(f.journal).head_sha256);assert.deepEqual(a.counts,{HANDOFF:1,GO_AHEAD:1});
 assert.equal(a.tools.node,process.versions.node);assert.deepEqual(seal(f.ctx),a);assert.deepEqual(fs.readFileSync(sealFile(f)),bytes);
});
test('new day links the exact previous seal bytes',t=>{
 const f=ready(t);seal(f.ctx);const old=sha(fs.readFileSync(sealFile(f)));note(f);
 const second=seal(f.ctx,tomorrow());assert.equal(second.previous_sha256,old);assert.equal(verifySeals(f.ctx).seals.length,2);
});
test('same day never overwrites an earlier seal when new records arrive',t=>{
 const f=ready(t);seal(f.ctx);const bytes=fs.readFileSync(sealFile(f));note(f);
 assert.throws(()=>seal(f.ctx),/would_change/);assert.deepEqual(fs.readFileSync(sealFile(f)),bytes);assert.equal(verifySeals(f.ctx).unsealed_records,1);
});
test('changed seal count, digest and version are rejected',t=>{
 const f=ready(t);seal(f.ctx);const bytes=fs.readFileSync(sealFile(f)),s=JSON.parse(bytes);
 for(const altered of [{...s,records:0},{...s,head_sha256:'e'.repeat(64)},{...s,tools:{journal:'1',node:'99.0.0'}}]){
  fs.writeFileSync(sealFile(f),canonical(altered)+'\n');assert.throws(()=>verifySeals(f.ctx));
 }
 fs.writeFileSync(sealFile(f),bytes);assert.equal(verifySeals(f.ctx).sealed_records,2);
});
test('removed final seal and missing seal anchor fail verification',t=>{
 const f=ready(t);seal(f.ctx);seal(f.ctx,tomorrow());
 fs.unlinkSync(path.join(path.dirname(sealFile(f)),tomorrow()+'.json'));assert.throws(()=>verifySeals(f.ctx),/anchor/);
 fs.unlinkSync(path.join(path.dirname(sealFile(f)),'.head'));assert.throws(()=>verifySeals(f.ctx),/anchor/);
});
test('seals reject future-record mismatch, invalid dates and out-of-order insertion',t=>{
 const f=ready(t);assert.throws(()=>seal(f.ctx,'2026-02-30'),/invalid_day/);assert.throws(()=>seal(f.ctx,'2020-01-01'),/before_records/);
 seal(f.ctx,tomorrow());assert.throws(()=>seal(f.ctx,today()),/out_of_order/);
});
test('empty journal can be sealed without a private home',t=>{
 const f=fixture(t);assert.equal(seal({...f.ctx,home:null}).records,0);assert.equal(verifySeals(f.ctx).sealed_records,0);
});
test('edited, removed, reordered and truncated journal records block seal and export',t=>{
 const f=ready(t);note(f);const bytes=fs.readFileSync(f.journal),lines=bytes.toString().trimEnd().split('\n');
 for(const broken of [bytes.toString().replace('rehearsal','safety_stop'),lines.slice(0,-1).join('\n')+'\n',[lines[1],lines[0],lines[2]].join('\n')+'\n',bytes.subarray(0,-2)]){
  fs.writeFileSync(f.journal,broken);assert.throws(()=>seal(f.ctx));assert.throws(()=>exportData(f.ctx));
 }fs.writeFileSync(f.journal,bytes);assert.equal(verifyJournal(f.journal).ok,true);
});

test('export validates records, preserves original chain evidence, never reads private captures',t=>{
 const f=ready(t);step(f);seal(f.ctx);const original=fs.readFileSync;
 const spy=t.mock.method(fs,'readFileSync',function(file,...args){
  assert.ok(!String(file).includes('captures'));return original.call(this,file,...args);
 });
 const result=exportData(f.ctx);spy.mock.restore();assert.equal(result.records.length,3);assert.equal(result.unsealed_records,0);
 assert.equal(result.source.head_sha256,verifyJournal(f.journal).head_sha256);assert.ok(!JSON.stringify(result).includes('private command'));
});
test('privacy coarsens all record and gate times to minutes and drops observation minutes',t=>{
 const f=ready(t);f.ctx.run=begin(f.ctx,{}).run;observe(f);end(f.ctx);
 const value=exportData(f.ctx,{privacy:true});assert.equal(value.source.derived_records,true);
 for(const r of value.records){assert.match(r.at,/:00\.000Z$/);if(r.type==='GATE_DECISION')assert.match(r.d.now,/:00\.000Z$/);}
 assert.ok(!Object.hasOwn(value.records.find(r=>r.type==='OBSERVATION').d,'minutes_after'));
 assert.equal(readJournal(f.journal).find(r=>r.type==='OBSERVATION').d.minutes_after,15);
});
test('denylist blocks even schema-valid private labels before writing',t=>{
 const f=ready(t),deny=path.join(f.root,'deny.txt'),out=path.join(f.root,'out.json');fs.writeFileSync(deny,'job-1\n');
 assert.throws(()=>exportFile(f.ctx,out,{deny}),/private_string/);assert.equal(fs.existsSync(out),false);
});
test('unknown schema fields and free text never enter a public record',t=>{
 const f=ready(t);assert.throws(()=>put(f.ctx,'NOTE',{code:'rehearsal',platform:null,job:null,agent:null,minutes_ago:null,text:'CANARY'}));
 assert.throws(()=>put(f.ctx,'NOTE',{code:'https://private.example',platform:null,job:null,agent:null,minutes_ago:null}));
 assert.ok(!JSON.stringify(exportData(f.ctx)).includes('CANARY'));
});
test('export bytes are deterministic and publication cannot overwrite evidence',t=>{
 const f=ready(t),out=path.join(f.root,'export.json');exportFile(f.ctx,out);const first=fs.readFileSync(out);exportFile(f.ctx,out);assert.deepEqual(fs.readFileSync(out),first);
 for(const target of [f.journal,path.join(f.home,'operator.json'),path.join(path.dirname(f.journal),'seals','bad.json')])assert.throws(()=>exportFile(f.ctx,target),/destination/);
});
test('export carries command-level losses but drops all other CrossCast content',t=>{
 const f=ready(t),file=audit(f,[run({dropped_events:3,sequence_gap:true,title:'SECRET',path:'PRIVATE'})]);
 const result=exportData(f.ctx,{crosscast:file});assert.equal(result.crosscast_command_losses[0].dropped_events,3);
 assert.equal(result.crosscast_command_losses[0].sequence_gap,true);assert.equal(result.crosscast_command_losses[0].loss_complete,false);
 assert.ok(!JSON.stringify(result).includes('SECRET'));assert.ok(!JSON.stringify(result).includes('PRIVATE'));
});
test('old CrossCast exports retain unknown rather than zero losses',t=>{
 const f=ready(t),file=audit(f,[{run:'a'.repeat(32)}]);const result=exportData(f.ctx,{crosscast:file}).crosscast_command_losses[0];
 assert.equal(result.dropped_events,null);assert.equal(result.sequence_gap,null);assert.equal(result.loss_complete,false);
});
test('invalid CrossCast loss fields, duplicate commands and schema are refused',t=>{
 const f=ready(t);for(const rows of [[run({dropped_events:-1})],[run({sequence_gap:'false'})],[run(),run()],[run({run:'PRIVATE'})]])assert.throws(()=>crosscast(audit(f,rows)));
 assert.throws(()=>crosscast(audit(f,[],{schema:'other'})));
});
test('export validates seals, and absent seals are explicitly unsealed',t=>{
 const f=ready(t);assert.equal(exportData(f.ctx).unsealed_records,2);seal(f.ctx);fs.writeFileSync(sealFile(f),'{}');assert.throws(()=>exportData(f.ctx));
});
test('export command prints counts and never edits source journal',t=>{
 const f=ready(t),out=path.join(f.root,'output.json'),bytes=fs.readFileSync(f.journal);
 const result=cli(f,'export',['--site','owner','--journal',f.journal,'--out',out,'--privacy']);
 assert.equal(result.status,0,result.stderr);assert.equal(JSON.parse(result.stdout).records,2);assert.deepEqual(fs.readFileSync(f.journal),bytes);
});
test('daily defaults to five summary lines and writes no seal or signoff',t=>{
 const f=ready(t);step(f);const bytes=fs.readFileSync(f.journal),result=daily(f.ctx);
 assert.equal(result.lines.length,7);assert.match(result.lines[0],/^records_today: 3$/);assert.equal(result.checked.in_doubt_open,1);
 assert.equal(fs.existsSync(sealFile(f)),false);assert.deepEqual(fs.readFileSync(f.journal),bytes);
});
test('daily apply seals and prompts without fabricating a SIGN_OFF',t=>{
 const f=ready(t),result=cli(f,'daily',['--site','owner','--journal',f.journal,'--apply']);assert.equal(result.status,0,result.stderr);
 assert.ok(fs.existsSync(sealFile(f)));assert.match(result.stdout,/no answer recorded/);assert.ok(!readJournal(f.journal).some(r=>r.type==='SIGN_OFF'));
});

test('reconcile is explicit journal-only without CrossCast and detects missing captures',t=>{
 const f=ready(t);step(f);assert.equal(reconcile(f.ctx).captures_missing,0);fs.unlinkSync(path.join(f.home,'captures','fixture.jsonl'));
 const r=reconcile(f.ctx);assert.equal(r.captures_missing,1);assert.equal(r.crosscast_present,false);assert.equal(r.time_alignment,'journal_only');assert.equal(r.ungated_writes,null);
});
test('exact CrossCast writes match sessions; plain verify is write-class',t=>{
 const f=ready(t);f.ctx.run=begin(f.ctx,{}).run;const at=readJournal(f.journal).at(-1).at;step(f,{},f.ctx.run);end(f.ctx);
 const file=audit(f,[run({t_start:at}),run({run:'b'.repeat(32),command:'verify',t_start:'2020-01-01T00:00:00.000Z'}),run({run:'c'.repeat(32),command:'delete',flags:{verify_only:true},t_start:'2020-01-01T00:00:00.000Z'})]);
 const r=reconcile(f.ctx,file);assert.equal(r.ungated_writes,1);assert.equal(r.admitted_sessions_without_write,0);
});
test('relative times and damaged chains cannot imply ungated zero',t=>{
 const f=ready(t);for(const changes of [{times:'relative'},{integrity:{chain_ok:false}}]){
  const r=reconcile(f.ctx,audit(f,[run()],changes));assert.equal(r.ungated_writes,null);assert.equal(r.commands_without_time_coverage,1);
 }
});
test('open sessions and missing command times stay unmatched',t=>{
 const f=ready(t);f.ctx.run=begin(f.ctx,{}).run;
 const r=reconcile(f.ctx,audit(f,[run(),run({run:'b'.repeat(32),t_start:null})]));assert.equal(r.ungated_writes,0);assert.equal(r.commands_without_time_coverage,2);assert.equal(r.admitted_sessions_without_write,1);
});
test('in-doubt resolves on matching person evidence, not unrelated or unclear looks',t=>{
 const f=ready(t);step(f);observe(f,{job:'another'});observe(f,{seen:'unclear'});observe(f,{about:'delete'});
 assert.equal(reconcile(f.ctx).in_doubt_open,1);observe(f);
 const r=reconcile(f.ctx);assert.equal(r.in_doubt_open,0);assert.equal(r.in_doubt[0].by,'person');assert.ok(r.in_doubt[0].seconds>=0);
});
test('delete uncertainty resolves on read-only verification without another write',t=>{
 const f=ready(t);step(f,{role:'delete',action:'delete'});step(f,{role:'status',class:'read',result:'ok'});assert.equal(reconcile(f.ctx).in_doubt_open,1);
 step(f,{role:'delete_verify',action:'delete',class:'read',result:'ok'});assert.equal(reconcile(f.ctx).in_doubt[0].by,'read');
});
test('a later write cannot be credited as settling the original uncertainty',t=>{
 const f=ready(t);step(f);step(f,{role:'repeat_same',result:'ok'});observe(f);assert.equal(reconcile(f.ctx).in_doubt_open,1);
});
test('looks are owed only for started publication/edit/delete in test or real jobs',t=>{
 const f=ready(t);const jobs=JSON.parse(fs.readFileSync(path.join(f.home,'jobs.json')));
 for(const c of ['drill','refusal'])jobs.push({id:c,run_class:c});fs.writeFileSync(path.join(f.home,'jobs.json'),JSON.stringify(jobs));
 step(f,{job:'drill',role:'publish',exit:2,result:'refused'});step(f,{job:'refusal',role:'publish',exit:2,result:'refused'});step(f,{role:'verify',exit:0,result:'ok'});
 assert.equal(reconcile(f.ctx).looks_owed.length,0);step(f,{exit:127,result:'failed'});assert.equal(reconcile(f.ctx).looks_owed.length,0);
 step(f);assert.equal(reconcile(f.ctx).looks_owed.length,1);observe(f,{minutes_after:2});assert.equal(reconcile(f.ctx).looks_owed.length,1);observe(f);assert.equal(reconcile(f.ctx).looks_owed.length,0);
});
test('refusals, violations and CrossCast losses remain distinct counts',t=>{
 const f=ready(t);step(f,{role:'repeat_same',exit:2,result:'refused',code:'confirmation_required'});put(f.ctx,'VIOLATION',{rule:'other',found_by:'self_report',job:null});
 const r=reconcile(f.ctx,audit(f,[run({dropped_events:7})]));assert.equal(r.refusals_by_code.confirmation_required,1);assert.equal(r.violations,1);assert.equal(r.crosscast_command_losses[0].dropped_events,7);
});

test('review commands write only explicitly isolated paths; private source bytes stay unchanged',t=>{
 const f=ready(t),files=['operator.json','jobs.json'];const saved=files.map(n=>fs.readFileSync(path.join(f.home,n)));const journal=fs.readFileSync(f.journal);
 daily(f.ctx);reconcile(f.ctx);seal(f.ctx);exportFile(f.ctx,path.join(f.root,'out.json'));
 files.forEach((n,i)=>assert.deepEqual(fs.readFileSync(path.join(f.home,n)),saved[i]));assert.deepEqual(fs.readFileSync(f.journal),journal);
});
test('old gate and workflow test still have their fixed normalized digests',()=>{
 for(const [file,hash] of [['scripts/operations-gate.mjs','6c18061504739ee3080f276d5be40c714339af07a2304306f652f8f3afb0dccf'],['tests/workflows.test.mjs','c031956fe63af28a3ff1310b288ba7067ac0396f07a0ce1223f96c68075883da']])assert.equal(sha(fs.readFileSync(file,'utf8').replace(/\r\n/g,'\n')),hash);
});
test('new review modules have only local or Node standard-library imports',()=>{
 for(const name of ['seal','export','daily','reconcile','crosscast']){
  const text=fs.readFileSync('scripts/round2/'+name+'.mjs','utf8');
  for(const [,target] of text.matchAll(/from ['"]([^'"]+)['"]/g))assert.ok(target.startsWith('./')||target.startsWith('node:'));
  assert.ok(!/node:(?:http|https|net|child_process)|fetch\(/.test(text));
 }
});
