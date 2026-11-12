import {readFileSync} from 'node:fs';
export const schema = JSON.parse(readFileSync(new URL('../../data/round2/protocol/journal-record-v1.schema.json', import.meta.url)));
export const ID = /^[a-z0-9][a-z0-9._-]{0,63}$/;
export const SLOTS = ['owner','tester','codex','gemini','claude','tester-assistant','scheduler'];
export const PLATFORMS = ['bilibili','youtube','x','douyin','instagram'];
export const REFUSALS = schema.oneOf.find(s=>s.properties.type.const==='STEP_REFUSED').properties.d.properties.reason.enum;

// Only the vocabulary used by our closed schema is implemented. Unknown schema
// constructs are not silently accepted by this intentionally small validator.
function check(value, s, path) {
  if (s.anyOf) return s.anyOf.some(x => check(value,x,path).length === 0) ? [] : [path+': invalid value'];
  if ('const' in s) return value === s.const ? [] : [path+': wrong constant'];
  if (s.enum) return s.enum.includes(value) ? [] : [path+': unknown word'];
  if (s.type === 'null') return value === null ? [] : [path+': expected null'];
  if (s.type === 'boolean') return typeof value === 'boolean' ? [] : [path+': expected boolean'];
  if (s.type === 'integer') return Number.isSafeInteger(value) && (s.minimum === undefined || value >= s.minimum) ? [] : [path+': expected integer'];
  if (s.type === 'string') return typeof value === 'string' && (!s.pattern || new RegExp(s.pattern).test(value)) ? [] : [path+': invalid string'];
  if (s.type === 'array') return Array.isArray(value) ? value.flatMap((v,i)=>check(v,s.items,path+'.'+i)) : [path+': expected array'];
  if (s.type === 'object') {
    if (!value || typeof value !== 'object' || Array.isArray(value)) return [path+': expected object'];
    return [
      ...Object.keys(value).filter(k=>!Object.hasOwn(s.properties,k)).map(()=>path+': unknown field'),
      ...s.required.filter(k=>!Object.hasOwn(value,k)).map(k=>path+'.'+k+': missing'),
      ...Object.keys(s.properties).filter(k=>Object.hasOwn(value,k)).flatMap(k=>check(value[k],s.properties[k],path+'.'+k)),
    ];
  }
  return [path+': unsupported schema'];
}
export function validateRecord(record) {
  const definition = schema.oneOf.find(s=>s.properties.type.const === record?.type);
  if (!definition) return ['unknown record type'];
  const problems = check(record,definition,'record');
  if (problems.length) return problems;
  if (!Number.isFinite(Date.parse(record.at)) || new Date(record.at).toISOString() !== record.at) problems.push('invalid UTC time');
  if(record.type==='REAL_RELEASE'){
    const d=record.d;
    if(!Number.isFinite(Date.parse(d.due_at))||new Date(d.due_at).toISOString()!==d.due_at||
       !d.platforms.length||new Set(d.platforms).size!==d.platforms.length||
       d.release_writes!==1+3*d.platforms.length||d.by!==record.site)problems.push('invalid real release');
  }
  if(record.type==='FAULT_SETUP'&&(record.d.by!==record.site||record.d.enabled!==record.d.stop_registered))problems.push('invalid fault setup');
  if(['PROTOCOL_SEAL','RUN_END'].includes(record.type)&&Object.hasOwn(record.d,'by')){
    if(record.d.by!==record.site||!record.d.words_sha256)problems.push('owner words required');
    if(record.type==='RUN_END'&&(record.d.state!=='interrupted'||!record.d.operator))problems.push('invalid session recovery');
  }
  if (record.type === 'GATE_DECISION' && (!Number.isFinite(Date.parse(record.d.now)) || new Date(record.d.now).toISOString() !== record.d.now)) problems.push('invalid gate time');
  if (record.type === 'SIGN_OFF' && (!Number.isFinite(Date.parse(record.d.day)) || new Date(record.d.day).toISOString().slice(0,10) !== record.d.day)) problems.push('invalid sign-off day');
  if (record.type === 'SIGN_OFF' && record.d.answer==='no' && Object.hasOwn(record.d,'words_sha256') && record.d.words_sha256===null) problems.push('negative sign-off requires words digest');
  if (record.type === 'GATE_DECISION') for(const j of record.d.jobs) {
    if(Object.hasOwn(j,'due_at') && !Number.isFinite(Date.parse(j.due_at))) problems.push('invalid job time');
  }
  if (record.type === 'NOTE') {
    const d=record.d;
    if (d.code === 'outside_upload') {
      if (d.platform === null || d.agent === null || d.minutes_ago === null) problems.push('outside_upload requires platform, agent, minutes_ago');
    } else if (d.minutes_ago !== null) problems.push('minutes_ago only applies to outside_upload');
    if (d.code === 'model_changed' && !['codex','gemini','claude','tester-assistant'].includes(d.agent)) problems.push('model_changed requires agent');
    if (!['outside_upload','model_changed'].includes(d.code) && d.agent !== null) problems.push('unexpected agent');
    if (Object.hasOwn(d,'words_sha256') && d.code!=='guard_cleared') problems.push('words digest only applies to guard_cleared');
    if (d.code==='guard_cleared' && !Object.hasOwn(d,'words_sha256')) problems.push('guard recovery requires words digest');
  }
  return problems;
}
