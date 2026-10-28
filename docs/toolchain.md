# 工具與插件選型 / Tools and integrations

此表記錄本專案實際可見的工具與使用證據，不把「已安裝」寫成「已精通」。 / This table records observed tools and usage evidence; installation is not presented as mastery.

| 工具 / Tool | 實際用途 / Use | 證據與版本 / Evidence and version |
|---|---|---|
| ChatGPT／Codex | 對話拆解需求、研究、編輯程式與整理交付。 / Translate requests into research, code, and deliverables. | 本倉庫程式、文件、提交與製作紀錄。 / Repository code, documentation, commits, and production records. |
| DeepSeek 獨立營運實例 / Dedicated DeepSeek operations instance | 接手週期性營運與共享進度。 / Continue scheduled operations from shared checkpoints. | [實際排程紀錄](automation-verification.md)；不是同題模型能力跑分。 / [Run evidence](automation-verification.md), not an identical-task benchmark. |
| GitHub 插件 / GitHub integration | 讀回倉庫、提交文件、驗證遠端版本。 / Read repository state, commit documentation, verify remote versions. | 本倉庫的提交與檔案。 / This repository's commit history and files. |
| Bili CLI skill + CLI | 取得稿件清單、創作中心數據、發布回讀。 / Retrieve uploads, creator analytics, and publication read-back. | 此次 CLI 回報 **0.7.0**；[資料集](../data/README.md)。 / CLI version **0.7.0** and [datasets](../data/README.md). |
| Breeze CLI／Audio Studio | 語音、選用聲段與混音工作流。 / Voice takes and audio-production workflow. | 兩個可編輯交付案例；未把未確認的 CLI 版本填成數字。 / Two editable-delivery cases; CLI version not recorded. |
| HyperFrames + GSAP | 以 HTML 場景與時間軸組合影片。 / Compose HTML scenes and time-based animation. | 案例固定 **HyperFrames 0.8.208**；本地 GSAP 依賴。 / Project-pinned **HyperFrames 0.8.208** with local GSAP. |
| Seedance | 影片生成與教學實驗。 / Video generation and tutorial experiments. | GPT 第三期製作版本與第四、五期教學。 / Episode 03 production and episode 04/05 tutorials. |
| FFmpeg／ffprobe | 媒體格式、解碼、混音與編碼檢查。 / Format inspection, decoding, mixing, and encoding checks. | 製作交付中的技術檢查紀錄。 / Technical delivery records. |
| Node.js／JavaScript | 資料整理、示例流程、雜湊核對、測試。 / Data summaries, workflow demos, checksum verification, tests. | 此次環境 **Node.js 26.11.1**；公開腳本只用標準函式庫。 / **Node.js 26.11.1**, standard-library-only public scripts. |
| Python／PowerShell | 原專案發布控制、資料採集與驗證腳本。 / Original publication controls, collection, and verification scripts. | [程式導覽](code-map.md)。 / [Code map](code-map.md). |
| Git | 本地與遠端版本管理。 / Local and remote version control. | 此次環境 **2.53.0.windows.3**。 / Observed version **2.53.0.windows.3**. |

Computer Use 用於原生接口未涵蓋的網頁操作；先使用連接器或 CLI，再以瀏覽器補足。插件和 skill 是工作流入口，並不等於自行訓練了模型。

Computer Use fills gaps where a connector or CLI does not expose the needed operation. Plugins and skills are workflow interfaces; their use is not a claim of training a model.

職缺提到 Copilot，但本專案目前的證據是 ChatGPT／Codex 等工具；未將 GitHub Copilot 或 Microsoft Copilot 寫成已完成的使用案例。

The role mentions Copilot, but this project's evidence concerns ChatGPT/Codex and the tools above. It does not claim a completed GitHub Copilot or Microsoft Copilot case study.
