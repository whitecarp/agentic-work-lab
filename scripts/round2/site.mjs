import fs from 'node:fs';
import path from 'node:path';
import {readJSON} from './io.mjs';
import {keyedDigest} from './journal.mjs';

export const HOME_ERRORS=['vpub_home_missing','vpub_home_invalid','vpub_home_unsealed','vpub_home_mismatch'];

// Only the private setup file and directory metadata are read. Never inspect
// the selected directory's contents, profiles, sign-ins or ledger here.
export function siteHome(ctx,{createSalt=true}={}){
  try{
    const setup=path.join(ctx.home,'site.json');
    if(fs.lstatSync(setup).isSymbolicLink())throw new Error('vpub_home_invalid');
    const value=readJSON(setup);
    if(!value||Object.keys(value).some(k=>!['site','vpub_home','bili_cli_home'].includes(k))||value.site!==ctx.site||
       typeof value.vpub_home!=='string'||!path.isAbsolute(value.vpub_home))throw new Error('vpub_home_invalid');
    const resolved=fs.realpathSync.native(value.vpub_home);
    if(!fs.statSync(resolved).isDirectory())throw new Error('vpub_home_invalid');
    const normalized=process.platform==='win32'?resolved.toLowerCase():resolved;
    let biliHome;
    let binding='round2-vpub-home-path-v1\0'+normalized;
    if(Object.hasOwn(value,'bili_cli_home')){
      if(typeof value.bili_cli_home!=='string'||!path.isAbsolute(value.bili_cli_home))throw new Error('vpub_home_invalid');
      biliHome=fs.realpathSync.native(value.bili_cli_home);
      if(!fs.statSync(biliHome).isDirectory())throw new Error('vpub_home_invalid');
      binding+='\0bili-cli-home\0'+(process.platform==='win32'?biliHome.toLowerCase():biliHome);
    }
    return {home:resolved,biliHome,digest:keyedDigest(binding,ctx.home,{create:createSalt})};
  }catch(e){
    if(e.code==='ENOENT')throw new Error('vpub_home_missing');
    throw new Error('vpub_home_invalid');
  }
}

export function bindHome(ctx,records,{sealing=false}={}){
  const digests=records.filter(r=>r.type==='PROTOCOL_SEAL'&&r.d.vpub_home_sha256).map(r=>r.d.vpub_home_sha256);
  const value=siteHome(ctx,{createSalt:digests.length===0});
  if(digests.some(d=>d!==value.digest))throw new Error('vpub_home_mismatch');
  if(!sealing&&!digests.length)throw new Error('vpub_home_unsealed');
  return value;
}
