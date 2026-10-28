# 驗證狀態 / Validation status

日期 / Date: **2026-10-29**. 這是工程 alpha 的驗證邊界，不是五站完整產品宣告。 / These are engineering-alpha verification limits, not a five-platform completion claim.

- **31 項離線測試通過 / 31 offline tests passed:** metadata/file changes invalidate confirmation, platform subsets, declarations, scoped/global stop files, concurrent locks, atomic deduplication, partial failures, durable IDs, uncertain-attempt suppression, native account checks, evidence validation, single-use browser begin, TikTok suspension/legacy jobs, and X author/ID/exact-text receipts.
- **安裝 / Installation:** `install.ps1` 在本機既有 Python 環境成功建置及安裝 wheel，doctor 確認依賴；不代表全新電腦測試。 / The installer successfully built/installed a wheel and ran doctor in this existing local environment; this is not a clean-machine test.
- **Bilibili:** 原有 CLI 登入曾確認；現有 B站專用停止檔保留，未重啟 B站操作。 / Earlier CLI sign-in was confirmed; the current Bilibili-specific stop signal remains in effect.
- **Douyin:** 獨立後端曾保存會話並讀回上傳頁；內置瀏覽器已確認使用者切換後的目標帳號。兩者會話共享仍未證實，尚未實發。 / The standalone session reached upload UI; the user's switched target account was confirmed in the in-app browser. Session sharing remains unproven; no live upload yet.
- **YouTube:** 第一集原創影片的內置瀏覽器私密上傳已驗收。核對標題、繁體中文／英文說明、四個標籤、AI 聲明、非兒童受眾與私享設定後保存一次；內容列表顯示唯一一條 1:19 影片、相同 ID、上傳日期與私享狀態，版權檢查通過。回執為 `AGENT_REPORTED_VERIFIED`；沒有公開發布、公開播放或 API 驗證的宣告。 / One original episode passed the native-browser private-upload workflow. Title, Traditional Chinese/English description, four tags, AI disclosure, non-kids audience and private visibility were checked before one Save. The content table showed a single 1:19 video with the same ID, upload date and private status; copyright checks passed. Receipt: `AGENT_REPORTED_VERIFIED`, not public publication/playback or API validation.
- **YouTube 卡片與片尾 / Cards and end screens:** 尚未適配，測試片未新增。影片內的片尾動畫屬成片製作，與平台可點擊片尾元素分開。 / Not implemented or added to the test video. Baked-in outro animation is video production, separate from clickable platform elements.
- **Instagram:** 登入後首頁及「建立新貼文」照片／影片入口已確認。 / Authenticated home and the new-post photo/video picker were confirmed.
- **X（Twitter）:** 內置瀏覽器已顯示登入後的個人帳號頁；程式提供文案與回執交接，影片實發尚未驗收。 / An authenticated account page was observed in the in-app browser; text/receipt handoff is implemented, but live video publication is unverified.
- **TikTok:** 香港手機登入測試未成功，使用者要求暫停適配。[詳情及測試協助](TIKTOK_STATUS.md)。 / Hong Kong phone-number login test unsuccessful; integration suspended at the user's request. [Details and volunteer testing](TIKTOK_STATUS.md).
- **未執行 / Not performed:** verified public publication/playback, complete five-platform analytics/interaction acceptance, clean-machine installation, cross-OS acceptance, third-party app approval.

離線測試使用合成、不可播放的檔案和模擬後端，不會上傳。沒有把測試檔、帳號資料、Cookie 或原始頁面放入公開倉庫。

Tests use synthetic non-playable files and mocked backends; nothing is uploaded. Test media, account data, cookies and raw browser pages are not published.
