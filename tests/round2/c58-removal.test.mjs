import test from 'node:test';
import assert from 'node:assert/strict';
import {fixture} from '../fixtures/round2-analysis.mjs';
for (const [platform,minutes,deleteFirst,named] of [
 ['youtube',12,false,true], ['youtube',0.5,false,true], ['youtube',0.5,true,true],
 ['youtube',12,true,true], ['douyin',12,false,true], ['youtube',12,false,false],
]) test(`C58 removal ${platform}, ${minutes} minutes, prior delete ${deleteFirst}, named ${named}`,()=>{
 const f=fixture({platforms:[platform]});
 f.step({m:3});
 const del=deleteFirst&&f.step({m:10,role:'delete',state:'DELETE_PENDING',exit:4});
 const note=f.put('NOTE',{code:'manual_step',job:named?'job-a':null,platform},30);
 const obs=f.look({about:'delete',seen:'absent',m:30+minutes,minutes_after:minutes});
 const check=f.finish().reconcile.look_checks.find(c=>c.about==='delete');
 if(!named){assert.equal(check,undefined);return;}
 assert.equal(check.step,note.seq);
 assert.equal(check.last_tool_delete_step,del?del.seq:null);
 assert.deepEqual(check.counting_observations,minutes>=10?[obs.seq]:[]);
});
