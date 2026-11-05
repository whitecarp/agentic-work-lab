# Customized SCI Submission Cover Letters for Top 3 Target Journals

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
1. **Deterministic Stage-Gate Handoff Invariants (RQ1):** We formalize a pure scheduling gate (`planRun`, Algorithm 1) and cryptographic plan-hash locking (`--confirm CURRENT_PLAN_HASH`, Theorem A.3) that eliminated unauthorized writes across **$N=69$ longitudinal operational events** ($68$ non-write gates enforced, $1$ authorized write, $0$ duplicate replays) and locked **$N=30$ video delivery artifacts ($2.21\text{ GB}$)**.
2. **Idempotent Receipt State Machine under `UNKNOWN` States (RQ2):** We prove and empirically validate an eight-state release state machine (Algorithm 2, Theorem A.2) that latches uncertain post-submission outcomes into `UNKNOWN` rather than triggering naive retries, successfully preventing duplicate publication on Douyin during a live read-back timeout.
3. **Phase-Level Release Latency Decomposition (RQ3):** Across five platforms (Bilibili, YouTube, X, Douyin, and Instagram), millisecond-level decomposition on Instagram ($423.236\text{ s}$ end-to-end) reveals that **$92.32\%$ ($380.551\text{ s}$)** of command duration resides in post-submission platform confirmation waits—identifying the exact vulnerability window for non-idempotent agent retries.
4. **Complete Reproducible Open-Science Package (172 Pages Total):** Alongside the **21-page Main Manuscript**, we provide a **150-page Online Supplementary Material & Full Empirical Compendium** (Appendices A–H) containing complete mathematical proofs, all 69 operational logs, all 55 channel records, all 30 64-character SHA-256 digests, all 2,737 test function identifiers, 66 specification documents, and 14 raw JSON audit receipts.

This manuscript is original, has not been published previously, and is not under consideration elsewhere. Thank you for your time and consideration.

Sincerely,  
**HuaiCheng Sheng**  
Agentic Work Lab, Hangzhou Chengxin Holding Group Co., Ltd., Hangzhou, Zhejiang, China  
Email: `ZackSheng@gmail.com` | ORCID: `0009-0003-2900-6377`

---

## Option B: Cover Letter for *IEEE Transactions on Engineering Management (IEEE TEM)*

**Date:** November 5, 2026  
**To:** The Editor-in-Chief, *IEEE Transactions on Engineering Management*  
**Subject:** Manuscript Submission — *"Governing Human–AI Collaborative Project Workflows in Multi-Platform Digital Delivery: A Design Science Study of Auditable Handoffs and Idempotent Release Operations"*

Dear Editor-in-Chief,

We respectfully submit our original manuscript entitled **"Governing Human–AI Collaborative Project Workflows in Multi-Platform Digital Delivery: A Design Science Study of Auditable Handoffs and Idempotent Release Operations"** for peer review in *IEEE Transactions on Engineering Management*.

Engineering and project management organizations are rapidly adopting multi-agent AI workflows, yet classical Stage-Gate project governance models lack enforceable computational mechanisms to prevent asynchronous AI role drift, duplicate external commitments during network timeouts, and confounded productivity claims. Bridging Stage-Gate control theory (Cooper, 1990; Bianchi et al., 2022) with fault-tolerant systems engineering, this Design Science study establishes an auditable human-in-the-loop (HITL) governance framework and evaluates it across **$N=69$ operational events**, **$N=55$ longitudinal channel releases**, **$N=30$ cryptographically verified media versions ($2.21\text{ GB}$)**, and **$2,737$ automated verification tests**.

In particular, our non-parametric cohort evaluation (Mann–Whitney $U = 65.5, p = 0.0012$, Algorithm 3) demonstrates how engineering managers can separate exposure-age confounding ($3,477.00$ vs. $95.89$ mean views) from genuine operational reliability gains. The submission includes a **21-page Main Manuscript** and a **150-page Online Supplementary Material** archive for full empirical transparency.

This work is original and not under review elsewhere.

Sincerely,  
**HuaiCheng Sheng**  
Agentic Work Lab, Hangzhou Chengxin Holding Group Co., Ltd., Hangzhou, Zhejiang, China  
Email: `ZackSheng@gmail.com` | ORCID: `0009-0003-2900-6377`

---

## Option C: Cover Letter for *International Journal of Project Management (IJPM)*

**Date:** November 5, 2026  
**To:** The Editor-in-Chief, *International Journal of Project Management*  
**Subject:** Original Research Submission — *"Governing Human–AI Collaborative Project Workflows in Multi-Platform Digital Delivery: A Design Science Study of Auditable Handoffs and Idempotent Release Operations"*

Dear Editor-in-Chief,

We are pleased to submit our manuscript **"Governing Human–AI Collaborative Project Workflows in Multi-Platform Digital Delivery: A Design Science Study of Auditable Handoffs and Idempotent Release Operations"** for consideration in the *International Journal of Project Management*.

Recent project management scholarship highlights urgent questions surrounding how artificial intelligence transforms project governance, accountability, and human–machine agency (Holzmann et al., 2022; Müller et al., 2024; Raisch & Krakowski, 2021). However, empirical studies demonstrating how project managers govern heterogeneous foundation models (GPT/Codex, Claude, and DeepSeek) in live, multi-platform delivery environments remain scarce. Grounded in Design Science Research and Stage-Gate governance theory, our study demonstrates how project managers retain sole-operator authority through four enforceable governance layers: deterministic scheduling gates, cryptographic plan-hash locking, idempotent `UNKNOWN`-latching release state machines, and epistemic audit protocols.

The **21-page Main Manuscript** is accompanied by a **150-page Online Supplementary Material** compendium documenting every proof, operational log ($N=69$), cohort record ($N=55$), artifact hash ($N=30$), and verification test ($2,737$ tests). This manuscript is solely submitted to *IJPM*.

Sincerely,  
**HuaiCheng Sheng**  
Agentic Work Lab, Hangzhou Chengxin Holding Group Co., Ltd., Hangzhou, Zhejiang, China  
Email: `ZackSheng@gmail.com` | ORCID: `0009-0003-2900-6377`
