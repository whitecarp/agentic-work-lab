# Whale Environment Helper 0.7.0 / 大肥鱼环境助手

A Windows .NET Framework 4.8 companion with English and Chinese interfaces. This integrated version preserves the 0.6.0 maintenance, Codex connection repair, blocking-process workflow, updater, icon and orange theme, and adds the native DNS workflow developed in the separate 0.3.3 branch.

## Features

- **Measure first:** browser environment checks report missing or failed signals as unknown. Optimization changes only matched local timezone, browser language and eligible personal-font settings, with backup and undo. This third-party heuristic is not Claude's account eligibility score.
- **Maintenance & cleanup:** inspect old configuration overrides, preview narrow cache cleanup, back up before changes, and restore while preserving later conflicting changes. Histories, credentials and unrelated settings are not broadly erased.
- **Codex connection repair:** discover and verify actual local HTTP/mixed/SOCKS proxy endpoints, preview `.codex/.env` changes, preserve unrelated entries, and keep a private backup for undo. No fixed proxy port is assumed.
- **Close blocking apps:** the existing explicit workflow can request normal exit, with a separate confirmation for force-closing supported processes. It never silently terminates work. The DNS page itself does not close proxy processes.
- **Network & DNS:** one page inspects recognized CuteCloud DNS configuration, runs a native resolver observation, shows progress/results, and offers a separate exact preview, private backup, confirmed apply, undo and recheck. Main DNS observation does not open a browser. WebRTC remains an explicitly separate optional browser check.
- **Check updates:** manual release check through the existing shared updater.

## Network scope and dependencies

Native DNS observation uses the Net.Coffee public-page protocol and follows this helper's HTTPS/default-proxy path. It does not cover every application's traffic or browser-specific DoH. Five random probe names are used, with a six-second per-request and 45-second overall budget. Empty, partial, timed-out, cancelled and failed results remain distinct; resolver listings do not certify absence of leaks or account availability. The third-party protocol may change. No real settings, subscriptions, keys or credentials are sent to the observation service.

DNS repair currently supports recognized CuteCloud configuration only. It requires normal exit of the client/core, explicit preview confirmation, unchanged-file checks and a private backup. Unknown formats, reparse points, stale plans and conflicting later edits are refused. Other proxy clients can still be detected by Codex connection repair, but their DNS files are not automatically rewritten. See [Network guide](docs/NETWORK-GUIDE.md).

Backups under `%LOCALAPPDATA%\DafeiyuClaudeHelper\Backups` can contain private configuration. Keep them local. Do not add them to source control or release downloads.

## Build and checks

Run `build.ps1`; output defaults to `dist\大肥鱼环境助手.exe`. An alternative absolute output directory can be supplied with `-OutputDirectory`. The script uses Windows `csc.exe`, explicitly C# 5, with embedded page/icon/mascot/license resources. Keep this source under the repository's `apps/whale-claude-helper` so `../../shared/UpdateChecker.cs` resolves. No Python, Node or NuGet is needed to run the EXE; Node is only used for the offline JavaScript test suite.

Safe verification commands:

```powershell
node tests/WebRtcTests.js
./tests/run-score-measurement-tests.ps1
./tests/run-network-dns-tests.ps1
./tests/run-dns-leak-measurement-tests.ps1
./tests/run-network-ui-tests.ps1
./tests/run-fixtures.ps1
./tests/run-codex-proxy-tests.ps1
./tests/run-proxy-discovery-tests.ps1
./tests/run-process-close-tests.ps1
./tests/run-codex-proxy-ui-tests.ps1
```

These entry points use temporary fixtures, injected transports/process platforms, synthetic loopback endpoints and hidden UI rendering. They do not apply changes to the user's actual configuration. UI renders at 100%/150% are simulated layout scales, not changes to Windows display settings. See [QA report](docs/QA-0.7.0.md) for this build's validation and limits. No real DNS request, real process close or live apply/undo was repeated for 0.7.0.

## 中文说明

0.7.0 是整合版：保留 0.6.0 的维护与清理、Codex 连接修复、关闭占用程序、更新检查、独立图标和橙色主题，新增独立 0.3.3 分支的网络与 DNS 页面。没有用较小版本号覆盖已有功能。

主 DNS 检测直接在软件里显示解析器、进度与明确状态，不开浏览器；WebRTC 是另行点击的浏览器专项。检测仅覆盖助手 HTTPS/默认代理路径，不代表浏览器独立 DoH、全系统无泄露或账号可用。空结果、超时和失败不会标成“无泄露”。DNS 写入目前仅适用于识别出的 CuteCloud 文件，必须正常退出客户端及核心，预览确认后私密备份，再限范围应用；可撤回但保留后续独立修改。

构建及 1,668 项隔离检查通过，未改用户真实配置或停止真实进程；截图使用离屏模拟缩放，尚未完成独立 Windows 虚拟机全流程验收。网络观察、DNS 修复、环境评分及本机维护的范围各自独立。许可证见 `third-party/FuckClaude-LICENSE.txt`，同时嵌入 EXE。
