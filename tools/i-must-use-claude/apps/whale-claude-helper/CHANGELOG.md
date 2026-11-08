# Changelog

## 0.7.0 — 2026-11-01

- Integrates native DNS observation and reversible CuteCloud DNS configuration from the separate 0.3.3 branch into the existing toolbox helper.
- Preserves the 0.6.0 maintenance, proxy discovery/Codex `.env` repair, process-close workflow, updater and orange app identity. Adds Network & DNS to the existing entry links without redesigning the app.
- WebRTC gains the eight-second collection budget and explicit timeout, failed STUN probe and unsupported states. Unknown results never become zero-score successes. Cancelled tasks ignore late results.
- Uses the existing app icon for the network window; shortens the main hint and corrects the language button's scaled position to avoid clipping.
- Validation: 1,668 isolated checks; hidden bilingual 100%/150% simulated layout renders. No user configuration changes, real process termination or real DNS/RTC probe was performed for this integrated build. Final binary/hash evidence is regenerated for 0.7.0; the independent 0.3.3 hash is not reused.

中文：保留工具箱 0.6.0 已有全部入口、橙色主题和独立图标，增量合并独立 0.3.3 分支的原生 DNS 检测、CuteCloud 预览/私密备份/确认应用/撤回及 WebRTC 状态修复。修正提示和语言按钮边界，未重新设计前端。共 1,668 项隔离检查通过；本版未对真实用户配置、进程或网络进行变更/实测，离屏截图不等同真实 Windows DPI 验收。
