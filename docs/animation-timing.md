# 動畫節奏研究 / Animation timing study

研究日期 / Study date: **2026-10-26** · 整理 / Compiled: **2026-10-28**

## 結論 / Findings

輸出幀率、同一張圖停留的時間，以及相鄰姿勢之間的位移是三件事。把輸出從 24 fps 改成 30 fps，並不會自動補出接觸、放手或撤回等缺失的動作。

Output frame rate, the duration of an exposure, and displacement between poses are different quantities. Changing a render from 24 to 30 fps does not create missing contact, release, or withdrawal actions.

| 24 fps 時間格 / 24 fps timebase | 每個姿勢佔用幀數 / Frames per exposure | 每秒曝光段 / Exposures per second |
|---|---:|---:|
| 一拍一 / On ones | 1 | 24 |
| 一拍二 / On twos | 2 | 12 |
| 一拍三 / On threes | 3 | 8 |

曝光段數不等於每秒新畫的獨立圖數；同一張圖可以重複使用。 / Exposure count is not the number of unique new drawings; drawings may be reused.

CELSYS 的示例分別調整中間畫的間距和時間表，展示兩者如何改變速度與觀感。[原始教學 / Primary tutorial](https://howto.clip-studio.com/library/page/view/retasstudio_anime2_00_005)。

CELSYS demonstrates how changing in-between spacing and exposure timing affects motion. [Primary tutorial](https://howto.clip-studio.com/library/page/view/retasstudio_anime2_00_005).

Animation Mentor 的緩入／緩出教學可作為閱讀運動節奏的補充。[教學 / Tutorial](https://www.animationmentor.com/blog/slow-in-and-slow-out-the-12-basic-principles-of-animation/)。

Animation Mentor's slow-in/slow-out lesson provides additional context for reading motion. [Tutorial](https://www.animationmentor.com/blog/slow-in-and-slow-out-the-12-basic-principles-of-animation/).

## 本地診斷結果 / Local diagnostic result

研究時檢視的版本為 66.7 秒、30 fps、2,001 幀。時間表含 52 個片段，其中 44 個是保持片段；保持時間中位數為 0.85 秒，最長為 7 秒。這些數字描述當時版本的時間安排，不是對所有作品的品質評分。

The inspected version was 66.7 seconds at 30 fps, with 2,001 frames. Its timeline contained 52 segments, including 44 holds; the median hold was 0.85 seconds and the longest was 7 seconds. These numbers describe that version's timing, not a universal quality score.

以「把碗放到桌上」為例，檢查順序是：靠近 → 接觸 → 短暫承重 → 放手 → 撤回。必須逐段確認手、碗和桌面的接觸點及遮擋關係。淡入淡出只能處理畫面交接，不能替代物體交接。

For placing a bowl on a table, inspect: approach → contact → brief load transfer → release → withdrawal. Check the hand, bowl, table contact, and occlusion at each stage. A dissolve changes the image transition; it does not establish a physical handoff.

## 驗證邊界 / Verification limits

此項成果是診斷方法、時間表分析與審片標準。原研究完成時，新的自然動作效果尚未驗收；後續影片與舊版實驗片的狀態請查看各自目錄，不能把方法完成寫成所有動作已修好。

The result is a diagnostic method, timeline analysis, and review criteria. At the original study cutoff, improved motion had not yet been accepted. Consult the catalogue for each later video or experimental cut; completing a method does not establish that every motion defect is fixed.
