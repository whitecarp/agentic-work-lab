// Annex A 1.11: one rule for local reconciliation and exported analysis.
// Positive surviving evidence is usable even when a command is incomplete.
export const finiteCount=n=>Number.isSafeInteger(n)&&n>=0;
export function commandSnapshots(events,platform){
 return events.flatMap(e=>[
  ...(!e.platform||e.platform===platform?[e.type==='transition'?e:null,e.before,e.after]:[]),
  e.platforms?.[platform]
 ].filter(x=>x&&(!x.platform||x.platform===platform)));
}
export function pressEvidence(events,intact,platform,about='post',priorCount=null){
 const middle=events.filter(e=>(!e.platform||e.platform===platform)&&!['command_start','command_end'].includes(e.type));
 const snapshots=commandSnapshots(events,platform);
 const first=events.find(e=>e.type==='command_start'),end=events.findLast(e=>e.type==='command_end');
 const before=first?.before,after={...end?.after,...end?.platforms?.[platform]};
 const hasPress=middle.some(e=>e.stage==='pressing'||e.operation==='press'||e.platform_writes>0||
  e.type==='transition'&&(about==='post'?e.phase==='submitting'&&e.final_submit_attempted===true&&e.final_presses>0:
   about==='edit'?e.state==='EDIT_SUBMITTING':e.kind==='deletion'&&e.operation==='press'));
 const countChangedWithoutPress=!hasPress&&((finiteCount(before?.final_presses)&&finiteCount(after?.final_presses)&&before.final_presses!==after.final_presses)||
   !!before&&after?.delete_attempted===true&&before.delete_attempted!==true);
 const missingBeforePositive=about==='post'&&!finiteCount(before?.final_presses)&&
  snapshots.some(x=>finiteCount(x.final_presses)&&x.final_presses>0)&&
  !(finiteCount(priorCount)&&snapshots.filter(x=>finiteCount(x.final_presses)).every(x=>x.final_presses===priorCount));
 const noContact=!!intact&&!missingBeforePositive&&middle.every(e=>e.type==='span');
 const noPress=!!intact&&!hasPress&&!countChangedWithoutPress&&!missingBeforePositive&&
  middle.every(e=>['call','span','stage','transition'].includes(e.type))&&(platform!=='bilibili'||noContact);
 return {snapshot:snapshots,hasPress,noContact:noContact&&!countChangedWithoutPress,noPress,countChangedWithoutPress,missingBeforePositive};
}

// A positive counter in a different, earlier record can establish the baseline.
export function priorPressCount(all,events,platform){
 const first=events.find(e=>e.type==='command_start');
 if(!Number.isSafeInteger(first?.seq))return null;
 const refs=new Set(commandSnapshots(events,platform).map(s=>s.ref).filter(v=>typeof v==='string'));
 // A counter for one receipt cannot establish a baseline for a different one.
 if(refs.size!==1)return null;
 const counts=commandSnapshots(all.filter(e=>e.seq<first.seq),platform)
  .filter(s=>refs.has(s.ref)&&!['edit','deletion'].includes(s.kind)&&finiteCount(s.final_presses)).map(s=>s.final_presses);
 return counts.length?Math.max(...counts):null;
}
