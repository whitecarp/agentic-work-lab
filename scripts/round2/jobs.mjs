import path from 'node:path';
import {readJSON,sha} from './io.mjs';
import fs from 'node:fs';

// Supplemental real releases are journal records, never edits of the sealed list.
export function jobList(ctx,records){
 const list=readJSON(path.join(ctx.home,'jobs.json'),[]);
 if(!Array.isArray(list))throw new Error('bad_job_list');
 const plan=readJSON(path.join(ctx.home,'planned-runs.json'),null);
 if(plan?.jobs_sha256&&sha(fs.readFileSync(path.join(ctx.home,'jobs.json')))!==plan.jobs_sha256)throw new Error('working_list_mismatch');
 const extra=records.filter(r=>r.type==='REAL_RELEASE').map(r=>({id:r.d.job,class:'write',
   run_class:'real_release',platforms:r.d.platforms,due_at:r.d.due_at,release_writes:r.d.release_writes}));
 const all=[...list,...extra];
 if(new Set(all.map(j=>j.id)).size!==all.length)throw new Error('bad_job_list');
 return all;
}
