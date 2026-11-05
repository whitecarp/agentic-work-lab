import fs from 'fs';
import path from 'path';
import { execFileSync } from 'child_process';
import crypto from 'crypto';

const paperDir = 'C:\\Users\\ROG\\.gemini\\antigravity\\scratch\\agentic-sci-paper\\paper';
const repoPaperDir = 'C:\\Users\\ROG\\.gemini\\antigravity\\scratch\\agentic-sci-paper\\agentic-work-lab\\paper';

const edgePaths = [
  'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe',
  'C:\\Program Files\\Microsoft\\Edge\\Application\\msedge.exe',
  'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe'
];
const browserExe = edgePaths.find(p => fs.existsSync(p));
if (!browserExe) {
  throw new Error('No Chromium browser found for PDF compilation');
}

function htmlToPdf(htmlPath, pdfPath, landscape = false) {
  const fileUrl = 'file:///' + htmlPath.replace(/\\/g, '/');
  const args = [
    '--headless',
    '--disable-gpu',
    '--no-pdf-header-footer',
    `--print-to-pdf=${pdfPath}`
  ];
  if (landscape) args.push('--print-to-pdf-no-header');
  args.push(fileUrl);
  execFileSync(browserExe, args, { stdio: 'ignore' });
}

const baseCss = `
  @page { size: A4 portrait; margin: 18mm 18mm 18mm 18mm; }
  body {
    font-family: "Times New Roman", "Noto Serif SC", "Songti SC", "SimSun", Georgia, serif;
    font-size: 10.5pt;
    line-height: 1.52;
    color: #111827;
    margin: 0;
    padding: 0;
  }
  h1 { font-size: 16pt; font-weight: bold; color: #0f172a; margin: 0 0 8pt 0; border-bottom: 1.5pt solid #0f172a; padding-bottom: 6pt; }
  h2 { font-size: 12.5pt; font-weight: bold; color: #1e3a8a; margin: 14pt 0 6pt 0; border-bottom: 0.5pt solid #cbd5e1; padding-bottom: 3pt; }
  h3 { font-size: 11pt; font-weight: bold; color: #0f172a; margin: 10pt 0 4pt 0; }
  p { margin: 0 0 7pt 0; text-align: justify; }
  ul, ol { margin: 0 0 8pt 0; padding-left: 18pt; }
  li { margin-bottom: 4pt; }
  table { width: 100%; border-collapse: collapse; margin: 8pt 0 12pt 0; font-size: 9.2pt; }
  th { border-top: 1.5pt solid #0f172a; border-bottom: 1pt solid #0f172a; padding: 5pt 6pt; text-align: left; background: #f8fafc; font-weight: bold; }
  td { border-bottom: 0.5pt solid #cbd5e1; padding: 4.5pt 6pt; vertical-align: top; }
  tr:last-child td { border-bottom: 1.5pt solid #0f172a; }
  .box { border: 1pt solid #94a3b8; background: #f8fafc; padding: 8pt 10pt; border-radius: 4pt; margin: 8pt 0; }
  .badge { display: inline-block; background: #1e3a8a; color: #fff; font-size: 8.5pt; padding: 1.5pt 6pt; border-radius: 3pt; font-weight: bold; }
  code { font-family: "Consolas", "Courier New", monospace; font-size: 9pt; background: #f1f5f9; padding: 1pt 3pt; border-radius: 2pt; }
  .page-break { page-break-before: always; }
`;

// ============================================================================
// 1. ELSEVIER STRICT HIGHLIGHTS (<= 85 CHARS PER BULLET INCLUDING SPACES)
// ============================================================================
const highlightsBullets = [
  "Four-layer stage-gate architecture governs human-AI project handoffs.",     // 69 chars
  "Idempotent receipt state machine blocks duplicate releases on timeout.",    // 70 chars
  "Cryptographic plan-hash and SHA-256 gates lock 30 release artifacts.",      // 68 chars
  "Post-submit confirmation waits consume 92.32% of browser release time.",    // 70 chars
  "Epistemic audit rules separate cohort metrics (N=55) from causation."       // 68 chars
];

for (const b of highlightsBullets) {
  if (b.length > 85) {
    throw new Error(`Highlight exceeds 85 chars (${b.length}): ${b}`);
  }
}

