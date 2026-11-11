import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import {createHmac} from 'node:crypto';
import {protectDigests,exportData} from '../../scripts/round2/export.mjs';
import {fixture,setup,approve} from './helpers.mjs';
import {put,readJournal,canonical} from '../../scripts/round2/journal.mjs';
import {begin} from '../../scripts/round2/gate-run.mjs';
import {privateToolCopy} from '../../scripts/round2/private-tool-copy.mjs';
import {sha} from '../../scripts/round2/io.mjs';
const commit=s=>createHmac('sha256','synthetic-private-key').update(s).digest('hex');
test('C37 C1 every non-public digest is keyed, including capture and future nested fields',()=>{
 const privateDigest=sha('timestamped capture'),publicDigest=sha('public source');
 const input={records:[{d:{capture_sha256:privateDigest,gate_sha256:publicDigest,nested:{gate_sha256:privateDigest},future:privateDigest}}],crosscast:{session:privateDigest}};
 const out=protectDigests(input,commit);
 assert.equal(out.records[0].d.gate_sha256,publicDigest);
 assert.equal(out.records[0].d.capture_sha256,commit(privateDigest));
 assert.ok(!JSON.stringify(out).includes(privateDigest));assert.equal(input.records[0].d.capture_sha256,privateDigest);
});
test('C37 C1 actual privacy export removes the candidate capture digest',t=>{
 const f=fixture(t);setup(f.ctx);
 const digest=sha(JSON.stringify({at:'2026-11-11T12:00:00.123Z',stream:'stdout',bytes:Buffer.from('fixed public answer\n').toString('base64')})+'\n');
 put(f.ctx,'STEP',{job:'job-1',tool:'vpub',action:'publish',class:'write',role:'changed_plan',platforms:['youtube'],exit:4,ms:10,capture_sha256:digest,result:'refused',code:null,refs:[],fault_fired:null},'a'.repeat(32));
 assert.ok(!JSON.stringify(exportData(f.ctx,{privacy:true})).includes(digest));
 assert.equal(exportData(f.ctx).records.at(-1).d.capture_sha256,digest);
});

test('C37 C4 journal midnight with no tool events withholds STEP duration',t=>{
 const f=fixture(t);setup(f.ctx);approve(f.ctx);f.ctx.run=begin(f.ctx,{}).run;
 put(f.ctx,'STEP',{job:'job-1',tool:'other',action:'other',class:'write',role:'publish',platforms:['youtube'],exit:3,ms:2000,capture_sha256:'a'.repeat(64),result:'unknown',code:null,refs:[],fault_fired:null},f.ctx.run);
 const rows=readJournal(f.journal);let prev='0'.repeat(64);const lines=rows.map((r,i)=>{r.at=i===rows.length-1?'2026-11-11T00:00:01.000Z':'2026-11-10T23:59:59.000Z';r.prev=prev;const line=canonical(r);prev=sha(line);return line;});fs.writeFileSync(f.journal,lines.join('\n')+'\n');fs.writeFileSync(f.journal+'.head',JSON.stringify({records:rows.length,head_sha256:prev}));
 assert.equal(exportData(f.ctx,{privacy:true}).records.at(-1).d.ms,undefined);assert.equal(exportData(f.ctx).records.at(-1).d.ms,2000);
});
test('C37 C2/C5 record clocks and session are null and event numbers start at one',()=>{
 const time='2026-11-11T12:00:00.000Z',source={schema:'crosscast-audit/1',times:'exact',window:{},integrity:{first_seq:901,last_seq:902,first_bad_seq:null},
 runs:[{run:'a',session:'private-session',t_start:time,t_end:time}],events:[{run:'a',seq:901,type:'command_start',at:time,session:'private-session'},{run:'a',seq:902,type:'command_end',at:time}],
 publications:[{t_started:time,t_updated:time,t_verified:time,edits:[{t:time}],observations:[{t:time}]}],deletions:[{t_created:time,t_attempted:time,t_updated:time}]};
 const c=privateToolCopy(source,commit);assert.equal(c.runs[0].session,null);assert.equal(c.events[0].session,null);assert.deepEqual(c.events.map(e=>e.seq),[1,2]);assert.equal(c.integrity.first_seq,1);assert.equal(c.publications[0].t_started,null);assert.equal(c.publications[0].edits[0].t,null);assert.equal(c.deletions[0].t_attempted,null);assert.ok(!JSON.stringify(c).includes('private-session'));
});
