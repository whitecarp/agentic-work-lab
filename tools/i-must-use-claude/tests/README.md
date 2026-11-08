# Disposable Windows acceptance

The `acceptance` job runs only after the build and isolated unit tests. Real
operations require **both** `GITHUB_ACTIONS=true` and
`WHALE_E2E_ALLOW_SYSTEM_CHANGES=1`; scripts and native test entry points also
require `RUNNER_ENVIRONMENT=github-hosted` and `RUNNER_OS=Windows`.
Do not set these variables to bypass the guard on a personal computer.

The job records observed results, not an assumed end-to-end pass:

| Check | Applied scope | Undo verification |
| --- | --- | --- |
| Time zone | Real runner time zone, including DST flag | Service undo, conflict refusal, then exact original setting restored in `finally` |
| Browser language | Newly generated Chrome and Edge profile files under `RUNNER_TEMP` | Saved locale fields, unrelated JSON and the other browser checked; exact file hashes after service restore |
| Personal font | A copy of Windows Arial in a fresh temporary font folder with one random test-owned HKCU registration | Real GDI / registry / Recycle Bin removal and service restore; original hash and registration checked, then test setup removed in `finally` |
| Cache | Two generated allowlisted cache trees; one persistent sentinel | Real Windows Recycle Bin, service backup restore and original hash checks |
| CuteCloud DNS | Generated preferences and YAML, no running proxy | Preview, saved resolvers/policies, every original preference value restored, YAML hash unchanged (JSON member order may change) |
| Codex proxy | A generated `.codex/.env` | Four proxy values, unrelated setting and exact original hash after undo |

No existing account, browser profile, Claude history, real CuteCloud config,
actual Codex settings or network probe is used. Browser checks verify preference
file transactions; they do not prove a website language or score changed. Font
tests never remove a pre-existing installed font. No runtime or language-model
download is part of these checks.

`windows-acceptance-receipts` contains `timezone.json` and `workflows.json`.
Each records assertions and failure/restore state. Restore failure fails CI.
`launch-screenshots` contains four PNGs and `launch-results.json` with executable
paths, PIDs, HWNDs, real window titles, image dimensions/hashes and cleanup status.
The clock's visible WinForms HWND is located using `EnumWindows` by PID, even
though it has no taskbar button. A tray icon, missing HWND, blank image, failed
capture or process cleanup failure cannot pass. When the hosted desktop is
smaller than a window, `PrintWindow` captures the actual HWND in full; it never
substitutes a preview image. Launch screenshots do not prove translation,
publication or other network-dependent functionality.

Local development may parse the scripts and compile (without executing) the
acceptance harness using an explicit new output directory:

```powershell
./tests/run-windows-acceptance.ps1 -CompileOnly -OutputDirectory ./artifacts/acceptance-compile
```

The resulting native entry point remains guarded. Existing fixture runners can
be used locally; do not run real Windows acceptance or GUI screenshot scripts.
