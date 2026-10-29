# 第三方來源 / Third-party notices

本工具為獨立、AI 協助開發的整合實驗，不是下列專案或平台的官方產品；原作者授權保留。

This is an independent, AI-assisted integration experiment, not an official product of these projects or platforms. Original license terms remain applicable.

- [bili-cli](https://github.com/kukemc/bili-cli), MIT. 透過 `bili-agent==0.7.0` 呼叫，保留其登入、限流、審計。 / Called through the pinned package, retaining its login/rate-limit/audit behavior.
- [social-auto-upload](https://github.com/dreammis/social-auto-upload), MIT, Copyright 2023 dreammis. 可選抖音後端固定於 `3847fff01f5434a8a32829833163cd6723cabc59`，只用表單輔助，上游 LICENSE 留在獨立 checkout。 / Optional pinned Douyin form helpers; upstream LICENSE stays in the separate checkout.
- [Google API Python Client](https://github.com/googleapis/google-api-python-client), Apache-2.0; [google-auth-oauthlib](https://github.com/googleapis/google-auth-library-python-oauthlib), Apache-2.0. 選配 YouTube API / Optional YouTube API.
- [Patchright](https://github.com/Kaliiiiiiiiii-Vinyzu/patchright-python), Apache-2.0. 實驗性抖音瀏覽器後端，不實作驗證繞過。 / Experimental Douyin backend; no verification bypass.

完整直接依賴版本見 [pyproject.toml](pyproject.toml)，各依賴包含自己的授權。 / See pinned direct dependencies in [pyproject.toml](pyproject.toml); each dependency carries its license.
