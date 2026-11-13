import path from 'node:path';
import {readJSON,sha} from './io.mjs';
import fs from 'node:fs';

// Supplemental real releases are journal records, never edits of the sealed list.
export function sealedPlan(ctx,records){
 const file=path.join(ctx.home,'planned-runs.json'),seal=records.findLast(r=>r.type==='PROTOCOL_SEAL')?.d;
 if(!fs.existsSync(file)){
  if(seal?.plan_sha256)throw new Error('sealed_plan_missing');
  return null;
 }
 if(fs.lstatSync(file).isSymbolicLink())throw new Error('fault_plan_invalid');
 const bytes=fs.readFileSync(file);
 if(seal?.plan_sha256&&sha(bytes)!==seal.plan_sha256)throw new Error('sealed_plan_digest_mismatch');
 return JSON.parse(bytes);
}
export function jobList(ctx,records){
 const list=readJSON(path.join(ctx.home,'jobs.json'),[]);
 if(!Array.isArray(list))throw new Error('bad_job_list');
 const plan=sealedPlan(ctx,records);
 if(plan?.jobs_sha256&&sha(fs.readFileSync(path.join(ctx.home,'jobs.json')))!==plan.jobs_sha256)throw new Error('working_list_mismatch');
 const extra=records.filter(r=>r.type==='REAL_RELEASE').map(r=>({id:r.d.job,class:'write',
   run_class:'real_release',platforms:r.d.platforms,due_at:r.d.due_at,release_writes:r.d.release_writes}));
 const all=[...list,...extra];
 if(new Set(all.map(j=>j.id)).size!==all.length)throw new Error('bad_job_list');
 return all;
}
