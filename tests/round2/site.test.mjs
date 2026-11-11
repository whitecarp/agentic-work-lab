import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import {begin,step,end} from '../../scripts/round2/gate-run.mjs';
import {readJournal,keyedDigest} from '../../scripts/round2/journal.mjs';
import {siteHome} from '../../scripts/round2/site.mjs';
import {seal,verifySeals} from '../../scripts/round2/seal.mjs';
import {exportData} from '../../scripts/round2/export.mjs';
import {fixture,setup,approve,cli,flags,last} from './helpers.mjs';

const setSite=(f,value)=>fs.writeFileSync(path.join(f.home,'site.json'),JSON.stringify(value));
test('same resolved folder has a different keyed binding at each site',t=>{
  const first=fixture(t),second=fixture(t);
  setSite(second,{site:'owner',vpub_home:first.env.VPUB_HOME});
  const bound=siteHome(first.ctx),other=siteHome(second.ctx);
  assert.equal(bound.home,other.home);
  assert.match(bound.digest,/^[a-f0-9]{64}$/);
  assert.notEqual(bound.digest,other.digest);
  const normalized=process.platform==='win32'?bound.home.toLowerCase():bound.home;
  assert.equal(bound.digest,keyedDigest('round2-vpub-home-path-v1\0'+normalized,first.home));
  assert.equal(siteHome(first.ctx).digest,bound.digest);
});
function sealArgs(f){
  const file=path.join(f.root,'protocol.txt');fs.writeFileSync(file,'offline protocol');
  return ['protocol-seal',...flags(f),'--phase','rehearsal','--protocol',file,
    '--lab-commit','a'.repeat(40),'--crosscast-commit','b'.repeat(40)];
}
test('protocol seal contains resolved folder digest but no private path',t=>{
  const f=fixture(t),args=sealArgs(f);
  assert.equal(cli(f,'journal',args).status,0);
  assert.equal(last(f.ctx,'PROTOCOL_SEAL').d.vpub_home_sha256,siteHome(f.ctx).digest);
  const exported=JSON.stringify(exportData(f.ctx));
  assert.ok(!exported.includes(f.root));assert.ok(!exported.includes('round two offline'));
  assert.equal(fs.readdirSync(f.env.VPUB_HOME).length,0);
});
test('every child gets sealed folder despite a different shell variable',async t=>{
  const f=fixture(t);setup(f.ctx);approve(f.ctx,'job-1',2);f.ctx.run=begin(f.ctx,{}).run;
  const expected=fs.realpathSync.native(f.env.VPUB_HOME),prior=process.env.VPUB_HOME;
  try{
    process.env.VPUB_HOME=path.join(f.root,'wrong-shell-home');
    for(const role of ['publish','delete_verify']){
      const out=path.join(f.root,role+'.json');
      const command=[process.execPath,'-e',"require('fs').writeFileSync(process.argv[1],JSON.stringify(process.env.VPUB_HOME))",out];
      assert.equal(await step(f.ctx,{job:'job-1',role:'other'},command),0);
      assert.equal(JSON.parse(fs.readFileSync(out)),expected);
    }
    assert.equal(process.env.VPUB_HOME,path.join(f.root,'wrong-shell-home'));
    assert.equal(fs.existsSync(process.env.VPUB_HOME),false);
    assert.equal(fs.readdirSync(expected).length,0);
  }finally{if(prior===undefined)delete process.env.VPUB_HOME;else process.env.VPUB_HOME=prior;}
});
test('begin refuses missing setup, missing directory, relative path and wrong site',t=>{
  const f=fixture(t);setup(f.ctx);approve(f.ctx);
  fs.unlinkSync(path.join(f.home,'site.json'));
  assert.equal(begin(f.ctx,{}).error,'vpub_home_missing');
  for(const [value,expected] of [
    [{site:'owner',vpub_home:path.join(f.root,'absent')},'vpub_home_missing'],
    [{site:'owner',vpub_home:'relative'},'vpub_home_invalid'],
    [{site:'tester',vpub_home:f.env.VPUB_HOME},'vpub_home_invalid']]){
    setSite(f,value);assert.equal(begin(f.ctx,{}).error,expected);
    assert.equal(last(f.ctx,'GATE_ERROR').d.error,expected);
  }
  assert.equal(fs.existsSync(path.join(f.home,'run.lock')),false);
});
test('unsealed setup cannot begin a session',t=>{
  const f=fixture(t);assert.equal(begin(f.ctx,{}).error,'vpub_home_unsealed');
  assert.equal(fs.existsSync(path.join(f.home,'run.lock')),false);
});
test('changed setup refuses step, next begin and a new protocol seal',async t=>{
  const f=fixture(t);setup(f.ctx);approve(f.ctx);f.ctx.run=begin(f.ctx,{}).run;
  const marker=path.join(f.root,'never-ran'),args=sealArgs(f);
  setSite(f,{site:'owner',vpub_home:f.root});
  assert.equal(await step(f.ctx,{job:'job-1',role:'other'},
    [process.execPath,'-e',`require('fs').writeFileSync(${JSON.stringify(marker)},'bad')`]),5);
  assert.equal(last(f.ctx,'STEP_REFUSED').d.reason,'vpub_home_mismatch');
  assert.equal(fs.existsSync(marker),false);assert.equal(fs.existsSync(path.join(f.home,'reservations.json')),false);
  assert.equal(begin(f.ctx,{}).error,'vpub_home_mismatch');
  assert.equal(cli(f,'journal',args).status,70);
  assert.equal(readJournal(f.journal).filter(r=>r.type==='PROTOCOL_SEAL').length,1);
  // Recording can still close a session while account commands remain refused.
  assert.equal(end(f.ctx).exit,0);
});
test('same resolved folder spelling stays bound and may enter a later phase',t=>{
  const f=fixture(t);setup(f.ctx);approve(f.ctx);
  setSite(f,{site:'owner',vpub_home:path.join(f.env.VPUB_HOME,'..','vpub')});
  assert.equal(cli(f,'journal',sealArgs(f)).status,0);
  assert.equal(begin(f.ctx,{}).exit,0);
});
test('refused steps survive daily seal verification and export',async t=>{
  const f=fixture(t);setup(f.ctx);approve(f.ctx);f.ctx.run=begin(f.ctx,{}).run;
  setSite(f,{site:'owner',vpub_home:f.root});
  assert.equal(await step(f.ctx,{job:'job-1',role:'other'},[process.execPath,'-e','']),5);
  seal(f.ctx);assert.equal(verifySeals(f.ctx).seals[0].counts.STEP_REFUSED,1);
  assert.equal(exportData(f.ctx).counts.STEP_REFUSED,1);
});
