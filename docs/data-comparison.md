# 數據對比與判讀 / Data comparison and interpretation

來源：2026-10-28 取得的創作中心快照，詳見 [CSV](../data/creator-video-snapshot.csv) 與 [JSON](../data/creator-video-snapshot.json)。下表可由 `npm run report` 重算；結果見 [channel-summary.json](../results/channel-summary.json)。

Source: the creator-centre snapshot collected on 2026-10-28. See the [CSV](../data/creator-video-snapshot.csv) and [JSON](../data/creator-video-snapshot.json). Recompute with `npm run report`; output is in [channel-summary.json](../results/channel-summary.json).

| 指標 / Metric | 歷史人類主導 / Human-led history | 目前 AI 系列 / Current AI series |
|---|---:|---:|
| 稿件數 / Upload records | 45 | 7 |
| 已公開 / Public | 45 | 4 |
| 排期待發布 / Scheduled | 0 | 3 |
| 已公開稿件累積播放合計 / Sum of lifetime views | 1,839,368 | 507 |
| 已公開稿件播放中位數 / Median lifetime views | 15,407 | 146 |
| 發布日期範圍 / Publication range | 2019-10-08 – 2026-10-08 | 2026-10-25 – 2026-10-28 |

這張表描述資料覆蓋與現況，**不是模型效果排行榜**。歷史作品有多年累積、題材不同；新系列只有四部已公開影片。不能由此估算 AI 帶來的成長、簽約提升或人力節省。

This table describes coverage and the snapshot, **not a model-performance leaderboard**. Historical uploads have years of exposure and different topics; the new series has only four public works. It cannot establish AI-driven growth, signing-rate improvements, or labour savings.

目前週資料回傳播放 76,162、前期 21,398，以及粉絲增加 40、減少 29、淨增 11。這是全帳號資料，包含歷史作品；不是 AI 系列的獨立增量。[資料 / Data](../data/channel-weekly.json)

The weekly endpoint reported 76,162 plays versus 21,398 in the previous period, with 40 follows, 29 unfollows, and 11 net new followers. These are channel-wide metrics including historical works, not isolated incremental results of the AI series. [Data](../data/channel-weekly.json)

## 帶到 BD 工作的判斷習慣 / What transfers to BD

先統一指標口徑，再討論效果。對商戶拓展而言，名單數、有效研究數、已聯絡數、有效會談數與簽約數應分開。若要證明 AI 提效，應比較相同任務的完成時間與人工修正量，並記錄樣本大小；不能將產出更多文字當作業績提升。

Define metrics before discussing impact. In merchant expansion, distinguish listed leads, researched leads, contacts made, meaningful meetings, and signed partners. To demonstrate AI efficiency, compare time and human corrections on equivalent tasks and record sample sizes; generating more text is not a business outcome.
