# AI-assisted research: a merchant example

[繁體中文](business-development.md) · **English**

## From a request to a reusable workflow

Turn an ambiguous request into a workflow, select tools, retain sources, verify the output, and package the method for teammates. The Bilibili project provides real research, delivery, scheduling, and analytics records. The merchant workflow is an offline demonstration of applying those practices to merchant research.

## Example workflow

| Task | AI contribution | Human decision and output |
| --- | --- | --- |
| Market research | Structure public sources, classify business types, flag gaps. | Verify business and service-area facts; produce a one-page brief. |
| Prioritisation | Apply transparent criteria to organise leads. | Adjust priorities to territory strategy and capacity. |
| Conversation preparation | Draft value propositions, discovery questions, and objection responses. | Check claims and merchant context before outreach. |
| Follow-up | Convert notes into needs, commitments, owners, and dates. | Confirm the summary before updating CRM. |
| Team enablement | Standardise prompts, fields, examples, and checks. | Maintain useful practices and lessons together. |

## Runnable example

```sh
npm run demo
npm test
```

The input contains three fictional merchants. See [merchant-priorities-demo.json](../results/merchant-priorities-demo.json) for the output. Transparent weights produce a prioritised list; missing service-area information produces `research_required`, with no automatic messaging. The script processes structured data; it makes no model calls and does not present rule-based scores as AI predictions.

## A small team pilot

Start with one territory and merchant type. Measure the time and errors involved in preparing a brief manually. Trial the AI-assisted workflow using the same requirements, with peer checks of sources, completeness, and usability. Expand only if measured results justify it, and hand over templates, examples, and common failure cases together.

Suggested measures are median brief-preparation time, source traceability, human corrections, and follow-up completion. Signing rates need a separate observation window and comparison; faster preparation is not itself revenue growth. This is a pilot design, with no claimed real-merchant or team-efficiency results yet.

[Back to the lab](../README_EN.md)
