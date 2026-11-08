# Privacy and network behavior

This page describes the published toolbox source, not unreleased features in a developer's personal workspace. The applications do not upload entire Claude conversations, account databases, proxy subscriptions or backup folders to this project. They do make the feature-specific requests described below. A service receiving a request normally sees its network exit address and request metadata; its own privacy policy applies.

## Whale Translator

- **Google mode:** the text selected for translation, pasted into the translator, or entered in its compose window is sent over HTTPS to `translate.googleapis.com`, together with source/target language parameters. A connection test can send a small test phrase. This is a web translation transport, not a project-operated translation server or a promise of Google's official API service levels.
- **Offline mode:** translation runs locally using installed language packs. Installing or refreshing packs is a separate online action; offline inference does not need to send the text to Google.
- **Language packs:** the catalog is bundled with the app; an explicit catalog refresh uses the upstream Argos index on `raw.githubusercontent.com`. Pack downloads use the allowed HTTPS Argos sources (`argos-net.com` / `data.argosopentech.com`). The requested pack identifies a language direction. Package metadata and configured hashes are checked before installation; unavailable/unverified packages can be rejected rather than installed.
- **Local data:** preferences and the editable glossary live under `%LOCALAPPDATA%\DafeiyuTranslator`; installed packs live in its `packages` directory. The portable launcher also extracts its bundled application/runtime to a local cache. These are not uploaded as telemetry.
- **Session history:** Translator **0.9.4** retains at most 20 successful translations / 256,000 characters in process memory for the current session and clears them when it closes. It does not automatically write a translation-history file. Explicitly saved replies are the separate, persistent feature described next. The user can copy text to the Windows clipboard; Windows clipboard history, synchronization and other applications' clipboard access are separate from the toolbox. No claim is made that clipboard text is invisible to the operating system.
- **Saved replies:** only clicking **☆ Save** writes a reply to disk. Each reply is one Markdown file in the selected local folder, by default the Windows Documents folder's `大肥鱼收藏` subfolder. It contains the save timestamp, source-window title, translation method/language, tag, note, translated text and original text. Opening, searching and refreshing the notebook do not save replies or make network requests. A separate `%LOCALAPPDATA%\DafeiyuTranslator\favorites-settings.json` stores only the folder, tags and last-used tag, not reply content; there is no separate content index or notebook log. A sync service configured for the chosen folder may sync files independently under its own settings.
- **Editing and deleting favorites:** refreshing reloads valid notebook Markdown files from disk, including external edits/deletions. Updates and deletions check for external changes first. The notebook's Delete action asks for confirmation; clicking an already saved card again unsaves it. Both remove the corresponding Markdown file. Changing the notebook folder does not automatically move old files. The app does not upload or send saved replies to Claude.
- **Built-in glossary:** the initial dictionary has 76 product-name/technical-term entries (including case and target-language variants). An existing saved dictionary is preserved until you explicitly edit it, use **Add built-in terms**, and save. These preferences do not create a conversation archive.
- Window selection/reading and the optional compose/send feature operate locally on the window the user chooses. Sending a composed message to Claude causes the destination application to handle it under its own policy.

## Home-time Clock

Clock **0.5.0** reads the computer's clock and Windows time-zone information and stores its preferences locally at `%LOCALAPPDATA%\TwoZoneClock\settings.xml`. Time conversion does not need a network request. It does not change the system timezone. User-requested update checks are described below.

- **Reset-page reading:** after you choose a Claude window and request a read, a time-limited local child process uses Windows UI Automation to inspect visible static labels and eligible Usage headings. Supported windows are Claude Desktop, or Chrome/Edge/Firefox windows whose title identifies Claude. It does not log in, read cookies, tokens, password/edit fields, the clipboard, or an account database, and does not call Claude's account API. It cannot read a closed, minimized or inaccessible Usage page.
- **Pasted/manual times:** you can paste Usage text into the dialog or enter reset dates yourself. Parsing and timezone conversion happen locally. The full pasted/read Usage text is not saved to the settings file or sent to a server. The settings contain the resulting UTC reset timestamps, observation time, source label, estimate flag and display preferences; internal evidence fingerprints are memory-only. Source-window identity and the automatic-reading choice are also session-only.
- **Automatic reads:** a successful window read selects the visible 60-second reading option. Review or untick it before **Apply**. While enabled, the clock reads that same visible window about once a minute until the clock closes. Failed/ambiguous reads retain the previous snapshot and show a failure state; reaching the displayed reset time does not verify account quota restoration.

## Environment Helper

