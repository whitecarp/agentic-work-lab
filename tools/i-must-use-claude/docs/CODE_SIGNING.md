# Code signing and Windows warnings

**Current status: unsigned preview; SignPath Foundation application submitted, awaiting review.** The application form confirmed submission on 2026-11-02 UTC. Approval is still pending, no certificate has been issued to this project, and no production signing credentials are configured. The optional workflow integration is preparation, not evidence of signed downloads. Update this status only after the corresponding real event is verified.

Windows can warn about an unsigned or unfamiliar download. Authenticode identifies a publisher and detects changes after signing; it does not guarantee that SmartScreen reputation warnings disappear or that an application is harmless. Keep Windows security protections enabled. Check the release source and SHA-256, and inspect the digital signature when one is actually provided. Microsoft's [SmartScreen documentation](https://learn.microsoft.com/en-us/windows/security/operating-system-security/virus-and-threat-protection/microsoft-defender-smartscreen/) describes its reputation checks.

## Planned SignPath setup

The optional integration follows [SignPath's official GitHub instructions](https://docs.signpath.io/trusted-build-systems/github) and the [official action](https://github.com/SignPath/github-action-submit-signing-request). `release.yml` pins the official v3 action to commit `f6d04783b4569d051e0c80105fe66e82819d0092`.

Before enabling required signing, the maintainers must:

1. Await the submitted application's eligibility review and complete any additional requirements requested by SignPath. Approval is not assumed; review the [SignPath Foundation terms](https://signpath.org/terms.html).
2. Configure the approved organization, project, certificate, signing policy and authorized submitter/approver roles. Record the actual publisher and policy here after approval; do not invent an organization ID, certificate name or approval.
3. Install/authorize the SignPath GitHub App and link this repository to the project's trusted GitHub build system. Builds leading to the signing request run on GitHub-hosted runners.
4. Configure an artifact definition for the uploaded ZIP-shaped Actions artifact with the four EXEs at its root. Sign only this project's four outer EXEs. Do not relabel or sign upstream Python, CTranslate2, FFmpeg or other third-party binaries as this project's code.
5. Supply these GitHub settings through repository settings, not source files:

| Type | Name |
| --- | --- |
| Secret | `SIGNPATH_API_TOKEN` |
| Variable | `SIGNPATH_ORGANIZATION_ID` |
| Variable | `SIGNPATH_PROJECT_SLUG` |
| Variable | `SIGNPATH_SIGNING_POLICY_SLUG` |
| Optional variable | `SIGNPATH_ARTIFACT_CONFIGURATION_SLUG` |

6. Require the appropriate independent/manual approval in the signing policy and perform a real signed preview release. Verify all four signatures, the approved publisher identity, timestamp behavior and final checksums before describing the integration as operational. Add required SignPath attribution only once the service is actually provided.

`required` signing mode fails when these settings are absent, the service declines or times out, signed output is missing, or any final EXE lacks a valid Authenticode signature. The workflow does not catch those failures and publish unsigned files. `unsigned-preview` is a separate explicit choice, requires a preview tag, and clearly labels the release as unsigned.

## Actual detection reports

No malware false-positive report has been observed or submitted for this release. Do not submit files to WDSI solely to claim certification or to remove an ordinary unfamiliar-download warning. If Microsoft Defender actually misclassifies an exact release file, preserve its hash and the detection details, review the build, then submit that file through [Microsoft Security Intelligence](https://www.microsoft.com/en-us/wdsi/filesubmission). Retain the actual submission/result as evidence; submission alone is not approval or a guarantee against later warnings.

## 中文说明

当前发布仍是**未签名预览版**。SignPath Foundation 申请表已于 2026 年 11 月 2 日（UTC）确认提交，正在等待审核，尚未获批，也未配置生产签名凭据。仓库里的签名工作流只是预备实现，不表示下载文件已经签名。

签名可以验证发布者和签名后的文件完整性，但不能保证 SmartScreen 不再提示，也不能代替安全检查。不要关闭 Windows 安全防护。只有服务获批、实际签名并逐个核验四个 EXE 后，才能改写签名状态。

选择必须签名时，配置缺失、审批失败、超时或无有效签名都会停止发布，不自动退回未签名。未签名发布只能明确选择 preview。本轮没有已确认的 Defender 误报，也没有提交 WDSI；发生真实误报时才提交对应文件并保留结果。
