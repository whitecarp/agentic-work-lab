import fs from 'node:fs';
const snapshot = JSON.parse(fs.readFileSync(process.argv[2] || 'data/creator-video-snapshot.json','utf8'));
const output = process.argv[3] || 'results/channel-summary.json';
const median = values => {const a=values.filter(Number.isFinite).sort((x,y)=>x-y);return a.length ? (a[Math.floor((a.length-1)/2)]+a[Math.ceil((a.length-1)/2)])/2:null;};
const groups = ['human_led_history','current_ai_series'].map(cohort=>{
  const items=snapshot.items.filter(x=>x.cohort===cohort);
  const published=items.filter(x=>x.published===true);
  const views=published.map(x=>x.views).filter(Number.isFinite);
  return {cohort,records:items.length,published:published.length,not_yet_public:items.length-published.length,
    published_views_sum:views.reduce((a,b)=>a+b,0),published_views_median:median(views),
    views_available:views.length,views_missing:published.length-views.length,
    earliest_publication:published.map(x=>x.publish_time_raw).filter(Boolean).sort()[0]??null,
    latest_publication:published.map(x=>x.publish_time_raw).filter(Boolean).sort().at(-1)??null};
});
const result={source:'creator-video-snapshot.json',comparison_type:'descriptive_snapshot_not_causal_model_comparison',
  limitation_zh_hant:'片齡、題材與流量來源不同，累積播放差異不能歸因於模型或營運者。',
  limitation_en:'Different video ages, topics, and traffic sources prevent attributing lifetime-view differences to the model or operator.',groups};
fs.mkdirSync('results',{recursive:true});fs.writeFileSync(output,JSON.stringify(result,null,2)+'\n');
console.log(JSON.stringify(result,null,2));
