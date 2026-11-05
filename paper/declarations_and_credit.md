# Mandatory Author Declarations, CRediT Statement & Software Metadata
*(Required for Elsevier Editorial Manager — JSS / IJPM / SoftwareX — and IEEE Author Portal)*

## 1. Manuscript & Author Metadata
- **Title:** Governing Human–AI Collaborative Project Workflows in Multi-Platform Digital Delivery: A Design Science Study of Auditable Handoffs and Idempotent Release Operations
- **Author:** HuaiCheng Sheng
- **Affiliation:** Agentic Work Lab, Hangzhou Chengxin Holding Group Co., Ltd., Hangzhou, Zhejiang, China
- **Corresponding Author Email:** `ZackSheng@gmail.com`
- **ORCID iD:** `0009-0003-2900-6377` (`https://orcid.org/0009-0003-2900-6377`)

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
Not applicable. This study investigates software engineering governance protocols, automated release state machines, and de-identified operational and public channel telemetry. No human subjects experimentation, personally identifiable private user data, or clinical trials were conducted. All sensitive credentials, session cookies, and merchant details were rigorously sanitized and audited via automated leakage tests (`npm run privacy:check`).

---

## 6. Data, Code & Reproducibility Availability Statement
All source code, JSON schemas, verification scripts, de-identified operational telemetry ($N=69$ events), channel snapshot datasets ($N=55$ records), and media SHA-256 manifests ($N=30$ versions) are permanently archived and publicly accessible:
1. **Governance & Empirical Benchmark Repository (`agentic-work-lab`):** `https://github.com/whitecarp/agentic-work-lab` (Manuscript & reproduction package on branch `paper/sci-manuscript`).
2. **Release Operations Engine Repository (`publishops-crosscast`):** `https://github.com/whitecarp/publishops-crosscast`.
3. **One-Command Table Reproduction:** Execute `node paper/reproduce_paper_tables.mjs` to deterministically regenerate all statistical tables from raw JSON receipts.

---

## 7. Standard Software & Code Metadata Table (Elsevier *JSS* / *SoftwareX* Open Science Standard)

| Nr. | Code Metadata Description | Specification |
| :--- | :--- | :--- |
| **C1** | Current code version | `v0.1.0` (`publishops-crosscast`) / `v1.0.0` (`agentic-work-lab`) |
| **C2** | Permanent link to code/repository | `https://github.com/whitecarp/agentic-work-lab` and `https://github.com/whitecarp/publishops-crosscast` |
| **C3** | Permanent link to reproducible capsule / paper branch | `https://github.com/whitecarp/agentic-work-lab/tree/paper/sci-manuscript` |
| **C4** | Legal Code License | MIT License |
| **C5** | Code versioning system used | Git / GitHub |
| **C6** | Software code languages, tools, and services used | Python 3.11+ (`27,095 LOC` src; `37,155 LOC` tests across `2,737` test functions), Node.js 20+ (ESM verification scripts), Playwright, Typer, Pydantic, Rich |
| **C7** | Compilation requirements & operating environments | Cross-platform (Windows 10/11, Linux, macOS); Python >= 3.11, Node.js >= 20 |
| **C8** | Link to developer documentation / manual | `docs/operations-governance.md`, `docs/validation-guide.md`, and 150-page `supplementary_material.pdf` (Appendices A–H) |
| **C9** | Support email for questions | `ZackSheng@gmail.com` |
