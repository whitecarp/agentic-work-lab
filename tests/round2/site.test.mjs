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
  return ['protocol-seal',...flags(f),'--phase','rehearsal','--protocol',file,'--rules-card',file,'--analysis',file,'--crosscast-digest','c'.repeat(64),
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
  assert.equal(fs.existsSync(marker),false);assert.deepEqual(JSON.parse(fs.readFileSync(path.join(f.home,'reservations.json'))),[]);
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

test('Bilibili shell override is removed unless a custom folder was sealed',async t=>{
  const prior=process.env.BILI_CLI_HOME;
  try{
    for(const custom of [false,true]){
      const f=fixture(t);
      const bili=path.join(f.root,'bili');fs.mkdirSync(bili);
      if(custom)setSite(f,{site:'owner',vpub_home:f.env.VPUB_HOME,bili_cli_home:bili});
      setup(f.ctx);approve(f.ctx);f.ctx.run=begin(f.ctx,{}).run;
      process.env.BILI_CLI_HOME=path.join(f.root,'wrong-shell-home');
      const out=path.join(f.root,'child-env.json');
      assert.equal(await step(f.ctx,{job:'job-1',role:'other'},[process.execPath,'-e',
        "require('fs').writeFileSync(process.argv[1],JSON.stringify(process.env.BILI_CLI_HOME??null))",out]),0);
      assert.equal(JSON.parse(fs.readFileSync(out)),custom?fs.realpathSync.native(bili):null);
      assert.equal(fs.existsSync(process.env.BILI_CLI_HOME),false);
    }
  }finally{if(prior===undefined)delete process.env.BILI_CLI_HOME;else process.env.BILI_CLI_HOME=prior;}
});

test('custom Bilibili folder is keyed and cannot be changed or removed after sealing',async t=>{
  const f=fixture(t),bili=path.join(f.root,'bili');fs.mkdirSync(bili);
  const defaultDigest=siteHome(f.ctx).digest;
  setSite(f,{site:'owner',vpub_home:f.env.VPUB_HOME,bili_cli_home:bili});
  setup(f.ctx);approve(f.ctx);f.ctx.run=begin(f.ctx,{}).run;
  assert.notEqual(siteHome(f.ctx).digest,defaultDigest);
  for(const change of [{site:'owner',vpub_home:f.env.VPUB_HOME},
    {site:'owner',vpub_home:f.env.VPUB_HOME,bili_cli_home:f.root}]){
    setSite(f,change);
    assert.equal(begin(f.ctx,{}).error,'vpub_home_mismatch');
    assert.equal(await step(f.ctx,{job:'job-1',role:'other'},[process.execPath,'-e','throw new Error("must not run")']),5);
    assert.equal(last(f.ctx,'STEP_REFUSED').d.reason,'vpub_home_mismatch');
    assert.equal(cli(f,'journal',sealArgs(f)).status,70);
  }
  assert.equal(readJournal(f.journal).filter(r=>r.type==='PROTOCOL_SEAL').length,1);
});

test('adding a custom Bilibili folder after the default binding is sealed is refused',async t=>{
  const f=fixture(t);setup(f.ctx);approve(f.ctx);f.ctx.run=begin(f.ctx,{}).run;
  setSite(f,{site:'owner',vpub_home:f.env.VPUB_HOME,bili_cli_home:f.root});
  assert.equal(begin(f.ctx,{}).error,'vpub_home_mismatch');
  assert.equal(await step(f.ctx,{job:'job-1',role:'other'},[process.execPath,'-e','throw new Error("must not run")']),5);
  assert.equal(last(f.ctx,'STEP_REFUSED').d.reason,'vpub_home_mismatch');
  assert.equal(cli(f,'journal',sealArgs(f)).status,70);
});

test('a sealed journal with a lost salt refuses without creating a replacement',async t=>{
  const f=fixture(t);setup(f.ctx);approve(f.ctx);f.ctx.run=begin(f.ctx,{}).run;
  const salt=path.join(f.home,'salt');fs.unlinkSync(salt);
  assert.equal(begin(f.ctx,{}).error,'vpub_home_invalid');
  assert.equal(await step(f.ctx,{job:'job-1',role:'other'},[process.execPath,'-e','throw new Error("must not run")']),5);
  const result=cli(f,'journal',sealArgs(f));
  assert.equal(result.status,70);assert.equal(result.stderr.trim(),'vpub_home_invalid');
  assert.equal(fs.existsSync(salt),false);
  assert.equal(readJournal(f.journal).filter(r=>r.type==='PROTOCOL_SEAL').length,1);
});

test('a mixed-case Bilibili shell override never reaches a child',t=>{
  const f=fixture(t);setup(f.ctx);approve(f.ctx);f.ctx.run=begin(f.ctx,{}).run;
  for(const key of Object.keys(f.env))if(key.toUpperCase()==='BILI_CLI_HOME')delete f.env[key];
  f.env.Bili_Cli_Home=path.join(f.root,'wrong-shell-home');
  const out=path.join(f.root,'child-env.json');
  const result=cli(f,'gate-run',['step',...flags(f),'--run',f.ctx.run,'--job','job-1','--role','other','--',
    process.execPath,'-e',"require('fs').writeFileSync(process.argv[1],JSON.stringify(Object.keys(process.env).filter(k=>k.toUpperCase()==='BILI_CLI_HOME')))",out]);
  assert.equal(result.status,0,result.stderr);
  assert.deepEqual(JSON.parse(fs.readFileSync(out)),[]);
});

test('custom Bilibili folder must be an existing absolute directory',t=>{
  const f=fixture(t),file=path.join(f.root,'not-directory');fs.writeFileSync(file,'fixture');
  for(const candidate of ['relative',file,42]){
    setSite(f,{site:'owner',vpub_home:f.env.VPUB_HOME,bili_cli_home:candidate});
    assert.throws(()=>siteHome(f.ctx),/vpub_home_invalid/);
  }
  setSite(f,{site:'owner',vpub_home:f.env.VPUB_HOME,bili_cli_home:path.join(f.root,'absent')});
  assert.throws(()=>siteHome(f.ctx),/vpub_home_missing/);
});
test('refused steps survive daily seal verification and export',async t=>{
  const f=fixture(t);setup(f.ctx);approve(f.ctx);f.ctx.run=begin(f.ctx,{}).run;
  setSite(f,{site:'owner',vpub_home:f.root});
  assert.equal(await step(f.ctx,{job:'job-1',role:'other'},[process.execPath,'-e','']),5);
  seal(f.ctx);assert.equal(verifySeals(f.ctx).seals[0].counts.STEP_REFUSED,1);
  assert.equal(exportData(f.ctx).counts.STEP_REFUSED,1);
});
