# Merchant brief example: Demo Cafe A (fictional)

[繁體中文](merchant-brief-demo-cafe-a.md) · **English**

> Every merchant detail, source and score on this page is fictional. It matches `DEMO-001` in [merchants-demo.json](merchants-demo.json) and shows how to fill in the [merchant brief template](../templates/merchant-brief_EN.md). No real contact details are involved and no message was sent.

**As of:** 2026-10-28 (fictional snapshot)  
**Merchant:** Demo Cafe A (`DEMO-001`)  
**Public sources:** `synthetic://demo/cafe-a` (fictional; a real brief lists URLs and retrieval times)

| Field | Verified information | Source and time |
| --- | --- | --- |
| Category | Independent café selling coffee and light meals. | `#profile`, 2026-10-28 |
| Territory and service area | In the fictional "Demo University Town" district; the shop lists delivery within 3 km. | `#location`, `#delivery`, 2026-10-28 |
| Offering and customer needs | Menu centred on pour-over, lattes and sandwiches; several of 20 sampled reviews mention "seats free in the afternoon" and "good for studying". | `#menu`, `#reviews`, 2026-10-28 |
| Current channels | Walk-in sales and one delivery-platform page; no membership scheme or social account found. | `#channels`, 2026-10-28 |
| Unknowns | Revenue, margin, actual traffic by hour, decision-maker, willingness to partner. Not found does not mean absent. | — |

## Inferences (to validate)

- "Seats free in the afternoon" may mean 14:00–17:00 is off-peak. This rests on reviews only, with no traffic data, and needs confirming in the interview.

## Discovery preparation

- **Business problem to understand:** whether off-peak hours actually strain the business, and how the shop handles them today.
- **Three open-ended questions:**
  1. Which hours of the day are busiest, and which are quietest?
  2. What have you tried during quiet hours, and how did it go?
  3. If you tried a new partnership, what would matter most to you?
- **Initial value proposition (to validate):** a two-week small pilot that brings measurable off-peak traffic, comparing orders in the same hours before and during the pilot.
- **Likely objections and evidence needed:** "We tried discounts and they didn't work" → bring a pilot design and how it will be measured, with no promised results; "We're short-staffed" → confirm the pilot adds no work for staff.
- **Next step, owner, date:** gather off-peak traffic data, by the researcher (fictional), before 2026-10-30; once complete, a manager decides whether to make contact.

## AI assistance and human review

- **Steps assisted by AI:** organizing fields from the sources, grouping reviews, drafting interview questions and objections.
- **Facts checked:** category, district, delivery area and menu were each checked against the source.
- **Remaining uncertainty:** see Unknowns and Inferences; no contacts, revenue or willingness to partner are invented.
- **Human corrections and time:** not measured for this example. In real use, record minutes to finish and the number of human corrections so AI-assisted and manual work can be compared.

## Feeding the prioritization script

After research, turn the verifiable results into the 0–1 fields of [merchants-demo.json](merchants-demo.json) and run `npm run demo`:

| Field | Value | Basis |
| --- | --- | --- |
| `category_fit` | 0.9 | Category matches the target type |
| `service_area_fit` | 1 | Delivery area lies inside the target district |
| `evidence_quality` | 0.8 | Main facts checked against sources; traffic data still missing |
| `evidence` | `synthetic://demo/cafe-a`, `reviewed: true` | — |

The output score is 90, `next_step` is `human_review` and `outreach_sent` is `false` (see [merchant-priorities-demo.json](../results/merchant-priorities-demo.json)). With any field missing or no reviewed source, the script marks `research_required` instead and contacts no one.

[Back to the lab](../README_EN.md)
