# 數據對比、必火投流實證與 Google 自動化營運判讀

**繁體中文** · [English](data-comparison_EN.md)

基線來源：2026-10-28 晚間取得的創作中心快照，詳見 [CSV](../data/creator-video-snapshot.csv) 與 [JSON](../data/creator-video-snapshot.json)。下表可由 `npm run report` 重算；結果見 [channel-summary.json](../results/channel-summary.json)。

| 指標 | 歷史人類主導（2019–2026-10） | AI 系列基線快照（2026-10-28） |
| --- | ---: | ---: |
| 稿件數 | 45 | 10 |
| 已公開 | 45 | 5 |
| 排期待發布 | 0 | 5 |
| 已公開稿件累積播放合計 | 1,839,794 | 666 |
| 已公開稿件播放中位數 | 15,407 | 183 |
| 發布日期範圍 | 2019-10-08 – 2026-10-08 | 2026-10-25 – 2026-10-28 |

這張基線表描述 2026-10-28 資料覆蓋與現況，**不是模型效果排行榜**。歷史作品有多年累積、題材不同；當時新系列只有五部已公開影片。

---

## 一、2026-11-08 必火人群包實證與 Google 全天候營運協同

自 `2026-11-02` 至 `2026-11-08`，我們在保持上述 2026-10-28 歷史比較基線不混併的前提下，於 [`production-update-2026-11-08.json`](../data/production-update-2026-11-08.json) 與[11 月 8 日更新報告](updates/2026-11-08.md)中記錄了最新單片同質對照實驗與 Google Antigravity／Gemini 的自動化營運貢獻：

### 1. 同一支影片內的必火四組人群包 A/B/C/D 對照實驗（控制內容變數）
不同於跨年份、跨題材的粗略對比，我們在同一支換裝短片 [`BV1Eaxt6LEhb`](https://www.bilibili.com/video/BV1Eaxt6LEhb/)（截至 `2026-11-08 08:29` 達 **`99,604` 播放、`14,988` 點讚、`4,821` 收藏**，前 3 秒跳失率 `19.95%` vs 同類均值 `28.56%`）上執行了 `4 × ¥100`（`24h` · `播放量`）平行人群包測試：
- **自定義核心 UP 主包（`h50` / `fresh25`）讚藏率達 `5.30%–5.68%`**，是系統智能推薦（`1.42%`）與寬泛動畫興趣標籤（`1.76%`）的 **3.5 倍以上**。
- 將勝出包 `h50` 改以 **`72h` 長週期放量**（訂單 `497711549966336`）後，千次播放成本由 `¥18.28/1k` 降至 **`¥12.74/1k`**，必火歸因讚藏率升至 **`8.03%`**，單粉獲取成本降至 **`¥13.59`**。
- 在長篇動畫 PV [`BV1sQ4E6eEhK`](https://www.bilibili.com/video/BV1sQ4E6eEhK/)（《AiGO!!!!! · 迷途算力》，截至 `2026-11-08 08:29` 達 **`15,686` 播放、`330` 點讚、`390` 收藏**，收藏數持續反超點讚數）的 `4 × ¥250` 四池賽馬中，垂類 AI 角色受眾池（`Pool A`，讚藏率 **`5.58%`**）與 AI MV 視覺池（`Pool D`，讚藏率 **`5.10%`**）同樣顯著優於泛二遊池（`Pool B`，`3.24%`）。

### 2. Google Cloud 官方流量扶持實證：Gemini Saver（`AI Memory Keeper`）三片矩陣（自費必火 `¥0`）
在 **Google Cloud（Google 出海創新競賽單元）官方流量扶持**下，我們圍繞開源工具 [`AI Memory Keeper`](../tools/ai-memory-keeper/README.md) 發布的 3 支作品在 **自費必火投放 `¥0`** 的前提下，截至 `2026-11-08 08:29`（UTC+8）合計斬獲 **`34,810` 播放、`2,048` 點讚、`2,144` 收藏（合計 `4,192` 讚藏，整體收藏反超點讚）、`87` 投幣、`305` 評論、`154` 分享、`129` 彈幕**：
- **主力工具演示 [`BV1WQ4t6xEdT`](https://www.bilibili.com/video/BV1WQ4t6xEdT)**：**`24,283` 播放、`1,846` 點讚、`1,760` 收藏（讚藏率高達 `14.85%`）、`241` 評論、`118` 分享**；
- **Google Cloud 贈金與免費密鑰教學 [`BV1nw4h64Ebf`](https://www.bilibili.com/video/BV1nw4h64Ebf)**：**`5,734` 播放、`140` 點讚、`357` 收藏（收藏／點讚比高達 `2.55×`）、`42` 評論、`36` 分享**；
- **模型驗真小劇場 [`BV1BM4h6pEY7`](https://www.bilibili.com/video/BV1BM4h6pEY7)**：**`4,793` 播放、`62` 點讚、`27` 收藏、`22` 評論、`50` 彈幕**。

### 3. Google（Gemini / Antigravity / Vertex AI / Google Cloud）在日常營運數據與連載中的具體幫助
- **每 2 小時唯讀數據採集**：由 Google Antigravity 定時拉取 B站創作中心與必火推廣後台數據並附加至 `bihuo-readings.jsonl`，確保每次投流決策都有連續時序快照支撐。
- **每小時評論區真人互動巡檢**：自動識別新公開稿件下的真實觀眾留言並結合角色語境回覆，維持高評論互動率。
- **每日定時分P連載與前置互動組件掛載**：針對《大肥魚值班小遊戲》（[`BV1YL4M6EEek`](https://www.bilibili.com/video/BV1YL4M6EEek/)），於每日北京時間 `12:00` 自動追加新結局分P、更新簡介進度表，並在 `12:30` 過審後自動掛載 `0.5s` 關注、`6.0s` 投票、`13.5s` 評分與 `19.0s` 跳轉組件。
- **跨模型記憶共享**：透過 [`AI Memory Keeper`](../tools/ai-memory-keeper/README.md) 將上述營運數據口徑與禁區規則同步給 Claude 與 Codex，消除跨視窗失憶。

---

## 二、如何判斷 AI 是否真正提高效率

先明確任務目標與驗收條件，再比較相同任務的完成時間、返工次數和結果可用性。記錄樣本大小、工具版本與人工介入，將草稿數量、完成數量和真正可用的成果分開。產出更多文字不等於節省時間；這些是評估方法，並非將不同片齡的作品直接混算為提效比例。

- **2026-10-28 下午快照**：52 筆快照保留於[歷史快照](../data/snapshots/2026-10-28-afternoon/creator-video-snapshot.json)。
- **2026-10-29 首日觀察**：[鯨魚娘02](../data/whale-log02-24h-2026-10-29.json) 記錄 308 次播放、24 個讚、5 次收藏、51 條回覆、完播率 36.63%。
- **2026-10-30 至 11-08 歷次交付紀錄**：分別保存在 [`production-update-2026-10-30-evening.json`](../data/production-update-2026-10-30-evening.json)、[`production-update-2026-11-01.json`](../data/production-update-2026-11-01.json)、[`production-update-2026-11-02.json`](../data/production-update-2026-11-02.json)、[`production-update-2026-11-03.json`](../data/production-update-2026-11-03.json) 與 [`production-update-2026-11-08.json`](../data/production-update-2026-11-08.json)。

[返回實驗室首頁](../README.md)
