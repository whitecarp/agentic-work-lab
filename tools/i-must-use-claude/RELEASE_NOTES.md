# Release notes

## v0.4.5-preview — Translator 0.9.6 offline-retry status

Published **2026-11-03**, with four verified standalone EXEs. Translator **0.9.6** is **29,146,112 bytes (29.15 MB)**, including its runtime; language packs remain optional downloads.

| App | Version | Download |
| --- | --- | --- |
| Whale Environment Helper | 0.7.0 | [WhaleEnvironmentHelper.exe](https://github.com/whitecarp/I-Must-Use-Claude/releases/download/v0.4.5-preview/WhaleEnvironmentHelper.exe) |
| Whale Translator | 0.9.6 | [WhaleTranslator.exe](https://github.com/whitecarp/I-Must-Use-Claude/releases/download/v0.4.5-preview/WhaleTranslator.exe) |
| Two-Zone Clock | 0.5.0 | [HomeTimeClock.exe](https://github.com/whitecarp/I-Must-Use-Claude/releases/download/v0.4.5-preview/HomeTimeClock.exe) |
| Whale Pack Helper | 0.1.1 | [WhalePackHelper.exe](https://github.com/whitecarp/I-Must-Use-Claude/releases/download/v0.4.5-preview/WhalePackHelper.exe) |

- **The offline-retry message is no longer cut off.** When Google rate-limits a selection, the status beside **Use offline** now shows in full at the default and minimum window sizes, in English and Chinese; before, it was truncated with "…". While the button shows, the status gets the rest of the row, and the English text is shorter: "Google is unavailable. Use offline for this selection."
- **Four EXEs, one toolbox release:** clock **0.5.0**, helper **0.7.0** and pack **0.1.1** keep their versions from v0.4.4-preview.

**Signing:** this is still an **unsigned preview**. SignPath review is pending; Windows SmartScreen may warn. Verify the [release SHA-256 values](https://github.com/whitecarp/I-Must-Use-Claude/releases/tag/v0.4.5-preview#sha-256); hashes verify integrity, not publisher identity. See [code signing](docs/CODE_SIGNING.md).

<!-- release-validation:v0.4.5-preview:41575739298 -->
**Validation:** source commit `3cbf4bf0a8100bdb1664c6c514482e99c0baaef7` passed [build and Windows acceptance](https://github.com/whitecarp/I-Must-Use-Claude/actions/runs/41556180523), including the glossary and session-history fixtures, the translation-worker tests, the translator UI and saved-reply regressions, a check that the offline-retry status fits beside Use offline at the default and minimum window sizes in both languages, and four visible app launches. [Release verification](https://github.com/whitecarp/I-Must-Use-Claude/actions/runs/41575739298) confirmed portable extraction, native runtime imports, cache reuse, four EXE versions and upload/download byte equality before publication. All four public downloads were then downloaded again and matched their advertised SHA-256 values; the translator reports file version 0.9.6.0.

**已于 2026-11-03 发布：** 环境助手 **0.7.0**、翻译器 **0.9.6**、时钟 **0.5.0**、打包助手 **0.1.1**。四款附件已重新下载核对版本与 SHA-256；翻译器为 **29.15 MB**，含运行库，不含选装语言包。

- **离线重译的提示不再被截断。** Google 限流时，「改用离线翻译」按钮旁的提示在默认窗口和最小窗口下都能完整显示，中英文都一样；以前会被省略号截掉。

## v0.4.4-preview — Translator 0.9.5 glossary matching

Published **2026-11-02**, with four verified standalone EXEs. Translator **0.9.5** is **29,145,088 bytes (29.15 MB)**, including its runtime; language packs remain optional downloads.

| App | Version | Download |
| --- | --- | --- |
| Whale Environment Helper | 0.7.0 | [WhaleEnvironmentHelper.exe](https://github.com/whitecarp/I-Must-Use-Claude/releases/download/v0.4.4-preview/WhaleEnvironmentHelper.exe) |
| Whale Translator | 0.9.5 | [WhaleTranslator.exe](https://github.com/whitecarp/I-Must-Use-Claude/releases/download/v0.4.4-preview/WhaleTranslator.exe) |
| Two-Zone Clock | 0.5.0 | [HomeTimeClock.exe](https://github.com/whitecarp/I-Must-Use-Claude/releases/download/v0.4.4-preview/HomeTimeClock.exe) |
| Whale Pack Helper | 0.1.1 | [WhalePackHelper.exe](https://github.com/whitecarp/I-Must-Use-Claude/releases/download/v0.4.4-preview/WhalePackHelper.exe) |

- **Terms ignore case.** `claude code`, `Claude Code` and `CLAUDE CODE` use the same entry. In **⚙ → Glossary**, tick **Match case** for a term that should only match its exact spelling. Built-in names that are also everyday words (Skills, Artifacts, Hooks, Opus, Sonnet, Haiku, Fable, Gemini, Grok, Llama, Mistral, Perplexity, Windsurf, React, Rust, Swift, Cursor, Copilot) have it ticked, so "improve your skills" still translates.
- **135 built-in entries** (was 76): product names machine translation tends to mangle (Hugging Face, Midjourney, Claude Max, GitHub Actions, VS Code…), more technical phrases (usage limit, prompt caching, edge case, race condition…) and more Chinese → English terms for Write in Chinese (提示词, 仓库, 报错信息…). One entry now covers every case, so the capitalised copies are gone. The editor holds up to 200 entries.
- **Existing dictionaries upgrade when read.** Rows that differ only in case and share a translation become one row; rows with different translations (Apple / apple) keep Match case, so nothing changes meaning. To add the new built-ins, click **Add built-in terms**, then **Save**.
- **Four EXEs, one toolbox release:** clock **0.5.0**, helper **0.7.0** and pack **0.1.1** keep their versions from v0.4.3-preview.

**Signing:** this is still an **unsigned preview**. SignPath review is pending; Windows SmartScreen may warn. Verify the [release SHA-256 values](https://github.com/whitecarp/I-Must-Use-Claude/releases/tag/v0.4.4-preview#sha-256); hashes verify integrity, not publisher identity. See [code signing](docs/CODE_SIGNING.md).

<!-- release-validation:v0.4.4-preview:41548334492 -->
**Validation:** source commit `b2320477b28f9a1a73fbb54241633bb9e7b6782d` passed [build and Windows acceptance](https://github.com/whitecarp/I-Must-Use-Claude/actions/runs/41547561018), including the glossary and session-history fixtures, the translation-worker tests, the translator UI and saved-reply regressions, and four visible app launches. [Release verification](https://github.com/whitecarp/I-Must-Use-Claude/actions/runs/41548334492) confirmed portable extraction, native runtime imports, cache reuse, four EXE versions and upload/download byte equality before publication. All four public downloads were then downloaded again and matched their advertised SHA-256 values; the translator reports file version 0.9.5.0.

**已于 2026-11-02 发布：** 环境助手 **0.7.0**、翻译器 **0.9.5**、时钟 **0.5.0**、打包助手 **0.1.1**。四款附件已重新下载核对版本与 SHA-256；翻译器为 **29.15 MB**，含运行库，不含选装语言包。

- **术语默认不区分大小写。** `claude code`、`Claude Code`、`CLAUDE CODE` 用同一条；在 ⚙ → 术语词典 勾选「区分大小写」的词条只认原样写法。Skills、Sonnet、Rust 等本身也是普通英文单词的内置名称默认勾选，"improve your skills" 仍正常翻译。
- **内置 135 条**（原 76 条）：补充机翻容易弄错的产品名（Hugging Face、Midjourney、Claude Max、GitHub Actions、VS Code 等）、技术词组（用量上限、提示缓存、边界情况、竞态条件等），以及「中文提问」用的中译英词条（提示词、仓库、报错信息等）。一条覆盖所有大小写，首字母大写的重复词条已删除。词典上限 200 条。
- **旧词典读取时自动升级。** 只差大小写且译法相同的行合并为一行；译法不同的行（如 Apple / apple）自动勾选「区分大小写」，意思不变。要加入新的内置词，点「补充内置词条」再保存。

## v0.4.3-preview — Translator 0.9.4 glossary and saved replies

Published **2026-11-02**, with four verified standalone EXEs. Translator **0.9.4** is **29,143,552 bytes (29.14 MB)**, including its runtime; language packs remain optional downloads.

| App | Version | Download |
| --- | --- | --- |
| Whale Environment Helper | 0.7.0 | [WhaleEnvironmentHelper.exe](https://github.com/whitecarp/I-Must-Use-Claude/releases/download/v0.4.3-preview/WhaleEnvironmentHelper.exe) |
| Whale Translator | 0.9.4 | [WhaleTranslator.exe](https://github.com/whitecarp/I-Must-Use-Claude/releases/download/v0.4.3-preview/WhaleTranslator.exe) |
| Two-Zone Clock | 0.5.0 | [HomeTimeClock.exe](https://github.com/whitecarp/I-Must-Use-Claude/releases/download/v0.4.3-preview/HomeTimeClock.exe) |
| Whale Pack Helper | 0.1.1 | [WhalePackHelper.exe](https://github.com/whitecarp/I-Must-Use-Claude/releases/download/v0.4.3-preview/WhalePackHelper.exe) |

- **76 starter glossary entries:** Claude/product names and common technical phrases include case and target-language variants. Existing saved dictionaries remain yours. In **⚙ → Glossary**, **Add built-in terms** fills nonconflicting gaps without replacing your translations; **Save** applies the changes. Terms work online and offline.
- **Saved replies are explicit:** click **☆ Save** on a successful translation, choose a tag with the arrow, and browse it later in **⚙ → Saved replies**. Saving is never automatic. Each reply becomes a local Markdown file with its timestamp, source-window title, translation method/language, tag, note, translation and original text. The default folder is **Documents\大肥鱼收藏**, and **Location…** selects another local folder without moving old files.
- **Search and edit locally:** search both texts, filter tags, save notes, copy the original/translation, and open the folder. Refresh uses the Markdown files as the source of truth, including external changes/deletions. File-change checks prevent stale overwrites. Deleting inside the notebook asks for confirmation; clicking a saved card again unsaves it. Both delete the corresponding Markdown file.
- **History remains separate:** bounded session history is still memory-only and clears on exit. Only explicit favorites persist. The notebook itself makes no network requests, writes no content index/log, and does not send replies to Claude. Google online translation continues to send the selected text to Google; offline mode and its optional language packs keep their existing behavior. [Translator guide](apps/claude-live-translator/README.md) · [Privacy](docs/PRIVACY.md).
- **Four EXEs, one toolbox release:** clock **0.5.0**, helper **0.7.0** and pack **0.1.1** retain their app versions. Every release includes all four standalone EXEs and complete `whale-app-versions` metadata. The translator includes its runtime; offline language packs remain optional installs.

**Signing:** this is still an **unsigned preview**. SignPath review is pending; Windows SmartScreen may warn. Verify the [release SHA-256 values](https://github.com/whitecarp/I-Must-Use-Claude/releases/tag/v0.4.3-preview#sha-256); hashes verify integrity, not publisher identity. See [code signing](docs/CODE_SIGNING.md).

<!-- release-validation:v0.4.3-preview:41542652373 -->
**Validation:** source commit `53fc1baedb5bc140c2e6b468868b1f201e6d91a6` passed [build and Windows acceptance](https://github.com/whitecarp/I-Must-Use-Claude/actions/runs/41542153141), including all 22 translator regression suites (94 Python tests, 3 conditional skips), four visible app launches, and 16 synthetic bilingual 96-DPI translator previews. The additional 150% images are resampled size comparisons, not independent Windows DPI tests. [Release verification](https://github.com/whitecarp/I-Must-Use-Claude/actions/runs/41542652373) confirmed fresh portable extraction, four pinned runtime imports, cache reuse, four EXE versions, and upload/download byte equality before publication. All four public downloads were then independently checked again against their advertised SHA-256 values. Tests use generated fixtures, not private conversations, a real Claude account or personal settings. Live Google translation and real offline-model inference were not part of this release acceptance.

**已于 2026-11-02 发布：** 环境助手 **0.7.0**、翻译器 **0.9.4**、时钟 **0.5.0**、打包助手 **0.1.1**。四款附件已重新下载核对版本与 SHA-256；翻译器为 **29.14 MB**，含运行库，不含选装语言包。

- **76 条内置术语**包括产品名称、技术词组及大小写/目标语言变体；已有个人词典保留。在 ⚙ → 术语词典点「补充内置词条」，只补未冲突的词，再点保存。在线、离线都生效。
- **回复收藏本主动保存。** 点译文卡片「☆ 收藏」，箭头选标签，⚙ → 回复收藏本 查看。默认一条一个本机 Markdown，位于「文档\大肥鱼收藏」，包含保存时间、来源窗口、翻译方式和语言、标签、备注、译文和原文；可改保存位置，不自动搬移旧文件。
- **文件是唯一内容依据。** 搜索两种文字、筛选标签、编辑备注、复制或打开文件夹；刷新采用外部修改/删除后的文件，冲突检查避免覆盖新改动。收藏本删除先确认，卡片取消收藏也会删除对应 Markdown。
- **历史仍只在内存。** 关闭软件清空本次记录，只有主动收藏才持久保存；收藏本不联网、不另建内容索引或日志、不自动发给 Claude。Google 在线翻译仍发送所选文字，离线模式和选装语言包流程不变。
- **四个 EXE 附齐，仍未签名。** 本次源码通过 22 组翻译器回归、四款程序启动与截图检查；单文件首次解压、运行库导入、缓存复用及公开附件哈希均通过。150% 图是放大对比，不代表独立的 Windows 150% 缩放验收；本次未测试真实 Google 请求或真实离线模型推理。

---

## v0.4.2-preview — Home-Time Clock 0.5.0 with Claude reset countdowns

| App | Version | Download |
| --- | --- | --- |
| Whale Environment Helper | 0.7.0 | [WhaleEnvironmentHelper.exe](https://github.com/whitecarp/I-Must-Use-Claude/releases/download/v0.4.2-preview/WhaleEnvironmentHelper.exe) |
| Whale Translator | 0.9.1 | [WhaleTranslator.exe](https://github.com/whitecarp/I-Must-Use-Claude/releases/download/v0.4.2-preview/WhaleTranslator.exe) |
| Two-Zone Clock | 0.5.0 | [HomeTimeClock.exe](https://github.com/whitecarp/I-Must-Use-Claude/releases/download/v0.4.2-preview/HomeTimeClock.exe) |
| Whale Pack Helper | 0.1.1 | [WhalePackHelper.exe](https://github.com/whitecarp/I-Must-Use-Claude/releases/download/v0.4.2-preview/WhalePackHelper.exe) |

- **Clock 0.5.0 now includes the session and weekly reset countdowns shown in the tool videos.** Keep Claude's Settings → Usage page visible, choose its window and displayed timezone in **Claude reset countdown…**, read the times and apply. A successful read selects the 60-second reading option; untick it before applying for a single read. Automatic reads stop when the clock closes.
- **Paste and manual fallback:** supported Chinese/English Usage text can be pasted, or reset times entered manually. Failed, incomplete or ambiguous reads preserve the previous snapshot. The compact **本周 / This week** dropdown heading is supported, along with the aggregate **Weekly limits → All models** layout.
- **Home time stays first:** the original region is large and above the compact target region. Local day/night colors, date/difference text, rounded controls and bilingual settings accompany the compact countdown card.
- **Local reading, explicit limits:** the reader uses visible labels in the selected window; it does not log in, query an account API, or save the original Usage text. It stores parsed reset timestamps and observation metadata locally. `≈` is an estimate, and zero does not confirm restored quota. Progress bars show elapsed reference time, not quota consumption. [Clock guide](apps/two-zone-clock/README.md) · [Privacy](docs/PRIVACY.md).
- **Four standalone EXEs again:** Translator **0.9.1**, Environment Helper **0.7.0** and Pack Helper **0.1.1** keep their app versions. Every release includes all four apps and a complete `whale-app-versions` marker. No configuration sidecar or convenience ZIP is required; translator models remain optional installs.

**Signing:** these are **unsigned preview** files. SignPath Foundation review is pending, and Windows SmartScreen may still warn. Compare downloads with this release's [SHA-256 values](https://github.com/whitecarp/I-Must-Use-Claude/releases/tag/v0.4.2-preview#sha-256). Hashes verify integrity, not publisher identity. [Signing policy](docs/CODE_SIGNING.md).

<!-- release-validation:v0.4.2-preview:run-41537825771 -->
**Validation:** [Build and disposable Windows acceptance](https://github.com/whitecarp/I-Must-Use-Claude/actions/runs/41537825771) passed for source `91f70f9`: 1,166 clock checks, synthetic reader fixtures, the existing 49 workflow + 11 timezone checks, four visible app launches, and ten clock preview images. [Release run 41538274164](https://github.com/whitecarp/I-Must-Use-Claude/actions/runs/41538274164) verified the portable translator runtime and downloaded all four uploaded EXEs back to compare versions and SHA-256 before publication. Clock fixtures/screenshots contain synthetic times, not a private Claude account. This does not establish compatibility with every real or future Claude UI. Published files remain unsigned previews.

本次 **v0.4.2-preview** 提供四个单 EXE：环境助手 **0.7.0**、翻译器 **0.9.1**、两地时钟 **0.5.0**、打包助手 **0.1.1**。

- **视频里展示的本轮和每周重置倒计时进入公开时钟版本。** 打开 Claude「设置 → 使用情况」并保持重置文字可见，在时钟「Claude 重置倒计时…」里选择页面时区与窗口，读取、核对后应用。读取成功会勾选每 60 秒读取；只想读一次可在应用前取消，自动读取只持续到关闭时钟。
- **保留粘贴与手动方式。** 支持常见中英文用量文字、「本周 / This week」下拉标题和「每周限额 → 所有模型」布局。读取失败、不完整或有歧义时保留原倒计时。
- **原本时间大且在上，目标时间小且在下。** 加入按地区当地时间变化的昼夜配色、圆角控件、日期时差与中英文倒计时设置。
- **本机可见页面读取。** 不登录、不调用账号 API、不保存原始用量文字，只保存解析结果与读取元数据。估算归零不保证额度恢复，进度条不是额度用量。详见[时钟说明](apps/two-zone-clock/README.md)和[隐私说明](docs/PRIVACY.md)。
- **继续附齐四款 EXE。** 其他三款软件版本不变，无需旁置配置文件或 ZIP；离线翻译模型仍由用户选装。文件仍为**未签名预览版**，签名审核尚未完成，SmartScreen 可能提示。

源码 `91f70f9` 的[构建与临时 Windows 验收](https://github.com/whitecarp/I-Must-Use-Claude/actions/runs/41537825771)已通过：1,166 项时钟检查、读取器合成夹具、既有 49 项流程与 11 项时区检查、四款窗口启动及十张时钟预览图。[发布运行 41538274164](https://github.com/whitecarp/I-Must-Use-Claude/actions/runs/41538274164)核验便携翻译运行时，并将上传的四个 EXE 重新下载，版本与 SHA-256 一致后才公开。截图和测试使用模拟时间，没有读取私人账号；不代表已保证所有真实或未来 Claude 界面的读取兼容性。文件仍为未签名预览版。

---

## v0.4.1-preview — four EXEs, manual updates and single-file Pack Helper

| App | Version | Download |
| --- | --- | --- |
| Whale Environment Helper | 0.7.0 | [WhaleEnvironmentHelper.exe](https://github.com/whitecarp/I-Must-Use-Claude/releases/download/v0.4.1-preview/WhaleEnvironmentHelper.exe) |
| Whale Translator | 0.9.1 | [WhaleTranslator.exe](https://github.com/whitecarp/I-Must-Use-Claude/releases/download/v0.4.1-preview/WhaleTranslator.exe) |
| Two-Zone Clock | 0.3.0 | [HomeTimeClock.exe](https://github.com/whitecarp/I-Must-Use-Claude/releases/download/v0.4.1-preview/HomeTimeClock.exe) |
| Whale Pack Helper | 0.1.1 | [WhalePackHelper.exe](https://github.com/whitecarp/I-Must-Use-Claude/releases/download/v0.4.1-preview/WhalePackHelper.exe) |

- **Pack Helper 0.1.1 is a single EXE.** No `.exe.config` or convenience ZIP is required. Runtime switches and the Windows manifest support long paths without a configuration sidecar. Windows .NET Framework 4.8 is still required; video compression and WebP decoding still need an existing local FFmpeg.
- **All four apps offer manual update checks.** Pack Helper uses **Settings → Check updates**. Checks and downloads contact this repository's GitHub releases only when requested. Downloads require the release asset's SHA-256 digest; apps do not replace themselves or launch the downloaded EXE.
- **Translator 0.9.1 refreshes portable-package verification and the offline-package hash catalog.** Language directions remain optional installs; this does not bundle every offline model into the EXE.
- **Local packing does not upload files or contact an AI service.** Pack Helper's manual update check/download is the GitHub network exception. See the [privacy notice](docs/PRIVACY.md) for the other tools' data flows.
- **The bilingual [project page](https://whitecarp.github.io/I-Must-Use-Claude/) is live.** Environment Helper remains first, and all four download links use this release. The existing design, CSS, illustrations and language interactions are preserved.

**Signing:** this release remains an **unsigned preview**. Read the [code-signing policy and current status](docs/CODE_SIGNING.md), [security reporting guide](SECURITY.md), and this release's [release SHA-256 checksums](https://github.com/whitecarp/I-Must-Use-Claude/releases/tag/v0.4.1-preview#sha-256). A hash match verifies file integrity, not publisher identity. Windows SmartScreen may still warn on first launch.

<!-- release-validation:v0.4.1-preview:run-41398526505 -->
**Validation status:** [Build and cloud Windows acceptance passed](https://github.com/whitecarp/I-Must-Use-Claude/actions/runs/41398526505) for source `5959687`. The acceptance scope is **49 workflow checks + 11 time-zone checks + four captured application windows**, including Pack Helper startup without a configuration sidecar. Apply/verify/undo actions used temporary fixtures on a disposable GitHub-hosted Windows VM. No personal PC, real user settings, Claude account or private media were used. This does not establish live update-download or real-media end-to-end behavior. Later source changes require a new successful run; final publication records must tie each downloadable EXE to its version, size and SHA-256.

**Final release-package acceptance:** [Release run 41398725441](https://github.com/whitecarp/I-Must-Use-Claude/actions/runs/41398725441) passed for `5959687`: the translator completed fresh extraction, imported the four bundled Python dependencies (CTranslate2, SentencePiece, NumPy and PyYAML) with their pinned versions and extracted-package paths verified, and reused its verified cache. All four uploaded EXEs were downloaded back and their SHA-256 hashes matched before publication. Real Google translation and offline-model downloads were not exercised. The published files remain **unsigned previews**.

本次四个软件统一从 **v0.4.1-preview** 下载：环境助手 **0.7.0**、翻译器 **0.9.1**、两地时钟 **0.3.0**、打包助手 **0.1.1**。

- **打包助手改为单 EXE**，无需同名配置文件或 ZIP。长路径运行时开关和 Windows 清单已内置；仍依赖 .NET Framework 4.8，视频压缩与 WebP 解码仍需要本机已有 FFmpeg。
- **四个软件都有手动更新检查**；打包助手入口是「设置 → 检查更新」。只有用户主动操作时才访问 GitHub；下载须通过发布附件的 SHA-256 验证，不自动替换或运行新版。
- **翻译器 0.9.1 更新便携包验证和离线包哈希目录**，离线语言方向仍由用户选装，不代表 EXE 已内置所有模型。
- **本地打包不上传文件、不连接 AI 服务**；手动检查和下载更新会联系 GitHub。各工具的数据流详见[隐私说明](docs/PRIVACY.md)。
- 中英文[项目主页](https://whitecarp.github.io/I-Must-Use-Claude/)已开启，环境助手排在前面；保留现有布局、CSS、插图和语言切换，四个下载入口统一本版。

**签名状态：** 本轮仍是**未签名预览版**。详见[代码签名政策](docs/CODE_SIGNING.md)、[安全报告方式](SECURITY.md)与本版 [SHA-256 校验值](https://github.com/whitecarp/I-Must-Use-Claude/releases/tag/v0.4.1-preview#sha-256)。哈希只能验证文件完整性，不代表发布者身份认证；SmartScreen 仍可能提示。

**验收状态：** 源码 `5959687` 的[构建和云端 Windows 验收已通过](https://github.com/whitecarp/I-Must-Use-Claude/actions/runs/41398526505)，范围为 **49 项流程检查 + 11 项时区检查 + 四款软件窗口截图**，包含打包助手无旁置配置文件启动。应用、核验与撤回使用 GitHub 临时 Windows 虚拟机内的临时测试数据，没有使用个人电脑、真实用户设置、Claude 账号或私人素材；不代表已验证在线更新下载或真实素材端到端使用。后续源码变更须重新通过，最终发布记录还应关联各 EXE 的版本、大小和 SHA-256。

**最终发布包验收：** 源码 `5959687` 的[发布运行 41398725441](https://github.com/whitecarp/I-Must-Use-Claude/actions/runs/41398725441) 已通过：翻译器完成首次解包，内置 Python 成功导入 CTranslate2、SentencePiece、NumPy、PyYAML 四个依赖并核对固定版本与解包路径，再验证缓存复用；四个 EXE 上传后重新下载，SHA-256 全部一致后才发布。未实测真实 Google 在线翻译或离线模型下载；发布文件仍为**未签名预览版**。

---

## v0.4.0-preview — Network & DNS and Pack Helper

- **Whale Environment Helper 0.7.0:** combines the toolbox's existing maintenance, Codex proxy configuration, close-app controls and dedicated icon with the separate 0.3.3 Network & DNS work. The integrated build is a new artifact; it does not reuse the old 0.3.3 binary or checksum.
- **DNS measurement:** resolvers, progress and explicit timeout/cancellation/empty-result states appear inside the app. This measures the helper's HTTPS/default-proxy path, not browser-specific DoH or every application on the machine. Browser WebRTC remains a separate optional check.
- **DNS repair:** preview supported CuteCloud DNS settings, make a private backup, confirm application, recheck, and undo without overwriting unrelated later edits. Environment optimization itself does not silently change network settings.
- **Whale Pack Helper 0.1.0:** review and package local source, images and video, with optional handoff notes and outputs capped at 29,000,000 bytes each. Originals remain intact and nothing is uploaded. Video compression and WebP decoding need an existing local FFmpeg.
- **Claude's project-page design is preserved.** The existing layout, CSS, illustrations and bilingual interactions are retained; content and download links now cover the added tool and network functions. GitHub Pages deployment is separate from source publication.

Download `WhaleEnvironmentHelper.exe`, or keep `WhalePackHelper.exe` together with `WhalePackHelper.exe.config` (both are also in the convenience ZIP). Translator **0.9.0** and Clock **0.3.0** continue to use their v0.3.0-preview downloads; this release does not replace them.

**Validation:** the final integrated helper passed 1,684 isolated checks, Pack Helper passed 7,088 assertions, and the shared updater passed 54 checks. Asset hashes are recorded in `docs/SHA256SUMS.txt`; Pack Helper's runtime configuration must accompany the EXE. Independent Windows VM end-to-end acceptance remains pending. No personal configuration, credentials, subscriptions, private backups or user logs belong in this release.

环境助手 **0.7.0** 保留维护清理、Codex 代理、关闭程序入口和专属图标，增量整合独立 0.3.3 的网络与 DNS 功能，使用重新构建的成品。DNS 结果仅说明助手 HTTPS／默认代理路径，不能代表浏览器独立 DoH 或全系统无泄露；修改须单独预览、私密备份、确认应用，可撤回。WebRTC 是独立可选检测。

新增打包助手 **0.1.0**，在本机整理源码、图片和视频，不上传，原文件不变；视频与 WebP 需要本机 FFmpeg。EXE 和同名配置需放在一起，亦可下载配套 ZIP。翻译器 0.9.0、时钟 0.3.0 保持原发布下载。沿用 Claude 的前端布局、样式与插图，只同步功能说明和下载链接。环境助手 1,684 项、打包助手 7,088 项及共用更新器 54 项隔离检查通过，独立虚拟机端到端验收仍待完成。


## v0.3.0-preview — terminology, recent translations and updates

- **Whale Translator 0.9.0:** a personal glossary for preferred terms, plus Previous / Next navigation through translations from the current session. Translation history stays in memory and is cleared on exit; saved glossary entries are separate.
- **Two-Zone Clock 0.3.0:** show the target region's time difference and relative day compared with the home region, using Windows timezone rules including daylight saving changes.
- **All three apps, including Environment Helper 0.5.0:** manually check this repository's public GitHub releases, including previews, and save the correct standalone EXE to a user-selected location. A GitHub-provided SHA-256 digest is required and must match. Missing or mismatched digests are not accepted as verified updates. There is no automatic background check, replacement or execution; existing apps and settings stay in place.

Three Windows EXE downloads remain the delivery format. The clock's time display stays offline; manual update checks/downloads are its only new network use. These features do not change the apps' language defaults or the helper's protected-data and account-access boundaries.

**Validation scope:** isolated fixtures and build checks. Independent Windows VM end-to-end acceptance is still pending; this preview does not claim production-environment or account-access validation.

翻译器 **0.9.0** 新增个人术语表，以及仅保留在本次运行内存中的「上一条／下一条」翻译记录；关闭软件清空翻译记录，保存的术语表单独保留。两地时钟 **0.3.0** 新增目标地区相对原本地区的时差与跨日提示，考虑夏令时。

三个软件（含环境助手 **0.5.0**）新增手动检查更新，可查看包含预览版在内的 GitHub 发布，并把对应 EXE 下载到用户选择的位置。必须有 GitHub 提供的 SHA-256 且核验一致；缺少或不匹配时不接受为已验证更新。不后台自动检查、不自动替换或运行，保留旧软件和设置。时钟正常计时仍无需联网。

验证范围为隔离模拟测试与构建检查，独立 Windows 虚拟机端到端验收仍待完成；不代表已在真实用户环境验证，也不保证 Claude 账号可用。

## v0.2.0-preview — maintenance and cleanup

**Whale Environment Helper 0.4.0** adds a separate **Maintenance & cleanup** entry:

- Optional read-only installation, proxy-variable, DNS and HTTPS checks. No credentials are sent with connection probes; results do not determine account or region eligibility.
- Claude Code configuration inspection and selective removal of old endpoint, authentication and model overrides, with local backups.
- Allowlisted cache/log scanning, byte counts, verified backups, Recycle Bin cleanup and conflict-aware restoration. Chats, cookies, login data, skills, plugins, hooks, custom rules and identity are preserved.
- Additional protections against linked files, unsafe Windows path aliases and cache changes between scan and cleanup.
- English / 中文 maintenance pages and isolated test fixtures. No developer-machine cleanup, real Claude configuration modification or live connectivity probe was performed for this release. Independent Windows VM end-to-end acceptance is pending.

Translator **0.8.1** and the clock EXE are unchanged. The README also links to the independent **Cockpit Tools** project; it is not bundled. Three Windows EXE downloads, with public source code.

环境助手 **0.4.0** 新增「维护与清理」：连接与安装体检、官方配置检查与选择性修复、缓存日志扫描清理、备份恢复。先查看范围，再核验备份后移入回收站；会话、登录数据、Skills 等保留。只完成隔离模拟测试与构建，未在个人环境执行清理或真实连接探测；仍待独立 Windows 虚拟机验收。翻译器及时钟 EXE 保持不变，Cockpit Tools 作为独立项目推荐。

## v0.1.0-preview — initial public preview

A Windows companion collection for using Claude:

- **Whale Translator 0.8.1:** translate selected Claude text; review Chinese-to-English prompts; choose a source window using searchable cards; use Google online translation or explicitly installed offline language pairs; retry a rate-limited request offline when a matching pack is available.
- **Two-Zone Clock:** keep your original/home region and target region visible in a compact vertical whale-themed panel. Search regions directly in the selector. Display preferences and daylight saving behavior follow the app's saved settings and Windows timezone rules.
- **Whale Environment Helper 0.3.0 preview:** measure browser signals first, optimize matched reversible local settings, measure again, and undo changes from backups.

English is the repository's default documentation language, with a Simplified Chinese README. This packaging does not change the apps' existing language defaults.

### Validation and limits

The environment helper has compiled and passed isolated JavaScript scoring checks. Independent Windows VM end-to-end acceptance is still pending; it is published as a preview. A build or a successful simulated score is not proof that a real user's environment changed or that Claude access will work.

The translator's earlier window-selection/copy workflow was user-confirmed in Claude. Newer packaging and offline features should be checked on the intended Windows machine before travel. Download the required language pairs while online; copying the EXE to a new computer does not copy its installed models.

Google online translation may be unavailable or rate-limited. Offline translation quality depends on the chosen model. The clock depends on Windows' clock accuracy and timezone rules. The helper does not alter network routing or account restrictions.

### Data handling

Source publishing excludes personal settings, backups, diagnostic records, account data, translation history and development handoffs. App configuration backups remain local.

## 首个公开预览版

三款工具围绕 Claude 使用体验：翻译 Claude 的回复与中文问题、查看自己原本地区的时间、先检测再调整可恢复的本地环境。

GitHub 默认英文，提供简体中文说明；软件默认语言保持现状。环境助手仍待独立 Windows 虚拟机端到端验收。离线使用前请联网安装需要的语言方向，并在目标电脑上检查。仓库不包含私人配置、备份、凭据、诊断记录或开发交接材料。

Only the three Windows EXE files are attached. No compilation is needed to use them; source remains public. Optional translator language packs are installed inside the app.

下载区仅提供三个 Windows EXE，双击运行，无需自行编译；源码公开。翻译器离线语言包在软件内选装。

Checksums / 校验值：[SHA256SUMS.txt](https://github.com/whitecarp/I-Must-Use-Claude/blob/main/docs/SHA256SUMS.txt)
