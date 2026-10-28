import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';

const directory=process.argv[2];
if(!directory){console.error('Usage: node scripts/check-delivery.mjs <download-directory>');process.exitCode=2;}
else {
  const manifest=JSON.parse(fs.readFileSync('media/manifest.json','utf8'));
  const results=[];
  for(const item of manifest.items){
    if(path.basename(item.file)!==item.file)throw Error('Unsafe manifest filename');
    const target=path.join(directory,item.file);
    if(!fs.existsSync(target)){results.push({file:item.file,status:'missing'});continue;}
    const hash=crypto.createHash('sha256');
    for await (const chunk of fs.createReadStream(target)) hash.update(chunk);
    const digest=hash.digest('hex');
    results.push({file:item.file,status:digest===item.sha256&&fs.statSync(target).size===item.bytes?'verified':'mismatch'});
  }
  console.log(JSON.stringify({results},null,2));
  if(results.some(r=>r.status!=='verified'))process.exitCode=1;
}
