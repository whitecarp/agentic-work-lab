import test from 'node:test';
import assert from 'node:assert/strict';
import {schema} from '../../scripts/round2/schema.mjs';
import {put} from '../../scripts/round2/journal.mjs';
import {seal,verifySeals} from '../../scripts/round2/seal.mjs';
import {fixture} from './helpers.mjs';

const time='2026-11-10T12:00:00.000Z';
function example(field){
 if(field.anyOf)return example(field.anyOf[0]);
 if(field.enum)return field.enum[0];
 if('const' in field)return field.const;
 if(field.type==='null')return null;
 if(field.type==='boolean')return false;
 if(field.type==='integer')return field.minimum??1;
 if(field.type==='array')return [];
 if(field.type==='object')return Object.fromEntries(field.required.map(k=>[k,example(field.properties[k])]));
 for(const word of [time,time.slice(0,10),'job-1','1',...[16,32,40,64].map(n=>'a'.repeat(n))]){
  if(!field.pattern||new RegExp(field.pattern).test(word))return word;
 }
 throw new Error('add an example for '+field.pattern);
}

test('a day containing every schema record kind can be sealed and verified twice',t=>{
 const {ctx}=fixture(t),types=schema.oneOf.map(s=>s.properties.type.const);
 for(const definition of schema.oneOf){
  const type=definition.properties.type.const,d=example(definition.properties.d);
  if(type==='REAL_RELEASE')Object.assign(d,{platforms:['youtube'],release_writes:4,by:'owner'});
  if(type==='FAULT_SETUP')Object.assign(d,{by:'owner'});
  if(type==='NOTE')Object.assign(d,{code:'rehearsal',platform:null,minutes_ago:null,agent:null});
  put(ctx,type,d);
 }
 const first=seal(ctx);
 assert.deepEqual(Object.keys(first.counts).sort(),types.sort());
 assert.equal(verifySeals(ctx).sealed_records,types.length);
 assert.deepEqual(seal(ctx),first);
});
