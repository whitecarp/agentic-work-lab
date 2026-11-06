# Peer Review Report and Changelog

**Manuscript:** Governing Human–AI Collaborative Project Workflows in Multi-Platform Digital Delivery: A Design Science Study of Auditable Handoffs and Idempotent Release Operations
**Author of the manuscript:** HuaiCheng Sheng (Agentic Work Lab, Hangzhou Chengxin Holding Group Co., Ltd.)
**Reviewed:** `paper/sci-manuscript` at commit `e3205d6` (main.tex, manuscript.pdf, build_publication_pdf.mjs), plus the extra documents added in `79d0378`
**Revision:** branch `paper/claude-review-fork` in `agentic-work-lab-claude-fork` (committed locally, not pushed)
**Date:** 4 November 2026, 23:15 US Central Time (5 November, 13:15 Beijing)
**Target journals considered:** Journal of Systems and Software (JSS), International Journal of Project Management (IJPM), IEEE Transactions on Engineering Management (IEEE TEM)

---

## 0. Read this first

1. **Some figures in the brief are not in the data.** The Mann–Whitney result (U = 65.5, p = 0.0012), the cohort means of 3,477, 95.89 and 32.56 views, "4 cohorts" and "2,377,509,759 bytes" do not appear in any data file in either repository, and no comparison of the 55-record snapshot reproduces them. The snapshot has two cohorts (45 human-led, 10 AI-series). Every human-led video has more views than every AI-series video (lowest 274 against highest 231), so a rank test on views gives U = 0, not 65.5. The p ≈ 0.0012 value happens to match the test on likes (45 vs 5), but that test's U is 12.5. These numbers appear only in Antigravity's extra documents, `CLAUDE.md` and the board brief. The revised paper does not use them and reports no significance test (Section 6.5 explains why).
2. **Several statements in the paper were invented or miscopied. They are now removed or corrected:**
   - The full SHA-256 of the film printed in Section 6.3 was wrong after the first 14 characters.
   - Two references were invented; their DOIs do not exist on Crossref, the official DOI registry. "Bianchi et al., IEEE TEM 2022" and "Müller et al., IJPM 2024" are now replaced by the real papers by those authors.
   - Table 3's five event categories could not be rebuilt from the journal: 31 of its 69 records have no name.
   - The state names in Figure 2 and the status codes in Algorithm 3 do not exist in the code.
   - Three audio values in Table 8 were miscopied.
   - Instagram was shown as "PUBLIC_VERIFIED", which the record does not say.
   - A cause was given for the Douyin timeout that no record states.
   - The unexplained local read problem was called "resolved", though the data say it is not.
3. **Your original `paper/` folder is being changed, and not by me.** Between 22:36 and 23:02 CT, files I created in the fork (`main.tex`, `references.bib`, `tex_blocks/`, `sync_main_tex.mjs`, `figures/`) appeared in `agentic-sci-paper/paper/`, and PDFs were rebuilt there. Some process copies the fork into that folder, most likely Antigravity. `watch_ops.py` is running but does not touch these folders. If the original folder should stay untouched, that process needs stopping.
4. **One accidental write of mine, already undone.** At 22:34 CT, a script run inside the fork wrote `paper_metrics_summary.json` into the original `paper/` folder; the old script had that path hard-coded. The only change was a new timestamp inside the file. I restored the exact original bytes and file date from the committed copy, and every script in the fork now writes only next to itself.
5. **The original branch also moved.** Antigravity re-created the fork folder at 22:19 CT and pushed `paper/claude-review-fork`. It also pushed two commits to `paper/sci-manuscript` (`79d0378` at 22:13 CT, and `8c21857` at 22:20 CT, which adds `CLAUDE.md`). This review compares against `e3205d6`; `main.tex` and `manuscript.pdf` are unchanged there.
6. **Decisions that are yours:**
   - **AI-assistance disclosure.** Elsevier and IEEE ask authors to declare generative-AI help in preparing a manuscript. Whether and how to declare it is your decision; I have not written such a statement anywhere.
   - **Competing interests.** The paper says "no known competing financial interests". CrossCast's YouTube, X and Instagram API routes are sold separately as a paid add-on. If that add-on is or will be sold, the journals' conflict-of-interest rules would normally expect it declared. The declaration text is unchanged.
   - **Licence.** `agentic-work-lab` has no licence file. The paper's data and code availability depends on it, so you may want to add one before submission.
   - **Abstract length.** The abstract is now 304 words (it was about 400). Check the chosen journal's limit; many cap abstracts at 250 words or fewer.
   - **Pushing.** Nothing is pushed. The branch already exists on GitHub because Antigravity pushed it.
   - **`CLAUDE.md` at the repository root** repeats the unsupported figures. I left it alone, since the workboard rules forbid agents to edit `CLAUDE.md`.

