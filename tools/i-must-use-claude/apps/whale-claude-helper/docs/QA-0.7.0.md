# 0.7.0 integration validation

Validated on 2026-11-01 in a separate release worktree. Original project folders and the user's desktop apps were not modified.

## Scope

The independent 0.3.3 EXE and archive were read-only verified against their manifests: EXE `A49D612B3D7FD3EACD9267A06FFF917E9A10A89F0E971E4A41A2DF189A55887A`, archive `FCA0894789B557B9CE0886AA4C623F34C8373841E884F8157B848F3310232924`; all 19 recorded source entries and 11 archive entries matched. These are provenance evidence, **not hashes for 0.7.0**.

The integrated helper retains 0.6.0's maintenance, Codex proxy repair, blocking-process dialogs, update checker and orange visual resources. Only the network modules, WebRTC/score collection changes, corresponding tests, entry link and minimal clipping/icon corrections were integrated. The repository website is managed separately and was not edited by this app integration.

## Passed checks

| Suite | Passed |
|---|---:|
| Offline WebRTC JavaScript | 38 |
| Score parser/formatter and pre-cancelled host | 198 |
| DNS configuration transactions | 186 |
| Native DNS transport/state fixtures | 178 |
| Hidden network UI and retained entries/theme | 364 |
| Cache cleanup/restore fixtures | 56 |
| Configuration repair/restore fixtures | 109 |
| Diagnostic fixtures | 13 |
| Maintenance UI fixtures | 36 |
| Codex proxy settings transactions | 219 |
| Synthetic loopback proxy discovery | 104 |
| Injected process-close tests | 153 |
| Proxy UI/plan fixtures | 30 |
| **Total** | **1,684** |

The DNS UI suite adds 20 checks to the previous 344 for retained maintenance/proxy/close/update entries and the orange theme. The old version-title assertion was updated for 0.7.0. Symbolic-link creation was unavailable for one proxy settings fixture; hardlink/alias guards were tested, and this limitation is not counted as successful symbolic-link creation.

## CI temporary-path portability correction

The first release CI run failed at the accepted `http://localhost:80` fixture. This was reproduced locally by adding a `ci-runner` directory to the process-only `TEMP` and `TMP` paths: the redundant GUID in that fixture's name produced a 214-character transaction directory and a 263-character receipt staging path. Legacy .NET rejected the file operation before changing `.env`; proxy validation itself accepted the explicit port correctly.

The fixture root remains unique, while accepted-URI case directories now use stable indexes instead of another GUID. Failure messages include the safe error code and whether the file changed. Sixteen additional checks cover missing-port rejection, accepted port boundaries 1 and 65535, and preservation of explicit HTTP/SOCKS ports and original spelling. The 219-check proxy settings suite passed both with the ordinary temporary path and the longer path that reproduced the original failure. The 104 proxy-discovery and 30 proxy-UI checks were also rerun successfully; repeated runs are not counted twice above.

Only tests and this report changed for the CI correction. No production URI validation or file-safety behavior changed, and the previously built 0.7.0 executable remains unchanged: 1,923,584 bytes, SHA-256 `13338D0BBE62E0022B08275D383018EBC57E8A25403C468BD7965627867A76DA`. A new CI run is still required to confirm the fix on the hosted Windows runner.

## Evidence and limits

- Compiled using Windows Framework `csc.exe` with `/langversion:5` and embedded app resources.
- Hidden form renders cover English/Chinese at simulated 100% and 150% layout scales; no visible application was opened and Windows DPI settings were not changed.
- DNS and configuration operations use injected temporary fixtures. Process checks use a fake process platform; no user process was enumerated/closed by the test entry point.
- Proxy discovery fixtures use synthetic local loopback sockets, not upstream proxy services.
- No actual user configuration, credential, subscription, private backup or network resolver address is included in the release.
- No real DNS/RTC observation was repeated for the integrated EXE. The separate 0.3.3 live observation does not certify the final 0.7.0 binary or the user's browser/network.
- Independent Windows VM full-workflow and actual DPI acceptance remain pending. DNS observation covers the helper default HTTPS/proxy route, not all applications or browser-specific DoH.

Final executable size, version, SHA-256 and resource matches are recorded in the accompanying `verification.json` produced after the final build.
