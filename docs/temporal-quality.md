# 逐幀修復與驗證 / Frame-level repair and verification

案例 / Case: GPT 值班日記 05 · v3-antiflash · 2026-10-28

## 問題與修正 / Problem and fix

畫面在淡入前突然完整出現、右側內容替換時閃白，以及影片入口殘留前一鏡頭，會讓教學難以跟讀。本次修正隱藏初始狀態、將動畫邊界對齊影格、整體替換右側內容區，並在一處入口保留四幀乾淨畫面。沿用 v2 音訊母帶，讓視覺修復不改動已確認的配音。

A full-opacity frame appearing before a fade, white flashes during panel changes, and a preceding shot leaking into an entry made the tutorial harder to follow. The revision sets explicit hidden initial states, aligns animation boundaries to frames, replaces the right-hand panel as a whole, and uses a four-frame clean-entry hold at one cut. The v2 audio master is retained so visual repairs do not change the selected voice track.

| 檢查 / Check | 紀錄結果 / Recorded result |
|---|---|
| 成片 / Delivery | 434.47 s · 1920 × 1080 · 30 fps · H.264/AAC |
| 掃描覆蓋 / Scan coverage | 13,034 / 13,034 frames; analysis at 320 × 180 |
| 已知缺陷 / Known defects | 11 before/after sequences reviewed and reported fixed |
| 指定脈衝候選 / Targeted pulse candidates | 0 isolated ABA; 0 premature contrast peaks; 0 black pulses; 0 right-panel white pulses |
| 影片入口 / Video entries | 12 entries reviewed; 72 entry frames compared with sources |
| 音訊 / Audio | Unchanged v2 master; 0 ms alignment offset; −15.97 LUFS, −2.21 dBTP |

掃描仍找出一個短暫變化事件，來源比對確認是原頁面正常更新；另有 17 處刻意保留的左側換章空隙。這些分類防止把所有變化都當成錯誤。技術檢查與指定畫面複核通過，不等於人工以正常速度逐字看完、聽完整片，也不是光敏安全認證。

One short excursion remained and matched a normal update in the source page. Seventeen intentional gaps during left-panel changes were retained. Classifying such events avoids treating all visual changes as defects. Technical checks and targeted review passed; this is not a claim of continuous human viewing/listening or a photosensitivity safety certification.

## 對 BD 的可遷移方法 / Transferable practice for BD

先把「看起來不對」轉成具體錯誤、時間點與驗收條件；局部修復後保留未改動部分，再用同一套指標比對。商戶簡報也可採用相同習慣：鎖定來源、標出錯誤欄位、檢查修正，並讓同事知道哪些內容尚未核實。

Translate vague dissatisfaction into specific defects, locations, and acceptance criteria. Repair the affected part, preserve the rest, and compare with the same measures. The same discipline applies to merchant briefs: retain sources, identify incorrect fields, verify corrections, and make unresolved claims visible to teammates.

數據來自本地交付與逐幀驗證回執；公開摘要見 [production update](../data/production-update-2026-10-28-evening.json)，成片與雜湊見[影片清單](../media/manifest.json)。

Figures come from local delivery and temporal-QA receipts. See the public [production summary](../data/production-update-2026-10-28-evening.json) and [video manifest](../media/manifest.json).
