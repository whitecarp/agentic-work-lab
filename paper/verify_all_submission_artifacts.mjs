import fs from 'fs';
import path from 'path';
import crypto from 'crypto';
import { fileURLToPath } from 'url';

const paperDir = path.dirname(fileURLToPath(import.meta.url)); // hashes the files next to this script

function sha256File(filePath) {
  const hash = crypto.createHash('sha256');
  const data = fs.readFileSync(filePath);
  hash.update(data);
  return {
    sha256: hash.digest('hex'),
    bytes: data.length
  };
}

const requiredDeliverables = [
  { file: 'manuscript.pdf', role: 'Main manuscript (22 pages, single-column A4), built by build_publication_pdf.mjs' },
  { file: 'supplementary_material.pdf', role: 'Online supplementary material (142 pages, Appendices A–H)' },
  { file: 'graphical_abstract.pdf', role: 'Graphical Abstract PDF (1 Page Landscape Vector Diagram)' },
  { file: 'graphical_abstract.svg', role: 'Graphical Abstract Vector Source (1328x531 SVG)' },
  { file: 'highlights.pdf', role: 'Highlights (five bullets, each at most 85 characters)' },
  { file: 'highlights.md', role: 'Elsevier Research Highlights Markdown Source' },
  { file: 'declarations_and_credit.pdf', role: 'CRediT Author Statement, Competing Interest, Funding & Code Metadata Table (C1–C9)' },
  { file: 'declarations_and_credit.md', role: 'Declarations & CRediT Source' },
  { file: 'cover_letter.pdf', role: 'Journal-neutral cover letter' },
  { file: 'cover_letters_3_journals.pdf', role: 'Cover letters for JSS, IEEE TEM and IJPM' },
  { file: 'reviewer_defense_playbook.pdf', role: 'Reviewer playbook (SUPERSEDED: predates the 4 Nov revision; see its banner)' },
  { file: 'manuscript_CN_companion.pdf', role: 'Chinese companion (SUPERSEDED: predates the 4 Nov revision; see its banner)' },
  { file: 'main.tex', role: 'LaTeX source generated from the same HTML as manuscript.pdf (sync_main_tex.mjs)' },
  { file: 'references.bib', role: 'BibTeX references (41 entries, all cited; DOIs checked against Crossref)' },
  { file: 'figures/fig1.pdf', role: 'Figure 1 for main.tex (exported from the manuscript SVG)' },
  { file: 'figures/fig2.pdf', role: 'Figure 2 for main.tex' },
  { file: 'figures/fig3.pdf', role: 'Figure 3 for main.tex' },
  { file: 'figures/fig4.pdf', role: 'Figure 4 for main.tex' },
  { file: 'figures/fig5.pdf', role: 'Figure 5 for main.tex' },
  { file: 'CLAUDE_PEER_REVIEW_REPORT.md', role: 'Peer review report and changelog for the 4 Nov revision' },
  { file: 'paper_metrics_summary.json', role: 'Every reported figure, recomputed by reproduce_paper_tables.mjs' }
];

const auditEntries = [];
for (const item of requiredDeliverables) {
  const fullPath = path.join(paperDir, item.file);
  if (!fs.existsSync(fullPath)) {
    throw new Error(`Missing required deliverable: ${fullPath}`);
  }
  const info = sha256File(fullPath);
  auditEntries.push({
    file: item.file,
    role: item.role,
    bytes: info.bytes,
    sha256: info.sha256
  });
}

const receipt = {
  generatedAt: new Date().toISOString(),
  author: 'HuaiCheng Sheng',
  affiliation: 'Agentic Work Lab, Hangzhou Chengxin Holding Group Co., Ltd., Hangzhou, Zhejiang, China',
  orcid: '0009-0003-2900-6377',
  email: 'ZackSheng@gmail.com',
  totalDeliverablesVerified: auditEntries.length,
  deliverables: auditEntries
};

fs.writeFileSync(path.join(paperDir, 'SUBMISSION_AUDIT_RECEIPT.json'), JSON.stringify(receipt, null, 2), 'utf8');
console.log(`Verified ${auditEntries.length} deliverables and wrote SUBMISSION_AUDIT_RECEIPT.json.`);
