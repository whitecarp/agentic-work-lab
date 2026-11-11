import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {privateToolCopy} from '../../scripts/round2/private-tool-copy.mjs';
import {selectFault} from '../../scripts/round2/planned-faults.mjs';
import {fixture,setup} from './helpers.mjs';
import {put,readJournal} from '../../scripts/round2/journal.mjs';
import {sha} from '../../scripts/round2/io.mjs';
test('cross-midnight commands and receipts keep separate origins and no event-day boundary',()=>{
 const times=['2026-11-10T23:59:59.000Z','2026-11-11T00:00:01.000Z','2026-11-11T08:00:00.000Z'];
 const source={schema:'crosscast-audit/1',times:'exact',window:{origin:times[0],days:['2026-11-10','2026-11-11']},
  runs:[{run:'a',t_start:times[0],t_end:times[1],stages:[[null,null,'pressing',times[1]]]},{run:'b',t_start:times[2],t_end:times[2]}],
  events:[{type:'command_start',run:'a',at:times[0],day:'2026-11-10'},{type:'stage',run:'a',at:times[1],day:'2026-11-11'},{type:'command_start',run:'b',at:times[2],day:'2026-11-11'}],
  publications:[{t_started:times[0],t_updated:times[1],edits:[{t:times[1]}]}],deletions:[{t_created:times[1],t_updated:times[2]}]};
 const c=privateToolCopy(source,sha);assert.deepEqual(c.events.map(e=>e.at),[null,null,0]);assert.ok(c.events.every(e=>!('day' in e)));assert.equal(c.runs[0].stages[0][3],null);
 assert.equal(c.publications[0].edits[0].t,null);assert.equal(c.deletions[0].t_created,null);assert.equal(c.runs[1].t_start,0);
 assert.ok(!JSON.stringify(c).includes('T23:'));assert.ok(!JSON.stringify(c).includes('T00:'));assert.equal(source.events[1].day,'2026-11-11');
});
test('counted-phase missing digest refuses a listed fault; rehearsal may use the practice list',t=>{
 const f=fixture(t);setup(f.ctx);const job={id:'job-1',run_class:'test_post',platforms:['youtube'],visibility:'private'};
 const data={runs:[{...job,site:'owner',faults:[{role:'publish',kind:'readback_timeout',trigger:'none',mechanism:'tool_switch',delay_ms:0,armed_on:'youtube_private'}]}]};
 fs.writeFileSync(path.join(f.home,'planned-runs.json'),JSON.stringify(data));const select=()=>selectFault(f.ctx,job,'publish',{tool:'vpub'});
 assert.ok(select());const seal=readJournal(f.journal).find(r=>r.type==='PROTOCOL_SEAL').d;
 put(f.ctx,'PROTOCOL_SEAL',{...seal,phase:'2a',plan_sha256:null});assert.ok(select());
 put(f.ctx,'PROTOCOL_SEAL',{...seal,phase:'2b',plan_sha256:null});assert.throws(select,/fault_plan_invalid/);
});
