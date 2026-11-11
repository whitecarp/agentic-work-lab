// Derive the shareable tester copy from CrossCast's bounded local export.
export function privateToolCopy(source,commit){
 if(source.schema!=='crosscast-audit/1'||!['exact','relative'].includes(source.times))throw new Error('invalid_crosscast_export');
 const copy=structuredClone(source), absolute=source.times==='exact';
 function seconds(value){
  if(value==null)return null;
  const n=absolute?Date.parse(value)/1000:value;
  if(!Number.isFinite(n))throw new Error('invalid_crosscast_time');
  return n;
 }
 function elapsed(value,origin){
  const n=seconds(value);return n==null||origin==null?null:Math.round((n-origin)*1e6)/1e6;
 }
 const byRun=new Map(),suppressed=new Set();
 const daysByRun=new Map();
 for(const e of source.events){
  if(!daysByRun.has(e.run))daysByRun.set(e.run,new Set());
  if(e.day)daysByRun.get(e.run).add(e.day);
 }
 const crosses=values=>absolute&&new Set(values.filter(v=>typeof v==='string').map(v=>v.slice(0,10))).size>1;
 for(const run of copy.runs){
  const boundary=daysByRun.get(run.run)?.size>1||crosses([run.t_start,run.t_end]);
  const origin=boundary?null:seconds(run.t_start);byRun.set(run.run,origin);
  if(boundary){
   suppressed.add(run.run);run.seconds=null;run.concurrency=null;
   for(const span of run.spans??[]){span[2]=null;span[3]=null;}
  }
  const startEvent=copy.events.find(e=>e.run===run.run&&e.type==='command_start');
  run.day=startEvent?.day??null;
  run.t_end=elapsed(run.t_end,origin);run.t_start=origin==null?null:0;
  for(const stage of run.stages??[])stage[3]=elapsed(stage[3],origin);
 }
 for(const event of copy.events){
  event.at=elapsed(event.at,byRun.get(event.run));delete event.day;
  if(suppressed.has(event.run)){
   for(const k of ['seconds','t_start','t_end'])if(k in event)event[k]=null;
  }
  if(event.prev)event.prev=commit(event.prev);
 }
 // A record can link several commands, including a command withheld at
 // midnight. It therefore has no relative origin or interval in the share.
 for(const loss of copy.losses??[]){
  const origin=byRun.get(loss.run);
  for(const k of ['at','reported_at'])if(k in loss)loss[k]=elapsed(loss[k],origin);
 }
 for(const [rows,fields] of [[copy.publications,['t_started','t_updated','t_verified']],[copy.deletions,['t_created','t_attempted','t_updated']]]){
  for(const row of rows??[]){
   const nested=[...(row.edits??[]),...(row.observations??[])];
   for(const key of fields)if(key in row)row[key]=null;
   for(const e of nested)if('t' in e)e.t=null;
  }
 }
 copy.window={since:source.window?.since?.slice(0,10)??null,until:source.window?.until?.slice(0,10)??null};
 if(copy.integrity?.head)copy.integrity.head=commit(copy.integrity.head);
 for(const row of [...copy.runs,...copy.events])if('session' in row)row.session=null;
 const sequence=new Map(copy.events.map((e,i)=>[e.seq,i+1]));
 copy.events.forEach((e,i)=>{e.seq=i+1;});
 if(copy.integrity){copy.integrity.first_seq=copy.events.length?1:null;copy.integrity.last_seq=copy.events.length||null;
  if('first_bad_seq' in copy.integrity)copy.integrity.first_bad_seq=sequence.get(copy.integrity.first_bad_seq)??null;}
 copy.times='per_command';copy.derived=true;
 if(/\d{4}-\d\d-\d\dT\d\d:\d\d/.test(JSON.stringify(copy)))throw new Error('unhandled_clock_time');
 return copy;
}
