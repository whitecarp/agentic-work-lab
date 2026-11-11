import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {begin,step,end} from '../../scripts/round2/gate-run.mjs';
import {put,readJournal} from '../../scripts/round2/journal.mjs';
import {sha} from '../../scripts/round2/io.mjs';
import {fixture,setup,approve} from './helpers.mjs';

function ready(t) {
  const f=fixture(t);setup(f.ctx);approve(f.ctx,'job-1',5);
  f.ctx.run=begin(f.ctx,{}).run;return f;
}
const flags={job:'job-1',role:'publish',platform:['youtube']};
const command=f=>[process.execPath,'-e','require("node:fs").writeFileSync('+JSON.stringify(path.join(f.root,'sentinel'))+',"ran")'];
for(const rule of ['daily','spacing','outside','warning']){
  test(rule+' guard prevents real stand-in spawn',async t=>{
    const f=ready(t);
    if(rule==='daily'||rule==='spacing'){
      const count=rule==='daily'?4:1;
      const at=new Date(Date.now()-(rule==='daily'?3600000:60000)).toISOString();
      fs.writeFileSync(path.join(f.home,'reservations.json'),JSON.stringify(Array.from({length:count},(_,i)=>({
        job:'another-job-'+i,class:'write',role:'publish',platforms:['youtube'],at,started:true,run_class:'test_post'
      }))));
    }else put(f.ctx,'NOTE',{code:rule==='outside'?'outside_upload':'platform_warning',
      platform:'youtube',job:null,agent:rule==='outside'?'owner':null,minutes_ago:rule==='outside'?5:null});
    assert.equal(await step(f.ctx,flags,command(f)),5);
    assert.equal(fs.existsSync(path.join(f.root,'sentinel')),false);
    assert.equal(readJournal(f.journal).some(r=>r.type==='STEP'),false);
  });
}
test('a role label cannot turn a destructive vpub command into a read',async t=>{
  const f=ready(t);
  assert.equal(await step(f.ctx,{...flags,role:'verify'},['vpub','delete','a'.repeat(64)]),5);
  assert.equal(readJournal(f.journal).some(r=>r.type==='STEP'),false);
});
test('second concurrent step is refused while the first child is alive',async t=>{
  const f=ready(t);
  const first=step(f.ctx,{...flags,role:'other'},[process.execPath,'-e','setTimeout(()=>process.exit(0),200)']);
  assert.equal(await step(f.ctx,{...flags,role:'other'},command(f)),5);
  assert.equal(await first,0);
  assert.equal(fs.existsSync(path.join(f.root,'sentinel')),false);
  end(f.ctx);
});
test('cross-site records cannot enter another sites journal',t=>{
  const f=fixture(t);approve(f.ctx);
  assert.throws(()=>put({...f.ctx,site:'tester'},'RUN_END',{state:'skipped',steps:0}));
});
test('missing session lock does not erase the admitted session',t=>{
  const f=ready(t),old=f.ctx.run;
  fs.unlinkSync(path.join(f.home,'run.lock'));
  assert.equal(begin(f.ctx,{}).reason,'already_running');
  assert.equal(readJournal(f.journal).some(r=>r.run===old&&r.type==='RUN_END'),false);
});
test('old gate implementation and old test bytes remain unchanged',()=>{
  assert.equal(sha(fs.readFileSync('scripts/operations-gate.mjs','utf8').replace(/\r\n/g,'\n')),'6c18061504739ee3080f276d5be40c714339af07a2304306f652f8f3afb0dccf');
  assert.equal(sha(fs.readFileSync('tests/workflows.test.mjs','utf8').replace(/\r\n/g,'\n')),'c031956fe63af28a3ff1310b288ba7067ac0396f07a0ce1223f96c68075883da');
});
