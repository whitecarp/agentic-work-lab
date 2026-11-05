# Agentic Work Lab — SCI Manuscript & Multi-Agent Handoff Protocol (`CLAUDE.md`)

## 1. Repository & Branch Isolation
- **Fork Workspace:** `C:\Users\ROG\.gemini\antigravity\scratch\agentic-sci-paper\agentic-work-lab-claude-fork` (Branch: `paper/claude-review-fork`)
- **Primary Workspace:** `C:\Users\ROG\.gemini\antigravity\scratch\agentic-sci-paper\agentic-work-lab` (Branch: `paper/sci-manuscript`)
- **Sibling Code Checkout (Read-Only):** `C:\Users\ROG\.gemini\antigravity\scratch\agentic-sci-paper\publishops-crosscast` (Evaluated commit: `75b7abc`; `v0.16.0` tag: `a46bd19`)

## 2. Locked Author Identity & Affiliation (Do Not Alter)
- **Author:** `HuaiCheng Sheng`
- **Affiliation:** `Agentic Work Lab, Hangzhou Chengxin Holding Group Co., Ltd., Hangzhou, Zhejiang, China`
- **Corresponding Email:** `ZackSheng@gmail.com`
- **ORCID:** `0009-0003-2900-6377` (`https://orcid.org/0009-0003-2900-6377`)
- **License:** MIT License (`LICENSE`)

## 3. Verified Ground-Truth Metrics (`node paper/reproduce_paper_tables.mjs`)
All numbers reported in the manuscript and companion deliverables are deterministically recomputed into `paper/paper_metrics_summary.json` by running `node paper/reproduce_paper_tables.mjs`:
1. **Operational Journal (`data/operations-events.json`, $N = 69$ records; 46 timestamped across 25.3 h, 26–27 Oct 2026 UTC+8):**
   - `explicit_zero_write`: `20` records (`28.99%`, `13` platform reads, `0` writes)
   - `zero_platform_calls`: `39` records (`56.52%`)
   - `platform_call_without_write_field`: `1` record (`#22`)
   - `platform_write`: `1` record (`#52`: `4` reads, `1` write, `replay = 0`, `new_risk = false`)
   - `no_call_or_write_field`: `8` records (`11.59%`, early local checks)
   - Timed task runs (`#67–#69`, $n = 3$): mean start delay `24.992 s`, mean duration `31.175 s`, `0` task errors; `tests/workflows.test.mjs`: `10/10` passed.
2. **Software & Test Volume (`publishops-crosscast` at commit `75b7abc` vs. `v0.16.0` `a46bd19`):**
   - `123` Python source files (`26,972` physical lines) = `81` first-party `CrossCast` files (`18,005` lines) + `42` unmodified bundled `bili-cli` v1.2.1 files (`8,967` lines, MIT).
   - `126` test files (`122` test modules + `4` helpers, `37,029` physical lines, `2,737` `def test_*` functions; test-to-source line ratio `1.37` overall, `2.06` against `CrossCast`'s own package).
3. **Hybrid 5-Platform Release & Recovery Evidence:**
   - Platforms evaluated: Bilibili, YouTube, X, Douyin, Instagram.
   - Two Douyin post-submit read-back timeouts latched safely in `UNKNOWN` (`final_submit_attempted = true` persisted before `PressFinalSubmitOnce()`), preventing duplicate submission; one private YouTube upload killed at `8,388,608` acknowledged bytes resumed in a second process under the same video ID.
   - Timed Instagram run ($N = 1$): `423.236 s` end-to-end, `412.217 s` inside `vpub publish`, of which `380.551 s` (`92.32%`) is spent in the post-press transfer/processing/confirmation wait (`0.289 s` derived remainder).
4. **Media Manifest (`data/media-manifest.json`, $N = 30$ MP4 versions):**
   - Exact total size: `2,211,716,129 bytes` (`2,109.26 MiB` / `2.21 GB` decimal), `0` SHA-256 mismatches against `media/SHA256SUMS.txt`.
5. **Channel Snapshot (`data/creator-video-snapshot.json`, $N = 55$ records):**
   - `human_led_history` ($n = 45$, Oct 2019–Oct 2026): views mean `40,884.31`, SD `64,604.81`, median `15,407.0`, IQR `26,315.0`, range `274–333,725`.
   - `current_ai_series` ($n = 10$: `5` published aged `1.6 h–3.1 days`, `5` scheduled): published views mean `133.20`, SD `99.53`, median `183.0`, IQR `148.0`, range `11–231`.
   - Complete separation ($\min(\text{human}) = 274 > \max(\text{AI}) = 231$).

## 4. Build & Synchronization Pipeline (`paper/`)
```bash
node paper/reproduce_paper_tables.mjs          # Recomputes paper/paper_metrics_summary.json
node paper/build_publication_pdf.mjs           # Builds manuscript.pdf (22 pages) + figures/fig1..5.pdf
node paper/sync_main_tex.mjs                   # Generates main.tex from manuscript_print.html + tex_blocks/
node paper/build_supplementary_pdf.mjs         # Builds supplementary_material.pdf (142 pages, Appendices A–H)
node paper/build_extra_sci_deliverables.mjs    # Builds highlights, graphical_abstract, declarations, cover_letters
node paper/build_defense_and_cn_companion.mjs  # Builds manuscript_full_EN.md, reviewer_defense_playbook.pdf, manuscript_CN_companion.pdf
node paper/verify_all_submission_artifacts.mjs # Verifies all 21 deliverables and writes SUBMISSION_AUDIT_RECEIPT.json
```
