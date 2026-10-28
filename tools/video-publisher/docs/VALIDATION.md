# 驗證狀態 / Validation status

日期 / Date: **2026-10-29**. 這是工程 alpha 的驗證邊界，不是五站完整產品宣告。 / These are engineering-alpha verification limits, not a five-platform completion claim.

- **25 項離線測試通過 / 25 offline tests passed:** metadata/file changes invalidate confirmation, platform subsets, declarations, stop files, concurrent locks, atomic deduplication, partial failures, durable IDs, uncertain-attempt suppression, native account checks, evidence validation and single-use browser begin.
- **安裝 / Installation:** `install.ps1` 在本機既有 Python 環境成功建置及安裝 wheel，doctor 確認依賴；不代表全新電腦測試。 / The installer successfully built/installed a wheel and ran doctor in this existing local environment; this is not a clean-machine test.
- **Bilibili:** 原有 CLI 登入已確認 / Existing CLI sign-in confirmed.
- **Douyin:** 使用者完成驗證後保存會話，獨立無頭瀏覽器重新打開上傳頁成功。尚未證明與內置瀏覽器共享。 / Session saved after user verification; a fresh headless check reached upload UI. Sharing with the in-app browser is not established.
- **YouTube:** 真正內置瀏覽器已顯示登入後 Studio。 / Actual in-app browser displayed authenticated Studio.
- **Instagram:** 登入後首頁及「建立新貼文」照片／影片入口已確認。 / Authenticated home and the new-post photo/video picker were confirmed.
- **TikTok:** 官方登入頁可達；使用者正在處理登入。 / Official login page reachable; user is handling sign-in.
- **未執行 / Not performed:** any live video upload/publication, public playback checks, complete five-platform analytics/interaction acceptance, clean-machine installation, cross-OS acceptance, third-party app approval.

離線測試使用合成、不可播放的檔案和模擬後端，不會上傳。沒有把測試檔、帳號資料、Cookie 或原始頁面放入公開倉庫。

Tests use synthetic non-playable files and mocked backends; nothing is uploaded. Test media, account data, cookies and raw browser pages are not published.
