import fs from 'node:fs';
import path from 'node:path';
import {sha,readJSON} from './io.mjs';

export const HOME_ERRORS=['vpub_home_missing','vpub_home_invalid','vpub_home_unsealed','vpub_home_mismatch'];

// Only the private setup file and directory metadata are read. Never inspect
// the selected directory's contents, profiles, sign-ins or ledger here.
export function siteHome(ctx){
  try{
    const setup=path.join(ctx.home,'site.json');
    if(fs.lstatSync(setup).isSymbolicLink())throw new Error('vpub_home_invalid');
    const value=readJSON(setup);
    if(!value||Object.keys(value).sort().join(',')!=='site,vpub_home'||value.site!==ctx.site||
       typeof value.vpub_home!=='string'||!path.isAbsolute(value.vpub_home))throw new Error('vpub_home_invalid');
    const resolved=fs.realpathSync.native(value.vpub_home);
    if(!fs.statSync(resolved).isDirectory())throw new Error('vpub_home_invalid');
    const normalized=process.platform==='win32'?resolved.toLowerCase():resolved;
    return {home:resolved,digest:sha('round2-vpub-home-path-v1\0'+normalized)};
  }catch(e){
    if(e.code==='ENOENT')throw new Error('vpub_home_missing');
    throw new Error('vpub_home_invalid');
  }
}

export function bindHome(ctx,records,{sealing=false}={}){
  const value=siteHome(ctx);
  const digests=records.filter(r=>r.type==='PROTOCOL_SEAL'&&r.d.vpub_home_sha256).map(r=>r.d.vpub_home_sha256);
  if(digests.some(d=>d!==value.digest))throw new Error('vpub_home_mismatch');
  if(!sealing&&!digests.length)throw new Error('vpub_home_unsealed');
  return value;
}
