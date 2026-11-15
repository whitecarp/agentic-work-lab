import test from 'node:test';
import assert from 'node:assert/strict';
import {EventEmitter} from 'node:events';
import {spawn} from 'node:child_process';
import {fixture,setup,last,ageJournal} from './helpers.mjs';
import {put} from '../../scripts/round2/journal.mjs';
import {faultController} from '../../scripts/round2/planned-faults.mjs';
import {processHandles} from '../../scripts/round2/process-handles.mjs';
import {exportData} from '../../scripts/round2/export.mjs';
import {validateRecord} from '../../scripts/round2/schema.mjs';

const fault={job:'job-1',kind:'end_command_after_press',role:'publish',delay_ms:0,armed_on:'youtube_private'};
for(const outcome of ['survives','ended','unopened','init_failed','scan_failed'])test(`C62 C1 observation ${outcome} leaves launcher-only fault unchanged`,async t=>{
 const f=fixture(t);setup(f.ctx);
 const child=Object.assign(new EventEmitter(),{pid:900,exitCode:null,signalCode:null});
 let requested=0,ended=0,closed=0,readonly=false;
 const observer={unopened:outcome==='unopened'?1:0,unopenedPresent:outcome==='unopened'?1:0,
  async init(){if(outcome==='init_failed')throw Error('private error');return [{pid:900,born:'100'}];},
  async scan(){if(outcome==='scan_failed')throw Error('private error');return [{pid:900,ended:child.signalCode!==null},{pid:901,ended:outcome==='ended'}];},
  async end(){ended++;throw Error('C1 must never end a descendant');},close(){closed++;}};
 const c=faultController(f.ctx,fault,{observerFactory:options=>{readonly=options.readOnly;return observer;},survivorDelayMs:1,
  kill:async(pid,tree,handle)=>{assert.equal(pid,900);assert.equal(tree,false);assert.equal(handle,child);requested++;child.signalCode='SIGKILL';child.emit('exit');return true;}});
 c.attach(child);child.emit('spawn');
 c.line('{"event":"browser_progress","platform":"youtube","stage":"waiting for platform confirmation"}');
 await c.finish('',3);
 const d=last(f.ctx,'FAULT_RESULT').d,s=d.survivor_check;
 assert.equal(d.fired,true);assert.equal(d.failed,false);assert.equal(d.reason,null);
 assert.equal(requested,1);assert.equal(ended,0);assert.equal(closed,1);assert.equal(readonly,true);
 assert.deepEqual(d.processes,[{pid:900,root:true,kind:'launcher',ended:true}]);
 assert.ok(s.after_gone_ms>=1);
 if(outcome.endsWith('failed')){assert.equal(s.reason,'process_observation_failed');assert.equal(s.alive,null);assert.equal(s.known_descendants,null);}
 else{assert.equal(s.reason,null);assert.equal(s.known_descendants,1);assert.equal(s.alive,outcome==='ended'?0:1);assert.equal(s.unopened_present,outcome==='unopened'?1:0);}
 const copy=exportData(f.ctx,{privacy:true});
 assert.deepEqual(copy.records.find(r=>r.type==='FAULT_RESULT').d.survivor_check,s);
 assert.ok(!JSON.stringify(copy).includes('private error'));
});

test('C62 privacy export keeps counts but withholds a cross-day observation delay',t=>{
 const f=fixture(t);setup(f.ctx);
 put(f.ctx,'FAULT',{...fault,mechanism:'wrapper_kill',trigger:'waiting_for_platform_confirmation'});
 ageJournal(f.ctx,1440);
 put(f.ctx,'FAULT_RESULT',{job:'job-1',kind:fault.kind,role:'publish',triggered:true,fired:true,failed:false,reason:null,
  processes:[],action_delay_ms:500,gone_delay_ms:517,kill_exit:0,
  survivor_check:{after_gone_ms:3010,known_descendants:2,alive:1,unopened_present:0,reason:null}});
 const exact=exportData(f.ctx),copy=exportData(f.ctx,{privacy:true});
 assert.equal(exact.records.at(-1).d.survivor_check.after_gone_ms,3010);
 assert.deepEqual(copy.records.at(-1).d.survivor_check,{after_gone_ms:null,known_descendants:2,alive:1,unopened_present:0,reason:null});
});

test('C62 failed observer cannot prevent or delay the requested C1 kill',async t=>{
 const f=fixture(t);setup(f.ctx);const child=Object.assign(new EventEmitter(),{pid:900,exitCode:null,signalCode:null});
 let release,requested=false;
 const observer={unopened:0,unopenedPresent:0,init:()=>new Promise((_,reject)=>{release=reject;}),close(){}};
 const c=faultController(f.ctx,fault,{observerFactory:()=>observer,survivorDelayMs:1,
  kill:async()=>{requested=true;child.signalCode='SIGKILL';child.emit('exit');release(Error('observer failed after kill'));return true;}});
 c.attach(child);child.emit('spawn');c.line('{"event":"browser_progress","platform":"youtube","stage":"waiting for platform confirmation"}');
 await c.finish('',3);assert.equal(requested,true);assert.equal(last(f.ctx,'FAULT_RESULT').d.fired,true);
});

test('C62 absent observations in historical fault records remain valid',t=>{
 const f=fixture(t);setup(f.ctx);const record={...last(f.ctx,'HANDOFF'),type:'FAULT_RESULT',d:{...fault,triggered:true,fired:true,failed:false,reason:null,processes:[],action_delay_ms:500,gone_delay_ms:517,kill_exit:0}};
 delete record.d.delay_ms;delete record.d.armed_on;
 assert.deepEqual(validateRecord(record),[]);
 record.d.survivor_check={after_gone_ms:3000,known_descendants:1,alive:2,unopened_present:0,reason:null};
 assert.ok(validateRecord(record).includes('invalid survivor observation'));
 record.d.survivor_check={after_gone_ms:3000,known_descendants:null,alive:0,unopened_present:null,reason:'process_observation_failed'};
 assert.ok(validateRecord(record).includes('invalid survivor observation'));
});

test('C62 native read-only observer rejects termination and lets own process expire',{skip:process.platform!=='win32',timeout:15000},async()=>{
 const created=Date.now(),child=spawn(process.execPath,['-e','setTimeout(()=>{},5500)'],{stdio:'ignore',windowsHide:true});
 const done=new Promise((resolve,reject)=>{child.once('exit',resolve);child.once('error',reject);});
 await new Promise(resolve=>child.once('spawn',resolve));
 const observer=processHandles({readOnly:true});
 try{
  const initial=await observer.init(child.pid,created);assert.equal(initial.find(r=>r.pid===child.pid).ended,false);
  await assert.rejects(observer.end(),/observation_only/);
  assert.equal((await observer.scan()).find(r=>r.pid===child.pid).ended,false);
  await done;assert.equal((await observer.scan()).find(r=>r.pid===child.pid).ended,true);
 }finally{observer.close();await done;}
});
