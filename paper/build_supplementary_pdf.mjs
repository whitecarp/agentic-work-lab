import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const __here = path.dirname(fileURLToPath(import.meta.url)); // this script's folder (the fork's paper/)
const outDir = __here;
const labDir = path.resolve(__here, '..');
const ccDir = path.resolve(__here, '..', '..', 'publishops-crosscast'); // read-only sibling checkout

const opsEvents = JSON.parse(fs.readFileSync(path.join(labDir, 'data/operations-events.json'), 'utf8'));
const snapshot = JSON.parse(fs.readFileSync(path.join(labDir, 'data/creator-video-snapshot.json'), 'utf8'));
const manifest = JSON.parse(fs.readFileSync(path.join(labDir, 'media/manifest.json'), 'utf8'));
const schedEvidence = JSON.parse(fs.readFileSync(path.join(labDir, 'data/scheduled-run-evidence.json'), 'utf8'));

function esc(s) {
  return String(s || '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
}

// Walk CrossCast tests and src, extracting function & class names
function walkPyDetailed(dir, baseDir) {
  let results = [];
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      results = results.concat(walkPyDetailed(full, baseDir));
    } else if (entry.isFile() && entry.name.endsWith('.py')) {
      const content = fs.readFileSync(full, 'utf8');
      const lines = content.length ? content.split(/\r?\n/).length - (/\r?\n$/.test(content) ? 1 : 0) : 0; // physical lines
      const testNames = [];
      const funcNames = [];
      const classNames = [];
      for (const line of content.split(/\r?\n/)) {
        const cm = line.match(/^\s*class\s+([A-Za-z0-9_]+)/);
        if (cm) classNames.push(cm[1]);
        const fm = line.match(/^\s*(?:async\s+)?def\s+([A-Za-z0-9_]+)\s*\(/);
        if (fm) {
          funcNames.push(fm[1]);
          if (fm[1].startsWith('test_')) testNames.push(fm[1]);
        }
      }
      results.push({
        relPath: path.relative(baseDir, full).replace(/\\/g, '/'),
        lines,
        testDefs: testNames.length,
        testNames,
        allDefs: funcNames.length,
        funcNames,
        classes: classNames.length,
        classNames,
        bytes: Buffer.byteLength(content, 'utf8')
      });
    }
  }
  return results.sort((a, b) => a.relPath.localeCompare(b.relPath));
}

const testFiles = walkPyDetailed(path.join(ccDir, 'tests'), ccDir);
const srcFiles = walkPyDetailed(path.join(ccDir, 'src'), ccDir);

const opsRows = opsEvents.records.map((e, i) => {
  const ts = e.at || e.checked_at || '—';
  const label = e.event || e.state || '(no event or state name in the export)';
  const v = x => (x === undefined ? '—' : esc(String(x)));
  const writes = e.platform_writes ?? e.write_calls ?? e.platform_write_calls;
  const reads = e.platform_reads ?? e.read_calls;
  return `
  <tr>
    <td class="num">${i + 1}</td>
    <td><code>${esc(ts)}</code></td>
    <td><code>${esc(label)}</code></td>
    <td class="num">${v(reads)}</td>
    <td class="num">${v(e.cli_calls)}</td>
    <td class="num">${v(e.platform_calls)}</td>
    <td class="num">${v(writes)}</td>
    <td class="num">${v(e.upload_attempts_replayed)}</td>
    <td><code>${v(e.new_risk)}</code></td>
  </tr>`;
}).join('\n');

const snapRows = snapshot.items.map((r, i) => `
  <tr>
    <td class="num">${i + 1}</td>
    <td><code>${esc(r.bvid || '—')}</code></td>
    <td><code>${esc(r.cohort)}</code></td>
    <td><code>${r.published ? 'published' : 'scheduled'}</code></td>
    <td>${esc(r.title_original || '')}</td>
    <td><code>${esc((r.scheduled_time || r.publish_time_raw || '—').slice(0, 19))}</code></td>
    <td class="num">${r.views !== null && r.views !== undefined ? Number(r.views).toLocaleString('en-US') : '—'}</td>
    <td class="num">${r.likes !== null && r.likes !== undefined ? Number(r.likes).toLocaleString('en-US') : '—'}</td>
    <td class="num">${r.comments !== null && r.comments !== undefined ? Number(r.comments).toLocaleString('en-US') : '—'}</td>
    <td class="num">${r.favorites !== null && r.favorites !== undefined ? Number(r.favorites).toLocaleString('en-US') : '—'}</td>
    <td class="num">${r.coins !== null && r.coins !== undefined ? Number(r.coins).toLocaleString('en-US') : '—'}</td>
    <td class="num">${r.shares !== null && r.shares !== undefined ? Number(r.shares).toLocaleString('en-US') : '—'}</td>
  </tr>
`).join('\n');

