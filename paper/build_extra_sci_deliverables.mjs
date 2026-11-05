// Builds the submission extras next to this script: highlights, graphical abstract,
// declarations with code metadata, and three cover letters (JSS, IEEE TEM, IJPM).
// Every figure quoted here is one the manuscript reports and paper/reproduce_paper_tables.mjs recomputes.
import fs from 'fs';
import path from 'path';
import { execFileSync } from 'child_process';
import { fileURLToPath } from 'url';

const paperDir = path.dirname(fileURLToPath(import.meta.url));

const edgePaths = [
  'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe',
  'C:\\Program Files\\Microsoft\\Edge\\Application\\msedge.exe',
  'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe'
];
const browserExe = edgePaths.find(p => fs.existsSync(p));
if (!browserExe) throw new Error('No Chromium browser found for PDF compilation');

function htmlToPdf(htmlPath, pdfPath) {
  execFileSync(browserExe, ['--headless', '--disable-gpu', '--no-pdf-header-footer', `--print-to-pdf=${pdfPath}`, 'file:///' + htmlPath.replace(/\\/g, '/')], { stdio: 'ignore' });
}

const title = 'Governing Human–AI Collaborative Project Workflows in Multi-Platform Digital Delivery: A Design Science Study of Auditable Handoffs and Idempotent Release Operations';
const authorLine = 'HuaiCheng Sheng (Agentic Work Lab, Hangzhou Chengxin Holding Group Co., Ltd., Hangzhou, Zhejiang, China | ORCID: 0009-0003-2900-6377)';