---

## 1. Reviewer's overall assessment

**Recommendation for the submitted version:** major revision; desk-rejection risk at all three journals.

The design idea is good and publishable: a stage gate expressed as code, approvals bound to content, a receipt saved *before* the one irreversible click, and honest reporting of non-comparable data. The CrossCast code really implements the central mechanism. `browser_backends.py` saves the receipt as `UNKNOWN` with `final_submit_attempted = true` before pressing, with the comment "Persist BEFORE clicking. A timeout must never cause another click." A later run refuses to press again.

The submitted version undermined this in three ways:
- **Factual integrity.** It contained invented details (a digest, references, categories, state names, status codes), copying errors, and counts attributed to the wrong software version. Any of these, found by a reviewer, would sink the paper.
- **Over-claiming.** "Suppressed 68 unauthorized writes" read an absence of writes as proof of blocking. Other examples:
  - "Guaranteeing zero duplicates under arbitrary faults" ignored the code's one exception.
  - "Longitudinal" described nine days.
  - "Ablation" described baselines that were never run.
  - "Across 5 platforms" was attached to a single Instagram timing.
- **Source and PDF out of sync.** `main.tex` was not the source of the 21-page PDF: it had 2 figures instead of 5, different contributions and research questions, and two LaTeX errors that would stop compilation.

**Assessment of the revised version:** the claims now match the evidence. The theory is stronger, and the limits are stated before a reviewer can raise them. What remains is the inherent limit of a single-case, nine-day study, which the paper now argues for rather than hides.

---

## 2. Review by dimension

### 2.1 Argumentation and alignment of the research questions

| Issue in the submitted version | Change |
|---|---|
| RQ1–RQ3 were never answered explicitly; the Discussion did not return to them. | New Section 7.2 answers each RQ with its evidence and its limit. |
| RQ1's evidence (Table 3) was an invented taxonomy, and "68 suppressed writes" confused absence with suppression. | Table 3 now classifies records by the evidence they carry. The text says plainly what the journal can and cannot show, and points to the gate's real evidence (the automation records, the heartbeat and the tests). |
| RQ2's proposition claimed zero duplicates under arbitrary faults, with no assumptions. | Proposition 1 now states assumptions A1–A4, including the code's one exception (a resumed run may press again when the platform shows a draft), and calls the result a safety guarantee, not a liveness one. |
| RQ2's field evidence was a single Douyin case. | Two more real instances were found and added: a Douyin scheduled-card test on 1 November with the same read-back timeout, and a private YouTube upload killed after 8,388,608 acknowledged bytes that resumed in a second process with the same video ID (30 October, version 0.15.1). |
| RQ3 called nine days "longitudinal". | RQ3 now says "a nine-day field deployment", and a new Section 3.3 lists each data source's dates. |

### 2.2 Theoretical depth

New **Section 2.4 (Reading Stage-Gate Control as a Commit Protocol)** joins project governance and distributed-systems reliability through five correspondences. Sections 4 and 7.1 then use them:

1. **Gatekeeping as admission control.** Cooper; Too & Weaver, for decision rights in project governance.
2. **Configuration baselines as content addresses.** Whyte et al.: the plan digest binds the approval to the exact content reviewed.
3. **The go decision as an at-most-once commit.** Helland; Kleppmann; Gray & Reuter. Without idempotency keys on the platform, the client can only choose at-least-once or at-most-once, and writing the intent down first (write-ahead) chooses at-most-once.
4. **Post-gate review as an end-to-end check.** Saltzer, Reed & Clark: only read-back from the platform proves the effect happened.
5. **Escalation as resolution of in-doubt transactions.** The UNKNOWN receipt is the "in-doubt" state of two-phase commit. Bainbridge's "ironies of automation" explain why the person ends up with the hardest cases. The saga compensation of Garcia-Molina & Salem shows why delete-and-repost is not a safe recovery for public posts.