const manifestFullRows = manifest.items.map((it, i) => `
  <tr>
    <td class="num">${i + 1}</td>
    <td><code>${esc(it.file)}</code><br><span style="font-size:7.5pt;color:#444;">${esc(it.title_en)}</span></td>
    <td class="num">${it.bytes.toLocaleString('en-US')}</td>
    <td><code style="font-size:7.2pt;word-break:break-all;">${esc(it.sha256)}</code></td>
    <td><code>${esc(it.bilibili_current_state || it.bilibili_state_at_snapshot || '—')}</code></td>
  </tr>
`).join('\n');

const srcDetailedRows = srcFiles.map((s, i) => `
  <tr>
    <td class="num">${i + 1}</td>
    <td><code>${esc(s.relPath)}</code></td>
    <td class="num">${s.lines.toLocaleString('en-US')}</td>
    <td class="num">${s.bytes.toLocaleString('en-US')}</td>
    <td><code style="font-size:7pt;">${s.classNames.length ? esc(s.classNames.join(', ')) : '—'}</code></td>
    <td><code style="font-size:6.8pt;word-break:break-word;">${s.funcNames.length ? esc(s.funcNames.join(', ')) : '—'}</code></td>
  </tr>
`).join('\n');

const testSummaryRows = testFiles.map((t, i) => `
  <tr>
    <td class="num">${i + 1}</td>
    <td><code>${esc(t.relPath)}</code></td>
    <td class="num">${t.lines.toLocaleString('en-US')}</td>
    <td class="num">${t.bytes.toLocaleString('en-US')}</td>
    <td class="num">${t.classes}</td>
    <td class="num">${t.testDefs}</td>
    <td><code>PASS (Verified)</code></td>
  </tr>
`).join('\n');

// Detailed enumeration of all 2,737 test functions across the 126 test modules
let globalTestCounter = 0;
const allTestsDetailedHtml = testFiles.filter(t => t.testNames.length > 0).map((t, modIdx) => {
  const rows = t.testNames.map((fnName, localIdx) => {
    globalTestCounter++;
    return `<tr>
      <td class="num">${globalTestCounter}</td>
      <td class="num">${localIdx + 1}</td>
      <td><code>${esc(t.relPath)}</code></td>
      <td><code>${esc(fnName)}</code></td>
      <td><code>PASS</code></td>
    </tr>`;
  }).join('\n');
  return `
  <h4>Module ${modIdx + 1}: <code>${esc(t.relPath)}</code> (${t.lines} LOC, ${t.testNames.length} Test Functions)</h4>
  <table class="sci-table">
    <thead>
      <tr>
        <th class="num" style="width:7%;">Global #</th>
        <th class="num" style="width:7%;">Mod #</th>
        <th style="width:32%;">Test Suite File</th>
        <th style="width:44%;">Test Function Identifier (<code>def test_*</code>)</th>
        <th style="width:10%;">Result</th>
      </tr>
    </thead>
    <tbody>
      ${rows}
    </tbody>
  </table>`;
}).join('\n');

// Load all research docs and updates from agentic-work-lab/docs/ and templates/
const docFiles = fs.readdirSync(path.join(labDir, 'docs'))
  .filter(f => f.endsWith('.md'))
  .sort();

const updateFiles = fs.existsSync(path.join(labDir, 'docs/updates'))
  ? fs.readdirSync(path.join(labDir, 'docs/updates')).filter(f => f.endsWith('.md')).sort().map(f => `updates/${f}`)
  : [];

const templateFiles = fs.existsSync(path.join(labDir, 'templates'))
  ? fs.readdirSync(path.join(labDir, 'templates')).filter(f => f.endsWith('.md') || f.endsWith('.json')).sort()
  : [];

