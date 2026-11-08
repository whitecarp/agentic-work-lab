# Multi-Agent Golden Triangle Workflow & Verification (Antigravity + Claude + Codex（及早期 DeepSeek）)

[繁體中文](model-workflow.md) · **English**

This page documents how our workflow evolved from the initial "GPT × DeepSeek two-model handoff" into the **"Antigravity + Claude + Codex Golden Triangle Workflow"** that now powers six-platform publishing, empirical Bihuo ad optimization, and three open-source tool suites. Every role below maps to concrete source code, public video releases, or time-series operational records (see the [8 November update](updates/2026-11-08_EN.md) and [3 November record](updates/2026-11-03_EN.md)).

---

## 1. Golden Triangle Division of Labor (Antigravity + Claude + Codex)

| Model & Agent Host | Core Responsibilities | Problems Solved & Verified Repository Outputs |
| --- | --- | --- |
| **Google (Gemini / Google Antigravity / Vertex AI / Google Cloud)** | **Google Cloud Official Traffic Support + Creative Hook Ideation + 24/7 Comment Patrol & Telemetry + Daily Serial Scheduling & Overlay Mounting + Branch Video Production + Public Repo Gatekeeper** | 1. **Google Cloud Official Traffic & Ecosystem Support**: Backed by official traffic support from the Google Cloud ("Google Global Innovation Track") contest unit, the 3-video Gemini Saver ([`AI Memory Keeper`](../tools/ai-memory-keeper/README_EN.md)) matrix achieved **`34,810` views, `2,048` likes, and `2,144` favorites (`4,192` total likes+favorites, including `24,283` views / `14.85%` L+S on `BV1WQ4t6xEdT` and `2.55×` favorite-to-like ratio on `BV1nw4h64Ebf`)** with `¥0` paid Bihuo spend, while Google Cloud credits and Service Account `.json` tokens power Vertex AI and Agent Platform API;<br>2. **24/7 Account Operations & Patrol**: Hourly inspection of newly published videos (`BV1WQ4t6xEdT`, `BV1Eaxt6LEhb`, `BV1sQ4E6eEhK`, `BV1YL4M6EEek`, etc.) to reply to real viewer comments in character; 2-hour read-only telemetry collection from Bilibili Creator Center and Bihuo ad console (`bihuo-readings.jsonl`);<br>3. **Daily Serial Scheduling & Interactive Component Mounting**: Runs the daily 12:00 BJT multi-part unlock queue for *WhaleGirl Shift Simulator* (`BV1YL4M6EEek`), updates description progress tables, and automatically mounts four front-loaded interactive overlays (`0.5s` follow card, `6.0s` poll, `13.5s` rating, `19.0s` jump link) at 12:30 BJT once moderation clears;<br>4. **High-Contrast Hook Ideation & Multimodal Batch Production**: Designs first-3-second visual/cognitive hooks and combines native image generation + Vertex AI (`gemini-omni-1.2-flash`) with FFmpeg pipelines to batch-produce branching ending shorts (`P4–P19`);<br>5. **Cross-Agent Memory Backbone & Public Lab Gatekeeper**: Drives [`AI Memory Keeper`](../tools/ai-memory-keeper/README_EN.md) across all agents and enforces daily bilingual documentation and GitHub Release SHA-256 verification for `Agentic Work Lab`. |
| **Anthropic (Claude / Claude Code)** | **Bihuo ROI Strategy Brain + Flagship Film Director + Cover & Frontend Visual Polish** | 1. **Bihuo Audience-Pool A/B/C/D Testing & Scaling**: Designed the 4-way audience test on dress-up short `BV1Eaxt6LEhb`, proving that scaling the `h50` (50 core creators) pool over a `72h` window lowered CPM to **`¥12.74 / 1k views`** and raised the L+S rate to **`8.03%`**; designed the 4-pool horse race for *Lost Compute*;<br>2. **Flagship Film Director**: Leads high-density HyperFrames / Remotion choreography, speech-clock pacing, and EBU R128 loudness QA;<br>3. **Desktop Workflow Toolbox**: Created [`I Must Use Claude`](../tools/i-must-use-claude/README_EN.md) around cross-timezone collaboration, glossary-locked live translation, and ≤29 MB context packaging. |
| **OpenAI (GPT / Codex)** | **Core Character Art & Keyframe Generation + `vpub` Cross-Platform Publishing & Infrastructure Engineering** | 1. **Character Visual Assets**: Generates unified character sheets, turnaround views, and keyframes for WhaleGirl, GPT, Claude, and Qwen;<br>2. **Cross-Platform Publishing Infrastructure**: Engineered [`PublishOps CrossCast`](../tools/publishops-crosscast/README_EN.md) (`vpub` CLI) resumable uploads, cross-process locks, six-platform receipt verification, and C#/.NET desktop test suites. |

---

## 2. Earlier GPT × DeepSeek Handoff & Schedule Verification (2026-10-28 Baseline Retained)

| Area | GPT/Codex workflow | DeepSeek operations workflow |
| --- | --- | --- |
| Task | Scripts, research, code, video production, and delivery. | Read checklists, due work, and shared progress for ongoing operations. |
| Evidence | Published works, upcoming cuts, timelines, and delivery checks. | Scheduled trigger, start, completion, and tool-activity records. |
| Acceptance | Separate technical checks, sampled frames, and human review. | Avoid duplicates, perform no platform writes when nothing is due, verify completion from logs. |

This is a cross-model workflow handoff and execution-consistency case. **There is no shared prompt set, fixed sample, blind review, or complete cost record, so it cannot establish a universal winner in capability, speed, or cost across all tasks.** Workflow names describe operational division of labor rather than independently authenticating the underlying model version of every API call.

### Observed Scheduled Run Verification
The verified hourly run on 2026-10-28 started 24.992 seconds after its scheduled time and completed in 31.175 seconds. The trigger was scheduled. No Bilibili actions were due, and no duplicate manual test was launched. See the [verification record](automation-verification_EN.md) and [machine-readable evidence](../data/scheduled-run-evidence.json).

---

## 3. Next: Identical-Task Merchant-Research Evaluation Plan

Give candidate models the same public merchant data, prompt, and output schema. A reviewer blinded to model names checks factual accuracy, source traceability, explicit gaps, relevance of the value proposition, and required corrections. Time and cost must come from actual request records rather than estimates.

This identical-task evaluation is a documented plan and **has not been run**. The merchant-prioritisation script in this repository is an offline demonstration, not its result.

[Back to the lab](../README_EN.md)
