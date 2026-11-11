export function countsLook(look,step,cutoff=Infinity){
 return !!step&&look.d.seen!=='unclear'&&look.d.minutes_after>=10&&look.d.minutes_after<=1440&&
  Date.parse(look.at)<=cutoff&&Date.parse(look.at)-Date.parse(step.at)>=look.d.minutes_after*60000;
}
