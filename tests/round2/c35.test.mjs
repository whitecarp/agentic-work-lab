import test from 'node:test';
import assert from 'node:assert/strict';
import {fixture,setup,approve,cli,flags,last} from './helpers.mjs';
import {put,readJournal} from '../../scripts/round2/journal.mjs';
import {exportData} from '../../scripts/round2/export.mjs';
import {reconcile} from '../../scripts/round2/reconcile.mjs';
import {daily} from '../../scripts/round2/daily.mjs';
import {begin,step,end} from '../../scripts/round2/gate-run.mjs';

test('privacy removes every minute field and every clock time including outside uploads',t=>{
 const f=fixture(t);setup(f.ctx);put(f.ctx,'NOTE',{code:'outside_upload',platform:'youtube',job:null,agent:'owner',minutes_ago:17});
 const copy=exportData(f.ctx,{privacy:true});assert.match(copy.time_policy,/dates_only/);
 assert.equal(copy.records.at(-1).at.length,10);assert.equal('minutes_ago' in copy.records.at(-1).d,false);
 assert.doesNotMatch(JSON.stringify(copy),/\d{4}-\d\d-\d\dT\d\d:/);
 assert.equal(readJournal(f.journal).at(-1).d.minutes_ago,17);
});
test('reconcile CLI records a fixed judgment moment and rejects invalid ones',t=>{
 const f=fixture(t);setup(f.ctx);
 const a=cli(f,'reconcile',['--site','owner','--journal',f.journal,'--as-of','2026-11-30T12:00:00Z']);assert.equal(a.status,0,a.stderr);
 assert.equal(JSON.parse(a.stdout).as_of,'2026-11-30T12:00:00.000Z');
 assert.deepEqual(reconcile(f.ctx,undefined,{now:'2026-11-30T12:00:00Z'}),JSON.parse(a.stdout));
 for(const invalid of ['invalid','2026-11-30T12:00:00'])assert.notEqual(cli(f,'reconcile',['--site','owner','--journal',f.journal,'--as-of',invalid]).status,0);
});
test('closing sign-off is explicit, normal sign-off remains daily',t=>{
 const f=fixture(t);setup(f.ctx);
 for(const closing of [false,true]){
  const r=cli(f,'journal',['sign-off',...flags(f),'--day','2026-11-30','--answer','yes',...(closing?['--closing']:[])],'');
  assert.equal(r.status,0,r.stderr);assert.equal(last(f.ctx,'SIGN_OFF').d.closing,closing);
 }
});
test('repeat and resume require a started publish for this job even across sessions',async t=>{
 const f=fixture(t);setup(f.ctx);approve(f.ctx,'job-1',8);f.ctx.run=begin(f.ctx,{}).run;
 for(const role of ['repeat_same','changed_plan','resume']){
  assert.equal(await step(f.ctx,{job:'job-1',role},[process.execPath,'-e','process.exit(0)']),5);
  assert.equal(last(f.ctx,'STEP_REFUSED').d.reason,'repeat_without_publish');
 }
 assert.equal(await step(f.ctx,{job:'job-1',role:'publish'},[process.execPath,'-e','process.exit(4)']),4);
 end(f.ctx);f.ctx.run=begin(f.ctx,{}).run;
 assert.equal(await step(f.ctx,{job:'job-1',role:'resume'},[process.execPath,'-e','process.exit(0)']),0);
});
test('daily distinguishes selected UTC day figures from cumulative figures',t=>{
 const f=fixture(t);setup(f.ctx);const r=daily(f.ctx,{selectedDay:'2026-01-01',now:'2026-01-02T00:00:00Z'});
 assert.equal(r.scopes.steps,'selected_UTC_day');assert.equal(r.scopes.in_doubt,'all_records');
 assert.match(r.lines.join('\n'),/steps_by_result_today:/);assert.match(r.lines.join('\n'),/open_in_doubt_all_records:/);
});
