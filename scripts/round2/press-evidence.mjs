// Annex A 1.11: one rule for local reconciliation and exported analysis.
// Positive surviving evidence is usable even when a command is incomplete.
export const finiteCount=n=>Number.isSafeInteger(n)&&n>=0;
export function commandSnapshots(events,platform){
 return events.flatMap(e=>[
  ...(!e.platform||e.platform===platform?[e.type==='transition'?e:null,e.before,e.after]:[]),
  e.platforms?.[platform]
 ].filter(x=>x&&(!x.platform||x.platform===platform)));
}
export function pressEvidence(events,intact,platform,about='post'){
 const middle=events.filter(e=>(!e.platform||e.platform===platform)&&!['command_start','command_end'].includes(e.type));
 const snapshots=commandSnapshots(events,platform);
 const first=events.find(e=>e.type==='command_start'),end=events.findLast(e=>e.type==='command_end');
 const before=first?.before,after={...end?.after,...end?.platforms?.[platform]};
 const hasPress=middle.some(e=>e.stage==='pressing'||e.operation==='press'||e.platform_writes>0||
  e.type==='transition'&&(about==='post'?e.phase==='submitting'&&e.final_submit_attempted===true&&e.final_presses>0:
   about==='edit'?e.state==='EDIT_SUBMITTING':e.kind==='deletion'&&e.operation==='press'));
 const countChangedWithoutPress=!hasPress&&((finiteCount(before?.final_presses)&&finiteCount(after?.final_presses)&&before.final_presses!==after.final_presses)||
   !!before&&after?.delete_attempted===true&&before.delete_attempted!==true);
 const noContact=!!intact&&middle.every(e=>e.type==='span');
 const noPress=!!intact&&!hasPress&&!countChangedWithoutPress&&
  middle.every(e=>['call','span','stage','transition'].includes(e.type))&&(platform!=='bilibili'||noContact);
 return {snapshot:snapshots,hasPress,noContact:noContact&&!countChangedWithoutPress,noPress,countChangedWithoutPress};
}