const highlightsMd = `# Research Highlights (Elsevier Strict <= 85 Characters Standard)

**Manuscript Title:** Governing Human–AI Collaborative Project Workflows in Multi-Platform Digital Delivery: A Design Science Study of Auditable Handoffs and Idempotent Release Operations  
**Author:** HuaiCheng Sheng (Agentic Work Lab, Hangzhou Chengxin Holding Group Co., Ltd.)

## Highlights (Upload as \`Highlights\` item in Elsevier Editorial Manager)

${highlightsBullets.map(b => `- ${b}`).join('\n')}

---

## Character-Count Verification Audit (Limit: <= 85 characters including spaces)

| # | Highlight Bullet Text | Character Count (with spaces) | Status |
| :--- | :--- | :---: | :---: |
${highlightsBullets.map((b, i) => `| ${i + 1} | \`${b}\` | **${b.length} / 85** | PASS |`).join('\n')}
`;

fs.writeFileSync(path.join(paperDir, 'highlights.md'), highlightsMd, 'utf8');

const highlightsHtml = `<!DOCTYPE html><html><head><meta charset="utf-8"><style>${baseCss}</style></head><body>
<h1>Research Highlights (Elsevier Standard &le; 85 Characters)</h1>
<p><strong>Title:</strong> Governing Human–AI Collaborative Project Workflows in Multi-Platform Digital Delivery: A Design Science Study of Auditable Handoffs and Idempotent Release Operations</p>
<p><strong>Author:</strong> HuaiCheng Sheng (Agentic Work Lab, Hangzhou Chengxin Holding Group Co., Ltd., Hangzhou, Zhejiang, China | ORCID: <code>0009-0003-2900-6377</code>)</p>
<h2>Highlights</h2>
<ul>
${highlightsBullets.map(b => `<li>${b}</li>`).join('\n')}
</ul>
<h2>Strict Character-Count Verification Table (Elsevier Maximum: 85 Characters Including Spaces)</h2>
<table>
<thead><tr><th>#</th><th>Highlight Bullet</th><th>Char Count</th><th>Compliance</th></tr></thead>
<tbody>
${highlightsBullets.map((b, i) => `<tr><td>${i + 1}</td><td><code>${b}</code></td><td><strong>${b.length} / 85</strong></td><td>PASS (&le; 85 chars)</td></tr>`).join('\n')}
</tbody>
</table>
</body></html>`;
const highlightsHtmlPath = path.join(paperDir, 'highlights_print.html');
fs.writeFileSync(highlightsHtmlPath, highlightsHtml, 'utf8');
htmlToPdf(highlightsHtmlPath, path.join(paperDir, 'highlights.pdf'));

// ============================================================================
// 2. GRAPHICAL ABSTRACT (SVG + 1-PAGE LANDSCAPE PDF)
// ============================================================================
const graphicalAbstractSvg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1328 531" width="100%" height="100%" font-family="Arial, Helvetica, sans-serif">
  <defs>
    <linearGradient id="bgGrad" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0%" stop-color="#f8fafc"/>
      <stop offset="100%" stop-color="#eff6ff"/>
    </linearGradient>
    <marker id="arrow" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="6" markerHeight="6" orient="auto-start-reverse">
      <path d="M 0 1 L 10 5 L 0 9 z" fill="#1e3a8a"/>
    </marker>
  </defs>
  <rect width="1328" height="531" fill="url(#bgGrad)" stroke="#0f172a" stroke-width="3" rx="10"/>
  
  <!-- Header Banner -->
  <rect x="16" y="14" width="1296" height="54" rx="6" fill="#0f172a"/>
  <text x="664" y="38" fill="#ffffff" font-size="17" font-weight="bold" text-anchor="middle">GRAPHICAL ABSTRACT: Governing Human–AI Collaborative Project Workflows in Multi-Platform Digital Delivery</text>
  <text x="664" y="58" fill="#93c5fd" font-size="12" text-anchor="middle">HuaiCheng Sheng (Agentic Work Lab, Hangzhou Chengxin Holding Group Co., Ltd.) | Design Science Research + Stage-Gate Control + Idempotent State Machine</text>

  <!-- Column 1: Problem Space -->
  <rect x="20" y="82" width="290" height="430" rx="8" fill="#ffffff" stroke="#dc2626" stroke-width="2"/>
  <rect x="20" y="82" width="290" height="36" rx="8" fill="#dc2626"/>
  <text x="165" y="105" fill="#ffffff" font-size="14" font-weight="bold" text-anchor="middle">1. UNGOVERNED MULTI-AGENT RISKS</text>

  <rect x="36" y="132" width="258" height="105" rx="6" fill="#fef2f2" stroke="#fca5a5"/>
  <text x="165" y="152" fill="#991b1b" font-size="13" font-weight="bold" text-anchor="middle">Handoff Drift &amp; Role Ambiguity</text>
  <text x="46" y="174" fill="#7f1d1d" font-size="11.5">• Asynchronous agents (GPT, Claude,</text>
  <text x="46" y="191" fill="#7f1d1d" font-size="11.5">  DeepSeek) overwrite shared state</text>
  <text x="46" y="208" fill="#7f1d1d" font-size="11.5">• Out-of-window autonomous writes</text>
  <text x="46" y="225" fill="#7f1d1d" font-size="11.5">• Missing ownership &amp; acceptance gates</text>

  <rect x="36" y="250" width="258" height="115" rx="6" fill="#fef2f2" stroke="#fca5a5"/>
  <text x="165" y="270" fill="#991b1b" font-size="13" font-weight="bold" text-anchor="middle">Non-Idempotent Retry Faults</text>
  <text x="46" y="292" fill="#7f1d1d" font-size="11.5">• Hybrid REST API &amp; Browser DOM timeouts</text>
  <text x="46" y="309" fill="#7f1d1d" font-size="11.5">• 92.32% latency in post-submit wait</text>
  <text x="46" y="326" fill="#7f1d1d" font-size="11.5">• Naive retries trigger duplicate posts</text>
  <text x="46" y="343" fill="#7f1d1d" font-size="11.5">• CAPTCHA / expired cookies crash flow</text>

  <rect x="36" y="378" width="258" height="116" rx="6" fill="#fef2f2" stroke="#fca5a5"/>
  <text x="165" y="398" fill="#991b1b" font-size="13" font-weight="bold" text-anchor="middle">Epistemic &amp; Metric Confounding</text>
  <text x="46" y="420" fill="#7f1d1d" font-size="11.5">• Exposure-time bias across cohorts</text>
  <text x="46" y="437" fill="#7f1d1d" font-size="11.5">• Legacy (3,477 views) vs AI series</text>
  <text x="46" y="454" fill="#7f1d1d" font-size="11.5">  (95.89 views, p = 0.0012)</text>
  <text x="46" y="471" fill="#7f1d1d" font-size="11.5">• Unverified causal attribution claims</text>

  <!-- Arrow 1 -> 2 -->
  <line x1="312" y1="297" x2="342" y2="297" stroke="#1e3a8a" stroke-width="3" marker-end="url(#arrow)"/>

  <!-- Column 2: 4-Layer Architecture -->
  <rect x="346" y="82" width="560" height="430" rx="8" fill="#ffffff" stroke="#1e3a8a" stroke-width="2.5"/>
  <rect x="346" y="82" width="560" height="36" rx="8" fill="#1e3a8a"/>
  <text x="626" y="105" fill="#ffffff" font-size="14" font-weight="bold" text-anchor="middle">2. FOUR-LAYER STAGE-GATE GOVERNANCE ARCHITECTURE</text>

  <!-- Layer 1 -->
  <rect x="364" y="130" width="524" height="82" rx="6" fill="#eff6ff" stroke="#3b82f6" stroke-width="1.5"/>
  <text x="376" y="150" fill="#1e3a8a" font-size="12.2" font-weight="bold">Layer 1: Sole-Operator Authority &amp; Deterministic Scheduling Gate (Alg. 1: planRun)</text>
  <text x="376" y="170" fill="#1e293b" font-size="10.6">• Role partitioning: Human PM (Sole Operator) + GPT/Codex (QA) + Claude (UI) + DeepSeek (Monitor)</text>
  <text x="376" y="188" fill="#1e293b" font-size="10.6">• Pure predicate G_run = I_enabled ∧ I_window ∧ I_cooldown ∧ I_checklist (Thm A.1: 0 unauthorized writes)</text>

  <!-- Layer 2 -->
  <rect x="364" y="222" width="524" height="82" rx="6" fill="#f0fdf4" stroke="#16a34a" stroke-width="1.5"/>
  <text x="376" y="242" fill="#14532d" font-size="12.2" font-weight="bold">Layer 2: Cryptographic Plan-Hash Locking &amp; Stream SHA-256 Gate (Theorem A.3)</text>
  <text x="376" y="262" fill="#1e293b" font-size="10.6">• 64-KB chunked SHA-256 digest H(f) + deterministic JSON plan hash H_plan = SHA256(Canon(M, T, H(f)))</text>
  <text x="376" y="280" fill="#1e293b" font-size="10.6">• Execution blocked unless token matches (--confirm CURRENT_PLAN_HASH); locks 30 artifacts (2.21 GB)</text>

  <!-- Layer 3 -->
  <rect x="364" y="314" width="524" height="92" rx="6" fill="#fffbeb" stroke="#d97706" stroke-width="1.5"/>
  <text x="376" y="334" fill="#78350f" font-size="12.2" font-weight="bold">Layer 3: Idempotent Receipt State Machine &amp; UNKNOWN Latching (Alg. 2 &amp; Thm A.2)</text>
  <text x="376" y="354" fill="#1e293b" font-size="10.6">• States: INIT → DRY_RUN → PREFLIGHT → SUBMITTING → {PUBLISHED, UNKNOWN, NEEDS_HUMAN, FAILED}</text>
  <text x="376" y="372" fill="#1e293b" font-size="10.6">• Post-submit read-back timeout latches receipt in UNKNOWN &amp; suppresses auto-retries (At-Most-Once</text>
  <text x="376" y="390" fill="#1e293b" font-size="10.6">  External Publication under K ≥ 1 recovery rounds; prevented duplicate post on Douyin)</text>

  <!-- Layer 4 -->
  <rect x="364" y="416" width="524" height="82" rx="6" fill="#f5f3ff" stroke="#7c3aed" stroke-width="1.5"/>
  <text x="376" y="436" fill="#4c1d95" font-size="12.2" font-weight="bold">Layer 4: Read-Back Verification, HITL Handoff &amp; Epistemic Audit Gate (Alg. 3)</text>
  <text x="376" y="456" fill="#1e293b" font-size="10.6">• Independent URL/ID read-back verification + structured NEEDS_HUMAN handoff for CAPTCHA / auth expiry</text>
  <text x="376" y="474" fill="#1e293b" font-size="10.6">• Mann–Whitney U cohort stratification (N = 55) + 4 mandatory epistemic hygiene rules</text>

  <!-- Arrow 2 -> 3 -->
  <line x1="908" y1="297" x2="938" y2="297" stroke="#1e3a8a" stroke-width="3" marker-end="url(#arrow)"/>

  <!-- Column 3: Empirical Evaluation -->
  <rect x="942" y="82" width="366" height="430" rx="8" fill="#ffffff" stroke="#0f766e" stroke-width="2"/>
  <rect x="942" y="82" width="366" height="36" rx="8" fill="#0f766e"/>
  <text x="1125" y="105" fill="#ffffff" font-size="14" font-weight="bold" text-anchor="middle">3. EMPIRICAL VALIDATION &amp; EVIDENCE</text>

  <rect x="958" y="130" width="334" height="86" rx="6" fill="#f0fdfa" stroke="#5eead4"/>
  <text x="970" y="150" fill="#115e59" font-size="12.5" font-weight="bold">Longitudinal Multi-Agent Governance (N = 69)</text>
  <text x="970" y="169" fill="#134e4a" font-size="11.5">• 68 non-write checkpoints blocked (100% gate accuracy)</text>
  <text x="970" y="186" fill="#134e4a" font-size="11.5">• 1 authorized write; 0 duplicate replays; 0 new risks</text>
  <text x="970" y="203" fill="#134e4a" font-size="11.5">• Heartbeat delay: 24.992 s | Run time: 31.175 s</text>

  <rect x="958" y="226" width="334" height="90" rx="6" fill="#f0fdfa" stroke="#5eead4"/>
  <text x="970" y="246" fill="#115e59" font-size="12.5" font-weight="bold">5-Platform Hybrid Release Telemetry</text>
  <text x="970" y="265" fill="#134e4a" font-size="11.5">• Bilibili, YouTube, X, Douyin, Instagram verified</text>
  <text x="970" y="282" fill="#134e4a" font-size="11.5">• Instagram E2E: 423.236 s (380.551 s / 92.32% in</text>
  <text x="970" y="299" fill="#134e4a" font-size="11.5">  post-submit wait window → primary retry hazard)</text>

  <rect x="958" y="326" width="334" height="84" rx="6" fill="#f0fdfa" stroke="#5eead4"/>
  <text x="970" y="346" fill="#115e59" font-size="12.5" font-weight="bold">Software &amp; Media Verification Scale</text>
  <text x="970" y="365" fill="#134e4a" font-size="11.5">• 123 Python source modules (27,095 LOC)</text>
  <text x="970" y="382" fill="#134e4a" font-size="11.5">• 126 test modules (37,155 LOC; 2,737 unit tests)</text>
  <text x="970" y="399" fill="#134e4a" font-size="11.5">• 30 SHA-256 locked video releases (2.21 GB)</text>

  <rect x="958" y="420" width="334" height="78" rx="6" fill="#f0fdfa" stroke="#5eead4"/>
  <text x="970" y="440" fill="#115e59" font-size="12.5" font-weight="bold">Reproducible Open-Science Compendium</text>
  <text x="970" y="459" fill="#134e4a" font-size="11.5">• 21-Page Main Manuscript + 150-Page Online</text>
  <text x="970" y="476" fill="#134e4a" font-size="11.5">  Supplementary Material (Appendices A–H)</text>
</svg>`;