const baseCss = `
  @page { size: A4 portrait; margin: 18mm 18mm 18mm 18mm; }
  body { font-family: "Times New Roman", Georgia, serif; font-size: 10.5pt; line-height: 1.52; color: #111827; margin: 0; padding: 0; }
  h1 { font-size: 16pt; font-weight: bold; color: #0f172a; margin: 0 0 8pt 0; border-bottom: 1.5pt solid #0f172a; padding-bottom: 6pt; }
  h2 { font-size: 12.5pt; font-weight: bold; color: #1e3a8a; margin: 14pt 0 6pt 0; border-bottom: 0.5pt solid #cbd5e1; padding-bottom: 3pt; }
  p { margin: 0 0 7pt 0; text-align: justify; }
  ul, ol { margin: 0 0 8pt 0; padding-left: 18pt; }
  li { margin-bottom: 4pt; }
  table { width: 100%; border-collapse: collapse; margin: 8pt 0 12pt 0; font-size: 9.2pt; }
  th { border-top: 1.5pt solid #0f172a; border-bottom: 1pt solid #0f172a; padding: 5pt 6pt; text-align: left; background: #f8fafc; font-weight: bold; }
  td { border-bottom: 0.5pt solid #cbd5e1; padding: 4.5pt 6pt; vertical-align: top; }
  tr:last-child td { border-bottom: 1.5pt solid #0f172a; }
  code { font-family: "Consolas", "Courier New", monospace; font-size: 9pt; background: #f1f5f9; padding: 1pt 3pt; border-radius: 2pt; }
  .page-break { page-break-before: always; }
`;
const esc = s => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
const mdToHtml = md => md.split('\n').map(l => esc(l)
  .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>').replace(/\*(.+?)\*/g, '<em>$1</em>').replace(/`(.+?)`/g, '<code>$1</code>')).join('\n');

// ---------------------------------------------------------------------------
// 1. Highlights (Elsevier: 3-5 bullets, at most 85 characters each including spaces)
// ---------------------------------------------------------------------------
const highlights = [
  'A four-layer stage-gate design governs human–AI delivery to five platforms',
  'Receipts are saved as UNKNOWN before the one final submit; no auto re-submit',
  'A 69-record field journal shows one platform write and no replayed upload',
  'In one Instagram run, 92.32% of the publish command was the wait after Share',
  'Descriptive channel data are kept apart from causal claims about AI output'
];
for (const h of highlights) if ([...h].length > 85) throw new Error(`Highlight over 85 characters (${[...h].length}): ${h}`);
const highlightsMd = `# Highlights

**Manuscript:** ${title}
**Author:** ${authorLine}

${highlights.map(h => `- ${h}`).join('\n')}

| # | Highlight | Characters (incl. spaces) |
| :--- | :--- | :---: |
${highlights.map((h, i) => `| ${i + 1} | ${h} | ${[...h].length} / 85 |`).join('\n')}
`;
fs.writeFileSync(path.join(paperDir, 'highlights.md'), highlightsMd, 'utf8');
fs.writeFileSync(path.join(paperDir, 'highlights_print.html'), `<!DOCTYPE html><html><head><meta charset="utf-8"><style>${baseCss}</style></head><body>
<h1>Highlights</h1>
<p><strong>Manuscript:</strong> ${esc(title)}</p>
<p><strong>Author:</strong> ${esc(authorLine)}</p>
<ul>
${highlights.map(h => `<li>${esc(h)}</li>`).join('\n')}
</ul>
<table><thead><tr><th>#</th><th>Highlight</th><th>Characters (incl. spaces)</th></tr></thead><tbody>
${highlights.map((h, i) => `<tr><td>${i + 1}</td><td>${esc(h)}</td><td>${[...h].length} / 85</td></tr>`).join('\n')}
</tbody></table>
</body></html>`, 'utf8');
htmlToPdf(path.join(paperDir, 'highlights_print.html'), path.join(paperDir, 'highlights.pdf'));

// ---------------------------------------------------------------------------
// 2. Graphical abstract (SVG and a one-page landscape PDF)
// ---------------------------------------------------------------------------
const col = (x, y, w, h, head, color, lines) => `
  <rect x="${x}" y="${y}" width="${w}" height="${h}" rx="10" fill="#ffffff" stroke="${color}" stroke-width="2"/>
  <rect x="${x}" y="${y}" width="${w}" height="40" rx="10" fill="${color}"/>
  <rect x="${x}" y="${y + 30}" width="${w}" height="10" fill="${color}"/>
  <text x="${x + w / 2}" y="${y + 26}" font-size="17" font-weight="bold" text-anchor="middle" fill="#ffffff">${esc(head)}</text>
  ${lines.map(([t, bold], i) => `<text x="${x + 18}" y="${y + 70 + i * 23}" font-size="14.2" ${bold ? 'font-weight="bold" ' : ''}fill="#111827">${esc(t)}</text>`).join('\n  ')}`;
const graphicalAbstractSvg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1328 531" width="1328" height="531" font-family="Arial, Helvetica, sans-serif">
  <rect x="0" y="0" width="1328" height="531" fill="#f8fafc"/>
  <text x="664" y="34" font-size="21" font-weight="bold" text-anchor="middle" fill="#0f172a">Governing Human–AI Collaborative Project Workflows in Multi-Platform Digital Delivery</text>
  <text x="664" y="58" font-size="14" text-anchor="middle" fill="#334155">Design Science study · HuaiCheng Sheng, Agentic Work Lab · Agentic Work Lab and PublishOps CrossCast</text>
  ${col(20, 76, 400, 400, 'Problem', '#9a3412', [
    ['Agents now act on platforms whose', true], ['actions cannot be undone', true],
    ['• A retried upload can publish twice:', false], ['  web submission offers no idempotency key', false],
    ['• An approved plan can drift from the', false], ['  file that is actually uploaded', false],
    ['• Young AI-series videos and years-old', false], ['  videos invite false claims of AI gains', false],
    ['Research questions: governance design (RQ1),', true], ['idempotent release (RQ2), evidence (RQ3)', true]
  ])}
  ${col(464, 76, 400, 400, 'Four governance layers', '#1e3a8a', [
    ['1  Admission gate (planRun)', true], ['   owner, stop flag, running lock, authorized', false], ['   due jobs → a plan, never an action', false],
    ['2  Content binding', true], ['   --confirm must present the plan digest;', false], ['   media checked by SHA-256', false],
    ['3  At-most-once receipt', true], ['   saved as UNKNOWN before the one final', false], ['   press; no automatic re-press', false],
    ['4  End-to-end read-back + people', true], ['   read-back closes the gate; a person', false], ['   settles in-doubt cases', false]
  ].slice(0, 14))}
  ${col(908, 76, 400, 400, 'Field evidence (26 Oct–3 Nov 2026)', '#166534', [
    ['• 69-record journal: 1 platform write,', false], ['no replayed upload recorded', false],
    ['• Douyin read-back timed out: receipt kept', false], ['UNKNOWN, no re-upload, owner confirmed', false],
    ['• One Instagram run: 92.32% of the 412.217 s', false], ['publish command was the wait after Share', false],
    ['• 2,737 offline test functions (commit 75b7abc);', false], ['30 video versions, 2.21 GB, SHA-256 recorded', false],
    ['• 55 channel records reported descriptively;', false], ['no causal claim about AI production', false]
  ])}
  <text x="442" y="282" font-size="30" text-anchor="middle" fill="#64748b">→</text>
  <text x="886" y="282" font-size="30" text-anchor="middle" fill="#64748b">→</text>
  <text x="664" y="505" font-size="14" text-anchor="middle" fill="#334155">Single-case evidence from one laboratory · code, data and a script that recomputes every figure are public</text>
</svg>`;
fs.writeFileSync(path.join(paperDir, 'graphical_abstract.svg'), graphicalAbstractSvg, 'utf8');
fs.writeFileSync(path.join(paperDir, 'graphical_abstract_print.html'), `<!DOCTYPE html><html><head><meta charset="utf-8"><style>
@page { size: 1328px 531px; margin: 0; } html, body { margin: 0; padding: 0; } svg { display: block; }
</style></head><body>${graphicalAbstractSvg}</body></html>`, 'utf8');
htmlToPdf(path.join(paperDir, 'graphical_abstract_print.html'), path.join(paperDir, 'graphical_abstract.pdf'));

// ---------------------------------------------------------------------------
// 3. Declarations and code metadata
// ---------------------------------------------------------------------------
const declarationsMd = `# Author Declarations, CRediT Statement and Code Metadata

## 1. Manuscript and author
- **Title:** ${title}
- **Author:** HuaiCheng Sheng
- **Affiliation:** Agentic Work Lab, Hangzhou Chengxin Holding Group Co., Ltd., Hangzhou, Zhejiang, China
- **Corresponding author e-mail:** \`ZackSheng@gmail.com\`
- **ORCID:** \`0009-0003-2900-6377\` (https://orcid.org/0009-0003-2900-6377)

## 2. CRediT author statement
**HuaiCheng Sheng:** Conceptualization, Methodology, Software, Validation, Formal analysis, Investigation, Resources, Data curation, Writing – original draft, Writing – review & editing, Visualization, Supervision, Project administration.

## 3. Declaration of competing interest
The author is affiliated with Agentic Work Lab, Hangzhou Chengxin Holding Group Co., Ltd., and is the developer and maintainer of the open-source artifacts evaluated in this study (*Agentic Work Lab* and *PublishOps CrossCast*); a separate API add-on package outside the evaluated open-source snapshot is distributed independently by the author. The author declares no other competing financial interests or personal relationships that could have appeared to influence the work reported in this paper.

## 4. Funding
This research was supported by the internal research and engineering resources of Agentic Work Lab, Hangzhou Chengxin Holding Group Co., Ltd., Hangzhou, Zhejiang, China. It did not receive any specific grant from funding agencies in the public, commercial or not-for-profit sectors.

## 5. Ethics and data protection
No human-subjects experiment was conducted. The study analyses the author's own operational records, the author's channel analytics and public platform metadata. The exported records exclude credentials, raw client logs, private messages and viewer personal data (\`data/production-update-2026-11-03.json\`, field \`excluded\`).

## 6. Data and code availability
The 69-record operational journal, the 55-record channel snapshot, the 30-entry media manifest, the workflow scripts and the manuscript sources are available at https://github.com/whitecarp/agentic-work-lab (data files as of commit \`e3205d6\`). The evaluated release software is available at https://github.com/whitecarp/publishops-crosscast (commit \`75b7abc\`). \`node paper/reproduce_paper_tables.mjs\` recomputes every figure reported in the manuscript.

## 7. Code metadata
| Nr. | Code metadata description | Value |
| :--- | :--- | :--- |
| C1 | Current code version | publishops-crosscast v0.16.0 (evaluated main-branch commit \`75b7abc\`); agentic-work-lab package 0.1.0 |
| C2 | Permanent link to code/repository | https://github.com/whitecarp/publishops-crosscast and https://github.com/whitecarp/agentic-work-lab |
| C3 | Paper sources and reproduction script | \`paper/\` folder of agentic-work-lab (\`reproduce_paper_tables.mjs\`, \`build_publication_pdf.mjs\`, \`sync_main_tex.mjs\`) |
| C4 | Legal code licence | publishops-crosscast: MIT (bundled bili-cli v1.2.1: MIT). agentic-work-lab: MIT (\`LICENSE\` in the paper branch; no licence file at data snapshot commit \`e3205d6\`) |
| C5 | Code versioning system | Git (GitHub) |
| C6 | Languages, tools and services | Python (CrossCast at \`75b7abc\`: 123 source files, 26,972 lines, of which 42 files are the bundled bili-cli; 126 test files with 2,737 test functions); Node.js (laboratory scripts, no package dependencies). Main Python dependencies: Typer, httpx, Patchright, Pillow, curl_cffi, twitter-text-parser; bili-cli also needs bilibili-api-python (GPL-3.0-or-later), installed separately |
| C7 | Requirements and operating environments | Python 3.10–3.14 (GitHub CI checks for \`75b7abc\` passed on all five); Node.js 20 or later. Package metadata lists Windows, Linux and macOS; installation acceptance on a fresh computer and across operating systems is recorded as not yet completed |
| C8 | Documentation | README_EN.md and docs/ in both repositories (for example \`docs/research-method_EN.md\`, \`docs/model-workflow_EN.md\`, CrossCast \`docs/VALIDATION_EN.md\`); online supplementary material |
| C9 | Support e-mail | ZackSheng@gmail.com |
`;
fs.writeFileSync(path.join(paperDir, 'declarations_and_credit.md'), declarationsMd, 'utf8');
const declRows = declarationsMd.match(/\| C\d[\s\S]*?\n(?=\n|$)/g).join('').trim().split('\n').map(r => r.split('|').slice(1, -1).map(c => c.trim()));
fs.writeFileSync(path.join(paperDir, 'declarations_print.html'), `<!DOCTYPE html><html><head><meta charset="utf-8"><style>${baseCss}</style></head><body>
<h1>Author Declarations, CRediT Statement and Code Metadata</h1>
${mdToHtml(declarationsMd.split('## 7. Code metadata')[0].replace(/^# .*\n/, ''))
  .replace(/^## (.*)$/gm, '<h2>$1</h2>').replace(/^- (.*)$/gm, '<li>$1</li>').replace(/(<li>[\s\S]*?<\/li>)(?!\n<li>)/g, '<ul>$1</ul>')
  .split(/\n{2,}/).map(b => /^<(h2|ul)/.test(b.trim()) ? b : `<p>${b}</p>`).join('\n')}
<h2>7. Code metadata</h2>
<table><thead><tr><th>Nr.</th><th>Code metadata description</th><th>Value</th></tr></thead><tbody>
${declRows.map(r => `<tr><td>${esc(r[0])}</td><td>${esc(r[1])}</td><td>${mdToHtml(r[2])}</td></tr>`).join('\n')}
</tbody></table>
</body></html>`, 'utf8');
htmlToPdf(path.join(paperDir, 'declarations_print.html'), path.join(paperDir, 'declarations_and_credit.pdf'));

// ---------------------------------------------------------------------------
// 4. Cover letters for three journals
// ---------------------------------------------------------------------------
const shared = {
  evidence: 'This study evaluates the framework in a nine-day field deployment (26 October–3 November 2026) in one creator–engineering laboratory. A 69-record operational journal records one platform write and no replayed upload; when Douyin\'s programmatic read-back timed out after the single final submission, the receipt stayed UNKNOWN, nothing was re-submitted, and the owner confirmed the work by phone. In one instrumented Instagram publication, 92.32% of the 412.217 s publish command was the wait after Share. The evaluated release software (PublishOps CrossCast, commit 75b7abc) carries 2,737 offline test functions, and 30 video versions (2.21 GB) are archived with recorded SHA-256 digests.',
  honesty: 'The study is a single-case design science evaluation. The manuscript states the assumptions under which automated re-submission cannot occur, reports every figure descriptively, keeps human-led and AI-assisted cohorts apart rather than comparing them causally, and publishes the data, code and a script that recomputes every reported figure.',
  close: 'The manuscript (22 pages, 11 tables, 5 figures, 3 algorithms) is accompanied by online supplementary material. It is original, has not been published previously, and is not under consideration elsewhere.'
};
const letters = [
  { key: 'A', journal: 'Journal of Systems and Software', publisher: 'Elsevier', to: 'The Editors-in-Chief, Journal of Systems and Software',
    pitch: 'LLM agents increasingly act on production systems that cannot undo or de-duplicate their actions. Consumer publishing platforms offer no idempotency key for web submission, so an agent that retries an uncertain upload can publish twice. Following Design Science Research, this study designs and evaluates a four-layer governance architecture implemented in two open-source artifacts, Agentic Work Lab and PublishOps CrossCast: a deterministic admission gate, binding of each release command to the digest of the reviewed plan, a receipt state machine that is saved as UNKNOWN before the single final submission and never re-submits automatically, and independent read-back with escalation to a person.',
    fit: 'For JSS readers, the contribution is a reusable engineering pattern for agentic release operations: at-most-once submission with an explicit proposition and its assumptions, mapped to concrete code and an offline test suite, together with a reproducible evidence package.' },
  { key: 'B', journal: 'IEEE Transactions on Engineering Management', publisher: 'IEEE', to: 'The Editor-in-Chief, IEEE Transactions on Engineering Management',
    pitch: 'Engineering managers who delegate delivery work to AI agents must decide who may authorize an irreversible step and on what evidence. Following Design Science Research and stage-gate control theory, this study formalizes four software-enforced governance layers for human–AI collaborative delivery and implements them in two open-source artifacts, Agentic Work Lab and PublishOps CrossCast. The study reads stage gates as commit protocols: admission control at the gate, content binding of approvals, at-most-once commitment, end-to-end verification, and human resolution of in-doubt outcomes.',
    fit: 'For IEEE TEM readers, the contribution links technology-management theory on stage gates, configuration management and human–automation authority to an operational design whose behaviour can be checked, and it shows where human work moves when automation is made safe.' },
  { key: 'C', journal: 'International Journal of Project Management', publisher: 'Elsevier', to: 'The Editors, International Journal of Project Management',
    pitch: 'Project governance research separates the framework that assigns decision rights from the management of execution. When AI agents execute project work, that separation must hold at the moment of action. Following Design Science Research, this study designs and evaluates a human-in-the-loop governance framework for multi-agent, multi-platform delivery, implemented in two open-source artifacts, Agentic Work Lab and PublishOps CrossCast, and proposes reading stage gates as commit protocols in which approvals bind to content and irreversible steps happen at most once.',
    fit: 'For IJPM readers, the contribution extends project governance and stage-gate theory to AI-executed project work and demonstrates a reporting protocol that keeps descriptive project evidence apart from causal claims about AI.' }
];
const letterMd = l => `## Option ${l.key}: ${l.journal} (${l.publisher})

**Date:** November 2026
**To:** ${l.to}
**Subject:** Submission of an original research article — "${title}"

Dear Editor,

Please consider the enclosed manuscript, "${title}", for publication in *${l.journal}*.

${l.pitch}

${shared.evidence}

${l.fit}

${shared.honesty}

${shared.close}

Sincerely,
HuaiCheng Sheng
Agentic Work Lab, Hangzhou Chengxin Holding Group Co., Ltd., Hangzhou, Zhejiang, China
E-mail: ZackSheng@gmail.com | ORCID: 0009-0003-2900-6377
`;
const coverLettersMd = `# Cover Letters for Three Target Journals

${letters.map(letterMd).join('\n---\n\n')}`;
fs.writeFileSync(path.join(paperDir, 'cover_letters_3_journals.md'), coverLettersMd, 'utf8');
fs.writeFileSync(path.join(paperDir, 'cover_letters_3_print.html'), `<!DOCTYPE html><html><head><meta charset="utf-8"><style>${baseCss}</style></head><body>
${letters.map((l, i) => `${i ? '<div class="page-break"></div>' : ''}
<h1>Cover Letter — ${esc(l.journal)}</h1>
<p><strong>Date:</strong> November 2026<br><strong>To:</strong> ${esc(l.to)}<br><strong>Subject:</strong> Submission of an original research article — &ldquo;${esc(title)}&rdquo;</p>
<p>Dear Editor,</p>
<p>Please consider the enclosed manuscript, &ldquo;${esc(title)}&rdquo;, for publication in <em>${esc(l.journal)}</em>.</p>
<p>${esc(l.pitch)}</p>
<p>${esc(shared.evidence)}</p>
<p>${esc(l.fit)}</p>
<p>${esc(shared.honesty)}</p>
<p>${esc(shared.close)}</p>
<p>Sincerely,<br>HuaiCheng Sheng<br>Agentic Work Lab, Hangzhou Chengxin Holding Group Co., Ltd., Hangzhou, Zhejiang, China<br>E-mail: ZackSheng@gmail.com | ORCID: 0009-0003-2900-6377</p>`).join('\n')}
</body></html>`, 'utf8');
htmlToPdf(path.join(paperDir, 'cover_letters_3_print.html'), path.join(paperDir, 'cover_letters_3_journals.pdf'));

// 5. Journal-neutral cover letter (cover_letter.md, cover_letter_template.md, cover_letter.pdf)
const generic = { key: '', journal: '[Journal name]', publisher: '', to: 'The Editor-in-Chief, [Journal name]',
  pitch: letters[0].pitch,
  fit: 'The contribution is a governance design for AI-executed project work whose behaviour can be checked: deterministic gates, approvals bound to content, at-most-once submission with human reconciliation, and a reporting protocol that keeps descriptive evidence apart from causal claims.' };
const genericMd = letterMd(generic).replace('## Option : [Journal name] ()', '# Cover Letter (journal-neutral template)');
fs.writeFileSync(path.join(paperDir, 'cover_letter.md'), genericMd, 'utf8');
fs.writeFileSync(path.join(paperDir, 'cover_letter_template.md'), genericMd, 'utf8');
fs.writeFileSync(path.join(paperDir, 'cover_letter_print.html'), `<!DOCTYPE html><html><head><meta charset="utf-8"><style>${baseCss}</style></head><body>
<h1>Cover Letter</h1>
<p><strong>Date:</strong> November 2026<br><strong>To:</strong> ${esc(generic.to)}<br><strong>Subject:</strong> Submission of an original research article &mdash; &ldquo;${esc(title)}&rdquo;</p>
<p>Dear Editor,</p>
<p>Please consider the enclosed manuscript, &ldquo;${esc(title)}&rdquo;, for publication in <em>${esc(generic.journal)}</em>.</p>
${[generic.pitch, shared.evidence, generic.fit, shared.honesty, shared.close].map(p => `<p>${esc(p)}</p>`).join('\n')}
<p>Sincerely,<br>HuaiCheng Sheng<br>Agentic Work Lab, Hangzhou Chengxin Holding Group Co., Ltd., Hangzhou, Zhejiang, China<br>E-mail: ZackSheng@gmail.com | ORCID: 0009-0003-2900-6377</p>
</body></html>`, 'utf8');
htmlToPdf(path.join(paperDir, 'cover_letter_print.html'), path.join(paperDir, 'cover_letter.pdf'));

console.log('Generated highlights, graphical abstract, declarations and three cover letters in', paperDir);
