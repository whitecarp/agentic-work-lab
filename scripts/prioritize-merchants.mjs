import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

// Offline demonstration. Scores are a configurable prioritisation rule, not AI predictions.
export function prioritize(input) {
  if (!Array.isArray(input.merchants)) throw new Error('merchants must be an array');
  const seen = new Set();
  const weights = {category_fit: 40, service_area_fit: 30, evidence_quality: 30};
  const merchants = input.merchants.map(m => {
    if (!m.id || seen.has(m.id)) throw new Error('Missing or duplicate merchant id');
    seen.add(m.id);
    const missing = [];
    let score = 0;
    for (const [key, weight] of Object.entries(weights)) {
      if (m[key] == null) { missing.push(key); continue; }
      if (typeof m[key] !== 'number' || !Number.isFinite(m[key]) || m[key] < 0 || m[key] > 1) throw new Error('Invalid score: ' + key);
      score += m[key] * weight;
    }
    const hasEvidence = Array.isArray(m.evidence) && m.evidence.some(e => e.reviewed === true && typeof e.source === 'string' && e.source.trim());
    return {
      id: m.id, name_zh_hant: m.name_zh_hant, name_en: m.name_en,
      score: Math.round(score * 100) / 100,
      missing_fields: missing,
      next_step: missing.length || !hasEvidence ? 'research_required' : 'human_review',
      outreach_sent: false,
      evidence: m.evidence ?? [],
      discussion_angle_zh_hant: m.discussion_angle_zh_hant,
      discussion_angle_en: m.discussion_angle_en
    };
  }).sort((a,b) => b.score - a.score || a.id.localeCompare(b.id));
  return {demo: input.demo === true, method: 'transparent_weighted_rule_not_model_prediction', weights, merchants};
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const inputFile = process.argv[2] || 'examples/merchants-demo.json';
  const outputFile = process.argv[3] || 'results/merchant-priorities-demo.json';
  const result = prioritize(JSON.parse(fs.readFileSync(inputFile, 'utf8')));
  fs.mkdirSync(path.dirname(outputFile), {recursive:true});
  fs.writeFileSync(outputFile, JSON.stringify(result,null,2)+'\n');
  console.log(JSON.stringify({output:outputFile,merchant_count:result.merchants.length,outreach_sent:false}));
}
