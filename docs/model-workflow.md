# GPT × DeepSeek：分工與驗證 / Roles and verification

## 已做過什麼 / What was actually done

| 面向 / Area | GPT／Codex 工作流 / GPT/Codex workflow | DeepSeek 營運工作流 / DeepSeek operations workflow |
|---|---|---|
| 任務 / Task | 劇本、研究、程式、影片製作與交付整理。 / Scripts, research, code, video production, and delivery. | 讀取檢查表、到期任務、共享進度並持續營運。 / Read checklists, due work, and shared progress for ongoing operations. |
| 交付證據 / Evidence | 已公開影片、未發布成片、時間軸與交付核驗。 / Published works, upcoming cuts, timelines, and delivery checks. | 排程觸發、開始、完成與實際工具行為紀錄。 / Scheduled trigger, start, completion, and tool-activity records. |
| 品質判準 / Acceptance | 技術檢查、畫面抽查與人類審片分開記錄。 / Separate technical checks, sampled frames, and human review. | 不重複執行；沒到期就不寫平台；以日誌確認完成。 / Avoid duplicates, perform no platform writes when nothing is due, verify completion from logs. |

這是一個跨模型工作流接力與執行一致性案例。**沒有同一批提示詞、固定樣本、盲評和完整成本記錄，因此不能據此宣稱 GPT 或 DeepSeek 的能力、速度或成本勝出。** 此處的工作流名稱不是所有請求底層模型版本的獨立身分認證。

This is a cross-model workflow handoff and execution-consistency case. **There is no shared prompt set, fixed sample, blind review, or complete cost record, so it cannot establish a winner in capability, speed, or cost.** Workflow names do not independently authenticate the underlying model version of every request.

## 真實驗證結果 / Observed verification

2026-10-28 的每小時輪次在預定時間後 24.992 秒啟動，31.175 秒後完成；觸發來源確為排程。該輪工作未到期，因此沒有 B站平台操作，也沒有為了測試而重跑。細節見[驗證紀錄](automation-verification.md)與[機器可讀證據](../data/scheduled-run-evidence.json)。

The verified hourly run on 2026-10-28 started 24.992 seconds after its scheduled time and completed in 31.175 seconds. The trigger was scheduled. No Bilibili actions were due, and no duplicate manual test was launched. See the [verification record](automation-verification.md) and [machine-readable evidence](../data/scheduled-run-evidence.json).

## 下一步：商戶研究同題測試 / Next: an identical-task merchant-research test

以相同的公開商戶資料、相同提示詞和相同輸出欄位分別測試兩個模型。每份簡報由不看模型名稱的同事檢查：事實正確、來源可追溯、缺失資料是否誠實標記、價值主張是否貼合、人工修正量。時間與成本從實際請求記錄取得，不能估填。

Give both models the same public merchant data, prompt, and output schema. A reviewer blinded to model names checks factual accuracy, source traceability, explicit gaps, relevance of the value proposition, and required corrections. Time and cost must come from actual request records rather than estimates.

此同題測試是已整理的評估方案，**尚未執行**。本次新增的商戶排序示例是離線程式演示，不是這項模型測試的結果。

This identical-task evaluation is a documented plan and **has not been run**. The merchant-prioritisation script added here is an offline demonstration, not its result.
