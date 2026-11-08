# Pack Helper 0.1.1 — single-EXE release checks

2026-11-01. Built from the merged 0.1.1 implementation plus the sidecar-free build/test fixes in this directory. The original project and all 0.1.0 release artifacts remain unchanged.

- Windows PowerShell 5.1 build with the Windows Framework compiler, `/langversion:5`, succeeded in a fresh isolated output folder.
- **4,713 core + 2,375 media + 14 runtime/update assertions = 7,102** passed without an EXE configuration sidecar. Tests use generated local fixtures only; they do not open the real application window, read personal settings, invoke FFmpeg or make network requests.
- The application and test entry points call the same runtime initialization before file access. Both binaries carry the product's long-path-aware Windows manifest. A real path longer than 260 characters passed file write/read and the production scanner. The old test build omitted that manifest; its failure exposed and fixed this acceptance gap.
- `build.ps1` no longer copies either product or test `.exe.config`. `tests/render-native.ps1` now includes the shared update checker when compiling and uses the same code initialization. The historical `app.config` source is not used by the 0.1.1 build.
- Reflection-only inspection of the product EXE confirmed version **0.1.1.0**, CLR metadata `v4.0.30319`, the `.NETFramework,Version=v4.8` target, framework-only assembly references, and embedded icon/whale resources. No product code or UI was executed by this inspection.
- Settings contains the shared **Check updates** action for app ID `pack`. Offline fixtures verify the single `WhalePackHelper.exe` asset mapping, app-specific version selection, preview metadata and SHA-256 requirement, and reject an untrusted URL, missing digest or configuration-file asset.
- Release download is one EXE. It still requires installed .NET Framework 4.8; local FFmpeg is optional for packing ordinary files and required for video compression/WebP processing. Manual update checking and optional downloads contact GitHub; file preparation and packing stay local.
- The local receipt is `artifacts/pack-helper-0.1.1/verification.json` at repository root. A real-window launch, live update download and real-media end-to-end acceptance are separate checks and are not claimed by this local no-UI report.

Local build EXE (not the published-release checksum): **250,880 bytes**. SHA-256:

```text
FAD876A0201D3EFA0C6F0FFC1F32780BF971122DE4D2FA182F4A9908A5C99C55
```

## 中文

0.1.1 在隔离目录使用 Windows PowerShell 5.1 和 C# 5 编译，通过 7,102 项检查。产品和测试均不生成 `.exe.config`；长路径开关在代码入口初始化，测试也使用产品的 Windows manifest，已实际验证超过 260 字符路径的读写及扫描。图标、鲸鱼插图、共享更新器均编入 EXE。这里只运行合成数据的无界面测试，不读取个人设置、不处理真实素材、不运行 FFmpeg、不联网；真实窗口启动和端到端媒体验收另行记录。发布为单个 EXE，仍需要系统已有 .NET Framework 4.8；仅手动检查更新和下载新版时访问 GitHub。

## Cloud Windows acceptance — source `5959687`

