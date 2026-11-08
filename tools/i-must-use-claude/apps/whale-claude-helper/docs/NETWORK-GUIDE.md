# 网络检测与修复 / Network checks and repair

适用版本：大肥鱼环境助手 0.7.0。所有本地配置读取、联网检测、预览、应用和撤回都在用户点击后进行；网络页不自动改系统 DNS、不停止进程、不代替用户切换节点；主工具另有明确确认的「关闭占用程序」入口。

## 同一个页面完成什么

主窗口进入「网络与 DNS」。主要页把「检查网络」、本机 DNS 文件核对、DNS 实测进度和解析器表、可选 WebRTC、代理组选择、修复预览、备份应用和撤回放在一起。主检查在助手里完成，不启动浏览器；也可只刷新本机配置，不发送探测请求。

- **本机 DNS 检查**：只读检查 CuteCloud 偏好文件、生成配置文件及客户端/核心进程。生成文件是磁盘快照，不能证明核心内存里当前加载了什么。
- **原生 DNS 泄露观察**：每次生成全新随机会话，以随机域名请求触发解析，再从 Net.Coffee 取得精确匹配本次会话的解析器列表。结果直接显示在助手中，不打开外部网页。显示已测、部分、超时、服务异常、无观察结果或取消；不会把空列表当成通过。
- **可选 WebRTC 检测**：独立按钮会打开本次会话的本机浏览器页面，用户点击开始后向 Google STUN 探测。最长等待 8 秒；仅回传状态和候选数量，不回传原始 IP。专项检测不会顺带采集字体、时区或语言；主 DNS 检测不自动打开它。

DNS 测的是助手 HTTPS 请求路径：使用 .NET 默认代理配置，无应用覆盖时使用当前用户 Internet 选项。因此通过系统 HTTP 代理时，域名可能由代理端解析；使用 TUN 时由实际 TUN 规则决定路径。助手不绕过现有代理强制直连，也不把这项测试冒充所有系统 DNS、Chrome / Edge 独立 DoH 或所有应用的检测。

服务会收到探测请求，可能看到出口 IP 和观察到的解析器 IP；助手不上传本机配置、订阅或登录凭据。Net.Coffee 的公开网页协议可能变化，没有稳定 API 兼容承诺。变化或不可用时保留未知，不自动改用另一个未告知的服务，也不承诺其“零日志”。

## 怎么看结果

| 看到的结果 | 正确含义 |
| --- | --- |
| WebRTC 超时 | 在 8 秒内未完成有效收集；结果未知。 |
| STUN 探测失败 | 服务器请求或路径失败；不能单凭这个断言服务器宕机。 |
| 浏览器不支持 | API 不存在或明确返回不支持；不是 DNS 泄露结论。 |
| 有 IP 候选 | 探测获得候选，单凭数量不能确认泄露。 |
| 零个匹配候选 | 不证明网络安全、代理完整覆盖或账号可用。 |
| DNS 文件字段相符 | 只说明显示范围内的文件对照，必须看未核对项；不是核心内存或外部实测回执。 |
| DNS 实测已完成、有解析器 | 服务观察到本次探测的解析器；核对它们是否符合你的预期，不是全系统“无泄露”证明。 |
| DNS 部分结果 | 已保留观察到的解析器，但有未完成步骤；不能当成完整检测。 |
| DNS 无观察结果 | 没有可用观察证据；不是“未泄露”，也不能直接归因于使用了加密 DNS。 |
| DNS 超时、服务异常或取消 | 当前结论未知；后续可重新发起全新会话，不复用旧结果。 |
| 归属或组织未知 | 归属数据未返回或不可用；不将未知补成正常，也不依据国家直接判断。 |
| 覆写 DNS 已开启 | 设置意图已保存，不保证客户端已重新加载。 |
| 网页总评是绿色 | 仍要看解析器表、出口及归属。归属为空或未知，不能补成“正常”。 |

DNS、WebRTC UDP、网页 HTTPS 可能使用不同路径，必须分别验证。解析器所在国家本身不等于安全或不安全；重点是是否符合用户选择的解析路径和预期。助手的原生观察只代表当次助手的请求，不能推出整个系统或浏览器永久没有旁路。

## 本机代理端口

HTTP/mixed 端口与 DNS 监听端口用途不同。`7890` 只是常见示例，不是固定答案；`1053` 可能仅接受 DNS 查询，不能按“正在监听”就当成 HTTP 代理。使用客户端当前的 HTTP/mixed 配置并验证协议。Socks-only、HTTP-only、mixed 也不能混称。

## CuteCloud 的有保护修复