Section 7.1 now makes four theoretical claims:
- Stage gates become commit protocols when agents act.
- Automation authority depends on the receipt's state.
- At-most-once moves work to people, and that work needs design.
- Epistemic governance is a project deliverable.

### 2.3 Wording a tough reviewer would attack, and the defenses added

| Attackable wording (submitted) | Revised wording or defense |
|---|---|
| "suppressed unauthorized mutations in 68 non-write intervals" | "one recorded write and no recorded replay; absence of writes is consistent with, but not evidence of, suppression" (6.1, 7.4) |
| "guaranteeing zero duplicate publications under arbitrary … faults" | Proposition 1 with assumptions A1–A4; "safety, not liveness" (4.4) |
| "trace-driven governance ablation" with FAIL/PASS outcomes for baselines never run | "analytical comparison"; baselines marked "reasoned, not executed"; every observed outcome labelled with its evidence type (field, test, review, code) (Table 11) |
| "92.32% … across 5 platforms" (highlight), "post-submission confirmation wait" | one Instagram run; the wait includes transfer, processing and confirmation, which cannot be separated (abstract, highlights, Table 7) |
| "empirically proving / validating the necessity" | "motivates" (contributions, 5.2) |
| "timeout anxiety" | removed |
| "2,737 distinct assertions", "fault-injection coverage" | "2,737 test functions"; counts measure checking volume, not coverage (6.2) |
| "longitudinal" | "nine-day field deployment"; dated evidence window (3.3) |
| single organization, author as operator | new "Researcher role" paragraph (3.1, 7.4): action design research / technical action research; contemporaneous receipts; public data; recomputation script |
| platform heterogeneity | external validity now argues analytical generalization by mechanism (Flyvbjerg; Wieringa & Daneva): any agent submitting to an endpoint without idempotency keys faces the same retry dilemma; the latency figures are not claimed to transfer |
| model identities | "model names denote workflow roles; records do not authenticate model versions" (4.1, 7.4) |
| who approved | the `--confirm` digest binds content, while `--person-approved` is the caller's attestation, as the CLI itself states (4.3) |
| `planRun` can be bypassed | stated: the gate is advisory; enforcement at the platform boundary comes from Layers 2–3 inside the release command (4.2) |
| comparing cohorts | no significance test, with the reason given; the "human-led" label does not mean no AI was used; weekly-play growth is shown to come from old travel vlogs and TV clients (6.5) |

### 2.4 Fact-check against the ground-truth repositories

**Confirmed exactly:**
- The 30-video manifest: every byte count, SHA-256 prefix and duration; 2,211,716,129 bytes in total; the manifest matches `SHA256SUMS.txt`.
- The heartbeat: 24,992 ms delay, 31,175 ms run, no task errors.
- All seven Instagram timings and their percentages.
- Every statistic in Table 10, as sample SD with linear quartiles.
- Weekly plays of 76,162 against 21,398, and both 24-hour checkpoints.
- 123 source files, 126 test files and 2,737 test functions in CrossCast. These counts hold at commit `75b7abc`, not at v0.16.0 (`a46bd19`: 107 / 84 / 2,009).
- `planRun` matches Algorithm 1.
- The plan hash is SHA-256 of sorted-key, compact JSON.
- The receipt latch works as described above.
- 18 of the 20 DOIs in the old bibliography; the other two were invented.
- GitHub CI for `75b7abc` passed on Python 3.10–3.14.

**Corrected:**

