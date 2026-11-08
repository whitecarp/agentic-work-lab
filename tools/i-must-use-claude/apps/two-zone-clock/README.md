# Whalegirl Two-Zone Clock 0.5.0

Keep your home time visible while your computer uses another timezone. The home region is the large upper card; the target region is a smaller card underneath. Optional Claude session and weekly reset countdowns sit below both.

Windows 10/11 and .NET Framework 4.8 are required. The app is one EXE and supports English / 中文. It does not change the computer's clock or timezone. [Public downloads](https://github.com/whitecarp/I-Must-Use-Claude/releases/latest) are unsigned previews until production code signing is available.

## Regions and display

Right-click the clock or its tray icon to open region settings. Search inside each selector and save your home and target regions. Choose a fixed home region instead of **Follow computer timezone** if home time should stay independent of later system changes.

The menu also controls 12/24-hour time, seconds, one/two regions, always-on-top and position locking. The day/night card colors follow each region's local time. Time differences and daylight saving follow Windows timezone rules.

## Claude reset countdowns

1. Open Claude **Settings → Usage**. Leave the relevant session and weekly reset text visible and keep the window unminimized.
2. Right-click the clock → **Claude reset countdown…**. Pick the timezone actually used by the Usage page, then its window and **Read Usage page**. The selector accepts Claude Desktop or a Claude-titled window in Chrome, Edge or Firefox.
3. Review the displayed reset times, then **Apply**. A successful read selects **Read every 60 seconds**. Untick that option before applying if you want only one read. The option and selected window last only for the current clock session; automatic reads still require the Usage page to remain visible.

The reader recognizes supported English and Chinese Usage labels, including **Current session / 当前会话**, **This week / 本周**, and the aggregate **Weekly limits → All models** layout. It does not interpret every product-specific limit. If the page is inaccessible or the text is ambiguous, use **Paste usage text** or **Manual**. Failed reads keep the previous snapshot instead of replacing it with zero.

The page timezone is separate from the two clock regions. It must match the time labels on Claude's page; changing it does not change Windows or Claude. Ambiguous or invalid daylight-saving times require manual correction.

The `≈` display is a page-based estimate. Reaching the reset time does not confirm restored quota. Progress bars show elapsed time against a five-hour session / seven-day weekly reference; they do not measure token use or percentage of quota consumed. Use Claude's Usage page for account status.

## Local data and network

- Preferences live at `%LOCALAPPDATA%\TwoZoneClock\settings.xml`. The saved reset snapshot contains UTC reset timestamps, observation time, a source label and an estimate flag. Original Usage text is not saved; internal evidence fingerprints remain in memory.
- A bounded local child process reads eligible visible labels with Windows UI Automation. It does not log in, query an account API, read credentials/cookies, password or edit fields, or read the clipboard. Closing/minimizing the page or changing its content can prevent a read.
- Pasted text is parsed locally. Clear countdowns in the dialog and apply to remove the saved reset snapshot.
- Clock display and countdowns require no network request. Only a manually requested update check/download contacts GitHub. The app does not replace itself or execute an update automatically.

See the shared [privacy notice](../../docs/PRIVACY.md) and [signing policy](../../docs/CODE_SIGNING.md).

## Build and tests

From this directory in Windows PowerShell:

```powershell
.\build.ps1 -RunTests -OutputDirectory "$env:TEMP\whale-clock-build"
```

The build uses Windows' C# 5 compiler and includes `ClockTests`, `ResetCountdownTests` and `ResetWindowReaderTests`. Tests use generated data and synthetic accessibility fixtures. GUI launch/acceptance runs on disposable GitHub-hosted Windows; it does not require a private Claude account. Unit tests and mock pages cannot establish compatibility with every future Claude UI.

Developer previews use synthetic data, for example `TwoZoneClock.exe --preview-reset output.png`, `--preview-reset-en output.png`, or `--preview-reset-settings output.png`.

## 中文使用说明

**鲸娘两地时钟 0.5.0**：原本地区大卡片在上，目标地区小卡片在下，还可显示 Claude 本轮和每周重置倒计时。右键主窗口或托盘图标打开设置，在地区框内直接搜索。选固定原本地区，电脑改时区时家乡时间也不会跟着改变。软件不修改系统时间或时区。

**读取倒计时：** 打开 Claude「设置 → 使用情况」，保持本轮和每周重置文字可见、窗口不要最小化。右键时钟 →「Claude 重置倒计时…」，选好该用量页显示的时区，再选择 Claude 窗口、点击「读取用量页」。支持 Claude 桌面端，以及标题包含 Claude 的 Chrome、Edge、Firefox 窗口。核对后点「应用」。

读取成功会勾选「每 60 秒自动读取」；只想读一次可在应用前取消勾选。自动读取只持续到关闭时钟，仍需保持用量页可见。页面时区与原本/目标地区分别设置，不会替你修改 Claude 或 Windows。

支持常见中英文用量标签，包括「当前会话」「本周」以及「每周限额 → 所有模型」；不是所有产品专属额度都能识别。读不到时使用「粘贴用量文字」或「手动设置」。读取失败、有歧义或夏令时导致时间无法确定时保留原结果，不会把额度当作零。`≈` 是页面估算，归零不代表额度已恢复；进度条只按本轮五小时/每周七天表示经过时间，**不是已用额度**。

设置存于 `%LOCALAPPDATA%\TwoZoneClock\settings.xml`，仅保存解析后的重置时刻及读取元数据，不保存原始用量文字。读取在本机完成，不登录账号、不调用账号 API、不读取密码、Cookie、令牌或剪贴板。需要清除时在倒计时窗口点「清空倒计时」再「应用」。正常计时无需联网，手动检查/下载更新会联系 GitHub。公开文件仍是未签名预览版。
