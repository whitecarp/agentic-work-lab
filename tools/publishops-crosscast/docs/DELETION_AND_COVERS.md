# 刪除與封面重發 / Deletion and cover replacement

這是 **Instagram 原生瀏覽器交接**，不是獨立刪除 API。Agent 必須先取得針對該作品的刪除及重發授權，再在官方頁核對帳號、作品 ID 與刪除範圍。一般上傳授權不包含刪除。

This is an **Instagram native-browser handoff**, not a standalone deletion API. The host needs explicit deletion and replacement authorization for the exact work, then checks the signed-in account, work ID and deletion scope. Upload authorization alone does not authorize deletion.

```sh
vpub browser delete-request KEY --identity OBSERVED_ACCOUNT --id ORIGINAL_ID
# Host inspects the exact work and deletes once in the official UI.
# Refresh the same profile and record the acknowledgement and absence.
vpub browser delete-record KEY --evidence deletion.json
```

`deletion.json` 範例只含結構示意，實際欄位必須由觀察填寫；不得將範例當成成功證據。 / This is a structural example only; replace it with actual observations, never treat fixtures as proof.

```json
{
  "id": "ORIGINAL_ID",
  "identity": "OBSERVED_ACCOUNT",
  "page_url": "https://www.instagram.com/reel/ORIGINAL_ID/",
  "profile_url": "https://www.instagram.com/OBSERVED_ACCOUNT/",
  "delete_acknowledged": true,
  "work_absent": true,
  "observation": "Describe the actual confirmation result and refreshed profile check.",
  "recovery_note": "Describe the actual recovery wording; state if recovery was not tested."
}
```

刪除途中不確定時停在 `DELETE_PENDING`，檢查原作品，不重複點刪除。只有實際確認及刷新後不存在，才記為 `AGENT_REPORTED_DELETED`。這是宿主提供的 UI 證據，CLI 沒有獨立查詢平台；原發布回執仍保留。

An uncertain deletion stays `DELETE_PENDING`; inspect the original work instead of clicking Delete again. Confirmed deletion plus refreshed absence becomes `AGENT_REPORTED_DELETED`. Evidence comes from the host UI, not an independent API query; the original receipt remains intact.

## 重發同一影片、替換封面 / Repost the same video with a replacement cover

```sh
vpub plan replacement.json
vpub browser repost-deleted KEY replacement.json --confirm NEW_PLAN_HASH --identity OBSERVED_ACCOUNT --deleted-id ORIGINAL_ID
vpub browser begin KEY --identity OBSERVED_ACCOUNT
# Host selects the returned snapshot, checks caption/AI label/audience/cover,
# submits once, then records the new durable ID through browser record.
```

本命令只允許同一影片位元組、同一帳號、相同文案／受眾／聲明，封面可以不同。它建立新快照並將原方案、發布及刪除回執放進 `deleted_generations`，不刪除歷史、不繞過去重，也不自動點擊分享。成功佇列不代表已發布；重發必須另行讀回新作品 ID。變更影片版本需要另建明確方案，不屬於此封面重發命令。

This command retains identical video bytes, account, metadata, audience and declarations while allowing a new cover. It creates a new snapshot and archives the previous plan/publication/deletion receipts in `deleted_generations`. It does not erase history, bypass deduplication or click Share. Queueing is not publishing; read back a new durable work ID separately. A changed video version needs its own explicit plan and is outside this cover-replacement command.

## Instagram 封面限制 / Cover limitations

本次網頁實測：發布後「編輯資訊」沒有換封面入口；發布前直式圖片仍被裁成橫式影片的預覽比例。因此專用圖本身完整，並不代表預覽及個人主頁縮圖都完整。發布前檢查文字，發布後再看主頁；不因封面問題自動刪除作品。保留原片比例；手機使用「編輯封面 → 從相機膠卷中添加」選抖音共用直式圖，再檢查主頁網格。

Observed in this web session: Edit info after publication had no cover-replacement control; a portrait image before publication was cropped to the landscape video's preview ratio. Complete source artwork does not guarantee complete previews or profile thumbnails. Inspect both; never automatically delete a work to fix its cover. Retain the original video ratio. On mobile, use Edit cover → Add from camera roll with the shared Douyin portrait and inspect the profile grid.


目前預設 Instagram 保留原片及原比例，上傳時另外設定封面；手機可從相機膠卷選抖音共用直式圖。[目前指南](../README.md#instagram-發布與封面)。0.2.4 的片頭實驗已撤回，既有發布記錄仍保留。

Current Instagram publishing retains original footage/ratio with a separate cover, using shared Douyin portrait artwork through the phone camera-roll picker. The 0.2.4 lead-in experiment is withdrawn; publication history remains intact.
