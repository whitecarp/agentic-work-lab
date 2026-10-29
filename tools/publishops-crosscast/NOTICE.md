# 第三方來源

**繁體中文** · [English](NOTICE_EN.md)

本工具為獨立、AI 協助開發的整合實驗，不是下列專案或平台的官方產品；原作者授權保留。

- [bili-cli](https://github.com/kukemc/bili-cli)，MIT。透過 `bili-agent==0.7.0` 呼叫，保留其登入、限流與審計。
- [social-auto-upload](https://github.com/dreammis/social-auto-upload)，MIT，Copyright 2023 dreammis。歷史選配抖音後端固定於 `3847fff01f5434a8a32829833163cd6723cabc59`，只用表單輔助；上游 LICENSE 留在獨立 checkout。
- [Google API Python Client](https://github.com/googleapis/google-api-python-client)，Apache-2.0；[google-auth-oauthlib](https://github.com/googleapis/google-auth-library-python-oauthlib)，Apache-2.0。用於選配 YouTube API。
- [Patchright](https://github.com/Kaliiiiiiiiii-Vinyzu/patchright-python)，Apache-2.0。用於抖音／Instagram／X 專用瀏覽器後端，不實作驗證繞過。

完整直接依賴版本見 [pyproject.toml](pyproject.toml)，各依賴包含自己的授權。

[返回實驗室首頁](../../README.md)

0.3.0 預設瀏覽器路徑不下載或匯入 social-auto-upload，舊適配器保留作為來源記錄。
