# 可編輯影片交付 / Editable video delivery

整理日期 / Compiled: **2026-10-28**

## 工作流程 / Workflow

1. **先鎖定台詞與聲音。** 保存選用的原始聲段，依實際音訊時長安排畫面。 / **Lock narration first.** Keep selected voice takes and time the picture to actual audio durations.
2. **場景、字幕、混音共用時間軸。** 標示全片時間與場景內時間，避免片段重用時錯位。 / **Use one timing reference.** Distinguish global and scene-local time when reusing clips.
3. **記錄已烘焙的混音。** 若音訊已包含音量與效果，重開工程時避免再次套用。 / **Document baked audio.** Avoid applying gain or effects twice when reopening a project.
4. **固定工具與依賴。** 交付版本使用固定的 HyperFrames 版本與本地依賴；重開時先檢查、再預覽、最後渲染。 / **Pin tools and dependencies.** Check, preview, and render with the documented HyperFrames version and local dependencies.
5. **以實際輸出檔驗收。** 記錄解碼結果、畫面抽查、聲音檢查、人類審片與 SHA-256。 / **Review the actual output.** Record decode checks, sampled frames, audio checks, human review, and SHA-256.

## 兩個實例 / Two delivery examples

| 項目 / Property | 鯨魚娘 02 / Whale Girl 02 | 鯨魚娘 03 / Whale Girl 03 |
|---|---:|---:|
| 時長 / Duration | 116.7 s | 188.8 s |
| 畫面 / Picture | 1920 × 1080, 30 fps | 1920 × 1080, 30 fps |
| 總幀數 / Frames | 3,501 | 5,664 |
| 已混音元件 / Baked audio components | 38 | 47 |
| 選用原始聲段 / Selected unmixed voice takes | 13 | 28 |

02 的第三版修正了第五場的 AI 聲明紅圈。03 的正式交付檔與人類接受的完整有聲預覽逐位元組一致，複製後以雜湊核對。

Revision 3 of episode 02 corrected the AI-declaration highlight in scene 5. Episode 03's delivered file is byte-identical to the full sound preview accepted by the human reviewer, confirmed by a hash comparison after copying.

03 的完整解碼與指定畫面抽查通過；工具檢查的 lint/runtime/layout 錯誤為零，但仍有提示與資訊。代理未以正常速度連續觀看或親耳聽完整片，因此不把機器檢查等同於人工逐字驗收。

Episode 03 passed full decoding and targeted frame checks. Lint/runtime/layout error counts were zero, while warnings and informational messages remained. The agent did not watch and listen to the entire video at normal speed, so machine checks are not presented as word-by-word human review.

## 可重用的交付資訊 / Reusable delivery information

使用[交付清單](../templates/delivery-manifest.json)記錄檔名、雜湊、媒體格式、依賴版本、審片方式、未解決問題與發布狀態。未完成的檢查使用 `null` 或 `not_run`，不預填成功。

Use the [delivery manifest](../templates/delivery-manifest.json) to record filenames, hashes, formats, dependency versions, review methods, unresolved issues, and publication status. Use `null` or `not_run` for unfinished checks rather than pre-filling success.