| Item | Submitted | Ground truth |
|---|---|---|
| Film SHA-256 (6.3) | `7d6fd76090f7a90b79d8…` | `7d6fd76090f7a93b2711e15db4bb924f741d3a904a01fc4b7628fc2130dbafdc` |
| Operational journal window | "26–28 October" | 46 of 69 records are timestamped, 26 Oct 01:27 to 27 Oct 02:45 (UTC+8); 25.3 h |
| Table 3 categories 32/16/16/4/1; reads 9/4/4/0/4 (sum 21) against a total of 17 | not derivable | evidence classes 20/39/1/1/8; reads 13/0/–/4/–; total 17 |
| "0 replays across all 69", "0 new risks" | — | the replay field appears in 1 record; `new_risk` appears in 8 (all false) |
| Code size | "27,095 / 37,155 LOC, v0.16.0" | 26,972 / 37,029 physical lines at `75b7abc`; the old numbers add one line per file. 42 of the 123 source files are the unmodified bundled bili-cli v1.2.1 (MIT); CrossCast's own package is 81 files and 18,005 lines |
| Module names | `upload_resume.py`, `browser_session.py`, `full_read*.py`, `playlists.py` | these exist only in the lab's 0.15.1 copy, not at `75b7abc` |
| Figure 2 states | PLANNED, PREFLIGHT_OK, MEDIA_STAGED, SUBMITTED_WAIT, UNKNOWN_LATCHED, USER_CONFIRMED | real states: PREPARING, UNKNOWN, SUBMISSION_VERIFIED, SUBMITTED_UNVERIFIED, WAITING_FOR_USER |
| Algorithm 3 codes | NO_SAVE_REQUIRED_MATCHED, EDIT_MISMATCH_DETECTED, MISSING_EDIT_ACCESS_NO_DELETE_OR_REPOST | real codes: EDIT_VERIFIED, EDIT_MISMATCH, EDIT_UNAVAILABLE, EDIT_SUBMITTED_UNVERIFIED |
| "neighbouring works intact" after deletion | claimed | not checked by the code; removed |
| Table 8, GPT Duty Log 05 | −1.21 dBTP | −2.21 dBTP |
| Table 8, transformation v4 peak | −0.636 dBFS | ≈ −1.636 dBFS |
| Table 8, episode 07 cuts | −0.5 dBFS peak | peaks ≈ −1.5 dBFS |
| Table 6, Instagram | PUBLIC_VERIFIED | only `SUBMISSION_VERIFIED` and metadata read-back; the owner replaced the cover on mobile |
| Table 6, Bilibili | REPORTED_PUBLIC / PUBLIC_PER_SESSION | `public_reported_by_production_session`, `fresh_check = false` |
| Table 6, YouTube route | "Official API" | the record says only "continued from the original draft" |
| Douyin cause | "due to platform UI verification constraints" | cause not recorded |
| Local read discrepancy | "resolved on subsequent reads" | `historical_local_read_discrepancy_resolved: false` |
| "every video must pass full decode" | — | the manifest records a zero-error full decode for 5 of 30 (entries 26–30) |
| Workflow test file | 38 lines, "108.5 ms" | 27 lines; 10/10 recorded on 28 Oct; no timing recorded |
| Table 2, Claude's role | PRs 8, 9 and 10–30 attributed to Claude | commit metadata records no model per change; branches are `codex/*` |
| References | 35 listed, 32 in the .bib, 4 never cited, 2 invented | 41, all cited, all checked |

---

## 3. Changelog: `paper/claude-review-fork` against `paper/sci-manuscript` (e3205d6)

**How the revision was made.** The 21-page PDF is generated by `build_publication_pdf.mjs`, and the old `main.tex` was a shorter, different version. I therefore revised the PDF source once, then generated `main.tex` from the same HTML with the new `sync_main_tex.mjs`. Equations, algorithms, the invariants and Proposition 1 are hand-written LaTeX in `tex_blocks/`. Both documents now have the same text, 11 tables, 5 figures (the same drawings, exported to `figures/fig1–5.pdf`), 3 algorithms with identical line numbers, 3 equations and the same 41 references. The script refuses to run if a citation has no bibliography entry or an entry is uncited.