[Build and acceptance run 41398526505](https://github.com/whitecarp/I-Must-Use-Claude/actions/runs/41398526505) passed. Across the toolbox, the run records **49 workflow checks**, **11 time-zone checks**, and visible-window captures for **all four apps**. Pack Helper was built and launched without an `.exe.config` sidecar; its nonblank screenshot and process-cleanup receipt were retained by the workflow.

This is separate from the local 7,102-assertion, no-UI build report above. Apply/verify/undo checks and window launches ran on a disposable GitHub-hosted Windows VM with temporary fixtures, not on the user's PC. They do not establish a successful real Claude login, personal-environment repair, live update download or real-media end-to-end run. The evidence applies to `5959687`; later changes need their own passing run, and release hashes must be taken from the actual published binaries.

源码 `5959687` 的云端构建与验收已通过：工具箱合计 49 项流程检查、11 项时区检查，并保存四款软件的可见窗口截图。打包助手在没有 `.exe.config` 的情况下启动，保留非空截图和测试进程清理回执。该记录与上面的本地 7,102 项无界面测试分开；所有修改、撤回和窗口启动仅在 GitHub 临时 Windows 虚拟机进行，没有操作用户电脑、真实账号或私人素材，不声称在线更新下载或真实素材端到端已验证。后续变更仍需重新通过，发版哈希以实际发布的 EXE 为准。

## Final publication verification

[Release run 41398725441](https://github.com/whitecarp/I-Must-Use-Claude/actions/runs/41398725441) passed for `5959687`. All four uploaded EXEs, including Pack Helper, were downloaded back and matched their SHA-256 hashes before publication. The translator's separate portable-package acceptance passed fresh extraction, four bundled Python dependency imports with pinned-version/path checks, and verified cache reuse. These checks did not exercise real Google translation, offline-model downloads or Pack Helper's real-media end-to-end flow. All published EXEs remain **unsigned previews**; use the [published release checksums](https://github.com/whitecarp/I-Must-Use-Claude/releases/tag/v0.4.1-preview#sha-256), not the earlier local-build hash above.

最终发布运行已通过：四个 EXE 上传后重新下载并核对 SHA-256，翻译器另通过首次解包、内置 Python 四依赖固定版本／路径导入和缓存复用验收。未实测真实 Google 在线翻译、离线模型下载或打包助手真实素材端到端流程；所有成品仍为未签名预览版。发布校验值以 Release 正文为准，上方哈希仅记录早先本地构建。

## Historical 0.1.0 integration record (not the 0.1.1 delivery contract)

2026-11-01. Imported the app source, tests, embedded icon/whale artwork, manifest, configuration, build script and README into this repository. The original project remains intact. Historical output, private media fixtures and internal task notes were not imported.

- Built with the Windows Framework compiler using `/langversion:5`, into a fresh isolated output directory.
- Default tests passed: **4,713 core + 2,375 media assertions = 7,088**.
- The tests use generated local fixtures and temporary files. They do not open the application UI, read user settings, use live Claude, or invoke FFmpeg.
- The supplied desktop EXE and `.exe.config` were copied byte-for-byte and hash-checked. Version: **0.1.0.0**.
- A fresh build in the isolated release worktree again passed all **7,088** assertions. The required `src/HandoffForm.cs` is explicitly retained despite the repository's general handoff-file exclusion.
- All **22** product/build/test/resource files match the original project byte-for-byte. README and this report contain repository-specific integration notes.
- Release files use `WhalePackHelper.exe` plus `WhalePackHelper.exe.config`; both preserve the supplied bytes. The configuration contains no name-specific binding. Keeping the sidecar named after the executable preserves the CLR runtime configuration convention. This check is static; the real application UI was not launched.
- The paired ZIP has exactly three entries: the executable, its configuration, and a short bilingual README. Each uncompressed entry was hash-checked against the prepared file.
- Source screening found no personal user paths or generated media/log/config artifacts. Credential-shaped matches occur in the scanner's detection rules and deliberately synthetic test fixtures, not personal credentials.
- This integration did not repeat the original project's real-video or screenshot acceptance. Publication is handled separately; this document records build and asset validation, not an upload receipt.

Supplied executable SHA-256:

```text
AC2888F31F3EA1A3887FC983CFEA196EB664D059088FAFD0DF7970CFAAA29749
```

Configuration SHA-256:

```text
6B5AA0A506E1824E04474920B6678ADE3825BDB3542DB92180A98444D6D8E7E7
```

Prepared paired ZIP (`WhalePackHelper-0.1.0.zip`, 172,742 bytes) SHA-256:

```text
3767CAD881A0129B739FEAB69A5F9D4520D0DA51A8F18E4F53DAA8E2AADF168E
```

## 中文

源码已归入工具箱仓库，保留构建必需的资源、测试、配置和说明；原项目未修改。没有重复导入历史打包产物或私人素材。发布工作区重新构建通过 7,088 项检查，22 个产品、构建、测试及资源文件与原项目一致。发布使用英文 EXE 文件名，配套配置必须同名；两者保留原始字节，ZIP 内三个文件均已逐项核验。源码未发现个人路径或生成日志，密钥形状命中均来自筛查规则与合成夹具。本次没有重新运行真实视频集成或界面截图验收；本文是发布前核验记录，不代表已经上传。
