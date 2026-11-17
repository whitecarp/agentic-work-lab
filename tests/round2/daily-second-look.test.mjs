import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import {fixture,setup,putObserved as put} from './helpers.mjs';
import {daily} from '../../scripts/round2/daily.mjs';

function publish(f){
 return put(f.ctx,'STEP',{job:'job-1',class:'write',tool:'vpub',action:'publish',role:'publish',
  platforms:['youtube'],exit:4,ms:10,capture_sha256:'a'.repeat(64),result:'in_doubt',
  code:null,refs:[],fault_fired:false});
}
function look(f,minutes,seen='absent'){
 return put(f.ctx,'OBSERVATION',{job:'job-1',platform:'youtube',ref:null,about:'post',
  by:'owner',client:'phone_app',seen,fields_match:null,minutes_after:minutes,
  words_sha256:'b'.repeat(64),shot_sha256:null});
}
function summary(f,last){
 const before=fs.readFileSync(f.journal);
 const result=daily(f.ctx,{selectedDay:last.at.slice(0,10),now:last.at});
 assert.deepEqual(fs.readFileSync(f.journal),before);
 assert.equal(result.applied,false);
 return {result,counts:JSON.parse(result.lines[4].split('looks_all_records: ')[1]),
  today:JSON.parse(result.lines[7].slice('looks_today: '.length))};
}

for(const minutes of [1200,2160])test(`daily counts a qualifying second look at ${minutes} minutes`,t=>{
 const f=fixture(t);setup(f.ctx);publish(f);look(f,15);
 const last=look(f,minutes),{result,counts,today}=summary(f,last);
 assert.equal(result.checked.look_checks[0].status,'second_look');
 assert.equal(counts.second_look,1);
 assert.ok(Object.values(counts).every(Number.isInteger));
 assert.equal(today.at(-1).status,'second_look');
 assert.equal(today.at(-1).status,result.checked.observation_checks.at(-1).status);
 assert.equal(result.checked.looks_owed.length,0);
});

for(const [minutes,seen,status] of [[1199,'absent','late'],[2161,'absent','outside_window'],
 [1300,'unclear','unclear']])test(`daily preserves ${status} for a nonqualifying look`,t=>{
 const f=fixture(t);setup(f.ctx);publish(f);look(f,15);
 const last=look(f,minutes,seen),{counts,today}=summary(f,last);
 assert.equal(counts.second_look??0,0);
 assert.equal(today.at(-1).status,status);
});

test('daily does not call a late first observation a second look',t=>{
 const f=fixture(t);setup(f.ctx);publish(f);
 const last=look(f,1300),{counts,today}=summary(f,last);
 assert.equal(counts.second_look??0,0);
 assert.equal(counts.late,1);
 assert.equal(today.at(-1).status,'late');
});