fs.writeFileSync(path.join(paperDir, 'graphical_abstract.svg'), graphicalAbstractSvg, 'utf8');

const graphicalAbstractHtml = `<!DOCTYPE html><html><head><meta charset="utf-8"><style>
  @page { size: A4 landscape; margin: 10mm; }
  body { margin: 0; padding: 0; display: flex; flex-direction: column; justify-content: center; align-items: center; font-family: "Times New Roman", serif; }
  .container { width: 100%; max-width: 277mm; }
</style></head><body>
  <div class="container">${graphicalAbstractSvg}</div>
</body></html>`;
const gaHtmlPath = path.join(paperDir, 'graphical_abstract_print.html');
fs.writeFileSync(gaHtmlPath, graphicalAbstractHtml, 'utf8');
htmlToPdf(gaHtmlPath, path.join(paperDir, 'graphical_abstract.pdf'), true);

// ============================================================================
// 3. ELSEVIER / IEEE MANDATORY DECLARATIONS, CRediT & CODE METADATA TABLE
// ============================================================================
const declarationsMd = `# Mandatory Author Declarations, CRediT Statement & Software Metadata
*(Required for Elsevier Editorial Manager — JSS / IJPM / SoftwareX — and IEEE Author Portal)*

## 1. Manuscript & Author Metadata
- **Title:** Governing Human–AI Collaborative Project Workflows in Multi-Platform Digital Delivery: A Design Science Study of Auditable Handoffs and Idempotent Release Operations
- **Author:** HuaiCheng Sheng
- **Affiliation:** Agentic Work Lab, Hangzhou Chengxin Holding Group Co., Ltd., Hangzhou, Zhejiang, China
- **Corresponding Author Email:** \`ZackSheng@gmail.com\`
- **ORCID iD:** \`0009-0003-2900-6377\` (\`https://orcid.org/0009-0003-2900-6377\`)

---

## 2. CRediT Author Statement
**HuaiCheng Sheng:** Conceptualization, Methodology, Software, Validation, Formal analysis, Investigation, Resources, Data Curation, Writing – Original Draft, Writing – Review & Editing, Visualization, Supervision, Project administration.

---

## 3. Declaration of Competing Interest
The author declares that he has no known competing financial interests or personal relationships that could have appeared to influence the work reported in this paper.

---

## 4. Funding Source Declaration
This research was supported by the internal research and engineering resources of Agentic Work Lab, Hangzhou Chengxin Holding Group Co., Ltd., Hangzhou, Zhejiang, China. It did not receive any specific grant from external public or commercial funding agencies.

---

## 5. Ethics & Human Subjects Declaration
Not applicable. This study investigates software engineering governance protocols, automated release state machines, and de-identified operational and public channel telemetry. No human subjects experimentation, personally identifiable private user data, or clinical trials were conducted. All sensitive credentials, session cookies, and merchant details were rigorously sanitized and audited via automated leakage tests (\`npm run privacy:check\`).

---

## 6. Data, Code & Reproducibility Availability Statement
All source code, JSON schemas, verification scripts, de-identified operational telemetry ($N=69$ events), channel snapshot datasets ($N=55$ records), and media SHA-256 manifests ($N=30$ versions) are permanently archived and publicly accessible:
1. **Governance & Empirical Benchmark Repository (\`agentic-work-lab\`):** \`https://github.com/whitecarp/agentic-work-lab\` (Manuscript & reproduction package on branch \`paper/sci-manuscript\`).
2. **Release Operations Engine Repository (\`publishops-crosscast\`):** \`https://github.com/whitecarp/publishops-crosscast\`.
3. **One-Command Table Reproduction:** Execute \`node paper/reproduce_paper_tables.mjs\` to deterministically regenerate all statistical tables from raw JSON receipts.

---

## 7. Standard Software & Code Metadata Table (Elsevier *JSS* / *SoftwareX* Open Science Standard)

| Nr. | Code Metadata Description | Specification |
| :--- | :--- | :--- |
| **C1** | Current code version | \`v0.1.0\` (\`publishops-crosscast\`) / \`v1.0.0\` (\`agentic-work-lab\`) |
| **C2** | Permanent link to code/repository | \`https://github.com/whitecarp/agentic-work-lab\` and \`https://github.com/whitecarp/publishops-crosscast\` |
| **C3** | Permanent link to reproducible capsule / paper branch | \`https://github.com/whitecarp/agentic-work-lab/tree/paper/sci-manuscript\` |
| **C4** | Legal Code License | MIT License |
| **C5** | Code versioning system used | Git / GitHub |
| **C6** | Software code languages, tools, and services used | Python 3.11+ (\`27,095 LOC\` src; \`37,155 LOC\` tests across \`2,737\` test functions), Node.js 20+ (ESM verification scripts), Playwright, Typer, Pydantic, Rich |
| **C7** | Compilation requirements & operating environments | Cross-platform (Windows 10/11, Linux, macOS); Python >= 3.11, Node.js >= 20 |
| **C8** | Link to developer documentation / manual | \`docs/operations-governance.md\`, \`docs/validation-guide.md\`, and 150-page \`supplementary_material.pdf\` (Appendices A–H) |
| **C9** | Support email for questions | \`ZackSheng@gmail.com\` |
`;

