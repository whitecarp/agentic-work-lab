#!/usr/bin/env node
// C12 / Annex A 1.7. Only JSON exports/revealed lists explicitly named by the
// input manifest are read. No vpub invocation, home lookup, network, or clock.
// Mapping: a "run" is plan.runs[].id / STEP.d.job, a session is record.run;
// state/press flag are STEP.d.answers[], and FAULT_RESULT holds firing evidence.
// Full mapping and unavailable fields: paper/round2/README.md.
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath,pathToFileURL} from 'node:url';
import {analyze} from './round2/metrics.mjs';
import {hash} from './round2/inputs.mjs';
export {analyze};
const paper=path.dirname(fileURLToPath(import.meta.url));
const read=file=>fs.readFileSync(file,'utf8').replace(/\r\n/g,'\n');
export function loadInputs(file,overrideCutoff) {
 const directory=path.dirname(path.resolve(file)),manifest=JSON.parse(read(file));
 if(manifest.schema!=='round2-analysis-manifest/1'||!Array.isArray(manifest.sites))throw new Error('invalid_input_manifest');
 const get=name=>{if(typeof name!=='string'||!name)throw new Error('missing_export_path');return path.resolve(directory,name);};
 const json=name=>JSON.parse(read(get(name)));
 const sites=manifest.sites.map(row=>{
  const journal=json(row.journal),planText=read(get(row.plan)),countsText=row.counts?read(get(row.counts)):null;
  return {site:row.site,journal,tool:journal.privacy?null:json(row.crosscast),reconcile:json(row.reconcile),
   plan:JSON.parse(planText),planText,seed:fs.readFileSync(get(row.seed)),counts:countsText?JSON.parse(countsText):null,countsText,
   asOf:overrideCutoff??row.as_of};
 });
 return {schema:'round2-analysis-inputs/1',synthetic:manifest.synthetic===true,sites,checklist:json(manifest.checklist)};
}
export function main(argv=process.argv.slice(2),{stdout=console.log,stderr=console.error}={}) {
 try{
  const flags={},switches=new Set();
  for(let i=0;i<argv.length;i++){
   const a=argv[i];
   if(['--check','--write'].includes(a)){if(switches.has(a))throw new Error('duplicate_option');switches.add(a);continue;}
   if(!['--inputs','--as-of','--summary'].includes(a)||Object.hasOwn(flags,a)||!argv[i+1]||argv[i+1].startsWith('--'))throw new Error('invalid_option');
   flags[a]=argv[++i];
  }
  if(switches.size>1)throw new Error('conflicting_modes');
  const inputFile=flags['--inputs']??path.join(paper,'round2-inputs.json');
  const output=path.resolve(flags['--summary']??path.join(paper,'round2_metrics_summary.json'));
  if(output===path.resolve(inputFile))throw new Error('output_overwrites_input');
  const inputs=loadInputs(inputFile,flags['--as-of']);
  // The unchanged gate source is frozen with this script in the Lab commit.
  const gateText=read(new URL('../scripts/operations-gate.mjs',import.meta.url));
  const gateHash=hash(gateText),gateHashes=[gateHash,hash(gateText.replaceAll('\n','\r\n'))];
  const result=analyze(inputs,{gateHash,gateHashes}),text=JSON.stringify(result,null,2)+'\n';
  if(switches.has('--check')){
   if(read(output)!==text){stderr('round2_summary_differs');return 1;}
   stdout('round2_summary_matches');return 0;
  }
  if(switches.has('--write')){
   // Reject every explicit input path as a destination, including the raw seed.
   const manifest=JSON.parse(read(inputFile)),dir=path.dirname(path.resolve(inputFile));
   const sources=[manifest.checklist,...manifest.sites.flatMap(r=>['journal','crosscast','reconcile','plan','seed','counts'].map(k=>r[k]))].filter(Boolean).map(p=>path.resolve(dir,p));
   sources.push(path.resolve(inputFile));
   if(sources.some(p=>p.toLowerCase()===output.toLowerCase()))throw new Error('output_overwrites_input');
   if(fs.existsSync(output)){
    const target=fs.lstatSync(output);
    if(target.isSymbolicLink()||!target.isFile())throw new Error('unsafe_summary_destination');
    if(sources.some(p=>{const input=fs.statSync(p);return input.dev===target.dev&&input.ino===target.ino;}))throw new Error('output_overwrites_input');
   }
   fs.mkdirSync(path.dirname(output),{recursive:true});fs.writeFileSync(output,text,'utf8');
  }
  stdout(text.trimEnd());return 0;
 }catch(e){stderr(e.code==='ENOENT'?'input_or_summary_unreadable':e.message);return 1;}
}
if(process.argv[1]&&import.meta.url===pathToFileURL(path.resolve(process.argv[1])).href)process.exitCode=main();