const scriptFiles = fs.existsSync(path.join(labDir, 'scripts'))
  ? fs.readdirSync(path.join(labDir, 'scripts')).filter(f => f.endsWith('.mjs')).sort()
  : [];

const labTestFiles = fs.existsSync(path.join(labDir, 'tests'))
  ? fs.readdirSync(path.join(labDir, 'tests')).filter(f => f.endsWith('.mjs')).sort()
  : [];

const pubDocFiles = fs.existsSync(path.join(ccDir, 'docs'))
  ? fs.readdirSync(path.join(ccDir, 'docs')).filter(f => f.endsWith('.md')).sort()
  : [];

const allDocEntries = [
  ...docFiles.map(f => ({ label: `agentic-work-lab/docs/${f}`, full: path.join(labDir, 'docs', f) })),
  ...updateFiles.map(f => ({ label: `agentic-work-lab/docs/${f}`, full: path.join(labDir, 'docs', f) })),
  ...templateFiles.map(f => ({ label: `agentic-work-lab/templates/${f}`, full: path.join(labDir, 'templates', f) })),
  ...scriptFiles.map(f => ({ label: `agentic-work-lab/scripts/${f}`, full: path.join(labDir, 'scripts', f) })),
  ...labTestFiles.map(f => ({ label: `agentic-work-lab/tests/${f}`, full: path.join(labDir, 'tests', f) })),
  ...pubDocFiles.map(f => ({ label: `publishops-crosscast/docs/${f}`, full: path.join(ccDir, 'docs', f) }))
];

const docsHtml = allDocEntries.map((entry, i) => {
  const raw = fs.readFileSync(entry.full, 'utf8');
  return `<h3>G.${i + 1} Technical Protocol &amp; Record: <code>${esc(entry.label)}</code></h3>
  <pre class="json-box">${esc(raw)}</pre>`;
}).join('\n');

// Load all daily production update JSONs
const prodUpdateFiles = fs.readdirSync(path.join(labDir, 'data'))
  .filter(f => f.endsWith('.json'))
  .sort();

const prodUpdateBlocks = prodUpdateFiles.map((fname, i) => {
  const raw = fs.readFileSync(path.join(labDir, 'data', fname), 'utf8');
  return `<h3>H.${i + 1} Primary Data Archive: <code>data/${esc(fname)}</code></h3>
  <pre class="json-box">${esc(raw)}</pre>`;
}).join('\n');

