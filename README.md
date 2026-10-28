# AI 應用實作作品集 / Applied AI Portfolio

### Business Development · 研究、執行與團隊效率 / Research, execution & team enablement

**把 AI 融入日常工作：用真實帳號驗證流程，再將方法整理成商戶拓展可複用的工具。**  
**Apply AI to everyday work: validate workflows on a real channel, then turn the lessons into reusable merchant-development tools.**

繁體中文 / English · 首次整理 First edition: **2026-10-28**

🎬 **Bilibili：[穿睡衣的小白白 · UID 10706217](https://space.bilibili.com/10706217)**  
📦 **[影片下載 / Video downloads](https://github.com/whitecarp/ai-video-lab/releases)** · **[影片目錄 / Video catalogue](media/README.md)**  
📊 **[帳號與營運資料 / Channel and operations data](data/README.md)**

**最新更新 / Latest update:** [2026-10-28 晚間：新增三個影片版本、首日數據與逐幀修復 / Three video versions, day-one analytics and frame-level repair](docs/updates/2026-10-28-evening.md)

## 給面試官的快速導覽 / A quick guide for interviewers

| 想了解 / To assess | 看這裡 / Start here |
|---|---|
| 如何把 AI 用於商戶拓展 / Applying AI to merchant expansion | [BD 工作流與團隊試點 / BD workflow and team pilot](docs/business-development.md) |
| 用了什麼工具、為什麼 / Tools used and why | [工具與插件選型 / Toolchain](docs/toolchain.md) |
| GPT 與 DeepSeek 如何協作 / GPT and DeepSeek collaboration | [分工、真實驗證與測試邊界 / Roles, evidence, and evaluation limits](docs/model-workflow.md) |
| 實際寫了哪些程式 / What was implemented | [程式導覽 / Code map](docs/code-map.md) · [可執行示例 / Scripts](scripts) |
| 如何讀取與比較數據 / Reading and comparing data | [數據對比 / Data comparison](docs/data-comparison.md) |
| 如何讓同事接手 / Enabling teammates | [商戶簡報範本 / Merchant brief](templates/merchant-brief.md) · [營運檢查表 / Checklist](templates/operations-checklist.md) |

**目前可核驗：** 55 筆稿件資料、69 筆去識別營運事件、19 個已公開影片版本、4 個腳本／模組，以及 10 項通過的流程測試。商戶示例使用虛構資料；沒有把模擬結果寫成真實簽約或團隊提效業績。

**Currently verifiable:** 55 upload records, 69 de-identified operations events, 19 publicly downloadable video versions, four runnable scripts/modules, and ten passing workflow tests. The merchant example uses fictional data; simulated results are not presented as real partnerships or measured team-efficiency gains.

我以需求提出、選題方向、回饋與審片推進專案，AI 協助研究、編寫程式及製作。這份作品集展示的是把工具落到實際流程、發現錯誤並改善交付的能力；程式與文件也明確標示 AI 協助。

I drove requirements, editorial direction, feedback, and review, with AI assisting research, implementation, and production. The portfolio demonstrates putting tools into a working process, identifying failures, and improving delivery. Code and documentation are explicitly AI-assisted.

### 三個可重跑的入口 / Three reproducible entry points

```sh
npm run demo     # 虛構商戶排序 / Synthetic merchant prioritisation
npm run report   # 真實資料摘要 / Real-data summary
npm test         # 流程檢查 / Workflow checks
```

不需額外 npm 套件；使用 Node.js 20+。測試與本地影片校驗結果見 [validation.json](results/validation.json)。 / No external npm packages are required; use Node.js 20+. See [validation.json](results/validation.json) for test and local-media verification results.

## 這個專案是什麼？ / What is this project?

這是「Ai娘在工作」系列的公開研究與製作紀錄。內容涵蓋 AI 角色短劇、語音與畫面的時間同步、可編輯影片工程，以及 B站帳號的日常營運。人類負責方向與審片，AI 協助研究、製作和執行；每一項結果都標示證據與限制。

This is the public research and production archive behind the **AI Girls at Work** series. It covers character-driven shorts, speech-to-picture timing, editable video delivery, and Bilibili channel operations. A human directs and reviews the work; AI assists with research, production, and execution. Results include their evidence and limitations.

## 近期成果 / Recent results

| 主題 / Topic | 已完成內容 / Delivered | 閱讀 / Read |
|---|---|---|
| 動畫節奏 / Animation timing | 區分輸出幀率、換姿勢頻率和運動間距；建立接觸動作檢查方法。 / Distinguish output FPS, exposure, and spacing; define contact-action checks. | [研究筆記 / Study](docs/animation-timing.md) |
| 可編輯影片 / Editable video | 以語音時間軸組織場景、字幕及混音；形成可重開的交付流程。 / Align scenes, captions, and audio to a speech clock; document editable delivery. | [工作流程 / Workflow](docs/editable-video-workflow.md) |
| 自動營運驗證 / Automation verification | 核實一次真正的每小時排程觸發與完成，保留單一執行者。 / Verify an actual hourly scheduled run and completion with one active operator. | [驗證紀錄 / Verification](docs/automation-verification.md) |
| 帳號研究 / Channel research | 整理歷史人工營運時期和目前 AI 系列的資料，保留採樣口徑。 / Document historical human-led uploads and the current AI series with sampling definitions. | [資料說明 / Data notes](data/README.md) |

這些是製作實驗與工程經驗，並非經過對照實驗驗證的成長保證。舊影片與新影片的題材、片齡和流量來源不同，累積播放量不能直接判定「人類或 AI 誰比較會營運」。

These are production experiments and engineering findings, not controlled evidence of guaranteed channel growth. Different topics, video ages, and traffic sources prevent a direct “human versus AI” verdict from lifetime views.

## 已公開作品 / Published work

下列標題顯示為繁體中文；B站原始標題保存在資料檔中，以便核對。 / Titles below use Traditional Chinese; the original platform titles are preserved in the datasets for verification.

| 發布日期 / Date (UTC+8) | 繁體中文標題 / Traditional Chinese title | English description |
|---|---|---|
| 2026-10-28 | [鯨魚娘代班日誌02｜上班第一天，我被人類派了三個活](https://www.bilibili.com/video/BV1Wfx56JEih) | Three tasks on Whale Girl’s first working day. |
| 2026-10-28 | [鯨魚娘代班日誌01｜當ChatGPT沒有token了](https://www.bilibili.com/video/BV1usxV69EUQ) | Whale Girl covers the shift when ChatGPT runs out of tokens. |
| 2026-10-27 | [GPT值班日記03｜問晚飯吃什麼，還得先填表？](https://www.bilibili.com/video/BV1UXxN6aECf) | Choosing dinner turns into a form-filling exercise. |
| 2026-10-26 | [GPT值班日記02｜有人問歌名，我先回了一個問題](https://www.bilibili.com/video/BV1gxnU6GEuz) | A song-title question receives another question. |
| 2026-10-25 | [GPT值班日記01｜我回了2022年的評論，粉絲說我有點遲鈍](https://www.bilibili.com/video/BV1qjHV6sEuD) | GPT replies to a comment from 2022. |

未在 B站公開的成片及舊版實驗片，另見[影片目錄](media/README.md)。GitHub 公開與 B站發布是兩個不同狀態；本倉庫不會更改 B站排期。

Videos not yet public on Bilibili and earlier experimental cuts are listed in the [catalogue](media/README.md). A public GitHub download does not mean a Bilibili upload has been published; this repository does not change the platform schedule.

## 使用資料與方法 / Using the material

- [研究與選題方法 / Research method](docs/research-method.md)：來源 → 證據 → 實測 → 限制。 / Source → evidence → test → limits.
- [研究記錄範本 / Research-note template](templates/research-note.md)：把觀察和推論分開。 / Separate observations from inferences.
- [交付清單範本 / Delivery manifest](templates/delivery-manifest.json)：記錄格式、雜湊、審片與發布狀態。 / Record formats, hashes, review, and publication status.
- [營運檢查表 / Operations checklist](templates/operations-checklist.md)：只處理到期事項，核驗結果後記錄。 / Act on due work and verify before recording success.

資料提供 CSV／JSON，影片提供可下載檔案與 SHA-256。缺失值保留為空，不當成零；採樣時間與資料來源見各資料檔。

Data is provided as CSV/JSON, with downloadable video files and SHA-256 checksums. Missing values remain empty rather than becoming zero. Sampling times and sources are documented with each dataset.

## 內容範圍 / Scope

本倉庫公開研究說明、影片、帳號整體及單片數據、去識別的營運紀錄。登入憑證、私人訊息、觀眾個人資料及不相關的私人文件不屬於公開資料。作品中引用的第三方素材仍屬各自權利人；公開展示不代表授予第三方素材的再利用授權。

The archive contains research notes, videos, aggregate and per-video channel data, and de-identified operations records. Credentials, private messages, viewer personal data, and unrelated private documents are excluded. Third-party material appearing in videos remains with its respective rights holders; public access does not grant reuse rights to that material.