| Section | Changes |
|---|---|
| Abstract | Rewritten to match the evidence: what was evaluated, with dates; "one platform write and no replayed upload"; the Douyin in-doubt case; the Instagram wait for one run only. 304 words, was about 400. |
| Highlights | Five new bullets of 73–76 characters each; the 92.32% bullet now says "one Instagram run". |
| 1 Introduction | Failure modes softened from "frequently" to "can"; the missing idempotency key added as the core gap (Helland). RQ3 now says nine-day field deployment. Contributions reworded (no "proving"); the test-suite growth from 2,009 to 2,737 functions added; a scope sentence (single-case) added. |
| 2.1–2.3 | Fabricated Bianchi citation replaced by Conforto & Amaral 2016 and the real Bianchi et al. 2022. Project-governance definition added (Too & Weaver). Amershi et al. now cited. Idempotency-key sentence added. |
| Table 1 | Caption explains what "No" means; the unexplained "(e.g., SAU)" removed; the "Ours" row made accurate (browser + bili-cli, API add-on separate; receipt saved as UNKNOWN before the press). |
| 2.4 (new) | Theoretical synthesis (Section 2.2 above). |
| 3.1 | "Field demonstration" with dates; evaluation described as naturalistic plus artificial (FEDS); new "Researcher role" paragraph. |
| 3.3 (new) | Data sources and their sampling windows. |
| Figure 1 | Layer 2a no longer credits `check-delivery` with decode checks; Layer 3 label corrected to commit `75b7abc` and the actual latch. |
| 4.1 / Table 2 | Claude's row no longer attributes specific PRs; a note that model names are workflow roles. |
| 4.2 | Eq. 3 matches the code (finite times, start ≤ end); the text explains when the gate throws, and that it is advisory. |
| 4.3 | Invariant 1 limited to name, size and SHA-256 (decode checks are separate records); Invariant 2 states the exact JSON serialization. The attestation boundary of `--person-approved` and the fact that the plan digest names the receipt file are added. The "targeting −15.97 to −17.5 LUFS" claim removed. |
| 4.4 | Figure 2 redrawn with the code's real states and the blocked second press; Algorithm 2 rewritten to the real control flow (attempt_exists, receipt_conflict, receipt_missing, already_submitted, UNKNOWN saved before the press, unique-match read-back); Proposition 1 with assumptions and proof sketch; safety-versus-liveness paragraph. |
| 4.5 | Algorithm 3 uses real status codes; deletion requires a dry run and a person's approval; the neighbour claim is dropped; the saga link added. |
| 5.1 / Figure 3 | Wording ("targeted", not "surgical"); caption points to Table 8 for values. |
| 5.2 / Figure 4 | Correct counts at `75b7abc` with the bili-cli disclosure; CI result; "no concurrent run has been timed"; API routes outside the evaluated snapshot. |
| 6.1 / Table 3 | Rebuilt from the data (Section 2.4 above); the text on what the journal can and cannot show; heartbeat paragraph with its four recorded work items. |
| 6.2 / Table 4 | Test groups recomputed by a file-name rule that the script documents; physical line counts; helper files counted; test-to-source ratio 1.37 (2.06 against CrossCast's own package); lab rows corrected. |
| Table 5 | "Lab commit 596e951"; TikTok wording; the YouTube crash-recovery check added; v0.16.0 had 2,009 test functions; CI result for `75b7abc`. |
| 6.3 / Table 6 | True SHA-256; Table 6 rebuilt with states quoted as recorded; new paragraph on the two Douyin read-back timeouts and on what is inference and what is observation. |
| Table 7 / Figure 5 | Row 5 says the wait cannot be separated; row 7 marked as derived; caption cites the source's warning that the phases are not exhaustive; N = 1 in the caption. |
| 6.4 / Tables 8–9 | Three audio values corrected; "reported fixed"; Table 9 uses MiB and explains the State column; "100% verified" replaced by what was actually recorded. |
| 6.5 / Table 10 | Age range 1.6 h–3.1 days; the paragraph rewritten (complete separation, no significance test, the label caveat, weekly-play evidence). |
| Table 11 | Re-framed as an analytical comparison with evidence types; outcomes reworded as "would" for baselines and "observed or tested" for the proposal. |
| 7.1 | Four theoretical implications, building on 2.4. |
| 7.2 (new) | Answers to RQ1–RQ3 with evidence and limits. |
| 7.3 | Lessons reworded to one-run evidence; new "budget for in-doubt resolution". |
| 7.4 | Threats to validity expanded: internal, researcher role, construct, external (analytical generalization), reliability. |
| 8 | Conclusion matched to the evidence; future work added: log gate decisions and refused attempts; time concurrency; run the blinded model test; replicate with other teams. |
| Data availability | Commits pinned (`e3205d6` data, `75b7abc` code); recomputation script; bili-cli and API add-on noted. |
| References | 41 entries: 2 invented replaced, 9 added (Saltzer et al.; Helland; Garcia-Molina & Salem; Bainbridge; Sein et al.; Wieringa; Wieringa & Daneva; Too & Weaver; Conforto & Amaral), 3 uncited removed (Lamport, Turner, Wooldridge). Every DOI checked against Crossref on 4 November 2026. |
| LaTeX defects | The stray "---" lines (which print as dashes), the unescaped "&" in RQ3 and the raw underscores in Section 7.1 are all gone with the regenerated `main.tex`. |

**Supporting files in `paper/`:**
- **New:** `sync_main_tex.mjs`, `tex_blocks/` (eight LaTeX blocks), `figures/fig1–5.pdf`, and this report.
- **Rewritten:**
  - `reproduce_paper_tables.mjs` now recomputes every figure in the paper (Tables 3, 4, 6, 7, 9, 10, the heartbeat and the code counts, which it reads from CrossCast with `git show`) into `paper_metrics_summary.json`.
  - `build_extra_sci_deliverables.mjs` regenerates the highlights, the graphical abstract (redrawn from verified facts), the declarations and code metadata (wrong version numbers, docs, commands, tools and operating-system claims fixed; pronoun-neutral), three tailored cover letters and a journal-neutral cover letter.
- **Corrected:**
  - `build_publication_pdf.mjs` writes only next to itself and exports the figures for `main.tex`.
  - `build_supplementary_pdf.mjs`: Appendix A is restated to match the revised algorithms and Proposition 1. Appendix B's table no longer invents event names or fills missing fields with 0 or false. Appendices E and F use physical line counts and disclose the bundled bili-cli.
  - `verify_all_submission_artifacts.mjs` describes and hashes this checkout's files.
- **Rebuilt outputs:** `manuscript.pdf` (22 pages, was 21) and `supplementary_material.pdf` (142 pages, was 150). The supplement is shorter only because Appendix B no longer prints invented labels; all ledgers are complete, with the same 3,267 table rows.
- **Marked superseded, not rewritten:** `reviewer_defense_playbook.*`, `manuscript_CN_companion.pdf` and `manuscript_full_EN.md` now carry an English and Chinese banner. Their content is built around the unsupported statistics.

---

## 4. What was checked, and what was not

**Checked:**
- Every number in the revised paper was checked against the data files, the lab's dated records or the code at `75b7abc`. Those derived from data are recomputed by `reproduce_paper_tables.mjs` into `paper_metrics_summary.json`.
- All 31 DOIs in the bibliography resolve on Crossref with matching titles, authors, volumes and pages. The other 10 entries are books and conference papers without DOIs.
- The PDF builds to 22 pages.
- `main.tex` passed structural checks: balanced braces and math, matched environments, no unescaped special characters, plain ASCII, figure files present, no uncited references.
- The figures were rendered and inspected.
- The 10 lab workflow tests passed locally.

**Not checked:**
- **`main.tex` was not compiled.** There is no LaTeX engine on this PC; please run one compile in Overleaf. The PDF's page count does not predict the LaTeX page count, which also depends on the journal template.
- **The CrossCast test suite was not run here** (no Python on this PC); the paper cites GitHub CI instead.
- **The 30 media files were not re-downloaded or re-hashed.** The paper relies on the lab's recorded GitHub verification of 3 November.
- **The supplement was corrected only in Appendices A, B, E, F and H.** Its other ledgers are generated from the repositories and were not reviewed line by line.
- **Antigravity's extra documents were rewritten or marked, not reviewed for style.**

---

## 5. Recommendations not carried out

1. **Archive the evidence.** Create a citable archive (for example, a versioned DOI) of both repositories at the submission commit, and cite it instead of branch names.
2. **Rebuild the submission packages.** `SCI_Submission_Overleaf_Package.zip` and `SCI_Submission_Complete_Package.zip` are stale; rebuild them from this branch (the Overleaf package needs `figures/` and the new `references.bib`).
3. **Retire or rewrite the old helper documents.** Either rewrite the reviewer playbook and the Chinese companion from the revised paper, or delete them.
4. **Fix the gap the reviewers will press hardest.** Log gate decisions and refused attempts, so that "suppression" becomes measurable in a follow-up study.
5. **Prepare for the chosen journal.** Pick the target journal before formatting (elsarticle for JSS and IJPM, IEEEtran for TEM), then check the word, abstract and page limits and the highlights file format.
6. **Minor.** Single-author papers often avoid "we"; consider "this study" where it reads naturally.
