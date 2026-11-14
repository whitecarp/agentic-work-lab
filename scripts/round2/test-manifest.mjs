import fs from 'node:fs';
import path from 'node:path';
import {randomUUID} from 'node:crypto';
import {parseArgs} from 'node:util';

const samePlatforms=(left,right)=>left.length===right.length&&new Set(left).size===left.length&&left.every(p=>right.includes(p));

// Run the same checked bytes. Keeping the snapshot beside the input preserves
// relative media paths and CrossCast's prepared-folder review settings.
export function checkedPublish(job,command) {
 try {
  if(command[1]!=='publish'||typeof job.title!=='string'||!job.title.trim()||!job.platforms.length)throw Error();
  const {values,positionals,tokens}=parseArgs({args:command.slice(2),allowPositionals:true,tokens:true,
   options:{confirm:{type:'string'},platforms:{type:'string',multiple:true},workers:{type:'string'},
    'person-approved':{type:'boolean'},'dry-run':{type:'boolean'}}});
  if(positionals.length!==1||values.platforms?.some(v=>!samePlatforms(v.split(','),job.platforms)))throw Error();
  const source=fs.realpathSync(positionals[0]);
  const bytes=fs.readFileSync(source);
  const manifest=JSON.parse(new TextDecoder('utf-8',{fatal:true}).decode(bytes));
  if(!manifest.platforms||Array.isArray(manifest.platforms)||!samePlatforms(Object.keys(manifest.platforms),job.platforms))throw Error();
  for(const platform of job.platforms){
   const entry=manifest.platforms[platform];
   if(!['youtube','douyin'].includes(platform)||!['private','unlisted'].includes(job.visibility)||
    (job.visibility==='unlisted'&&platform!=='youtube')||entry.visibility!==job.visibility||entry.schedule||
    (Object.hasOwn(entry,'title')?entry.title:manifest.title)!==job.title)throw Error();
  }
  const file=path.join(path.dirname(source),'.round2-'+randomUUID()+'.json');
  const fd=fs.openSync(file,'wx',0o600);
  try{try{fs.writeFileSync(fd,bytes);}finally{fs.closeSync(fd);}}
  catch(error){fs.unlinkSync(file);throw error;}
  const args=command.slice();
  args[tokens.find(t=>t.kind==='positional').index+2]=file;
  return {args,dispose:()=>fs.unlinkSync(file)};
 } catch {throw new Error('test_plan_invalid');}
}
