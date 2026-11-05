# Cover Letter for SCI / SSCI Journal Submission

**Date:** November 5, 2026

**To:**
The Editor-in-Chief
*Journal of Systems and Software / IEEE Transactions on Engineering Management*

**Subject:** Submission of Original Research Manuscript: *"Governing Human–AI Collaborative Project Workflows in Multi-Platform Digital Delivery: A Design Science Study of Auditable Handoffs and Idempotent Release Operations"*

Dear Editor-in-Chief,

I am pleased to submit our original research manuscript entitled **"Governing Human–AI Collaborative Project Workflows in Multi-Platform Digital Delivery: A Design Science Study of Auditable Handoffs and Idempotent Release Operations"** for consideration for publication.

As Large Language Model (LLM) agents increasingly transition from passive chat assistants to autonomous execution actors in project delivery and operations, project managers face severe governance challenges: uncoordinated multi-agent role handoffs, non-idempotent external side effects (such as duplicate publications when platform read-backs time out), and confounded performance claims. Grounded in the Design Science Research (DSR) paradigm and stage-gate project control theory, this study presents the design, formalization, and empirical evaluation of **Agentic Work Lab** and **PublishOps CrossCast (`vpub`)**—an open-source human-in-the-loop (HITL) project governance framework and multi-platform release operations console.

The primary contributions of this manuscript include:
1. **A Four-Layer Agentic Project Governance Architecture:** Formalizing stage-gate controls into deterministic software invariants across (i) sole-operator and temporal scheduling gates (`planRun`), (ii) cryptographic plan-hash locking (`--confirm CURRENT_PLAN_HASH`) and streaming SHA-256 media verification, (iii) an idempotent receipt state machine that latches uncertain post-submission outcomes (`UNKNOWN` states) to suppress automatic retry faults, and (iv) independent read-back and HITL verification handoffs.
2. **Comprehensive Empirical & Software Evaluation:** Validating the architecture across **69 de-identified operational event records** (achieving 68 zero-write monitoring intervals, 1 authorized write, 0 duplicate upload replays, and 0 new risk incidents), **30 cryptographically verified video delivery versions** (2.21 GB total), **55 channel cohort records**, and **2,737 automated unit/integration test cases** across 126 test modules in *PublishOps CrossCast* (`v0.16.0`).
3. **Cross-Platform Release & Latency Bottleneck Discovery:** Demonstrating live release governance across five major platforms (Bilibili, YouTube, X, Douyin, and Instagram), showing how `UNKNOWN`-state latching prevented duplicate publication during a Douyin read-back timeout, and proving via phase-level timing decomposition that **92.32% (380.551 s out of 412.217 s)** of browser-mediated publication latency occurs during the post-submission platform confirmation wait.
4. **Epistemic Hygiene Protocol for AI Project Reporting:** Establishing machine-readable boundaries between descriptive cohort metrics and causal inference to prevent exposure-age confounding in human–AI comparative studies.

All source code, schemas, and de-identified empirical datasets are publicly archived on GitHub (`https://github.com/whitecarp/agentic-work-lab` and `https://github.com/whitecarp/publishops-crosscast`) to ensure complete reproducibility.

This manuscript is original, has not been published previously, and is not under consideration for publication elsewhere. The author declares no competing financial or personal interests.

Thank you very much for your time and consideration of this manuscript. I look forward to hearing from you.

Sincerely,

**Zack Sheng**
Department of Project Management, Agentic Work Lab
E-mail: `zacksheng2000@gmail.com`
GitHub: `https://github.com/whitecarp`
