# Releasing the toolbox

The toolbox tag and individual app versions are different. `.github/release-versions.json` is the release manifest for all four app versions. Update the corresponding source assembly versions as well; the validator rejects a mismatch rather than rewriting an EXE's metadata.

Every new release must contain **exactly these four uploaded attachments**, including apps whose versions have not changed:

| Manifest key | Release filename |
| --- | --- |
| `translator` | `WhaleTranslator.exe` |
| `clock` | `HomeTimeClock.exe` |
| `helper` | `WhaleEnvironmentHelper.exe` |
| `pack` | `WhalePackHelper.exe` |

No `.config`, ZIP, language pack, or checksum file is uploaded as a release attachment. Checksums belong in the release body. GitHub may also display its automatically generated source archives; those are not toolbox attachments. Pack Helper 0.1.1 initializes its runtime switches in code and needs no configuration sidecar. FFmpeg remains a local prerequisite for its video/WebP features.

## Repeatable GitHub Actions path

1. Review and commit the source and manifest. Run **Build Windows apps** (`build.yml`) successfully on that exact commit. A successful build of an earlier commit is insufficient.
2. Run **Build and verify release** (`release.yml`) on the same commit. Supply a new tag, choose `unsigned-preview` or `required` signing, and leave `publish` off for a reviewable draft.
3. The workflow builds all four EXEs from that source. The translator is freshly packaged with its runtime and backend, not a renamed old release binary. Python 3.12.10 x64 embeddable is downloaded from Python.org and checked against a fixed SHA-256. CPU dependencies use the exact versions in `backend/requirements.txt` from PyPI; missing versions cause failure.
4. If signing is required, the workflow uploads an Actions artifact to the configured SignPath project and waits for signed output. Missing credentials, approval, output, or valid signatures fail the job. There is **no unsigned fallback**. Signing is not currently configured; see [code signing](CODE_SIGNING.md).
5. Validation checks the four filenames, PE headers, FileVersion and ProductVersion, the manifest, release-body metadata, signatures, and the actual final-byte hashes. Signed files are hashed **after** signing.
6. A draft is created, its attachments are downloaded into a new directory, and their names, sizes, versions, signatures and hashes are checked again. With `publish: true`, only a verified draft is published. Otherwise review and publish that draft manually after successful verification.

The workflow refuses an existing release or tag and never uses `--clobber`. If a job fails after creating a draft, inspect the existing draft before retrying; do not assume that no remote change happened. A failed verification leaves the release as a draft. This workflow has to run on GitHub to establish CI/signing/publication success; a local syntax check does not prove those external steps succeeded.

Actions artifacts are build/signing inputs and validation receipts, not user-facing release downloads. The translator receipt records the payload inputs. No user settings, translation text, installed model packs, subscription files, or local credentials are used as build inputs.

## Validate a locally prepared release

Use a fresh staging directory containing only the four final EXEs. Keep the body and reports outside it. Prepare the body using the checked-in manifest, including one single-line marker:

```powershell
$versions = Get-Content .github/release-versions.json -Raw | ConvertFrom-Json
$metadata = $versions | ConvertTo-Json -Compress
$body = "Unsigned preview. These EXEs are not Authenticode-signed. Windows SmartScreen may warn about an unknown publisher or unfamiliar download. Do not disable Windows security protections.`n`n<!-- whale-app-versions: $metadata -->`n"
[IO.File]::WriteAllText('C:\release-work\body.md', $body, (New-Object Text.UTF8Encoding($false)))
```

Preserve the app explanations and release notes when adding the metadata to an existing body. Validate and generate the body checksum block:

```powershell
.\.github\scripts\validate-release.ps1 `
  -AssetDirectory C:\release-work\exes `
  -BodyPath C:\release-work\body.md `
  -Tag v0.4.1-preview `
  -SigningMode unsigned-preview `
  -OutputDirectory C:\release-work\validation `
  -WriteChecksumsToBody
```

This command reads the EXEs without running them. It writes `validation.json`, `SHA256SUMS.txt` and `SHA256SUMS.md` to the report directory, and inserts/replaces only the marked checksum block in the specified body. Hashes are lowercase SHA-256. Omit `-WriteChecksumsToBody` for read-only body validation. Add `-RequireBodyChecksums` when checking a downloaded draft or published release. Without that flag a missing checksum block is allowed for initial preparation, but an existing mismatched block always fails.

For signed releases use `-SigningMode required`; all four Authenticode signatures must validate. Unsigned mode requires all four files to be unsigned, an explicit `-preview` tag, and an unsigned/SmartScreen notice in the body. Mixed signed/unsigned attachments are rejected.

Create a draft using the reviewed body and the four **explicit** filenames, targeting the exact source commit. After upload:

- Read the draft's body, target commit, prerelease flag and asset list back from GitHub. Require exactly the four uploaded EXEs, each in `uploaded` state.
- Download all four to a new empty directory. Run the validator with the downloaded body and `-RequireBodyChecksums`, using the release manifest from the tagged source.
- Compare each downloaded hash and byte count against the local validation receipt. Only then publish. Read back the publication state and download URLs afterward.

Do not attach the report folder or add unreviewed files through a broad upload glob. Do not mark the release as signed, malware-reviewed, or SmartScreen-approved without actual evidence.

## 中文维护说明

每次发布都必须包含上表四个 EXE，版本以 `.github/release-versions.json` 为准，发布正文里的 `whale-app-versions` 四键必须齐全，并同时匹配每个 EXE 的 FileVersion 和 ProductVersion。只上传 EXE；校验值放在发布正文，语言包按需另行下载。

先完成同一提交的 CI，再运行发布工作流。默认生成草稿，上传后下载回验，确认文件名、大小、版本、签名和 SHA-256 都一致才能发布。未签名版本必须明确标为 preview，并保留 SmartScreen 提示。选了必须签名时，缺少配置、审批或有效签名就失败，绝不自动退回未签名发布。

本地也可运行上面的验证脚本。`-WriteChecksumsToBody` 只写入指定正文的校验值区块；验收下载文件时使用 `-RequireBodyChecksums`，并与上传前回执逐项比对。工作流或草稿失败后先检查 GitHub 现状，不要直接覆盖或重复上传。
