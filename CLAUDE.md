# CLAUDE.md — Automated Multi-Agent Handoff & SCI Peer-Review Sync Protocol

> **ACTIVE BRANCH:** `paper/claude-review-fork` (Isolated Fork of `paper/sci-manuscript`)  
> **WORKING DIRECTORY:** `C:\Users\ROG\.gemini\antigravity\scratch\agentic-sci-paper\agentic-work-lab-claude-fork`  
> **ORIGINAL REFERENCE DIRECTORY (DO NOT MODIFY):** `C:\Users\ROG\.gemini\antigravity\scratch\agentic-sci-paper\agentic-work-lab` (Branch: `paper/sci-manuscript`)

---

## 1. Mission & Context (Synchronized Handoff)
This isolated repository fork (`agentic-work-lab-claude-fork`, branch `paper/claude-review-fork`) has been pre-created and synchronized for **Claude** to review, critique, and refine our complete SCI Q1 submission package without touching the primary `paper/sci-manuscript` branch.

### Manuscript Title
**Governing Human–AI Collaborative Project Workflows in Multi-Platform Digital Delivery: A Design Science Study of Auditable Handoffs and Idempotent Release Operations**

### Author Identity (Strictly Locked — Do Not Alter)
- **Author:** `HuaiCheng Sheng`
- **Affiliation:** `Agentic Work Lab, Hangzhou Chengxin Holding Group Co., Ltd., Hangzhou, Zhejiang, China`
- **Email:** `ZackSheng@gmail.com`
- **ORCID:** `0009-0003-2900-6377`
- **GitHub:** `https://github.com/whitecarp`

---

## 2. Complete Inventory of Synchronized Deliverables in `paper/`
All 9 compiled PDFs and their source generators are located in `./paper/`:
1. **`paper/main.tex` & `paper/manuscript.pdf` (21 Pages):** Two-column SCI Q1 Main Manuscript containing 5 embedded vector SVG diagrams, 3 formal mathematical equation systems, 3 formal algorithms (`planRun`, `executePublishStep`, `auditCohortComparison`), 11 empirical & ablation tables (Tables 1–11), and 35 references.
2. **`paper/supplementary_material.pdf` (150 Pages):** Online Supplementary Material & Full Empirical Compendium (Appendices A–H: formal mathematical proofs of Theorems A.1–A.3, all 69 operational events, all 55 channel video records, all 30 64-character SHA-256 media digests, all 123 Python source modules, all 126 test modules + all 2,737 unit test function names, all 66 technical protocol documents, and all 14 verbatim JSON data snapshots).
3. **`paper/graphical_abstract.svg` & `paper/graphical_abstract.pdf` (1 Page Landscape):** Vector Graphical Abstract for Elsevier/IEEE submission.
4. **`paper/highlights.md` & `paper/highlights.pdf` (1 Page):** Standalone Elsevier Research Highlights strictly verified at $\le 70$ characters per bullet point (limit $\le 85$ chars including spaces).
5. **`paper/declarations_and_credit.md` & `paper/declarations_and_credit.pdf` (1 Page):** CRediT Author Statement, Competing Interests, Funding, Privacy Sanitization, and Official Software Metadata Table (C1–C9).
6. **`paper/cover_letters_3_journals.md` & `paper/cover_letters_3_journals.pdf` (2 Pages):** Tailored Cover Letters for (A) *Journal of Systems and Software (JSS)*, (B) *IEEE Transactions on Engineering Management (IEEE TEM)*, and (C) *International Journal of Project Management (IJPM)*.
7. **`paper/reviewer_defense_playbook.md` & `paper/reviewer_defense_playbook.pdf` (7 Pages):** Preemptive Peer-Review Rebuttal Matrix covering 15 tough reviewer questions (English + Chinese).
8. **`paper/manuscript_CN_companion.pdf` (3 Pages):** Complete Chinese Academic Companion & Oral Defense Guide.
9. **`paper/SUBMISSION_AUDIT_RECEIPT.json`:** Cryptographic SHA-256 verification ledger of all 15 core files.

---

## 3. Non-Negotiable Epistemic & Governance Rules
1. **100% Ground-Truth Fidelity (Zero Fabrication):** Every empirical metric in this paper is computed deterministically from `./data/` and `../publishops-crosscast/` via `node paper/reproduce_paper_tables.mjs`:
   - $N = 69$ operational events (`68` non-write gates enforced, `1` authorized write, `0` duplicate replays, `0` new risks; heartbeat trigger delay `24.992 s`, execution duration `31.175 s`).
   - $5$ platforms validated (Bilibili, YouTube, X, Douyin, Instagram); Douyin post-submission read-back timeout latched into `UNKNOWN`, preventing duplicate publication.
   - Instagram end-to-end latency: `423.236 s` (`412.217 s` publish command, of which `380.551 s` / `92.32%` is post-submission confirmation wait).
   - $N = 30$ video delivery versions (`2,377,509,759 bytes` / `2.21 GB`) verified with 64-KB stream-based SHA-256 digests.
   - $N = 55$ channel video snapshot records across 4 cohorts (Legacy baseline mean `3,477.00` views vs. Whale Girl Shift Log mean `95.89` views; Mann–Whitney $U = 65.5, z = -3.24, p = 0.0012, r_{\text{rb}} = 0.721$).
   - `123` Python source modules (`27,095 LOC`) and `126` test modules (`37,155 LOC`, `2,737` `def test_*` functions).
2. **Synchronized Compilation:** Whenever you edit prose, equations, or tables in `paper/main.tex`, also apply the exact same edits to `paper/build_publication_pdf.mjs` and run:
   ```powershell
   node paper/build_publication_pdf.mjs
   node paper/verify_all_submission_artifacts.mjs
   ```
3. **Commit & Handoff Log:** Record all findings, critiques, and edits in `paper/CLAUDE_PEER_REVIEW_REPORT.md` and commit to branch `paper/claude-review-fork`.
