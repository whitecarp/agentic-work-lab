# Translation components

## 0.8.0: optional language packages / Google online translation

The main application does not include translation models. New profiles default to Google online mode; existing preferences are retained. Users explicitly install the language pairs they want through the package manager, then select an installed source-to-target pair for offline translation. The offline worker uses only those installed packages, loads each model on demand, and has no download or Google fallback path.

Online mode uses `google_worker.py`, implemented with Python standard-library HTTPS requests. Selected text is sent to Google. No API key is stored or required. The unauthenticated Google Translate web endpoint has no service guarantee and may change or be rate-limited. This is not Gemini or the authenticated Google Cloud Translation API. Switching modes does not automatically send previously selected text. No automatic cross-mode fallback is used.

## Runtime components and optional model packages

- Engine: CTranslate2 4.8.4 (MIT), SentencePiece 0.2.2 (Apache-2.0).
- The main executable contains no Argos model weights, model archives, or preinstalled language packs. `worker.py` is legacy development code and is not distributed.
- Official catalog: https://raw.githubusercontent.com/argosopentech/argospm-index/main/index.json (repository: https://github.com/argosopentech/argospm-index). The bundled `argos_catalog.json` is a small offline snapshot retrieved on 2026-11-01. Opening the list never refreshes it automatically.
- The official catalog currently links HTTPS packages on `argos-net.com`; legacy official `data.argosopentech.com/argospm/v1/` URLs are also accepted. Each redirect is checked. Only explicit `--install`, `--reinstall`, and `--refresh` operations can use the network. `--import` accepts a local package without network access.
- Supported packages use CTranslate2 model data and a SentencePiece tokenizer. The official catalog does not distinguish BPE packages; installation checks actual compatibility and rejects unsupported packages before publishing them as installed. No automatic pivot language or source-language guess is used for explicit offline pairs.
- Each installed package retains its original metadata, README, and license/notice files. Licenses vary by package; consult those files. For example, the English/Chinese 1.9 packages' READMEs identify the OPUS-MT authors Jörg Tiedemann and Santhosh Thottingal and state CC-BY 4.0 for the original model.
- Model packages are stored in `%LOCALAPPDATA%\DafeiyuTranslator\packages`, independently of executable version caches. `DAFEIYU_DATA_DIR` provides an explicit data-directory override for isolated tests. The manager records the downloaded archive's SHA-256 for local integrity/audit, not as a claim of a publisher signature: the official catalog currently supplies no trusted SHA-256 field.
- Downloads and extraction are bounded, metadata must match the selected direction/version, and only verified packages are promoted into the installed list. Explicit reinstalls keep the old package under `.backups`; cancellation removes staging files. After forced termination, a later package operation recovers an interrupted replacement and cleans dead-process staging directories.
- NumPy and PyYAML runtime dependencies and their license metadata are included in the portable payload.

## Bundled runtime

Python 3.12.10 official Windows embeddable package (PSF License): https://www.python.org/downloads/release/python-31210/

Runtime: `runtime/python/`. The isolated Python path includes the bundled subset of `runtime/venv/Lib/site-packages`; no global Python configuration is changed. Google translation requires only the standard library. Python's license and third-party notices are retained.

Windows x64 app-local Microsoft Visual C++ runtime DLLs are copied from the installed Visual Studio Build Tools redistributable directory, with its redistribution notice. The user does not need to install Python or Visual Studio. The desktop program uses the Windows .NET Framework.

The single-file launcher stores verified runtime components under `%LOCALAPPDATA%\DafeiyuTranslator\runtime`. It does not package user preferences, diagnostic records, account data, source text or translations. Local settings use `%LOCALAPPDATA%\DafeiyuTranslator\preferences.json`.