fs.writeFileSync(path.join(paperDir, 'declarations_and_credit.md'), declarationsMd, 'utf8');

const declarationsHtml = `<!DOCTYPE html><html><head><meta charset="utf-8"><style>${baseCss}</style></head><body>
<h1>Mandatory Author Declarations, CRediT Statement &amp; Software Metadata</h1>
<p><strong>Manuscript Title:</strong> Governing Human–AI Collaborative Project Workflows in Multi-Platform Digital Delivery: A Design Science Study of Auditable Handoffs and Idempotent Release Operations</p>
<p><strong>Author:</strong> HuaiCheng Sheng<br/>
<strong>Affiliation:</strong> Agentic Work Lab, Hangzhou Chengxin Holding Group Co., Ltd., Hangzhou, Zhejiang, China<br/>
<strong>Email:</strong> <code>ZackSheng@gmail.com</code> | <strong>ORCID:</strong> <code>0009-0003-2900-6377</code></p>

<h2>1. CRediT Author Statement</h2>
<p><strong>HuaiCheng Sheng:</strong> Conceptualization, Methodology, Software, Validation, Formal analysis, Investigation, Resources, Data Curation, Writing – Original Draft, Writing – Review &amp; Editing, Visualization, Supervision, Project administration.</p>

<h2>2. Declaration of Competing Interest</h2>
<p>The author declares that he has no known competing financial interests or personal relationships that could have appeared to influence the work reported in this paper.</p>

<h2>3. Funding Source Declaration</h2>
<p>This research was supported by the internal research and engineering resources of Agentic Work Lab, Hangzhou Chengxin Holding Group Co., Ltd., Hangzhou, Zhejiang, China. It did not receive any specific grant from external public or commercial funding agencies.</p>

<h2>4. Ethics &amp; Privacy Sanitization Statement</h2>
<p>Not applicable (no human subjects or clinical data). All operational records and public channel metrics are de-identified and verified via automated privacy and schema gates (<code>npm run privacy:check</code> and <code>npm test</code>).</p>

<h2>5. Open Science &amp; Reproducibility Artifact Statement</h2>
<p>All source code, schemas, raw JSON receipts, and automated reproduction scripts are publicly archived at <code>https://github.com/whitecarp/agentic-work-lab</code> (branch <code>paper/sci-manuscript</code>) and <code>https://github.com/whitecarp/publishops-crosscast</code>.</p>

<h2>6. Official Software Metadata Table (Elsevier JSS / SoftwareX Standard)</h2>
<table>
<thead><tr><th>Nr.</th><th>Code Metadata Description</th><th>Specification</th></tr></thead>
<tbody>
<tr><td><strong>C1</strong></td><td>Current code version</td><td><code>v0.1.0</code> (<code>publishops-crosscast</code>) / <code>v1.0.0</code> (<code>agentic-work-lab</code>)</td></tr>
<tr><td><strong>C2</strong></td><td>Permanent link to repositories</td><td><code>https://github.com/whitecarp/agentic-work-lab</code> &amp; <code>https://github.com/whitecarp/publishops-crosscast</code></td></tr>
<tr><td><strong>C3</strong></td><td>Reproducible paper package branch</td><td><code>https://github.com/whitecarp/agentic-work-lab/tree/paper/sci-manuscript</code></td></tr>
<tr><td><strong>C4</strong></td><td>Legal Code License</td><td>MIT License</td></tr>
<tr><td><strong>C5</strong></td><td>Versioning system</td><td>Git / GitHub</td></tr>
<tr><td><strong>C6</strong></td><td>Languages &amp; verification scale</td><td>Python 3.11+ (123 source modules, 27,095 LOC; 126 test modules, 37,155 LOC; 2,737 unit tests) &amp; Node.js 20+</td></tr>
<tr><td><strong>C7</strong></td><td>Operating environments</td><td>Windows, Linux, macOS</td></tr>
<tr><td><strong>C8</strong></td><td>Documentation &amp; Compendium</td><td>66 specification documents + 150-page <code>supplementary_material.pdf</code> (Appendices A–H)</td></tr>
<tr><td><strong>C9</strong></td><td>Support contact</td><td><code>ZackSheng@gmail.com</code></td></tr>
</tbody>
</table>
</body></html>`;
const declHtmlPath = path.join(paperDir, 'declarations_print.html');
fs.writeFileSync(declHtmlPath, declarationsHtml, 'utf8');
htmlToPdf(declHtmlPath, path.join(paperDir, 'declarations_and_credit.pdf'));

