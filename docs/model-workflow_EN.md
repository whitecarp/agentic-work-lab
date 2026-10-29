# GPT × DeepSeek: roles and verification

[繁體中文](model-workflow.md) · **English**

## What was actually done

| Area | GPT/Codex workflow | DeepSeek operations workflow |
| --- | --- | --- |
| Task | Scripts, research, code, video production, and delivery. | Read checklists, due work, and shared progress for ongoing operations. |
| Evidence | Published works, upcoming cuts, timelines, and delivery checks. | Scheduled trigger, start, completion, and tool-activity records. |
| Acceptance | Separate technical checks, sampled frames, and human review. | Avoid duplicates, perform no platform writes when nothing is due, verify completion from logs. |

This is a cross-model workflow handoff and execution-consistency case. **There is no shared prompt set, fixed sample, blind review, or complete cost record, so it cannot establish a winner in capability, speed, or cost.** Workflow names do not independently authenticate the underlying model version of every request.

## Observed verification

The verified hourly run on 2026-10-28 started 24.992 seconds after its scheduled time and completed in 31.175 seconds. The trigger was scheduled. No Bilibili actions were due, and no duplicate manual test was launched. See the [verification record](automation-verification_EN.md) and [machine-readable evidence](../data/scheduled-run-evidence.json).

## Next: an identical-task merchant-research test

Give both models the same public merchant data, prompt, and output schema. A reviewer blinded to model names checks factual accuracy, source traceability, explicit gaps, relevance of the value proposition, and required corrections. Time and cost must come from actual request records rather than estimates.

This identical-task evaluation is a documented plan and **has not been run**. The merchant-prioritisation script added here is an offline demonstration, not its result.

[Back to the lab](../README_EN.md)