1. 点击检查，选择生成配置中实际存在的出站组。不会将某位用户的 `IpEqual` 组名套到所有人。
2. 点击预览，核对目标及改动。方案使用 Cloudflare、Google HTTPS DNS，并通过所选已有组路由；清空 Fallback，仅移除受支持、明确识别的国内 DNS 例外。内网、混合和未知策略保留，仍可能影响部分解析路径。
3. 正常退出 CuteCloud，包括托盘驻留及 FlClashCore。若仍在运行，助手拒绝应用和撤回；不会强制结束、修改运行中偏好或自行启动客户端。
4. 确认具体计划后，助手先保存完整原文件并读回核验，再原子修改有限 DNS 字段。预览后文件或出站组发生变化，必须重新预览。
5. 自行正常启动 CuteCloud。回到助手点击「检查网络」完成全新的 DNS 复测，检查解析器明细。需要重载时，优先正常停止/启动，不把“强制重启”当作普通保存按钮。
6. 旧检测不代表修复后的状态。国内网站及所需境外服务也应分别检查可访问性；如关心特定浏览器的独立 DoH，再针对该浏览器单独验证。若仍不符合预期，逐项排查 DoH、IPv6 或分流规则，而不是直接关闭一切。

引导 DNS（default-nameserver）与代理节点 DNS（proxy-server-nameserver）保留，避免节点域名解析依赖尚未建立的代理而循环。因此即使浏览测试正常，也不能声称整个系统不再向其他 DNS 发出任何查询。系统 DNS、IPv6、节点、账号、订阅与其他配置不在本工具这次改动范围内。

## 撤回与私密备份

备份位置：`%LOCALAPPDATA%\DafeiyuClaudeHelper\Backups\NetworkDns\`。

完整备份可能包含账号、订阅或其他凭据，只保留在本机并限制为当前用户访问；不要把它上传或发给别人。界面和回执不显示这些内容。撤回只恢复本工具实际改过的字段，保留后来无关的修改；同一字段已被再次编辑时拒绝覆盖。

其他代理客户端、未知 JSON/YAML 结构、链接路径和异常文件不自动套用；保留手工指导并报告不支持。拒绝或结果未知时不要连续点击重试写入。

## English quick reference

Use **Network & DNS** in the same application. **Check network** combines read-only CuteCloud file inspection with a native DNS observation test. Progress and resolver rows stay in the assistant; no browser is launched. Repair preview, backup/apply, undo and a fresh retest are in the same window. WebRTC has a separate optional button that explicitly opens a local browser page.

The DNS test contacts Net.Coffee using a fresh random session, then requires the exact same session in the returned result. Empty, failed, partial and cancelled tests never become a clean bill of health. Locations are observations, not safety labels. The service can see request exits and observed resolvers; no local configuration, subscriptions or credentials are uploaded. Its public-page protocol is not a guaranteed stable API.

The scope is the assistant's HTTPS-request route under the .NET default proxy configuration. It does not measure every Windows application or a browser's independent DNS-over-HTTPS configuration. A successful file edit is also not a passed leakage test.

WebRTC waits up to eight seconds. Timeouts, STUN failures, and unsupported browsers remain unknown. Candidate counts are not proof of a leak; zero candidates are not proof of safety. No raw candidate addresses are returned. The dedicated probe does not collect fonts, time zone, or language.

For CuteCloud, choose an existing group, review changes, then exit the client and core normally. The helper verifies a private full backup before editing only approved DNS fields. Changed inputs invalidate the preview. It never kills or starts processes. Start the client yourself, then use Check network in the helper for a new test. Earlier results are not post-repair evidence.

Bootstrap and proxy-node DNS remain unchanged to avoid dependency loops. Internal and unknown policies are preserved. Undo restores only tool-owned fields and refuses conflicts. The full backup may contain credentials: keep it private and local. Other clients and unknown formats use guidance, not guessed edits.

## 参考

- [Mihomo DNS 配置文档](https://wiki.metacubex.one/config/dns/)：nameserver、nameserver-policy、fallback、代理节点解析及指定出站。
- [W3C WebRTC](https://www.w3.org/TR/webrtc/)：候选收集及浏览器 API。
- [Net.Coffee DNS 测试](https://ip.net.coffee/dns/)：原生 DNS 观察所用服务及公开网页协议；不是官方账号资格认证。
- [Microsoft DefaultWebProxy](https://learn.microsoft.com/en-us/dotnet/api/system.net.webrequest.defaultwebproxy?view=netframework-4.8.1)：默认请求代理的来源与范围。

## 合并版范围 / Integrated release scope

0.7.0 保留主工具的「关闭占用程序」入口。网络页本身不请求退出或强停代理；DNS 应用/撤回仍要求用户先正常退出 CuteCloud 及其核心。

Version 0.7.0 keeps Close blocking apps in the main helper. The network page does not close or terminate proxy processes; DNS apply/undo still requires CuteCloud and its core to be exited normally first.
