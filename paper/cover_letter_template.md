# Cover Letter (journal-neutral template)

**Date:** November 2026
**To:** The Editor-in-Chief, [Journal name]
**Subject:** Submission of an original research article — "Governing Human–AI Collaborative Project Workflows in Multi-Platform Digital Delivery: A Design Science Study of Auditable Handoffs and Idempotent Release Operations"

Dear Editor,

Please consider the enclosed manuscript, "Governing Human–AI Collaborative Project Workflows in Multi-Platform Digital Delivery: A Design Science Study of Auditable Handoffs and Idempotent Release Operations", for publication in *[Journal name]*.

LLM agents increasingly act on production systems that cannot undo or de-duplicate their actions. Consumer publishing platforms offer no idempotency key for web submission, so an agent that retries an uncertain upload can publish twice. Following Design Science Research, this study designs and evaluates a four-layer governance architecture implemented in two open-source artifacts, Agentic Work Lab and PublishOps CrossCast: a deterministic admission gate, binding of each release command to the digest of the reviewed plan, a receipt state machine that is saved as UNKNOWN before the single final submission and never re-submits automatically, and independent read-back with escalation to a person.

This study evaluates the framework in a nine-day field deployment (26 October–3 November 2026) in one creator–engineering laboratory. A 69-record operational journal records one platform write and no replayed upload; when Douyin's programmatic read-back timed out after the single final submission, the receipt stayed UNKNOWN, nothing was re-submitted, and the owner confirmed the work by phone. In one instrumented Instagram publication, 92.32% of the 412.217 s publish command was the wait after Share. The evaluated release software (PublishOps CrossCast, commit 75b7abc) carries 2,737 offline test functions, and 30 video versions (2.21 GB) are archived with recorded SHA-256 digests.

The contribution is a governance design for AI-executed project work whose behaviour can be checked: deterministic gates, approvals bound to content, at-most-once submission with human reconciliation, and a reporting protocol that keeps descriptive evidence apart from causal claims.

The study is a single-case design science evaluation. The manuscript states the assumptions under which automated re-submission cannot occur, reports every figure descriptively, keeps human-led and AI-assisted cohorts apart rather than comparing them causally, and publishes the data, code and a script that recomputes every reported figure.

The manuscript (11 tables, 5 figures, 3 algorithms, 50 references; 24 pages as a single-column PDF, 32 pages in the single-column LaTeX build) is accompanied by online supplementary material. It is original, has not been published previously, and is not under consideration elsewhere.

Sincerely,
HuaiCheng Sheng
Agentic Work Lab, Hangzhou Chengxin Holding Group Co., Ltd., Hangzhou, Zhejiang, China
E-mail: ZackSheng@gmail.com | ORCID: 0009-0003-2900-6377
