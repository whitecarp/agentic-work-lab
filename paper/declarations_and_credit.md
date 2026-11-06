# Author Declarations, CRediT Statement and Code Metadata

## 1. Manuscript and author
- **Title:** Governing Human–AI Collaborative Project Workflows in Multi-Platform Digital Delivery: A Design Science Study of Auditable Handoffs and Idempotent Release Operations
- **Author:** HuaiCheng Sheng
- **Affiliation:** Agentic Work Lab, Hangzhou Chengxin Holding Group Co., Ltd., Hangzhou, Zhejiang, China
- **Corresponding author e-mail:** `ZackSheng@gmail.com`
- **ORCID:** `0009-0003-2900-6377` (https://orcid.org/0009-0003-2900-6377)

## 2. CRediT author statement
**HuaiCheng Sheng:** Conceptualization, Methodology, Software, Validation, Formal analysis, Investigation, Resources, Data curation, Writing – original draft, Writing – review & editing, Visualization, Supervision, Project administration.

## 3. Declaration of competing interest
The author is affiliated with Agentic Work Lab, Hangzhou Chengxin Holding Group Co., Ltd., and is the developer and maintainer of the open-source artifacts evaluated in this study (*Agentic Work Lab* and *PublishOps CrossCast*); a separate API add-on package outside the evaluated open-source snapshot is distributed independently by the author. The author declares no other competing financial interests or personal relationships that could have appeared to influence the work reported in this paper.

## 4. Funding
This research was supported by the internal research and engineering resources of Agentic Work Lab, Hangzhou Chengxin Holding Group Co., Ltd., Hangzhou, Zhejiang, China. It did not receive any specific grant from funding agencies in the public, commercial or not-for-profit sectors.

## 5. Ethics and data protection
No human-subjects experiment was conducted. The study analyses the author's own operational records, the author's channel analytics and public platform metadata. The exported records exclude credentials, raw client logs, private messages and viewer personal data (`data/production-update-2026-11-03.json`, field `excluded`).

## 6. Data and code availability
The 69-record operational journal, the 55-record channel snapshot, the 30-entry media manifest, the workflow scripts and the manuscript sources are available at https://github.com/whitecarp/agentic-work-lab (data files as of commit `e3205d6`). The evaluated release software is available at https://github.com/whitecarp/publishops-crosscast (commit `75b7abc`). `node paper/reproduce_paper_tables.mjs` recomputes every figure reported in the manuscript.

## 7. Code metadata
| Nr. | Code metadata description | Value |
| :--- | :--- | :--- |
| C1 | Current code version | publishops-crosscast v0.16.0 (evaluated main-branch commit `75b7abc`); agentic-work-lab package 0.1.0 |
| C2 | Permanent link to code/repository | https://github.com/whitecarp/publishops-crosscast and https://github.com/whitecarp/agentic-work-lab |
| C3 | Paper sources and reproduction script | `paper/` folder of agentic-work-lab (`reproduce_paper_tables.mjs`, `build_publication_pdf.mjs`, `sync_main_tex.mjs`) |
| C4 | Legal code licence | publishops-crosscast: MIT (bundled bili-cli v1.2.1: MIT). agentic-work-lab: MIT (`LICENSE` in the repository root; no licence file at data snapshot commit `e3205d6`) |
| C5 | Code versioning system | Git (GitHub) |
| C6 | Languages, tools and services | Python (CrossCast at `75b7abc`: 123 source files, 26,972 lines, of which 42 files are the bundled bili-cli; 126 test files with 2,737 test functions); Node.js (laboratory scripts, no package dependencies). Main Python dependencies: Typer, httpx, Patchright, Pillow, curl_cffi, twitter-text-parser; bili-cli also needs bilibili-api-python (GPL-3.0-or-later), installed separately |
| C7 | Requirements and operating environments | Python 3.10–3.14 (GitHub CI checks for `75b7abc` passed on all five); Node.js 20 or later. Package metadata lists Windows, Linux and macOS; installation acceptance on a fresh computer and across operating systems is recorded as not yet completed |
| C8 | Documentation | README_EN.md and docs/ in both repositories (for example `docs/research-method_EN.md`, `docs/model-workflow_EN.md`, CrossCast `docs/VALIDATION_EN.md`); online supplementary material |
| C9 | Support e-mail | ZackSheng@gmail.com |
