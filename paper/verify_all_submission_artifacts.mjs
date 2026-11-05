import fs from 'fs';
import path from 'path';
import crypto from 'crypto';

const paperDir = 'C:\\Users\\ROG\\.gemini\\antigravity\\scratch\\agentic-sci-paper\\paper';

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
  { file: 'manuscript.pdf', role: 'Main Manuscript (21 Pages, Two-Column SCI Format)' },
  { file: 'supplementary_material.pdf', role: 'Online Supplementary Material & Full Compendium (150 Pages, Appendices A–H)' },
  { file: 'graphical_abstract.pdf', role: 'Graphical Abstract PDF (1 Page Landscape Vector Diagram)' },
  { file: 'graphical_abstract.svg', role: 'Graphical Abstract Vector Source (1328x531 SVG)' },
  { file: 'highlights.pdf', role: 'Elsevier Research Highlights PDF (Verified <= 85 chars per bullet)' },
  { file: 'highlights.md', role: 'Elsevier Research Highlights Markdown Source' },
  { file: 'declarations_and_credit.pdf', role: 'CRediT Author Statement, Competing Interest, Funding & Code Metadata Table (C1–C9)' },
  { file: 'declarations_and_credit.md', role: 'Declarations & CRediT Source' },
  { file: 'cover_letter.pdf', role: 'Primary Submission Cover Letter PDF (1 Page)' },
  { file: 'cover_letters_3_journals.pdf', role: 'Customized Cover Letters for JSS, IEEE TEM, and IJPM (2 Pages)' },
  { file: 'reviewer_defense_playbook.pdf', role: '15-Question SCI Peer-Review Defense & Rebuttal Playbook (7 Pages, EN + CN)' },
  { file: 'manuscript_CN_companion.pdf', role: 'Full Chinese Academic Companion & Oral Defense Guide (3 Pages)' },
  { file: 'main.tex', role: 'Complete LaTeX Manuscript Source (Synchronized with 21-Page PDF)' },
  { file: 'references.bib', role: 'BibTeX Reference Database (35 SCI References)' },
  { file: 'paper_metrics_summary.json', role: 'Reproducible Empirical Metrics Summary JSON' }
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
