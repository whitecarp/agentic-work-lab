import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import {atomic} from '../../scripts/round2/io.mjs';

function destination(t){
  const root=fs.mkdtempSync(path.join(os.tmpdir(),'round2-atomic-'));
  t.after(()=>fs.rmSync(root,{recursive:true,force:true}));
  const file=path.join(root,'head');fs.writeFileSync(file,'old');return {root,file};
}
test('temporary sharing refusals retry replacement without removing the destination',t=>{
  const {root,file}=destination(t),rename=fs.renameSync;let calls=0;
  t.mock.method(fs,'renameSync',(from,to)=>{
    calls++;assert.equal(fs.readFileSync(file,'utf8'),'old');
    if(calls<=3)throw Object.assign(new Error('synthetic sharing refusal'),{code:['EPERM','EACCES','EBUSY'][calls-1]});
    return rename(from,to);
  });
  atomic(file,'new');assert.equal(calls,4);
  assert.equal(fs.readFileSync(file,'utf8'),'new');assert.deepEqual(fs.readdirSync(root),['head']);
});
test('permanent replacement refusal remains bounded and preserves old bytes',t=>{
  const {root,file}=destination(t),denied=Object.assign(new Error('synthetic permanent denial'),{code:'EPERM'});let calls=0;
  t.mock.method(fs,'renameSync',()=>{calls++;throw denied;});
  assert.throws(()=>atomic(file,'new'),e=>e===denied);
  assert.ok(calls>1&&calls<=105);assert.equal(fs.readFileSync(file,'utf8'),'old');
  assert.deepEqual(fs.readdirSync(root),['head']);
});
test('non-sharing errors are not retried or converted into success',t=>{
  const {root,file}=destination(t),denied=Object.assign(new Error('synthetic missing path'),{code:'ENOENT'});let calls=0;
  t.mock.method(fs,'renameSync',()=>{calls++;throw denied;});
  assert.throws(()=>atomic(file,'new'),e=>e===denied);assert.equal(calls,1);
  assert.equal(fs.readFileSync(file,'utf8'),'old');assert.deepEqual(fs.readdirSync(root),['head']);
});
