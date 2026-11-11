import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {put,readJournal,verifyJournal,canonical,commitWords} from '../../scripts/round2/journal.mjs';
import {sha} from '../../scripts/round2/io.mjs';
import {begin,step,end,closeJob} from '../../scripts/round2/gate-run.mjs';
import {reconcile} from '../../scripts/round2/reconcile.mjs';
import {exportData} from '../../scripts/round2/export.mjs';
import {seal} from '../../scripts/round2/seal.mjs';
import {classify} from '../../scripts/round2/policy.mjs';
import {assertFaultReady,recordSetup} from '../../scripts/round2/fault-setup.mjs';
import {fixture,setup,approve,cli,flags} from './helpers.mjs';

const write=(f,name,value)=>fs.writeFileSync(path.join(f.home,name),JSON.stringify(value));
function ready(t,platforms=['youtube']){
 const f=fixture(t);setup(f.ctx,[{id:'job-1',class:'write',run_class:'test_post',due_at:'2020-01-01',platforms}]);
 approve(f.ctx,'job-1',20);f.ctx.run=begin(f.ctx,{}).run;return f;
}
function recorded(f,d={}){return put(f.ctx,'STEP',{job:'job-1',class:'write',tool:'vpub',action:'publish',role:'publish',
 platforms:['youtube'],exit:0,ms:1,capture_sha256:'a'.repeat(64),result:'ok',code:null,refs:[],fault_fired:null,...d},f.ctx.run);}
function look(f,d={}){return put(f.ctx,'OBSERVATION',{job:'job-1',platform:'youtube',ref:null,about:'post',by:'owner',client:'phone_app',
 seen:'present_private',fields_match:null,minutes_after:30,words_sha256:'b'.repeat(64),shot_sha256:null,...d});}
function redate(f,seq,at){
 const rows=readJournal(f.journal);rows.find(r=>r.seq===seq).at=at;let prev='0'.repeat(64);
 const lines=rows.map(r=>{r.prev=prev;const line=canonical(r);prev=sha(line);return line;});
 fs.writeFileSync(f.journal,lines.join('\n')+'\n');writeHead(f,rows.length,prev);
}
function writeHead(f,records,head_sha256){fs.writeFileSync(f.journal+'.head',JSON.stringify({records,head_sha256}));}

