# Third-party notices

## Environment scoring

The environment helper's browser signal definitions and scoring are adapted from [LinXiaoTao/FuckClaude](https://github.com/LinXiaoTao/FuckClaude), Copyright (c) 2026 LinXiaoTao, under the MIT License.

The retained license is in [`whale-claude-helper/third-party/FuckClaude-LICENSE.txt`](apps/whale-claude-helper/third-party/FuckClaude-LICENSE.txt) and is embedded in the helper executable. The reviewed signal-definition blob is `2d4b3b02a47ca021f99cada321f90dbb59eae2af`. The helper explicitly represents unavailable and failed measurements as unknown; it does not claim they are zero-risk results.

## Translator runtime

The portable translator uses these components:

| Component | License / reference |
| --- | --- |
| Python 3.12.10 embeddable runtime | [Python Software Foundation License](https://www.python.org/downloads/release/python-31210/) and bundled notices |
| CTranslate2 4.8.4 | [MIT](https://github.com/OpenNMT/CTranslate2/blob/v4.8.4/LICENSE) |
| SentencePiece 0.2.2 | [Apache-2.0](https://github.com/google/sentencepiece/blob/v0.2.2/LICENSE) |
| NumPy and PyYAML | License metadata retained in the portable runtime payload |
| Microsoft Visual C++ app-local runtime | Microsoft redistribution terms and accompanying notice in the portable payload |

The source includes [`claude-live-translator/packaging/licenses/`](apps/claude-live-translator/packaging/licenses/) and [`claude-live-translator/backend/MODEL-NOTICES.md`](apps/claude-live-translator/backend/MODEL-NOTICES.md). These notices do not replace the upstream license terms. Redistributed runtime components retain their own notices.

## Optional offline language packages

The catalog is supplied by [Argos Open Technologies](https://github.com/argosopentech/argospm-index). The main executable contains no preinstalled model weights. Users download specific translation directions themselves.

Package licenses vary. Installed packages retain their original metadata, README and license/notice files. For example, the English/Chinese 1.9 package READMEs credit OPUS-MT authors Jörg Tiedemann and Santhosh Thottingal and identify CC-BY 4.0 for the original model. Do not assume every package has the same license. A locally recorded SHA-256 verifies local archive integrity; it is not a publisher signature.

## Online translation and product names

Google online mode uses a key-free Google Translate web endpoint. Google Translate is a third-party service, not bundled model software, and may change or limit requests. This project is not Gemini or Google Cloud Translation API.

Claude and Anthropic are names of their respective owners. This independent companion project is not affiliated with or endorsed by Anthropic or Google. Its title is a project name, not a promise of account eligibility or service access.

## 中文摘要

环境评分代码参考 LinXiaoTao/FuckClaude，保留 MIT 许可。翻译器保留 Python、CTranslate2、SentencePiece 与便携运行库的第三方声明；离线语言包由用户选装，各包许可分别适用。Google 在线翻译属于第三方服务。本项目不是 Anthropic 或 Google 官方产品。