- Local checks read the settings/processes needed for the chosen diagnostic or repair. Repair, DNS/proxy changes and cache cleanup are user-initiated. Backups and receipts are stored locally under `%LOCALAPPDATA%\DafeiyuClaudeHelper`, including `Backups`; they may contain the original settings being preserved. Treat backups as private and do not attach them unreviewed to a public issue.
- **Network/DNS measurement:** an explicit check makes native HTTPS requests to Net.Coffee, including random subdomains of `d.ip.net.coffee`, and obtains observed DNS/exit information and optional IP location data from `ip.net.coffee`. The service can observe those random test domains, the request exit and resolver source addresses. No local proxy configuration, account credentials or subscription contents are sent. This measures the helper's HTTPS/default-proxy route, not every application or a browser's independent DoH route.
- **Browser measurement / optional WebRTC:** a local page measures browser-visible properties and returns results to the helper through loopback. Starting its WebRTC test contacts `stun.l.google.com:19302`. The STUN service can see network addressing information; the result returned to the helper contains candidate counts, not a list of private IP addresses. The optional browser check is separate from the native DNS measurement.
- A score is a third-party heuristic about measured properties, not an official Claude account eligibility verdict. Exported diagnostics/reports are written only where the user chooses; review them before sharing.
- The Codex proxy feature discovers local proxy listeners and writes selected proxy environment settings locally with backups. Later network traffic from Codex follows Codex and the chosen proxy provider's behavior; the helper does not become a traffic relay.

## Pack Helper

The app processes files the user selects, writes the chosen output package/files locally, and stores preferences in `%LOCALAPPDATA%\WhalePack\settings.json`. Media conversion invokes locally available FFmpeg. Packaging does not automatically upload source files or handoff packages to Claude or to this project. Sharing the resulting files is a separate user action.

## Updates and signing

The **Check for updates** action queries the public GitHub releases API for `whitecarp/I-Must-Use-Claude`. When the user requests a download, the app downloads the matching EXE from GitHub's release infrastructure and checks its advertised SHA-256. This transmits normal HTTP request information and the requested release/asset, not the user's chats or local backups.

Build-time dependency downloads and a future SignPath signing request concern release artifacts, not installed users' conversations. The SignPath Foundation application has been submitted and is awaiting review; approval and production signing are not yet in place. See [code signing](CODE_SIGNING.md). There is no background upload of users' files to a signing or malware-submission service.

## 中文说明

Google 模式会发送用户选中、粘贴或在提问框输入的待翻译文字及语言参数；不会自动上传整个 Claude 聊天。离线模式在本机处理，安装/刷新语言包则会连接 GitHub 的 Argos 目录及允许的 Argos 下载站。本次历史记录只在当前进程内保留，关闭即清空，不自动写入历史文件。设置、术语词典、模型包和运行时缓存保存在本机。复制后的文字受 Windows 剪贴板及其他应用规则影响。

翻译器 0.9.4 的「回复收藏本」是用户主动保存的例外：只有点击收藏才把该条回复写入一个本机 Markdown 文件，默认存于「文档\大肥鱼收藏」，包括保存时间、来源窗口标题、翻译方式和语言、标签、备注、译文及原文。收藏本不联网、不上传给 Claude，也没有另一份内容索引或日志；单独的 `favorites-settings.json` 只保存位置、标签和上次标签。打开、搜索、刷新都不会自动保存回复。刷新时以文件的外部修改/删除为准；修改或删除前会检查冲突。收藏本删除需确认，卡片取消收藏同样会删除对应文件。切换位置不自动移动旧文件；其他服务的目录同步由其设置决定。76 条内置术语可用「补充内置词条」加入，保留用户原译法，点击保存才生效。

时钟 0.5.0 的时间换算与重置时间解析在本机完成。读取功能只检查用户所选 Claude 窗口的可见静态用量文字与标题，不登录账号、不读取密码、Cookie、令牌、剪贴板或账号数据库，不调用 Claude 账号 API。原始用量文字不写入设置文件；仅保存解析后的 UTC 重置时刻、读取时间、来源、估算标记和显示偏好，内部证据指纹只在内存中。窗口读取成功会勾选每 60 秒读取，可在「应用」前取消；自动读取只持续到关闭时钟，不会在后台启动。

环境助手的本机检查、修复备份和报告保存在本机；网络检测会向 Net.Coffee 发送 HTTPS 测试请求，服务方可见出口、随机测试域名和观察到的解析器。可选浏览器 WebRTC 测试会连接 Google STUN。它们不上传本地代理订阅或凭据，也不代表所有应用的网络路径。

打包助手只处理用户选取的文件并生成本地结果，不自动发送给 Claude。手动检查更新会连接 GitHub，下载时获取对应 EXE。备份可能包含原始配置，请当作私人文件保管，分享前自行检查。SignPath Foundation 申请已提交、等待审核，尚未获批或用于生产签名；不会在用户电脑上自动上传文件用于签名或误报申诉。
