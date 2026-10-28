# 營運檢查表 / Operations checklist

- [ ] 確認本輪唯一執行者與最新使用者指示。 / Confirm the sole operator and latest user instructions.
- [ ] 讀取停止條件、上次執行結果與到期時間。 / Read stop conditions, prior results, and due times.
- [ ] 只執行已到期、已授權的工作。 / Execute only due, authorized work.
- [ ] 在平台寫入前檢查是否已完成，避免重複。 / Check whether an action already succeeded before writing again.
- [ ] 用實際回讀結果記錄成功；失敗保留原因。 / Record success from read-back evidence and retain failure reasons.
- [ ] 分開保存觸發、開始、完成與平台操作狀態。 / Separate trigger, start, completion, and platform-action states.
- [ ] 更新下次到期時間；沒有變化時保持安靜。 / Update due times and stay quiet when nothing changes.

本表是流程範本，本身不包含排程器或登入功能。 / This is a process template; it does not implement a scheduler or authentication.
