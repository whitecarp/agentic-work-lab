import fs from 'node:fs';
import path from 'node:path';
import {createHash,randomBytes} from 'node:crypto';
import {pathToFileURL} from 'node:url';
import {canonical,context,readJournal} from './journal.mjs';
import {sha,privateHome,parseFlags,allowed,withLock} from './io.mjs';
import {FAULTS} from './planned-faults.mjs';

function random(seed){let counter=0;return max=>{if(!Number.isSafeInteger(max)||max<1)throw new Error('bad_draw');
 const limit=Math.floor(0x100000000/max)*max;let n;
 do{n=createHash('sha256').update(seed).update(String(counter++)).digest().readUInt32BE(0);}while(n>=limit);return n%max;};}
function shuffled(rows,draw){const a=[...rows];for(let i=a.length-1;i>0;i--){const j=draw(i+1);[a[i],a[j]]=[a[j],a[i]];}return a;}
const count=n=>{if(!Number.isSafeInteger(n)||n<0)throw new Error('invalid_counts');return n;};
export function generate(counts,site,seed,{dueFrom,firstShort,firstLong=1}={}){
 if(!['owner','tester'].includes(site)||!Buffer.isBuffer(seed)||seed.length!==32||!Number.isFinite(Date.parse(dueFrom)))throw new Error('invalid_plan_input');
 const spec=counts.sites?.[site],draw=random(seed),runs=[];let serial=0;
 if(!spec)throw new Error('invalid_counts');
 const add=d=>{const r={id:`${site}-${String(++serial).padStart(3,'0')}`,site,run_class:'test_post',group:d.group,
  platforms:[],visibility:'private',clip:'short',edit:false,wrong_delete_code_first:false,pair:null,pair_mode:null,faults:[],...d};runs.push(r);return r;};
 function fault(r,kind){const a=FAULTS[kind],b=counts.fault_kinds[kind];
  if(!a||!b||a.mechanism!==b.mechanism||a.role!==b.role||a.trigger!==b.trigger.replaceAll(' ','_')||
    r.platforms.length!==1||!['youtube_private','youtube_unlisted','douyin_private'].includes(`${r.platforms[0]}_${r.visibility}`))throw new Error('invalid_fault_counts');
  return {kind,mechanism:a.mechanism,role:a.role,trigger:a.trigger,delay_ms:[0,500,2000][draw(3)],armed_on:`${r.platforms[0]}_${r.visibility}`};
 }
 for(const group of spec.test_posts){
  if(!Array.isArray(group.platforms)||!group.platforms.length||group.platforms.some(p=>!['youtube','douyin'].includes(p))||new Set(group.platforms).size!==group.platforms.length)throw new Error('invalid_counts');
  const members=[];for(let i=0;i<count(group.runs);i++){
   const r=add({group:group.group,platforms:group.platforms,visibility:group.visibility,clip:group.clip});if(group.fault)r.faults.push(fault(r,group.fault));members.push(r);
  }
  if(group.pairs){if(count(group.pairs)*2!==members.length)throw new Error('invalid_pairs');
   for(let i=0;i<members.length;i+=2){const pair=`pair-${members[i].id}`,modes=shuffled(['one_at_a_time','two_at_a_time'],draw);
    members[i].pair=members[i+1].pair=pair;members[i].pair_mode=modes[0];members[i+1].pair_mode=modes[1];}
  }
 }
 const variant=spec.variants_per_platform;
 for(const platform of ['youtube','douyin']){
  function eligible(v){return shuffled(runs.filter(r=>r.platforms.length===1&&r.platforms[0]===platform&&r.group===v.drawn_from.group&&
   v.drawn_from.faults.includes(r.faults.find(f=>f.role==='publish')?.kind??null)),draw);}
  const pool=eligible(variant),edits=pool.slice(0,count(variant.edit));if(edits.length!==variant.edit)throw new Error('variant_pool_exhausted');
  for(const r of edits)r.edit=true;
  const editFaults=count(variant.edit_with_fault_end_browser_after_edit_save);
  if(editFaults>edits.length)throw new Error('variant_pool_exhausted');
  for(const r of shuffled(edits,draw).slice(0,editFaults))r.faults.push(fault(r,'end_browser_after_edit_save'));
  const deletion=shuffled(pool,draw),n=count(variant.delete_with_fault_delete_readback_lost),wrong=count(variant.wrong_delete_code_first);
  if(n+wrong>pool.length)throw new Error('variant_pool_exhausted');
  for(const r of deletion.slice(0,n))r.faults.push(fault(r,'delete_readback_lost'));
  for(const r of deletion.slice(n,n+wrong))r.wrong_delete_code_first=true;
  if(variant.extra){const p=eligible(variant.extra),n=count(variant.extra.delete_with_fault_delete_readback_lost);
   if(n>p.length)throw new Error('variant_pool_exhausted');for(const r of p.slice(0,n))r.faults.push(fault(r,'delete_readback_lost'));}
 }
 for(const group of spec.refusals){
  if(Object.values(group.plans).reduce((a,b)=>a+count(b),0)!==group.runs)throw new Error('refusal_totals');
  for(const [platform,n] of Object.entries(group.plans)){if(!['youtube','douyin'].includes(platform))throw new Error('invalid_counts');
   for(let i=0;i<n;i++)add({run_class:'refusal',group:group.group,kind:group.kind,platforms:[platform]});}
 }
 for(const group of spec.drills)for(let i=0;i<count(group.runs);i++)add({run_class:'drill',group:group.group,kind:group.kind,clip:null});
 for(const group of spec.real_release_slots??[]){const days=shuffled([1,2,3,4,5,6,7,8,9],draw);if(count(group.slots)>days.length)throw new Error('invalid_slots');
  for(const day of days.slice(0,group.slots))add({run_class:'real_slot',group:group.group,platforms:[group.platform],visibility:'public',clip:null,day});}
 const units=[],seen=new Set();for(const r of runs){if(seen.has(r.id))continue;const unit=r.pair?shuffled(runs.filter(x=>x.pair===r.pair),draw):[r];units.push(unit);unit.forEach(x=>seen.add(x.id));}
 let ordered=shuffled(units,draw).flat();
 const refs=ordered.filter(r=>r.run_class==='refusal');refs.forEach((r,i)=>r.day=1+i%9);
 // Put each handoff immediately before its next refusal on a distinct day.
 const drills=ordered.filter(r=>r.kind==='handoff');ordered=ordered.filter(r=>r.kind!=='handoff');
 const usedDays=new Set();for(const drill of drills){const ref=refs.find(r=>!usedDays.has(r.day));if(!ref)throw new Error('handoff_pool_exhausted');
  usedDays.add(ref.day);drill.uses=ref.id;drill.day=ref.day;ordered.splice(ordered.indexOf(ref),0,drill);}
 const uploads=counts.clip_pools[site==='owner'?'owner_uploads':'tester_uploads'];
 let short=firstShort??uploads.short[0],long=firstLong;
 if(!Number.isSafeInteger(short)||short<uploads.short[0]||!Number.isSafeInteger(long)||long<1)throw new Error('invalid_clip_start');
 for(const r of ordered.filter(r=>r.run_class==='test_post')){
  const pool=uploads[r.clip],n=r.clip==='short'?short++:long++;if(!pool||n<pool[0]||n>pool[1])throw new Error('clip_pool_exhausted');r.clip_no=n;
 }
 let refusalIndex=0;for(const r of ordered.filter(r=>r.run_class==='refusal')){
  if(site==='owner'){const [lo,hi]=counts.clip_pools.refusal_runs.short;r.clip_no=lo+(refusalIndex++%(hi-lo+1));}
  else {if(short>uploads.short[1])throw new Error('clip_pool_exhausted');r.clip_no=short++;}
 }
 // Source IDs follow the public counts file, where consecutive rows name a
 // specific fault. Give operators only the final shuffled order, including
 // in references to a paired run or the refusal used for a handoff.
 const jobIds=new Map(ordered.map((r,i)=>[r.id,`${site}-${String(i+1).padStart(3,'0')}`]));
 const pairIds=new Map();
 for(const [i,r] of ordered.entries()){
  r.order=i+1;
  r.id=jobIds.get(r.id);
  if(r.uses)r.uses=jobIds.get(r.uses);
  if(r.pair){
   if(!pairIds.has(r.pair))pairIds.set(r.pair,`pair-${r.id}`);
   r.pair=pairIds.get(r.pair);
  }
 }
 const tests=ordered.filter(r=>r.run_class==='test_post'),faults=tests.flatMap(r=>r.faults),totals={
  test_post_runs:tests.length,test_posts:tests.reduce((n,r)=>n+r.platforms.length,0),
  faults_around_the_press:faults.filter(f=>['end_command_after_press','end_tree_after_press','readback_timeout','between_save_and_press'].includes(f.kind)).length,
  faults_before_the_press:faults.filter(f=>['browser_lost_during_upload','stop_during_upload'].includes(f.kind)).length,
  refusal_runs:ordered.filter(r=>r.run_class==='refusal').length,drills:ordered.filter(r=>r.run_class==='drill').length,
  repeats_after_a_delete_press:faults.filter(f=>f.kind==='delete_readback_lost').length};
 if(site==='owner')Object.assign(totals,{bilibili_real_release_slots:ordered.filter(r=>r.run_class==='real_slot').length,of_which_handoff_drills:drills.length,gate_drills_of_the_six_kinds:totals.drills-drills.length});
 const expected=counts.totals_to_check[site];for(const [k,n]of Object.entries(totals))if(expected[k]!==n)throw new Error('plan_total_mismatch');
 for(const p of ['youtube','douyin'])if(tests.filter(r=>r.platforms.includes(p)).length!==expected.test_posts_per_platform)throw new Error('platform_total_mismatch');
 const approval=counts.write_steps_a_go_ahead_covers;
 if(approval.test_post_from_the_days_list!==5||approval.test_post_on_two_platforms_from_the_days_list!==8||approval.real_release!==4||approval.delete!==2)throw new Error('approval_counts_mismatch');
 const jobs=ordered.map(r=>{const P=r.platforms.length;
  const j={id:r.id,order:r.order,run_class:r.run_class,platforms:r.platforms,visibility:r.visibility,
   clip:r.clip,edit:r.edit,wrong_delete_code_first:r.wrong_delete_code_first,pair:r.pair,pair_mode:r.pair_mode};
  for(const field of ['clip_no','kind','uses','day'])if(Object.hasOwn(r,field))j[field]=r[field];
  Object.assign(j,{class:r.run_class==='drill'?'local':'write',due_at:new Date(dueFrom).toISOString()});
  if(r.run_class==='test_post'){j.listed_writes=2+3*P+(site==='owner'&&r.edit?P:0);j.edit_writes=r.edit?P:0;j.delete_writes=(r.wrong_delete_code_first?approval.delete_with_another_posts_code_first:approval.delete)*P;
   j.approval_mode=site==='tester'?'each_post_edit_delete_separately':'daily_list_and_separate_delete';}
  if(r.run_class==='refusal')j.listed_writes=approval.refusal_run_from_the_days_list;
  if(r.run_class==='real_slot')j.release_writes=1+3*P;
  return j;});
 return {plan:{schema:'round2-planned-runs/1',site,counts_sha256:sha(canonical(counts)),totals,runs:ordered},jobs};
}
export function make(home,counts,site,options){return withLock(path.join(home,'plan.guard'),()=>{
 const planFile=path.join(home,'planned-runs.json'),jobsFile=path.join(home,'jobs.json'),seedFile=path.join(home,'plan-seed');
 if(fs.existsSync(planFile)||fs.existsSync(jobsFile))throw new Error('plan_already_exists');
 if(!fs.existsSync(seedFile))fs.writeFileSync(seedFile,randomBytes(32),{flag:'wx',mode:0o600});
 if(fs.lstatSync(seedFile).isSymbolicLink())throw new Error('invalid_seed');
 const {plan,jobs}=generate(counts,site,fs.readFileSync(seedFile),options),bytes=canonical(plan)+'\n';
 // Exclusive files: a partial failure is preserved and blocks a second make.
 for(const [file,value]of [[planFile,bytes],[jobsFile,canonical(jobs)+'\n']]){const fd=fs.openSync(file,'wx',0o600);try{fs.writeFileSync(fd,value);fs.fsyncSync(fd);}finally{fs.closeSync(fd);}}
 return {sha256:sha(bytes),runs:plan.runs.length,totals:plan.totals};
});}
export function check(home,sealed){if(!/^[a-f0-9]{64}$/.test(sealed??'')||sha(fs.readFileSync(path.join(home,'planned-runs.json')))!==sealed)throw new Error('plan_digest_mismatch');return {matches:true,sha256:sealed};}
export function reveal(ctx){const records=readJournal(ctx.journal),last=records.findLast(r=>r.type==='STEP'),closing=records.findLast(r=>r.type==='SIGN_OFF'&&r.d.closing===true);
 if(!closing||(last&&(closing.seq<last.seq||Date.parse(closing.at)<Date.parse(last.at))))throw new Error('closing_sign_off_required');
 const file=path.join(ctx.home,'planned-runs.json'),bytes=fs.readFileSync(file),seal=records.findLast(r=>r.type==='PROTOCOL_SEAL'&&r.d.plan_sha256);
 if(!seal)throw new Error('sealed_plan_required');check(ctx.home,seal.d.plan_sha256);
 if(JSON.parse(bytes).site!==ctx.site)throw new Error('site_mismatch');
 const out=path.join(path.dirname(ctx.journal),'planned-runs.json');fs.writeFileSync(out,bytes,{flag:'wx'});return {sha256:sha(bytes)};
}
export function main(argv=process.argv.slice(2)){
 const sub=argv.shift(),{flags:f,command}=parseFlags(argv);if(command.length)throw new Error('invalid_arguments');
 const keys={make:['counts','site','due-from','first-short','first-long'],check:['sealed'],reveal:['site','journal']};
 if(!keys[sub])throw new Error('invalid_arguments');allowed(f,keys[sub]);const home=privateHome();
 const result=sub==='make'?make(home,JSON.parse(fs.readFileSync(f.counts)),f.site,{dueFrom:f['due-from'],firstShort:f['first-short']===undefined?undefined:Number(f['first-short']),firstLong:f['first-long']===undefined?undefined:Number(f['first-long'])}):sub==='check'?check(home,f.sealed):reveal(context(f));
 console.log(JSON.stringify(result));return 0;
}
if(process.argv[1]&&import.meta.url===pathToFileURL(path.resolve(process.argv[1])).href){try{process.exitCode=main();}catch{console.error('planned_runs_refused');process.exitCode=70;}}
