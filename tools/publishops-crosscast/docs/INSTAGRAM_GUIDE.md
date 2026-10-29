# Instagram 原比例發布 / Original-ratio Instagram publishing

Instagram 準備流程預設判斷影片方向，不要求使用者選擇處理開關：**橫式影片自動在開頭加入 1 秒製作封面，直式與方形影片使用原片。上傳裁剪必須選「原版」。** 橫式影片仍維持原本尺寸，不改成直式畫布、不裁掉正片。

Preparation detects orientation by default: **landscape video receives a one-second production-cover lead-in; portrait and square video pass through unchanged. Select Original in the upload crop control.** Landscape dimensions remain landscape; original footage is not cropped.

```sh
# Required Instagram preparation; orientation routing is automatic.
vpub instagram prepare publish.json --out instagram-ready.json
vpub plan instagram-ready.json
vpub publish instagram-ready.json --confirm CURRENT_PLAN_HASH
```

準備命令只輸出 Instagram 方案，其他平台沿用原方案。需要本機 FFmpeg 與 ffprobe，可由 PATH 或 `--ffmpeg PATH --ffprobe PATH` 指定；缺少時回報依賴錯誤。CLI 產生方案與快照，宿主 Agent 仍須在內置瀏覽器操作官方網站。一般 `publish` 命令不會自行呼叫 FFmpeg；宿主須先執行上述準備步驟。

Preparation emits an Instagram-only manifest; other platforms retain the original plan. FFmpeg and ffprobe must be on PATH or supplied with `--ffmpeg PATH --ffprobe PATH`. The host must run this preparation step before plan/publish, then operate the official site in its built-in browser. The general publish command does not itself run FFmpeg.

## 原檔與重跑 / Sources and reruns

橫式版本另存 H.264/AAC MP4，保留完整正片與音軌；片頭靜音，正片音訊延後一秒。這會重新編碼，並非位元組無損。來源、封面和成品記錄 SHA-256；相同輸入可復用，已處理影片不再疊加片頭。原檔不覆蓋，成品變動時拒絕直接復用。旋轉資訊尚未正規化的橫式來源須先另行匯出；有章節的方案需明確調整時間。

A separate H.264/AAC MP4 retains the full original picture and audio with a silent cover lead and delayed original audio. This is a re-encode, not byte-lossless processing. SHA-256 records enable reuse and detect changed outputs. Prepared video is not prepended again; inputs are never overwritten. Normalize rotated landscape exports first; chapter-bearing manifests require explicit retiming.

## 手機選封面 / Select the cover on a phone

網頁編輯時保留開頭封面影格及原版比例。發布後在手機「編輯封面」將時間軸移到開頭，選取封面，再檢查「主頁網格」裁切。使用者提供的手機畫面也有「從相機膠卷中添加」，可依介面選擇圖片。封面放進影片省去手機另存圖片的步驟，但不能保證網格不裁字，仍須查看實際預覽。

Keep the opening cover frame and Original ratio in the web editor. On the phone, open Edit cover, move the timeline to the beginning, select the cover, then check the profile-grid crop. The supplied phone screenshot also exposes Add from camera roll. Embedding the artwork avoids transferring a separate image, but does not guarantee an uncropped grid; inspect the device preview.

手機操作是使用者輔助步驟，CLI 不控制手機。發布後不假設平台可替換影片檔案；修正版另建方案，刪除／重發須有相應授權並保留舊記錄。

Mobile adjustment is user-assisted; the CLI does not control the phone. Do not assume published media bytes can be replaced. Create a new plan for changed video, with authorized deletion/reposting and retained history.
