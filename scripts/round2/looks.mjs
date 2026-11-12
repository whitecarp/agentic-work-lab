import {operation,uncertainState,confirmedAnswer} from './answers.mjs';
export function lookStep(records,job,platform,about){
 return records.findLast(r=>r.d.job===job&&(about==='delete'&&platform==='douyin'&&
  r.type==='NOTE'&&r.d.code==='manual_step'&&r.d.platform===platform ||
  r.type==='STEP'&&r.d.platforms.includes(platform)&&
  r.d.started!==false&&r.d.exit!==127&&r.d.class==='write'&&operation(r)===about&&
  ['publish','repeat_same','changed_plan','resume','edit','delete'].includes(r.d.role)));
}
export function secondLookDue(look,step,records){
 if(!step||look.d.about!=='post')return false;
 const earlier=records.filter(r=>r.type==='OBSERVATION'&&r.seq<look.seq&&r.seq>step.seq&&
  r.d.job===look.d.job&&r.d.about==='post'&&(r.d.platform??look.d.platform)===look.d.platform);
 const first=earlier.find(r=>lookTiming(r,step,[]).counts);
 if(first?.d.seen!=='absent')return false;
 const steps=records.filter(r=>r.type==='STEP'&&r.d.job===look.d.job&&r.seq<=first.seq&&r.d.platforms.includes(look.d.platform));
 const last=steps.findLast(r=>['publish','repeat_same','resume','verify'].includes(r.d.role));
 const a=last?.d.answers?.find(a=>a.platform===look.d.platform);
 return a?!confirmedAnswer(a,'post')&&(uncertainState(a,'post')||last.d.result==='unknown'):
  ['in_doubt','unknown'].includes(last?.d.result);
}
export function lookTiming(look,step,records=[],cutoff=Infinity){
 const n=look.d.minutes_after;
 const status=!Number.isFinite(n)||n<0?'unknown':n<10?'early':n<=60?'on_time':n<=1440?'late':'outside_window';
 const second=secondLookDue(look,step,records)&&n>=1200&&n<=2160;
 const premature=!!step&&Date.parse(look.at)-Date.parse(step.at)<n*60000;
 return {status:look.d.seen==='unclear'?'unclear':second?'second_look':status,
  counts:!!step&&!premature&&look.d.seen!=='unclear'&&Date.parse(look.at)<=cutoff&&(second||n>=10&&n<=1440),
  recorded_before_claimed_minute:premature};
}
export function countsLook(look,step,cutoff=Infinity,records=[]){return lookTiming(look,step,records,cutoff).counts;}
