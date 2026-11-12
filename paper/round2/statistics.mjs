export function describe(values) {
 const a=values.filter(Number.isFinite).sort((a,b)=>a-b);
 const q=p=>{if(!a.length)return null;const i=(a.length-1)*p,lo=Math.floor(i);return a[lo]+(a[Math.ceil(i)]-a[lo])*(i-lo);};
 return {n:a.length,median:q(.5),q1:q(.25),q3:q(.75),min:a[0]??null,max:a.at(-1)??null};
}
// Exact one-sided binomial inversion; log-sum-exp avoids underflow.
export function upper95(bad,n) {
 if(!Number.isSafeInteger(n)||!Number.isSafeInteger(bad)||bad<0||bad>n)throw new Error('invalid_bound');
 if(!n)return {bad,n,upper:null,percent_rounded_up:null};
 let upper=1;
 if(!bad)upper=-Math.expm1(Math.log(.05)/n);
 else if(bad<n){
  const logChoose=[0];for(let k=1;k<=bad;k++)logChoose[k]=logChoose[k-1]+Math.log(n-k+1)-Math.log(k);
  function cdf(p){const terms=logChoose.map((v,k)=>v+k*Math.log(p)+(n-k)*Math.log1p(-p));
   const m=Math.max(...terms);return Math.exp(m)*terms.reduce((s,v)=>s+Math.exp(v-m),0);}
  let lo=0,hi=1;for(let i=0;i<80;i++){const m=(lo+hi)/2;if(cdf(m)>.05)lo=m;else hi=m;}upper=hi;
 }
 return {bad,n,upper,percent_rounded_up:Math.ceil(upper*1000)/10};
}
export const tally=(rows,key)=>Object.fromEntries([...rows.reduce((m,r)=>{const k=String(key(r)??'unknown');m.set(k,(m.get(k)??0)+1);return m;},new Map())].sort(([a],[b])=>a<b?-1:a>b?1:0));
export const share=(n,d)=>({n,denominator:d,share:d?n/d:null});
