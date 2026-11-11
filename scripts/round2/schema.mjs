import {readFileSync} from 'node:fs';
export const schema = JSON.parse(readFileSync(new URL('../../data/round2/protocol/journal-record-v1.schema.json', import.meta.url)));
export const ID = /^[a-z0-9][a-z0-9._-]{0,63}$/;
export const SLOTS = ['owner','tester','codex','gemini','claude','tester-assistant','scheduler'];
export const PLATFORMS = ['bilibili','youtube','x','douyin','instagram'];

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
  if (record.type === 'GATE_DECISION' && (!Number.isFinite(Date.parse(record.d.now)) || new Date(record.d.now).toISOString() !== record.d.now)) problems.push('invalid gate time');
  if (record.type === 'SIGN_OFF' && (!Number.isFinite(Date.parse(record.d.day)) || new Date(record.d.day).toISOString().slice(0,10) !== record.d.day)) problems.push('invalid sign-off day');
  if (record.type === 'NOTE') {
    const d=record.d;
    if (d.code === 'outside_upload') {
      if (d.platform === null || d.agent === null || d.minutes_ago === null) problems.push('outside_upload requires platform, agent, minutes_ago');
    } else if (d.minutes_ago !== null) problems.push('minutes_ago only applies to outside_upload');
    if (d.code === 'model_changed' && !['codex','gemini','claude','tester-assistant'].includes(d.agent)) problems.push('model_changed requires agent');
    if (!['outside_upload','model_changed'].includes(d.code) && d.agent !== null) problems.push('unexpected agent');
  }
  return problems;
}
