# 帳號與營運資料 / Channel and operations data

帳號 / Account: [穿睡衣的小白白 · UID 10706217](https://space.bilibili.com/10706217)

## 覆蓋範圍 / Coverage

本次取得公開投稿列表 **45** 筆、創作中心稿件 **52** 筆（兩頁），其中歷史人類主導作品 **45** 筆、目前 AI 系列 **7** 筆。兩個列表的可見範圍不同，分別保存，不為湊齊數字而合併或刪除。

The snapshot contains **45** public-list entries and **52** creator-centre entries across two pages: **45** historical human-led works and **7** current AI-series works. These endpoints expose different sets, so their records remain separate.

| 資料 / Dataset | 內容 / Contents |
|---|---|
| [公開列表 CSV](public-video-snapshot.csv) / [JSON](public-video-snapshot.json) | 標題、時間、播放、評論、彈幕 / Titles, times, views, comments, danmaku |
| [創作中心 CSV](creator-video-snapshot.csv) / [JSON](creator-video-snapshot.json) | 所有取得的稿件狀態、排期、播放與互動 / All retrieved upload states, schedules, views, and interactions |
| [歷史人類營運 / Human-led history](human-led-history.csv) | 歷史作品逐片數據 / Per-video historical data |
| [目前 AI 系列 / Current AI series](current-ai-series.csv) | 包含已公開與排期待發布 / Includes public and scheduled uploads |
| [每週帳號數據 / Weekly analytics](channel-weekly.json) | 播放、訪客、粉絲變動、播放端與昨日前列影片 / Views, visitors, fan changes, devices, and yesterday's top videos |
| [過往 AI 數據快照 / Earlier AI snapshots](historical-ai-analytics.json) | 可取得的完播率、平均觀看與互動 / Available completion, average watch, and interaction metrics |
| [營運事件 / Operations events](operations-events.json) | 69 筆去識別事件欄位 / 69 de-identified event records |
| [排程驗證 / Scheduled-run evidence](scheduled-run-evidence.json) | 真正自動觸發與完成的時間 / Actual automatic trigger and completion times |

## 解讀方式 / Interpretation

- 指標是接口回傳的快照，並非獨立稽核的流量。 / Metrics are API snapshots, not independently audited traffic.
- sampled_at 使用 UTC；平台發布與排期時間保留時區。 / sampled_at is UTC; platform times retain their offsets.
- publish_time_raw 保留原始欄位，待發布稿件不能把此欄誤認為已公開時間。 / For scheduled uploads, publish_time_raw must not be mistaken for an actual public-release time.
- 歷史稿件的「人類主導」是研究分組，不表示完全未使用 AI。 / Human-led is a cohort label, not a claim that no AI tools were used.
- 過往快照的 file_modified_at 是檔案來源線索，不保證等於精確採樣時刻。 / Historical file_modified_at is provenance, not a guaranteed exact observation time.
- 每週帳號數據包含舊作與新作，不能全部歸功於 AI 系列。 / Weekly channel analytics include old and new uploads and cannot all be attributed to the AI series.
- 原始標題與平台狀態文字保留原文；其餘說明為繁體中文與英文。 / Original titles and platform state text are preserved verbatim; documentation is Traditional Chinese and English.
- 缺失值為空或 null，不改成零；未公開影片的零播放保留平台原值。 / Missing values remain empty or null; platform-reported zero views on unpublished uploads remain zero.

本資料包不包含觀眾名單、評論正文、私信、登入資訊或原始客戶端日誌。 / The dataset excludes viewer lists, comment text, private messages, credentials, and raw client logs.
