import fs from 'node:fs';
import path from 'node:path';
import {randomBytes, createHash} from 'node:crypto';
export const sha = bytes => createHash('sha256').update(bytes).digest('hex');
export const utc = () => new Date().toISOString();
export function atomic(file, value) {
  fs.mkdirSync(path.dirname(file), {recursive:true});
  const temp=file+'.'+randomBytes(8).toString('hex')+'.tmp';
  let fd;
  try {
    fd=fs.openSync(temp,'wx',0o600);
    fs.writeFileSync(fd,typeof value==='string'||Buffer.isBuffer(value)?value:JSON.stringify(value)+'\n');
    fs.fsyncSync(fd); fs.closeSync(fd); fd=undefined;
    fs.renameSync(temp,file);
  } finally {
    if (fd !== undefined) fs.closeSync(fd);
    if (fs.existsSync(temp)) fs.unlinkSync(temp);
  }
}
export function withLock(file, fn) {
  fs.mkdirSync(path.dirname(file),{recursive:true});
  const deadline=Date.now()+1000;
  let fd;
  while (fd === undefined) {
    if(fs.existsSync(file+'.recovery')) throw new Error('lock_unavailable');
    try { fd=fs.openSync(file,'wx',0o600); }
    catch (e) {
      // Windows can report a sharing/delete race as EACCES/EPERM while an
      // exclusive file is being released. Wait only within the same deadline.
      if(!['EEXIST','EACCES','EPERM'].includes(e.code)||Date.now()>=deadline) throw new Error('lock_unavailable',{cause:e});
      Atomics.wait(new Int32Array(new SharedArrayBuffer(4)),0,0,10);
    }
  }
  try {
    if(fs.existsSync(file+'.recovery')) throw new Error('lock_unavailable');
    fs.writeFileSync(fd,JSON.stringify({pid:process.pid})); fs.fsyncSync(fd); return fn();
  }
  finally { fs.closeSync(fd); fs.unlinkSync(file); }
}
export function privateHome() {
  if (!process.env.ROUND2_HOME) throw new Error('private_home_required');
  fs.mkdirSync(process.env.ROUND2_HOME,{recursive:true});
  const home=fs.realpathSync(process.env.ROUND2_HOME);
  // Private evidence must never be placed inside a Git checkout.
  let current=home;
  for (;;) {
    if(fs.existsSync(path.join(current,'.git'))) throw new Error('private_home_in_repository');
    const parent=path.dirname(current); if(parent===current) break; current=parent;
  }
  return home;
}
export function readJSON(file, fallback) {
  try { return JSON.parse(fs.readFileSync(file,'utf8')); }
  catch(e) { if(e.code==='ENOENT' && fallback !== undefined) return fallback; throw e; }
}
export function parseFlags(argv) {
  const flags={}, command=[];
  for(let i=0;i<argv.length;i++){
    if(argv[i]==='--'){command.push(...argv.slice(i+1));break;}
    if(!/^--[a-z][a-z-]*$/.test(argv[i]) || i+1>=argv.length || argv[i+1].startsWith('--')) throw new Error('invalid_arguments');
    const key=argv[i].slice(2), value=argv[++i];
    if(key==='platform') (flags.platform??=[]).push(value);
    else {if(Object.hasOwn(flags,key)) throw new Error('duplicate_flag');flags[key]=value;}
  }
  return {flags,command};
}
export function allowed(flags, keys) {
  if(Object.keys(flags).some(k=>!keys.includes(k))) throw new Error('unknown_flag');
}
export function alive(pid) {
  if(!Number.isSafeInteger(pid)||pid<=0) return false;
  try{process.kill(pid,0);return true;}catch(e){return e.code==='EPERM';}
}