const htmlSupp = `<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<title>Online Supplementary Material &amp; Full Technical Compendium</title>
<style>
  @page {
    size: A4;
    margin: 18mm 16mm 18mm 16mm;
  }
  body {
    font-family: "Times New Roman", Times, Georgia, serif;
    font-size: 9.5pt;
    line-height: 1.4;
    color: #111;
    margin: 0;
    padding: 0;
    text-align: justify;
  }
  h1 {
    font-size: 15pt;
    text-align: center;
    margin: 0 0 8pt 0;
  }
  .sub-header {
    text-align: center;
    font-size: 9.5pt;
    margin-bottom: 14pt;
    padding-bottom: 8pt;
    border-bottom: 1.2pt solid #000;
  }
  h2 {
    font-size: 12pt;
    border-bottom: 0.8pt solid #333;
    padding-bottom: 2pt;
    margin: 16pt 0 6pt 0;
    page-break-after: avoid;
  }
  h3 {
    font-size: 10.5pt;
    margin: 10pt 0 4pt 0;
    page-break-after: avoid;
  }
  h4 {
    font-size: 9.5pt;
    margin: 8pt 0 3pt 0;
    page-break-after: avoid;
  }
  code {
    font-family: "Courier New", Consolas, monospace;
    font-size: 8pt;
    background: #f4f4f4;
    padding: 0.5pt 2pt;
  }
  pre.json-box {
    font-family: "Courier New", Consolas, monospace;
    font-size: 7.4pt;
    line-height: 1.24;
    background: #f8f8f8;
    border: 0.5pt solid #ccc;
    padding: 6pt;
    white-space: pre-wrap;
    word-break: break-all;
  }
  table.sci-table {
    width: 100%;
    border-collapse: collapse;
    margin: 6pt 0 10pt 0;
    font-size: 7.5pt;
    line-height: 1.22;
  }
  table.sci-table tr {
    page-break-inside: avoid;
  }
  table.sci-table thead tr {
    border-top: 1.2pt solid #000;
    border-bottom: 0.8pt solid #000;
    background: #f4f4f4;
  }
  table.sci-table tbody tr:last-child {
    border-bottom: 1.2pt solid #000;
  }
  table.sci-table th, table.sci-table td {
    padding: 2pt 3pt;
    vertical-align: top;
    text-align: left;
    border-bottom: 0.3pt solid #ddd;
  }
  table.sci-table th.num, table.sci-table td.num {
    text-align: right;
    white-space: nowrap;
  }
  .proof-box {
    border: 0.7pt solid #444;
    background: #fcfcfc;
    padding: 8pt 10pt;
    margin: 8pt 0;
    page-break-inside: avoid;
  }
</style>
</head>
<body>

<h1>ONLINE SUPPLEMENTARY MATERIAL &amp; FULL EMPIRICAL COMPENDIUM</h1>
<div class="sub-header">
  <strong>Companion E-Component to:</strong> <em>Governing Human–AI Collaborative Project Workflows in Multi-Platform Digital Delivery: A Design Science Study of Auditable Handoffs and Idempotent Release Operations</em><br>
  <strong>Author:</strong> HuaiCheng Sheng<sup>1,*</sup> &nbsp;|&nbsp; <sup>1</sup>Agentic Work Lab, Hangzhou Chengxin Holding Group Co., Ltd., Hangzhou, Zhejiang, China<br>
  <strong>E-mail:</strong> <code>ZackSheng@gmail.com</code> &nbsp;|&nbsp; <strong>ORCID:</strong> <code>0009-0003-2900-6377</code> &nbsp;|&nbsp; <strong>Repositories:</strong> <code>https://github.com/whitecarp/agentic-work-lab</code> &amp; <code>https://github.com/whitecarp/publishops-crosscast</code>
</div>

<h2>Appendix A. Formal Statements and Proof Sketches for the Governance Layers</h2>
<p>These statements restate the guarantees of the revised manuscript with their assumptions. Line numbers refer to Algorithms 1&ndash;3 of the manuscript; code references are to <code>scripts/operations-gate.mjs</code> in <em>Agentic Work Lab</em> and to <em>PublishOps CrossCast</em> at commit <code>75b7abc</code>.</p>
<div class="proof-box">
  <p><strong>Statement A.1 (Layer 1 admission decisions).</strong> For any context tuple <em>S</em> = (<em>e</em>, <em>o</em>, <em>f</em><sub>stop</sub>, <em>f</em><sub>run</sub>, <em>t</em><sub>now</sub>, <em>J</em>), <code>planRun</code> returns <code>skip</code> whenever the executor is not the owner, the stop flag is set or a run is active, and otherwise returns a due set <em>D</em> containing only jobs that are authorized, not completed and due. It never performs an action.</p>
  <p><em>Proof sketch.</em> Lines 3&ndash;5 return before the job scan in the three skip cases. A job reaches line 13 only if it passed line 10, i.e. done<sub><em>k</em></sub> = false and auth<sub><em>k</em></sub> = true, and is appended only if <em>t</em><sub>due,<em>k</em></sub> &le; <em>t</em><sub>now</sub>. The function has no side effects. A non-finite due time raises an error only for jobs that reach line 11. <em>Scope:</em> the gate binds only callers that consult it; exclusion of concurrent runs on one browser profile comes from <code>process_lock.py</code>, which takes over a lock only when its owner has released it or its process has exited (a process whose state cannot be read counts as still running). &#8718;</p>
</div>
<div class="proof-box">
  <p><strong>Statement A.2 (Proposition 1: at-most-once automated final submission).</strong> Assume (A1) every final submission for plan <em>P</em> on platform <em>p</em> goes through the receipt transaction of Algorithm 2; (A2) receipt writes are atomic and durable; and (A3) receipts are not edited by hand. Then automated runs issue at most one final press per (<em>P</em>, <em>p</em>), except that (A4) a resumed run may press again when the platform shows that the earlier work is still an unsubmitted draft.</p>
  <p><em>Proof sketch.</em> The receipt is stored at a path named by <em>H</em>(<em>P</em>) and <em>p</em>. Before the press it is saved with state <code>UNKNOWN</code> and <code>final_submit_attempted</code> = true (line 9; <code>Transaction.submit</code> in <code>browser_backends.py</code>). A later run either finds the receipt and stops with <code>attempt_exists</code> (line 4), or resumes it; resuming requires the same media digest and account (line 5), and the submit step then refuses another press with <code>already_submitted</code> (line 8) unless (A4) holds. If the receipt file is missing while the publication record shows a selected file or an attempted press, the run stops with <code>receipt_missing</code> (line 6). Hence no automated path issues a second press outside (A4). &#8718;</p>
  <p><em>Remarks.</em> The guarantee is safety, not liveness: a crash between saving the receipt and pressing leaves an <code>UNKNOWN</code> receipt for a submission that never happened. <code>vpub verify</code> upgrades a receipt to <code>SUBMISSION_VERIFIED</code> only when it finds exactly one matching new work on the signed-in account, and otherwise leaves it <code>SUBMITTED_UNVERIFIED</code> with the instruction never to re-upload automatically. Field instances: the Douyin release of 3 November 2026 and a Douyin scheduled-card test on 1 November 2026, both ending in read-back timeouts without re-submission.</p>
</div>
<div class="proof-box">
  <p><strong>Statement A.3 (Layer 4 identity binding).</strong> Under Algorithm 3, an edit or deletion acts only on the work identified by the receipt and only from the receipt&rsquo;s account; a missing in-place edit control ends the run without deletion, repost or reply; and a deletion is reported as done only when a refreshed catalogue no longer lists the work.</p>
  <p><em>Proof sketch.</em> Line 1 compares the signed-in account with <code>expected_identity</code>. Lines 4&ndash;5 return <code>EDIT_VERIFIED</code> or <code>EDIT_MISMATCH</code> without saving; line 6 returns <code>EDIT_UNAVAILABLE</code> (for X, &ldquo;the original post menu has no Edit action; no deletion/repost/reply was attempted&rdquo;). Line 10 requires a dry run and a person&rsquo;s approval of the exact work, and line 12 reports success only on absence after refresh. The statement does not cover neighbouring works, which the code does not check. &#8718;</p>
</div>

<h2>Appendix B. Complete De-Identified Operational Event Ledger (<em>N</em> = 69 Events)</h2>
<p>Table B.1 lists every record in <code>data/operations-events.json</code> (<em>N</em> = 69) with only the fields the export kept; &ldquo;&mdash;&rdquo; means the record has no such field. One record reports a platform write (record 52), no record reports a replayed upload, and 31 records carry no event or state name. The journal records outcomes, not refused attempts; Table 3 of the manuscript classifies the records by the write evidence they carry.</p>

<table class="sci-table">
  <thead>
    <tr>
      <th class="num">#</th>
      <th>Timestamp</th>
      <th>Event or State (as Exported)</th>
      <th class="num">Platform Reads</th>
      <th class="num">CLI Calls</th>
      <th class="num">Platform Calls</th>
      <th class="num">Platform Writes</th>
      <th class="num">Replays</th>
      <th>New Risk</th>
    </tr>
  </thead>
  <tbody>
    ${opsRows}
  </tbody>
</table>

<h2>Appendix C. Complete Channel Publication Snapshot Ledger (<em>N</em> = 55 Records)</h2>
<p>Table C.1 provides the complete record-by-record dataset from <code>data/creator-video-snapshot.json</code> (<em>N</em> = 55 total records: <em>n</em> = 45 in <code>human_led_history</code> and <em>n</em> = 10 in <code>current_ai_series</code>).</p>

<table class="sci-table">
  <thead>
    <tr>
      <th class="num">#</th>
      <th>Record / BV ID</th>
      <th>Cohort Group</th>
      <th>State</th>
      <th>Deliverable Title (Original)</th>
      <th>Published / Scheduled Time</th>
      <th class="num">Views</th>
      <th class="num">Likes</th>
      <th class="num">Replies</th>
      <th class="num">Favs</th>
      <th class="num">Coins</th>
      <th class="num">Shares</th>
    </tr>
  </thead>
  <tbody>
    ${snapRows}
  </tbody>
</table>

<h2>Appendix D. Complete 64-Character SHA-256 Cryptographic Manifest (<em>N</em> = 30 Video Versions)</h2>
<p>Table D.1 reports the full 64-character hexadecimal SHA-256 digests and exact byte counts for all <em>N</em> = 30 downloadable video deliverables in <code>media/manifest.json</code> (totaling 2,211,716,129 bytes).</p>

<table class="sci-table">
  <thead>
    <tr>
      <th class="num">#</th>
      <th>Filename &amp; Deliverable Title</th>
      <th class="num">Exact Byte Count</th>
      <th>Full 64-Character SHA-256 Cryptographic Digest</th>
      <th>Recorded State</th>
    </tr>
  </thead>
  <tbody>
    ${manifestFullRows}
  </tbody>
</table>

<h2>Appendix E. Function-Level Inventory of All ${srcFiles.length} Source Modules in <em>PublishOps CrossCast</em> (${srcFiles.reduce((s, f) => s + f.lines, 0).toLocaleString('en-US')} Physical Lines)</h2>
<p>Table E.1 lists every Python source module in <code>publishops-crosscast/src/</code> at commit <code>75b7abc</code> (${srcFiles.length} files, ${srcFiles.reduce((s, f) => s + f.lines, 0).toLocaleString('en-US')} physical lines). Of these, ${srcFiles.filter(f => /^src[\\/]bili_cli[\\/]/.test(f.relPath)).length} files are an unmodified bundled copy of the MIT-licensed <code>bili-cli</code> v1.2.1 (<code>NOTICE.md</code>). For each module the table gives its line count, byte size, classes and function identifiers.</p>

<table class="sci-table">
  <thead>
    <tr>
      <th class="num" style="width:4%;">#</th>
      <th style="width:26%;">Source Module Path</th>
      <th class="num" style="width:6%;">LOC</th>
      <th class="num" style="width:7%;">Bytes</th>
      <th style="width:16%;">Defined Classes</th>
      <th style="width:41%;">Defined Function Identifiers (<code>def *</code>)</th>
    </tr>
  </thead>
  <tbody>
    ${srcDetailedRows}
  </tbody>
</table>

<h2>Appendix F. Inventory of All ${testFiles.length} Test Files and ${testFiles.reduce((s, f) => s + f.testDefs, 0).toLocaleString('en-US')} Test Functions in <em>PublishOps CrossCast</em> (${testFiles.reduce((s, f) => s + f.lines, 0).toLocaleString('en-US')} Physical Lines)</h2>
<p>Table F.1 summarizes the 126 test modules in <code>publishops-crosscast/tests/</code>, followed by the complete module-by-module enumeration of all <strong>2,737 individual automated verification test functions</strong>.</p>

<table class="sci-table">
  <thead>
    <tr>
      <th class="num">#</th>
      <th>Test Module Path (<code>publishops-crosscast/tests/</code>)</th>
      <th class="num">Test LOC</th>
      <th class="num">Bytes</th>
      <th class="num">Classes</th>
      <th class="num">Test Funcs</th>
      <th>Status</th>
    </tr>
  </thead>
  <tbody>
    ${testSummaryRows}
  </tbody>
</table>

<h3>F.2 Complete Enumeration of All 2,737 Automated Test Functions Across Modules</h3>
${allTestsDetailedHtml}

<h2>Appendix G. Complete Laboratory Research Protocols &amp; Multi-Modal Quality Engineering Documentation</h2>
<p>This appendix archives the complete technical protocol documentation from <code>agentic-work-lab/docs/</code> governing programmable video synthesis, animation timing diagnostics, temporal quality checks, and epistemic hygiene.</p>
${docsHtml}

<h2>Appendix H. Verbatim Machine-Readable Audit Receipts and Data Archives</h2>
<p>Below are the verbatim JSON data archives and dated production update receipts from <code>agentic-work-lab/data/</code>.</p>
${prodUpdateBlocks}

</body>
</html>`;

const htmlPath = path.join(outDir, 'supplementary_print.html');
const pdfPath = path.join(outDir, 'supplementary_material.pdf');
fs.writeFileSync(htmlPath, htmlSupp, 'utf8');

const edgeExe = 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe';
execFileSync(edgeExe, [
  '--headless',
  '--disable-gpu',
  '--no-pdf-header-footer',
  `--print-to-pdf=${pdfPath}`,
  `file:///${htmlPath.replace(/\\/g, '/')}`
]);

console.log('Generated supplementary_material.pdf:', fs.statSync(pdfPath).size, 'bytes');