// ============================================================================
// 4. THREE CUSTOMIZED COVER LETTERS FOR TARGET SCI Q1 JOURNALS
// ============================================================================
const coverLettersMd = `# Customized SCI Submission Cover Letters for Top 3 Target Journals

This package contains three tailored Cover Letters ready for direct upload depending on which SCI journal you choose first:
1. **Option A (Primary Software & Systems Engineering Q1 Target):** *Journal of Systems and Software (JSS)* (Elsevier)
2. **Option B (Primary Project & Engineering Management Q1 Target):** *IEEE Transactions on Engineering Management (IEEE TEM)* (IEEE)
3. **Option C (Project Management Flagship Q1 Target):** *International Journal of Project Management (IJPM)* (Elsevier)

---

## Option A: Cover Letter for *Journal of Systems and Software (JSS)*

**Date:** November 5, 2026  
**To:** The Editors-in-Chief, *Journal of Systems and Software* (Elsevier)  
**Subject:** Original Research Manuscript Submission — *"Governing Human–AI Collaborative Project Workflows in Multi-Platform Digital Delivery: A Design Science Study of Auditable Handoffs and Idempotent Release Operations"*

Dear Editors-in-Chief,

Please find enclosed our original research manuscript entitled **"Governing Human–AI Collaborative Project Workflows in Multi-Platform Digital Delivery: A Design Science Study of Auditable Handoffs and Idempotent Release Operations"** for consideration as a full-length research article in the *Journal of Systems and Software*.

As heterogeneous Large Language Model (LLM) agents (e.g., GPT/Codex, Claude, and DeepSeek) transition into active execution roles in software delivery and digital release pipelines, systems engineering faces three severe reliability hazards: (1) uncoordinated multi-agent handoff drift, (2) non-idempotent external side effects (duplicate publications) when hybrid REST API and browser-automated endpoints experience post-submission verification timeouts, and (3) confounded performance attribution.

Following the Design Science Research (DSR) paradigm, this study designs, formalizes, and empirically evaluates a four-layer governance architecture (*Agentic Work Lab* and *PublishOps CrossCast*, comprising **27,095 LOC** of production Python source across **123 modules** and **37,155 LOC** of automated tests across **126 test modules / 2,737 unit tests**). Our work makes four primary contributions aligned with the scope of *JSS*:
1. **Deterministic Stage-Gate Handoff Invariants (RQ1):** We formalize a pure scheduling gate (\`planRun\`, Algorithm 1) and cryptographic plan-hash locking (\`--confirm CURRENT_PLAN_HASH\`, Theorem A.3) that eliminated unauthorized writes across **$N=69$ longitudinal operational events** ($68$ non-write gates enforced, $1$ authorized write, $0$ duplicate replays) and locked **$N=30$ video delivery artifacts ($2.21\\text{ GB}$)**.
2. **Idempotent Receipt State Machine under \`UNKNOWN\` States (RQ2):** We prove and empirically validate an eight-state release state machine (Algorithm 2, Theorem A.2) that latches uncertain post-submission outcomes into \`UNKNOWN\` rather than triggering naive retries, successfully preventing duplicate publication on Douyin during a live read-back timeout.
3. **Phase-Level Release Latency Decomposition (RQ3):** Across five platforms (Bilibili, YouTube, X, Douyin, and Instagram), millisecond-level decomposition on Instagram ($423.236\\text{ s}$ end-to-end) reveals that **$92.32\\%$ ($380.551\\text{ s}$)** of command duration resides in post-submission platform confirmation waits—identifying the exact vulnerability window for non-idempotent agent retries.
4. **Complete Reproducible Open-Science Package (172 Pages Total):** Alongside the **21-page Main Manuscript**, we provide a **150-page Online Supplementary Material & Full Empirical Compendium** (Appendices A–H) containing complete mathematical proofs, all 69 operational logs, all 55 channel records, all 30 64-character SHA-256 digests, all 2,737 test function identifiers, 66 specification documents, and 14 raw JSON audit receipts.

This manuscript is original, has not been published previously, and is not under consideration elsewhere. Thank you for your time and consideration.

Sincerely,  
**HuaiCheng Sheng**  
Agentic Work Lab, Hangzhou Chengxin Holding Group Co., Ltd., Hangzhou, Zhejiang, China  
Email: \`ZackSheng@gmail.com\` | ORCID: \`0009-0003-2900-6377\`

---

## Option B: Cover Letter for *IEEE Transactions on Engineering Management (IEEE TEM)*

**Date:** November 5, 2026  
**To:** The Editor-in-Chief, *IEEE Transactions on Engineering Management*  
**Subject:** Manuscript Submission — *"Governing Human–AI Collaborative Project Workflows in Multi-Platform Digital Delivery: A Design Science Study of Auditable Handoffs and Idempotent Release Operations"*

Dear Editor-in-Chief,

We respectfully submit our original manuscript entitled **"Governing Human–AI Collaborative Project Workflows in Multi-Platform Digital Delivery: A Design Science Study of Auditable Handoffs and Idempotent Release Operations"** for peer review in *IEEE Transactions on Engineering Management*.

Engineering and project management organizations are rapidly adopting multi-agent AI workflows, yet classical Stage-Gate project governance models lack enforceable computational mechanisms to prevent asynchronous AI role drift, duplicate external commitments during network timeouts, and confounded productivity claims. Bridging Stage-Gate control theory (Cooper, 1990; Bianchi et al., 2022) with fault-tolerant systems engineering, this Design Science study establishes an auditable human-in-the-loop (HITL) governance framework and evaluates it across **$N=69$ operational events**, **$N=55$ longitudinal channel releases**, **$N=30$ cryptographically verified media versions ($2.21\\text{ GB}$)**, and **$2,737$ automated verification tests**.

In particular, our non-parametric cohort evaluation (Mann–Whitney $U = 65.5, p = 0.0012$, Algorithm 3) demonstrates how engineering managers can separate exposure-age confounding ($3,477.00$ vs. $95.89$ mean views) from genuine operational reliability gains. The submission includes a **21-page Main Manuscript** and a **150-page Online Supplementary Material** archive for full empirical transparency.

This work is original and not under review elsewhere.

Sincerely,  
**HuaiCheng Sheng**  
Agentic Work Lab, Hangzhou Chengxin Holding Group Co., Ltd., Hangzhou, Zhejiang, China  
Email: \`ZackSheng@gmail.com\` | ORCID: \`0009-0003-2900-6377\`

---

## Option C: Cover Letter for *International Journal of Project Management (IJPM)*

**Date:** November 5, 2026  
**To:** The Editor-in-Chief, *International Journal of Project Management*  
**Subject:** Original Research Submission — *"Governing Human–AI Collaborative Project Workflows in Multi-Platform Digital Delivery: A Design Science Study of Auditable Handoffs and Idempotent Release Operations"*

Dear Editor-in-Chief,

We are pleased to submit our manuscript **"Governing Human–AI Collaborative Project Workflows in Multi-Platform Digital Delivery: A Design Science Study of Auditable Handoffs and Idempotent Release Operations"** for consideration in the *International Journal of Project Management*.

Recent project management scholarship highlights urgent questions surrounding how artificial intelligence transforms project governance, accountability, and human–machine agency (Holzmann et al., 2022; Müller et al., 2024; Raisch & Krakowski, 2021). However, empirical studies demonstrating how project managers govern heterogeneous foundation models (GPT/Codex, Claude, and DeepSeek) in live, multi-platform delivery environments remain scarce. Grounded in Design Science Research and Stage-Gate governance theory, our study demonstrates how project managers retain sole-operator authority through four enforceable governance layers: deterministic scheduling gates, cryptographic plan-hash locking, idempotent \`UNKNOWN\`-latching release state machines, and epistemic audit protocols.

The **21-page Main Manuscript** is accompanied by a **150-page Online Supplementary Material** compendium documenting every proof, operational log ($N=69$), cohort record ($N=55$), artifact hash ($N=30$), and verification test ($2,737$ tests). This manuscript is solely submitted to *IJPM*.

Sincerely,  
**HuaiCheng Sheng**  
Agentic Work Lab, Hangzhou Chengxin Holding Group Co., Ltd., Hangzhou, Zhejiang, China  
Email: \`ZackSheng@gmail.com\` | ORCID: \`0009-0003-2900-6377\`
`;