test('release verify at 10m, 1h and 24h never invalidates the 30m look',t=>{
 const f=ready(t);write(f,'jobs.json',[{id:'job-1',run_class:'real_slot',platforms:['youtube']}]);
 const base=Date.parse('2026-01-01T00:00:00.000Z'),s=recorded(f);redate(f,s.seq,new Date(base).toISOString());
 for(const minute of [10,30,60,1440]){
  const r=minute===30?look(f):recorded(f,{role:'verify',action:'verify',confirmed_state:'SUBMISSION_VERIFIED'});
  redate(f,r.seq,new Date(base+minute*60000).toISOString());
 }
 const r=reconcile(f.ctx,undefined,{now:base+1500*60000});
 assert.equal(r.look_checks[0].status,'on_time');assert.equal(r.look_checks[0].step,s.seq);
 assert.deepEqual(r.look_checks[0].recorded_before_claimed_minute,[]);
});
test('post, edit and delete looks survive their later read-backs, but a resume moves the post point',t=>{
 const f=ready(t);recorded(f);look(f);recorded(f,{role:'verify',action:'verify',confirmed_state:'SUBMISSION_VERIFIED'});
 recorded(f,{role:'edit',action:'edit'});look(f,{about:'edit'});recorded(f,{role:'edit',action:'edit',class:'read'});
 recorded(f,{role:'delete',action:'delete'});look(f,{about:'delete',seen:'absent'});recorded(f,{role:'delete_verify',action:'delete',class:'read'});
 assert.equal(reconcile(f.ctx).looks_owed.length,0);
 recorded(f,{role:'resume',action:'resume'});assert.deepEqual(reconcile(f.ctx).looks_owed.map(r=>r.about),['post']);
});
test('premature recording is flagged while late transcription is allowed and statement retained',t=>{
 const f=ready(t),s=recorded(f),l=look(f);
 assert.deepEqual(reconcile(f.ctx).look_checks[0].recorded_before_claimed_minute,[l.seq]);
 redate(f,s.seq,new Date(Date.parse(l.at)-3600000).toISOString());
 assert.deepEqual(reconcile(f.ctx).look_checks[0].recorded_before_claimed_minute,[]);
 assert.equal(readJournal(f.journal).find(r=>r.seq===l.seq).d.minutes_after,30);
});
for(const exit of [0,4])test(`complete no-press repeat exit ${exit} permits verify closure; strict view and dedup preserved`,t=>{
 const f=ready(t),id='c'.repeat(32),rid='d'.repeat(32),first=recorded(f,{result:'in_doubt',exit:4});
 const repeat=recorded(f,{role:'repeat_same',exit,result:exit===4?'in_doubt':'ok',awl_run_id:id});
 recorded(f,{role:'verify',action:'verify',confirmed_state:'SUBMISSION_VERIFIED'});
 const file=path.join(f.root,'cc.json'),snap={final_presses:1,delete_attempted:false};
 const cc={schema:'crosscast-audit/1',times:'relative',integrity:{chain_ok:true,unreadable_loss_records:0,loss_records_complete:true,loss_folder_unreadable:false},
  runs:[{run:rid,awl_run_id:id,command:'publish',flags:{},exit_code:exit,ended:true,dropped_events:0,sequence_gap:false,chain_ok:true}],
  events:[{run:rid,type:'command_start',before:snap},{run:rid,type:'command_end',after:snap}]};
 const check=()=>{fs.writeFileSync(file,JSON.stringify(cc));return reconcile(f.ctx,file);};
 let r=check();assert.equal(r.in_doubt.length,1);assert.equal(r.in_doubt[0].step,first.seq);
 assert.equal(r.in_doubt_open,0);assert.equal(r.in_doubt_open_strict,1);assert.deepEqual(r.in_doubt[0].ignored_no_press_steps,[repeat.seq]);
 assert.equal(reconcile(f.ctx).in_doubt[0].last_may_press,repeat.seq);
 for(const change of ['missing','writes','gap','damage','exit']){
  const before=structuredClone(cc);
  if(change==='missing')delete cc.events[0].before;
  if(change==='writes')cc.events.push({run:rid,operation:'press'});
  if(change==='gap')cc.runs[0].sequence_gap=true;
  if(change==='damage')cc.integrity.loss_folder_unreadable=true;
  if(change==='exit')cc.runs[0].exit_code=99;
  assert.equal(check().in_doubt[0].last_may_press,repeat.seq,change);Object.assign(cc,before);
 }
});
test('successful verify closes the matching platform only; unchanged strict count without repeat',t=>{
 const f=ready(t,['youtube','douyin']);recorded(f,{platforms:['youtube','douyin'],exit:4,result:'in_doubt'});
 recorded(f,{role:'verify',action:'verify',confirmed_state:'SUBMISSION_VERIFIED'});let r=reconcile(f.ctx);assert.equal(r.in_doubt_open,1);
 assert.equal(r.in_doubt.find(x=>x.platform==='youtube').closed,true);
 assert.equal(r.in_doubt.find(x=>x.platform==='douyin').closed,false);
});
for(const role of ['resume','verify','edit','delete','delete_dry_run','delete_verify'])test(`keyed ${role} needs one declared platform before spawning`,async t=>{
 const f=ready(t,['youtube','douyin']),out=path.join(f.root,'spawned');
 const cmd=[process.execPath,'-e',"require('fs').writeFileSync(process.argv[1],'once')",out];
 if(role==='resume')assert.equal(await step(f.ctx,{job:'job-1',role:'publish'},[process.execPath,'-e','process.exit(0)']),0);
 assert.equal(await step(f.ctx,{job:'job-1',role},cmd),5);assert.ok(!fs.existsSync(out));
 assert.equal(await step(f.ctx,{job:'job-1',role,platform:['x']},cmd),5);assert.ok(!fs.existsSync(out));
 assert.equal(await step(f.ctx,{job:'job-1',role,platform:['youtube']},cmd),0);
 assert.deepEqual(readJournal(f.journal).filter(r=>r.type==='STEP').at(-1).d.platforms,['youtube']);
});
test('two-platform job closure requires both read-backs; absence after delete is not nothing-posted',t=>{
 const f=ready(t,['youtube','douyin']);recorded(f,{role:'delete',action:'delete'});
 look(f,{about:'delete',seen:'absent'});assert.throws(()=>closeJob(f.ctx,{job:'job-1',reason:'nothing_posted'}),/evidence_missing/);
 recorded(f,{class:'read',role:'delete_verify',action:'delete'});
 assert.throws(()=>closeJob(f.ctx,{job:'job-1',reason:'done'}),/evidence_missing/);
 recorded(f,{class:'read',role:'delete_verify',action:'delete',platforms:['douyin']});
 assert.equal(closeJob(f.ctx,{job:'job-1',reason:'done'}).exit,0);
});
test('privacy copy keys all chain links; a 60000-time search cannot recover original digests',t=>{
 const f=ready(t);recorded(f);look(f);const head=verifyJournal(f.journal);seal(f.ctx);
 put(f.ctx,'SIGN_OFF',{day:new Date().toISOString().slice(0,10),by:'owner',answer:'yes',words_sha256:null,head_sha256:head.head_sha256,records:head.records});
 seal(f.ctx,new Date(Date.now()+86400000).toISOString().slice(0,10));
 const raw=readJournal(f.journal),original=fs.readFileSync(f.journal),value=exportData(f.ctx,{privacy:true});
 for(let i=1;i<value.records.length;i++)assert.equal(value.records[i].prev,value.records[i-1].source_sha256);
 assert.equal(value.source.head_sha256,value.records.at(-1).source_sha256);
 assert.equal(value.seals[0].head_sha256,value.records[head.records-1].source_sha256);
 assert.equal(value.records.at(-1).d.head_sha256,value.seals[0].head_sha256);
 assert.equal(value.seals[1].previous_sha256,value.seals[0].source_sha256);
 assert.equal(value.source.seal_head_sha256,value.seals[1].source_sha256);
 const guess=structuredClone(raw[0]),start=Date.parse(guess.at.slice(0,16)+':00.000Z');let matched=false;
 for(let ms=0;ms<60000;ms++){guess.at=new Date(start+ms).toISOString();if(sha(canonical(guess))===value.records[1].prev)matched=true;}
 assert.equal(matched,false);assert.ok(!JSON.stringify(value).includes(head.head_sha256));assert.deepEqual(fs.readFileSync(f.journal),original);
 assert.deepEqual(exportData(f.ctx,{privacy:true}),value);
});
for(const site of ['owner','tester'])test(`${site} words have random private openings and cannot be dictionary-hashed`,t=>{
 const f=fixture(t),bytes=Buffer.from('yes');
 const args=['go-ahead','--site',site,'--journal',f.journal,'--by',site,'--job','one','--scope','one_write','--count','1'];
 for(let i=0;i<2;i++)assert.equal(cli(f,'journal',args,bytes).status,0);
 const rows=readJournal(f.journal);assert.ok(rows.every(r=>r.site===site));const [a,b]=rows.map(r=>r.d.words_sha256);
 assert.notEqual(a,b);assert.notEqual(a,sha(bytes));
 for(const h of [a,b]){const nonce=fs.readFileSync(path.join(f.home,'captures',h+'.words.nonce'));assert.equal(nonce.length,32);assert.equal(h,sha(Buffer.concat([nonce,bytes])));}
});
test('privacy export cannot silently replace a missing bound salt',t=>{
 const f=ready(t);fs.unlinkSync(path.join(f.home,'salt'));
 assert.throws(()=>exportData(f.ctx,{privacy:true}),/invalid_salt/);
 assert.equal(fs.existsSync(path.join(f.home,'salt')),false);
});
test('supplemental real release is recorded separately, needs its own grant and owes each platform a look',t=>{
 const f=fixture(t);setup(f.ctx,[]);const original=fs.readFileSync(path.join(f.home,'jobs.json'));
 const args=['real-release',...flags(f),'--job','release-1','--platform','youtube','--platform','x','--due-at','2020-01-01','--by','owner'];
 assert.equal(cli(f,'journal',args,'Owner approved this release').status,0);
 assert.equal(cli(f,'journal',args,'Duplicate').status,70);
 assert.deepEqual(fs.readFileSync(path.join(f.home,'jobs.json')),original);
 assert.equal(readJournal(f.journal).at(-1).d.release_writes,7);
 assert.equal(begin(f.ctx,{}).exit,3);approve(f.ctx,'release-1',7);f.ctx.run=begin(f.ctx,{}).run;
 recorded(f,{job:'release-1',platforms:['youtube','x']});
 assert.deepEqual(reconcile(f.ctx).looks_owed.map(x=>x.platform),['youtube','x']);
 assert.equal(cli(f,'journal',['observe',...flags(f),'--job','release-1','--about','post','--client','phone_app','--seen','present_once','--minutes-after','30'],'words').status,70);
 const copy=exportData(f.ctx,{privacy:true});
 assert.equal(copy.records.find(r=>r.type==='REAL_RELEASE').d.due_at,'2020-01-01');
});
test('manual removal evidence must cover both platforms before a two-platform close',t=>{
 const f=ready(t,['youtube','douyin']);
 const note={code:'manual_step',platform:'youtube',job:'job-1',agent:null,minutes_ago:null};
 put(f.ctx,'NOTE',note);assert.throws(()=>closeJob(f.ctx,{job:'job-1',reason:'removed_by_hand'}),/evidence_missing/);
 put(f.ctx,'NOTE',{...note,platform:'douyin'});assert.equal(closeJob(f.ctx,{job:'job-1',reason:'removed_by_hand'}).exit,0);
});
test('only audit export is local; future audit commands remain fail-closed',()=>{
 assert.equal(classify(['vpub','audit','export'],{}).class,'local');
 assert.equal(classify(['vpub','audit','something-else'],{}).class,'write');
 assert.equal(classify(['vpub','verify','KEY'],{}).class,'write');
});
test('unarmed child strips all inherited fault variables without changing parent',async t=>{
 const f=ready(t),name='Vpub_Fault',old=process.env[name],out=path.join(f.root,'env.json');process.env[name]='after_press';
 try{assert.equal(await step(f.ctx,{job:'job-1',role:'other'},[process.execPath,'-e',"require('fs').writeFileSync(process.argv[1],JSON.stringify(Object.keys(process.env).filter(k=>k.toUpperCase()==='VPUB_FAULT')))",out]),0);
  assert.deepEqual(JSON.parse(fs.readFileSync(out)),[]);assert.equal(process.env[name],'after_press');
 }finally{if(old===undefined)delete process.env[name];else process.env[name]=old;}
});
test('fault setup only observes config; arming requires a record and current matching registration',t=>{
 const f=ready(t),file=path.join(f.root,'vpub','config.json'),config={fault_injection:true,external_stop_file:path.join(f.home,'STOP')};
 fs.writeFileSync(file,JSON.stringify(config));const original=fs.readFileSync(file);
 assert.throws(()=>assertFaultReady(f.ctx,'stop_during_upload'),/unconfirmed/);
 recordSetup(f.ctx,{enabled:true,by:'owner'},()=>commitWords(f.home,Buffer.from('Owner enabled planned faults')));
 assert.equal(assertFaultReady(f.ctx,'stop_during_upload').registered,true);assert.deepEqual(fs.readFileSync(file),original);
 config.external_stop_file=path.join(f.root,'wrong');fs.writeFileSync(file,JSON.stringify(config));
 assert.throws(()=>assertFaultReady(f.ctx,'stop_during_upload'),/unconfirmed/);
 config.fault_injection=false;delete config.external_stop_file;fs.writeFileSync(file,JSON.stringify(config));
 recordSetup(f.ctx,{enabled:false,by:'owner'},()=>commitWords(f.home,Buffer.from('Owner disabled faults')));
 assert.throws(()=>assertFaultReady(f.ctx,'readback_timeout'),/unconfirmed/);
 assert.ok(!fs.readFileSync(f.journal,'utf8').includes(f.root));
});
