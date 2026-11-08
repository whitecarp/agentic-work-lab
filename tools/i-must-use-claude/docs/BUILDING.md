# Building from source

Use Windows 10/11 x64 with .NET Framework 4.8. In PowerShell at the repository root, compile without launching the apps or changing settings:

```powershell
./apps/whale-claude-helper/build.ps1 -OutputDirectory "$PWD/artifacts/helper"
./apps/claude-live-translator/build.ps1 -OutputDirectory "$PWD/artifacts/translator"
./apps/two-zone-clock/build.ps1 -OutputDirectory "$PWD/artifacts/clock"
./apps/whale-pack-helper/build.ps1 -OutputDirectory "$PWD/artifacts/pack-helper"
```

The environment helper and clock outputs are standalone .NET applications. The translator command produces its UI and helper components, not the portable bundle.

## Pack Helper

Pack Helper 0.1.0 builds with the Windows .NET Framework C# 5 compiler and embeds its icon/artwork. Keep the produced EXE and its matching `.exe.config` together. If you rename the EXE to `WhalePackHelper.exe` for distribution, rename the configuration to `WhalePackHelper.exe.config` too. A convenience ZIP may contain this pair; it does not include FFmpeg.

Ordinary packaging and PNG/JPEG/BMP processing use built-in components. Video compression and WebP decoding need an existing local FFmpeg, found beside the app, on PATH or through its settings. Nothing is downloaded automatically. The default build runs isolated core/media tests; the optional real-media integration runner assumes the default `apps/whale-pack-helper/dist` location.

## Portable translator

Run its build.ps1 without OutputDirectory to place components at its project root, then use `packaging/build-portable.ps1`. The packager requires:

- Official Python 3.12.10 Windows x64 embeddable distribution under `runtime/python`, including its license.
- CTranslate2 4.8.4, SentencePiece 0.2.2, NumPy 2.5.4 and PyYAML 6.0.3 under `runtime/venv/Lib/site-packages`, with all package license metadata. These are the versions in this release payload.
- Official Visual Studio x64 VC redistributable directory; optionally pass `-VCRedistDirectory`. Its notice is retained.

```powershell
./apps/claude-live-translator/build.ps1
./apps/claude-live-translator/packaging/build-portable.ps1
```

For separate runtime and component build folders, pass `-RuntimeRoot` (a directory containing `runtime/`), `-ApplicationExe` (the compiled UI EXE, next to its three helper EXEs), and optionally `-BuildRoot` for a short temporary output path. The payload is allowlisted and includes `backend/glossary.py`; personal preferences, glossary files and translation history are never packaged.

Runtime binaries and models are not committed to Git. For normal use, download the prepared EXE from Releases; optional language packs are installed inside the translator.

## Isolated helper tests

```powershell
./apps/whale-claude-helper/tests/run-fixtures.ps1
```

This explicit runner tests synthetic cache/configuration data, injected network results and offscreen demo controls. It never starts the live helper entry point, reads real Claude configuration, sends diagnostic network requests, or shows application windows. Synthetic files and test receipts are retained for inspection; file moves use an injected fixture recycler, not the real Recycle Bin. Outputs stay under the helper's ignored `artifacts` folder.

CI also runs the clock's `-RunTests`, translator `tests/run-glossary-history.ps1`, mocked Google/offline/glossary Python tests, and `shared/tests/run-fixtures.ps1` for update selection and verified file saving. None opens Claude, translates real conversations, or applies environment settings. Independent Windows VM acceptance of the helper's modification/undo workflow remains pending. Compilation and fixtures are not end-to-end validation.

## Publishing an update

From the next release, Pack Helper 0.1.1 is a single EXE: attach `WhalePackHelper.exe` on its own (the `.exe.config` and paired ZIP are no longer needed) and add `"pack"` to the version comment, for example `<!-- whale-app-versions: {"helper":"0.7.0","pack":"0.1.1"} -->`. The shared updater now recognises the `pack` asset. Notes for the earlier v0.4.0-preview release follow.


For v0.4.0-preview, attach `WhaleEnvironmentHelper.exe`, `WhalePackHelper.exe`, `WhalePackHelper.exe.config` and `WhalePackHelper-0.1.0.zip` (the EXE/config pair). Translator 0.9.0 and Clock 0.3.0 remain linked to v0.3.0-preview. The Pack Helper is not part of the updater schema yet. Include a single-line machine-readable comment for the helper asset actually attached to this release:

```html
<!-- whale-app-versions: {"helper":"0.7.0"} -->
```

The updater reads public published releases, including previews, validates the asset's repository URL, size and GitHub SHA-256 digest, and never overwrites or launches a download. Missing digests or version metadata are not accepted. Preview builds pass `currentPreview=true` to `UpdateChecker.Show` (the default); stable builds must pass `false`. A stable release with the same app version can replace a preview. Keep the artifact versions, this metadata and each app's current version in sync.

## Language pack checksums

The official Argos catalog has no checksums, so the translator can pin known-good SHA-256 values itself. To pin a package, download it once on a trusted machine, run `Get-FileHash <file> -Algorithm SHA256`, and add the lowercase hash as `"sha256"` to that package's row in `apps/claude-live-translator/backend/argos_catalog.json`. Installs of a pinned package are rejected when the download does not match (`checksum_mismatch`). Pins are read only from the bundled catalog, so a refreshed online catalog cannot remove them. Unpinned packages install as before.

## Windows acceptance in CI

The `acceptance` job in `.github/workflows/build.yml` runs on GitHub's disposable Windows runner. It changes the real Windows time zone with the helper, undoes it, checks that a later manual change is not overwritten, then builds the four apps, starts each one and keeps a screenshot artifact (`launch-screenshots`). `tests/run-e2e-timezone.ps1` refuses to run unless `GITHUB_ACTIONS=true` and `WHALE_E2E_ALLOW_SYSTEM_CHANGES=1`; do not run it on a personal computer.

## Project page

`docs/index.html` is the bilingual project page. It is a single static file with no build step; it reuses the images in `docs/images/`, and `docs/.nojekyll` keeps GitHub Pages from processing the folder. To publish it, open the repository's **Settings → Pages**, choose **Deploy from a branch**, then select `main` and `/docs`. Preview it locally by opening the file in a browser.

## 中文

日常使用请到 Releases 下载 EXE；打包助手还需配套同名 `.exe.config`，也可下载已包含两者的 ZIP。源码构建要求 Windows 与 .NET Framework 4.8，上面的命令只编译、不启动程序。

翻译器普通构建只生成组件，自包含打包还需要上述 Python、离线引擎和官方 VC 运行库。模型由用户按需安装。CI 构建、检查语法并运行上述隔离测试，不操作真实 Claude 或修改系统；环境助手仍需独立 Windows 虚拟机验收。

项目主页是 `docs/index.html`（中英双语、无需构建）。在仓库 **Settings → Pages** 里选择 **Deploy from a branch**，分支选 `main`、目录选 `/docs` 即可发布。