fs.writeFileSync(path.join(paperDir, 'cover_letters_3_journals.md'), coverLettersMd, 'utf8');

const cl3Html = `<!DOCTYPE html><html><head><meta charset="utf-8"><style>${baseCss}</style></head><body>
<h1>Customized SCI Cover Letters for Top 3 Target Q1 Journals</h1>
<p>This document provides three ready-to-use Cover Letters tailored for (A) <em>Journal of Systems and Software (JSS)</em>, (B) <em>IEEE Transactions on Engineering Management (IEEE TEM)</em>, and (C) <em>International Journal of Project Management (IJPM)</em>.</p>
<div class="box">
<h2>Option A: Journal of Systems and Software (Elsevier, SCI Q1)</h2>
<p><strong>To:</strong> The Editors-in-Chief, <em>Journal of Systems and Software</em><br/>
<strong>Subject:</strong> Original Research Submission — <em>"Governing Human–AI Collaborative Project Workflows in Multi-Platform Digital Delivery: A Design Science Study of Auditable Handoffs and Idempotent Release Operations"</em></p>
<p>Dear Editors-in-Chief,</p>
<p>Please find enclosed our original research manuscript entitled <strong>"Governing Human–AI Collaborative Project Workflows in Multi-Platform Digital Delivery: A Design Science Study of Auditable Handoffs and Idempotent Release Operations"</strong> for consideration in the <em>Journal of Systems and Software</em>.</p>
<p>Following the Design Science Research (DSR) paradigm, this study designs, formalizes, and empirically evaluates a four-layer governance architecture (<em>Agentic Work Lab</em> and <em>PublishOps CrossCast</em>, comprising <strong>27,095 LOC</strong> of production Python source across <strong>123 modules</strong> and <strong>37,155 LOC</strong> of automated tests across <strong>126 test modules / 2,737 unit tests</strong>). Across <strong>N = 69</strong> operational checkpoints, <strong>N = 55</strong> channel releases, <strong>N = 30</strong> SHA-256 verified video versions (2.21 GB), and <strong>5 publishing platforms</strong>, we prove that deterministic stage-gate invariants and idempotent <code>UNKNOWN</code>-latching state machines eliminate unauthorized writes and prevent duplicate releases during the post-submission confirmation window (which consumes <strong>92.32%</strong> of browser-mediated command latency).</p>
<p>Alongside the <strong>21-page Main Manuscript</strong>, we submit a <strong>150-page Online Supplementary Material</strong> compendium (Appendices A–H) containing formal proofs, complete datasets, and all 2,737 test identifiers. This manuscript is original and not under consideration elsewhere.</p>
<p>Sincerely,<br/><strong>HuaiCheng Sheng</strong><br/>Agentic Work Lab, Hangzhou Chengxin Holding Group Co., Ltd., Hangzhou, Zhejiang, China<br/>Email: <code>ZackSheng@gmail.com</code> | ORCID: <code>0009-0003-2900-6377</code></p>
</div>

<div class="page-break"></div>
<div class="box">
<h2>Option B: IEEE Transactions on Engineering Management (IEEE, SCI/SSCI Q1)</h2>
<p><strong>To:</strong> The Editor-in-Chief, <em>IEEE Transactions on Engineering Management</em><br/>
<strong>Subject:</strong> Manuscript Submission — <em>"Governing Human–AI Collaborative Project Workflows in Multi-Platform Digital Delivery: A Design Science Study of Auditable Handoffs and Idempotent Release Operations"</em></p>
<p>Dear Editor-in-Chief,</p>
<p>We respectfully submit our original manuscript entitled <strong>"Governing Human–AI Collaborative Project Workflows in Multi-Platform Digital Delivery: A Design Science Study of Auditable Handoffs and Idempotent Release Operations"</strong> for review in <em>IEEE Transactions on Engineering Management</em>.</p>
<p>Bridging Stage-Gate project control theory (Cooper, 1990; Bianchi et al., 2022) with fault-tolerant distributed release engineering, this Design Science study demonstrates how engineering managers can govern heterogeneous AI workflows (GPT/Codex, Claude, DeepSeek) via deterministic scheduling predicates (Algorithm 1), cryptographic plan-hash locks (Theorem A.3), idempotent receipt state machines (Algorithm 2), and non-parametric epistemic audit gates (Algorithm 3; Mann–Whitney U = 65.5, p = 0.0012). The submission comprises a <strong>21-page Main Manuscript</strong> and a <strong>150-page Online Supplementary Material</strong> compendium.</p>
<p>Sincerely,<br/><strong>HuaiCheng Sheng</strong><br/>Agentic Work Lab, Hangzhou Chengxin Holding Group Co., Ltd., Hangzhou, Zhejiang, China<br/>Email: <code>ZackSheng@gmail.com</code> | ORCID: <code>0009-0003-2900-6377</code></p>
</div>

<div class="box">
<h2>Option C: International Journal of Project Management (Elsevier, SSCI/SCI Q1)</h2>
<p><strong>To:</strong> The Editor-in-Chief, <em>International Journal of Project Management</em><br/>
<strong>Subject:</strong> Manuscript Submission — <em>"Governing Human–AI Collaborative Project Workflows in Multi-Platform Digital Delivery: A Design Science Study of Auditable Handoffs and Idempotent Release Operations"</em></p>
<p>Dear Editor-in-Chief,</p>
<p>We are pleased to submit our manuscript <strong>"Governing Human–AI Collaborative Project Workflows in Multi-Platform Digital Delivery: A Design Science Study of Auditable Handoffs and Idempotent Release Operations"</strong> for consideration in the <em>International Journal of Project Management</em>.</p>
<p>Responding to recent calls in project management scholarship on governing human–AI agency (Holzmann et al., 2022; Müller et al., 2024; Raisch &amp; Krakowski, 2021), this study operationalizes Stage-Gate project governance into four computational invariants and validates them across N = 69 multi-agent handoff events, N = 55 publication records, and 5 platforms. Full empirical ledgers are provided in the accompanying <strong>150-page Online Supplementary Material</strong>.</p>
<p>Sincerely,<br/><strong>HuaiCheng Sheng</strong><br/>Agentic Work Lab, Hangzhou Chengxin Holding Group Co., Ltd., Hangzhou, Zhejiang, China<br/>Email: <code>ZackSheng@gmail.com</code> | ORCID: <code>0009-0003-2900-6377</code></p>
</div>
</body></html>`;
const cl3HtmlPath = path.join(paperDir, 'cover_letters_3_print.html');
fs.writeFileSync(cl3HtmlPath, cl3Html, 'utf8');
htmlToPdf(cl3HtmlPath, path.join(paperDir, 'cover_letters_3_journals.pdf'));

console.log('Generated highlights, graphical abstract, declarations, and 3 cover letters.');
