import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {spawn} from 'node:child_process';
import {pathToFileURL} from 'node:url';
import {appendRecord, canonical, verifyJournal, readJournal, validateRecord, pseudonym, newRunId, put} from '../../scripts/round2/journal.mjs';
import {sha} from '../../scripts/round2/io.mjs';
import {fixture, approve, cli, flags, last} from './helpers.mjs';
const note={code:'rehearsal',platform:null,job:null,agent:null,minutes_ago:null};

test('canonical order and exact previous line hash excluding LF',t=>{
  const f=fixture(t);put(f.ctx,'NOTE',note);put(f.ctx,'NOTE',note);
  const lines=fs.readFileSync(f.journal,'utf8').trimEnd().split('\n');
  assert.equal(JSON.parse(lines[1]).prev,sha(lines[0]));
  assert.equal(canonical({z:1,a:{b:2,a:3}}),'{"a":{"a":3,"b":2},"z":1}');
  assert.equal(verifyJournal(f.journal).records,2);
});
test('unknown fields and private free text are rejected',t=>{
  const f=fixture(t),record=approve(f.ctx);
  for(const patch of [{url:'https://private.invalid'}, {slot:'private-user'}, {d:{...record.d,words:'private sentence'}},
    {d:{...record.d,job:'C:/private file'}}]){
    assert.ok(validateRecord({...record,...patch}).length);
  }
  assert.throws(()=>appendRecord(f.journal,{site:'owner',slot:'gemini',type:'NOTE',run:null,d:{...note,extra:'private'}}));
  assert.equal(verifyJournal(f.journal).records,1);
});
for(const kind of ['edit','remove','reorder','truncate','remove-last']){
  test('detect '+kind+' tampering',t=>{
    const f=fixture(t);for(let i=0;i<3;i++)approve(f.ctx,'job-'+i);
    const lines=fs.readFileSync(f.journal,'utf8').trimEnd().split('\n');
    if(kind==='edit')lines[1]=lines[1].replace('job-1','job-z');
    if(kind==='remove')lines.splice(1,1);
    if(kind==='reorder')[lines[0],lines[1]]=[lines[1],lines[0]];
    if(kind==='remove-last')lines.pop();
    fs.writeFileSync(f.journal,lines.join('\n')+(kind==='truncate'?'':'\n'));
    assert.equal(verifyJournal(f.journal).ok,false);
    assert.throws(()=>approve(f.ctx));
  });
}
test('pseudonym stability and distinct salt without raw identifiers',t=>{
  const f=fixture(t), other=path.join(f.root,'second');fs.mkdirSync(other);
  const name='private account https://secret.invalid';
  const ref=pseudonym(name,f.home);
  assert.match(ref,/^[a-f0-9]{16}$/);assert.equal(ref,pseudonym(name,f.home));
  assert.notEqual(ref,pseudonym(name,other));assert.equal(fs.statSync(path.join(f.home,'salt')).size,32);
});
test('new run IDs have fixed privacy-safe form',()=>{const a=newRunId();assert.match(a,/^[a-f0-9]{32}$/);assert.notEqual(a,newRunId());});
test('append lock refuses simultaneous ownership and preserves bytes',t=>{
  const f=fixture(t);approve(f.ctx);const before=fs.readFileSync(f.journal);
  fs.writeFileSync(f.journal+'.lock','held');
  assert.throws(()=>approve(f.ctx));assert.deepEqual(fs.readFileSync(f.journal),before);
});
test('concurrent writers keep all records and one chain',async t=>{
  const f=fixture(t),url=pathToFileURL(path.resolve('scripts/round2/journal.mjs')).href;
  const js='import {appendRecord} from '+JSON.stringify(url)+';for(let i=0;i<5;i++)appendRecord('+JSON.stringify(f.journal)+','+
    JSON.stringify({site:'owner',slot:'gemini',type:'NOTE',run:null,d:note})+');';
  const results=await Promise.allSettled(Array.from({length:3},()=>new Promise((resolve,reject)=>{
    const c=spawn(process.execPath,['--input-type=module','-e',js],{env:f.env,stdio:['ignore','pipe','pipe'],windowsHide:true});
    let err='';c.stderr.on('data',b=>err+=b);c.on('error',reject);c.on('close',code=>code===0?resolve():reject(new Error(err)));
  })));
  for(const result of results) assert.equal(result.status,'fulfilled',result.reason?.message);
  assert.equal(verifyJournal(f.journal).ok,true);assert.equal(readJournal(f.journal).length,15);
});
test('own words stay private and empty go-ahead/observation refuses',t=>{
  const f=fixture(t),args=[...flags(f),'--job','job-1','--scope','one_write','--count','1'];
  assert.equal(cli(f,'journal',['go-ahead',...args],'').status,70);
  const text='Owner private sentence, path C:/private, token CANARY.';
  assert.equal(cli(f,'journal',['go-ahead',...args],text).status,0);
  assert.equal(fs.readFileSync(f.journal,'utf8').includes('CANARY'),false);
  const record=last(f.ctx,'GO_AHEAD');assert.equal(record.d.words_sha256,sha(text));
  assert.equal(cli(f,'journal',['observe',...flags(f),'--job','job-1','--about','post','--client','phone_app',
    '--seen','present_private','--minutes-after','1'],'').status,70);
});
test('handoff records before operator and refuses on damaged journal',t=>{
  const f=fixture(t);
  assert.equal(cli(f,'journal',['handoff',...flags(f),'--to','gemini'],'Allow Gemini').status,0);
  assert.equal(JSON.parse(fs.readFileSync(path.join(f.home,'operator.json'))).operator,'gemini');
  const before=fs.readFileSync(path.join(f.home,'operator.json'));
  fs.appendFileSync(f.journal,'broken');
  assert.equal(cli(f,'journal',['handoff',...flags(f),'--to','claude'],'Allow Claude').status,70);
  assert.deepEqual(fs.readFileSync(path.join(f.home,'operator.json')),before);
});
test('stop and clear-stop only mutate controls after journal commit',t=>{
  const f=fixture(t);
  assert.equal(cli(f,'journal',['stop',...flags(f)]).status,0);
  assert.ok(fs.existsSync(path.join(f.home,'STOP')));
  fs.writeFileSync(f.journal+'.lock','held');
  assert.equal(cli(f,'journal',['clear-stop',...flags(f)]).status,70);
  assert.ok(fs.existsSync(path.join(f.home,'STOP')));
  fs.unlinkSync(f.journal+'.lock');
  assert.equal(cli(f,'journal',['clear-stop',...flags(f)]).status,0);
  assert.equal(fs.existsSync(path.join(f.home,'STOP')),false);
});
test('protocol hashes text after LF normalization and includes v1.1 fields',t=>{
  const f=fixture(t),file=path.join(f.root,'protocol.txt');fs.writeFileSync(file,'hello\r\nworld\r\n');
  const r=cli(f,'journal',['protocol-seal',...flags(f),'--phase','rehearsal','--protocol',file,
    '--lab-commit','c'.repeat(40),'--crosscast-commit','d'.repeat(40)]);
  assert.equal(r.status,0,r.stderr);
  const d=last(f.ctx,'PROTOCOL_SEAL').d;assert.equal(d.protocol_sha256,sha('hello\nworld\n'));
  assert.equal(d.analysis_sha256,null);assert.equal(d.crosscast_digest,null);
});
test('new NOTE fields have conditional restrictions',t=>{
  const f=fixture(t),r=put(f.ctx,'NOTE',note);
  assert.ok(validateRecord({...r,d:{...note,code:'outside_upload'}}).length);
  assert.ok(validateRecord({...r,d:{...note,code:'model_changed',agent:'other'}}).length);
  assert.equal(validateRecord({...r,d:{...note,code:'outside_upload',platform:'youtube',agent:'gemini',minutes_ago:3}}).length,0);
});
test('refuses private home inside repository and never creates operator',t=>{
  const f=fixture(t);fs.mkdirSync(path.join(f.home,'.git'));
  const r=cli(f,'journal',['handoff',...flags(f),'--to','gemini'],'allowed');
  assert.equal(r.status,70);assert.equal(fs.existsSync(path.join(f.home,'operator.json')),false);
});
