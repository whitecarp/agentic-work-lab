# AI 輔助 Business Development / AI-assisted Business Development

## 我想展示的能力 / What this portfolio demonstrates

把模糊需求拆成可執行流程，選擇工具，保留資料來源，檢查結果，再把可用的方法整理成團隊能複用的範本。B站專案提供真實的研究、內容交付、排程與數據紀錄；商戶拓展部分是將這套方法遷移到 BD 的離線演示。

Turn an ambiguous request into a workflow, select tools, retain sources, verify the output, and package the method for teammates. The Bilibili project provides real research, delivery, scheduling, and analytics records. The merchant workflow is an offline demonstration of transferring those practices into BD.

## 對應商戶拓展工作 / Mapping to merchant expansion

| 日常工作 / Task | AI 協助方式 / AI contribution | 人類決策與交付 / Human decision and output |
|---|---|---|
| 商圈與商戶研究 / Market research | 整理公開來源、分類業態、標記缺失資料。 / Structure public sources, classify business types, flag gaps. | 核對營業與服務範圍，形成一頁簡報。 / Verify business and service-area facts; produce a one-page brief. |
| 線索排序 / Prioritisation | 依明確欄位產生可解釋排序。 / Apply transparent criteria to organise leads. | 依地區策略與資源調整優先級。 / Adjust priorities to territory strategy and capacity. |
| 溝通準備 / Conversation preparation | 起草價值主張、訪談問題和異議處理。 / Draft value propositions, discovery questions, and objection responses. | 核對承諾與商戶情況，再決定如何聯絡。 / Check claims and merchant context before outreach. |
| 會後整理 / Follow-up | 將會議筆記整理成需求、承諾、負責人與日期。 / Convert notes into needs, commitments, owners, and dates. | 確認重點後更新 CRM。 / Confirm the summary before updating CRM. |
| 團隊分享 / Team enablement | 統一提示詞、欄位、示例與檢查表。 / Standardise prompts, fields, examples, and checks. | 共同維護有效做法與失敗案例。 / Maintain useful practices and lessons together. |

## 可執行示例 / Runnable example

```sh
npm run demo
npm test
```

輸入為三個虛構商戶，輸出見 [merchant-priorities-demo.json](../results/merchant-priorities-demo.json)。排序採用可解釋權重，缺失服務範圍的商戶會標記為 `research_required`，不會自動發信。此程式處理結構化資料；它沒有呼叫模型，也不把規則評分冒充 AI 預測。

The input contains three fictional merchants. See [merchant-priorities-demo.json](../results/merchant-priorities-demo.json) for the output. Transparent weights produce a prioritised list; missing service-area information produces `research_required`, with no automatic messaging. The script processes structured data; it makes no model calls and does not present rule-based scores as AI predictions.

## 如何在團隊內試點 / How I would pilot this with a team

先選一個商圈、一種商戶類型，記錄人工整理一份簡報的時間與錯誤。以相同規格試用 AI 輔助流程，由同事抽查來源、完整度與可用性。只有在實測有收益時才擴展，將範本、示例和常見錯誤一起交接。

Start with one territory and merchant type. Measure the time and errors involved in preparing a brief manually. Trial the AI-assisted workflow using the same requirements, with peer checks of sources, completeness, and usability. Expand only if measured results justify it, and hand over templates, examples, and common failure cases together.

建議衡量：每份簡報的中位完成時間、來源可追溯比例、人工更正次數、完成跟進比例。簽約率需要另設觀察期與對照，不能把整理速度直接當成營收成長。這些是試點設計，尚無真實商戶或團隊提效實測。

Suggested measures are median brief-preparation time, source traceability, human corrections, and follow-up completion. Signing rates need a separate observation window and comparison; faster preparation is not itself revenue growth. This is a pilot design, with no claimed real-merchant or team-efficiency results yet.
