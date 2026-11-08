using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace WhaleClaudeHelper {
    internal enum DiagnosticStatus { Pass, Warn, Fail }
    internal sealed class DiagnosticCheck {
        public string Id, TitleZh, TitleEn, DetailZh, DetailEn;
        public DiagnosticStatus Status;
    }
    internal sealed class DiagnosticReport {
        public readonly List<DiagnosticCheck> Checks = new List<DiagnosticCheck>();
        public bool IsDemo, NetworkRequested;
        public DateTime CollectedUtc;
    }
    internal sealed class DiagnosticSnapshot {
        public bool CodeExecutableFound, DesktopExecutableFound, CodeConfigFound, DesktopConfigFound;
        public string[] ProxyVariableNames = new string[0];
    }
    internal enum ProbeOutcome { NotRun, Success, Failed, Timeout }
    internal enum ConnectionFailure { None, Dns, Https, Tls, Proxy }
    internal sealed class ConnectionProbeResult {
        public ProbeOutcome Dns = ProbeOutcome.NotRun, Https = ProbeOutcome.NotRun;
        public ConnectionFailure Failure;
        public int? HttpStatusCode;
    }
    internal interface IDiagnosticsSnapshotProvider { DiagnosticSnapshot Capture(); }
    internal interface IDiagnosticsConnectionProbe { Task<ConnectionProbeResult> ProbeAsync(string host, CancellationToken cancellation); }

    // Construction performs no environment access. Only the UI's explicit RunAsync action
    // reads existence/variable-presence flags; the connection option is separately opt-in.
    internal sealed class DiagnosticsService {
        private readonly IDiagnosticsSnapshotProvider snapshots;
        private readonly IDiagnosticsConnectionProbe transport;
        internal static readonly string[] OfficialHosts = { "claude.ai", "api.anthropic.com" };
        internal static readonly string[] ProxyVariables = { "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY", "NO_PROXY" };
        public DiagnosticsService() : this(new WindowsDiagnosticSnapshotProvider(), new OfficialConnectionProbe()) { }
        internal DiagnosticsService(IDiagnosticsSnapshotProvider snapshotProvider, IDiagnosticsConnectionProbe connectionProbe) {
            if (snapshotProvider == null || connectionProbe == null) throw new ArgumentNullException();
            snapshots = snapshotProvider; transport = connectionProbe;
        }
        public async Task<DiagnosticReport> RunAsync(bool includeNetwork, CancellationToken cancellation) {
            cancellation.ThrowIfCancellationRequested();
            var report = new DiagnosticReport { NetworkRequested = includeNetwork, CollectedUtc = DateTime.UtcNow };
            try {
                var snapshot = await Task.Run(() => snapshots.Capture(), cancellation).ConfigureAwait(false);
                cancellation.ThrowIfCancellationRequested();
                if (snapshot == null) throw new InvalidDataException();
                AddSnapshot(report, snapshot);
            } catch (OperationCanceledException) { throw; }
            catch {
                report.Checks.Add(Check("local-unavailable", DiagnosticStatus.Warn, "本机检查未完成", "Local inspection incomplete",
                    "无法完成只读存在性检查；未读取配置内容或更改任何设置。", "The read-only presence check could not finish. No configuration contents were read or settings changed."));
            }
            if (includeNetwork) {
                var tasks = OfficialHosts.Select(host => InspectConnection(host, cancellation)).ToArray();
                var results = await Task.WhenAll(tasks).ConfigureAwait(false);
                foreach (var checks in results) report.Checks.AddRange(checks);
            } else {
                report.Checks.Add(Check("network-not-requested", DiagnosticStatus.Warn, "连接检查未运行", "Connection check not run",
                    "仅检查本机安装与设置是否存在。点连接体检后才会访问固定官方主机。", "Only local installation/configuration presence was checked. Connection checks contact fixed official hosts only when requested."));
            }
            cancellation.ThrowIfCancellationRequested();
            report.Checks.Add(ScopeCheck());
            return report;
        }
        private static void AddSnapshot(DiagnosticReport report, DiagnosticSnapshot snapshot) {
            report.Checks.Add(Check("claude-code", snapshot.CodeExecutableFound ? DiagnosticStatus.Pass : DiagnosticStatus.Warn,
                "Claude Code 安装", "Claude Code installation",
                snapshot.CodeExecutableFound ? "常见安装位置存在启动文件；未执行文件，也未验证版本或签名。" : "常见位置未找到启动文件；自定义安装、WSL 或其他打包位置可能未覆盖。",
                snapshot.CodeExecutableFound ? "A launch file exists in a common installation location; it was not executed and its version/signature was not verified." : "No launch file found in the checked locations. Custom, WSL or other packaged installations may not be covered."));
            report.Checks.Add(Check("claude-desktop", snapshot.DesktopExecutableFound ? DiagnosticStatus.Pass : DiagnosticStatus.Warn,
                "Claude Desktop 安装", "Claude Desktop installation",
                snapshot.DesktopExecutableFound ? "常见位置存在启动文件或应用包目录；未启动应用，也未验证版本或签名。" : "常见位置未找到启动文件或应用包；可能使用了不同安装位置。",
                snapshot.DesktopExecutableFound ? "A launch file or app-package directory exists in a common location; no application was launched or version/signature verified." : "No launch file or app package found in the checked locations; a different installation location may be in use."));
            report.Checks.Add(Check("claude-code-config", snapshot.CodeConfigFound ? DiagnosticStatus.Pass : DiagnosticStatus.Warn,
                "Claude Code 配置路径", "Claude Code configuration paths",
                snapshot.CodeConfigFound ? "发现常见配置路径，仅检查存在性，没有读取文件内容。" : "尚未发现常见配置路径；首次运行前没有配置可能是正常情况。",
                snapshot.CodeConfigFound ? "A common configuration path exists. Only presence was checked; no file contents were read." : "No common configuration path found. This may be normal before first use."));
            report.Checks.Add(Check("claude-desktop-config", snapshot.DesktopConfigFound ? DiagnosticStatus.Pass : DiagnosticStatus.Warn,
                "Claude Desktop 配置路径", "Claude Desktop configuration paths",
                snapshot.DesktopConfigFound ? "发现常见配置或数据目录，仅检查存在性，没有读取文件内容。" : "尚未发现常见配置或数据目录；其他打包版本的位置可能不同。",
                snapshot.DesktopConfigFound ? "A common configuration or data directory exists. Only presence was checked; no file contents were read." : "No common configuration/data location found; other packaging variants may use different paths."));
            // Never trust arbitrary strings supplied by a provider as UI output.
            var names = (snapshot.ProxyVariableNames ?? new string[0]).Where(x => ProxyVariables.Contains(x, StringComparer.OrdinalIgnoreCase))
                .Select(x => x.ToUpperInvariant()).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            string joined = String.Join(", ", names);
            report.Checks.Add(Check("proxy-variables", names.Length > 0 ? DiagnosticStatus.Warn : DiagnosticStatus.Pass,
                "代理环境变量", "Proxy environment variables",
                names.Length > 0 ? "已设置：" + joined + "。仅显示变量名，不显示地址、凭据或值；已设置不一定代表异常。" : "未设置所检查的代理变量。这不排除系统代理、VPN、应用代理或网络过滤。",
                names.Length > 0 ? "Set: " + joined + ". Names only; no addresses, credentials or values are displayed. Presence alone is not a fault." : "No checked proxy variable is set. This does not rule out a system proxy, VPN, application proxy or network filtering."));
        }
        private async Task<List<DiagnosticCheck>> InspectConnection(string host, CancellationToken cancellation) {
            ConnectionProbeResult result;
            try { result = await transport.ProbeAsync(host, cancellation).ConfigureAwait(false); }
            catch (OperationCanceledException) { throw; }
            catch { result = new ConnectionProbeResult { Dns = ProbeOutcome.Failed, Failure = ConnectionFailure.Https }; }
            cancellation.ThrowIfCancellationRequested();
            if (result == null) result = new ConnectionProbeResult { Dns = ProbeOutcome.Failed };
            var checks = new List<DiagnosticCheck>();
            bool dnsOk = result.Dns == ProbeOutcome.Success;
            checks.Add(Check(host + "-dns", dnsOk ? DiagnosticStatus.Pass : DiagnosticStatus.Fail,
                host + " · DNS", host + " · DNS",
                dnsOk ? "域名解析成功；不记录或显示解析地址。" : (result.Dns == ProbeOutcome.Timeout ? "域名解析超时。未继续 HTTPS 检查。" : "无法确认域名解析。未继续 HTTPS 检查。"),
                dnsOk ? "Name resolution succeeded; resolved addresses are not recorded or displayed." : (result.Dns == ProbeOutcome.Timeout ? "Name resolution timed out. HTTPS was not attempted." : "Name resolution could not be confirmed. HTTPS was not attempted.")));
            DiagnosticStatus status = DiagnosticStatus.Fail;
            string zh, en;
            if (!dnsOk) { status = DiagnosticStatus.Warn; zh = "等待 DNS 成功后才能检查。"; en = "Not run because DNS did not succeed."; }
            else if (result.Https == ProbeOutcome.Timeout) { zh = "HTTPS 请求超时。不能据此判定账号或地区资格。"; en = "HTTPS request timed out. This does not establish account or region eligibility."; }
            else if (result.Https == ProbeOutcome.Success && result.HttpStatusCode >= 100 && result.HttpStatusCode <= 599) {
                int code = result.HttpStatusCode.Value;
                if (code >= 500) { zh = "收到 HTTP " + code + "：已连到远端，但远端或网关报告服务错误。"; en = "HTTP " + code + " received: the remote endpoint was reached, but it or a gateway reported a server error."; }
                else if (code == 401 || code == 403) { status = DiagnosticStatus.Warn; zh = "收到 HTTP " + code + "：HTTPS 可达，但请求被限制。不是网络超时，也不能证明已登录或符合地区要求。"; en = "HTTP " + code + " received: HTTPS is reachable, but this request is restricted. This is not a network timeout or proof of login/region eligibility."; }
                else if (code == 429) { status = DiagnosticStatus.Warn; zh = "收到 HTTP 429：HTTPS 可达，远端正在限流。"; en = "HTTP 429 received: HTTPS is reachable, but the remote endpoint is rate-limiting."; }
                else if ((code >= 200 && code < 400) || code == 404 || code == 405) { status = DiagnosticStatus.Pass; zh = "收到 HTTP " + code + "：HTTPS 可达。仅探测公开入口，没有登录、调用模型或跟随跳转。"; en = "HTTP " + code + " received: HTTPS is reachable. Only a public entry point was probed; no login, model call or redirect was followed."; }
                else { status = DiagnosticStatus.Warn; zh = "收到 HTTP " + code + "：远端有响应，但不能据此判断服务或账号可用。"; en = "HTTP " + code + " received: a response was returned, but service/account availability is not established."; }
            } else if (result.Failure == ConnectionFailure.Tls) { zh = "TLS 证书或安全连接校验失败；没有关闭证书验证。"; en = "TLS certificate or secure-channel validation failed; certificate validation was not disabled."; }
            else if (result.Failure == ConnectionFailure.Proxy) { zh = "代理连接未成功；未修改代理或使用存储凭据。"; en = "The proxy connection failed; no proxy settings were changed or stored credentials used."; }
            else { zh = "HTTPS 连接未完成；可能与网络、代理或远端有关。未修改设置。"; en = "HTTPS did not complete; the network, proxy or remote endpoint may be involved. No settings were changed."; }
            checks.Add(Check(host + "-https", status, host + " · HTTPS", host + " · HTTPS", zh, en));
            return checks;
        }
        internal static DiagnosticCheck Check(string id, DiagnosticStatus status, string titleZh, string titleEn, string detailZh, string detailEn) {
            return new DiagnosticCheck { Id = id, Status = status, TitleZh = titleZh, TitleEn = titleEn, DetailZh = detailZh, DetailEn = detailEn };
        }
        private static DiagnosticCheck ScopeCheck() {
            return Check("scope", DiagnosticStatus.Warn, "检查范围", "What this check establishes",
                "只读安装/配置存在性与可选连接检查，不验证登录、余额、模型权限或账号地区资格，也不修改网络与配置。", "Read-only installation/configuration presence and optional connectivity checks do not verify login, credit, model access or account-region eligibility, and do not change network or configuration settings.");
        }
        public static DiagnosticReport DemoReport() {
            var report = new DiagnosticReport { IsDemo = true, NetworkRequested = false, CollectedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
            AddSnapshot(report, new DiagnosticSnapshot { CodeExecutableFound = true, DesktopExecutableFound = true, CodeConfigFound = true, DesktopConfigFound = true, ProxyVariableNames = new[] { "HTTPS_PROXY" } });
            report.Checks.Insert(0, Check("demo", DiagnosticStatus.Warn, "演示数据", "Demonstration data", "以下是静态示例，没有读取电脑或发送网络请求。", "Static sample results; no computer data was read and no network request was made."));
            report.Checks.Add(ScopeCheck()); return report;
        }
    }

    internal sealed class WindowsDiagnosticSnapshotProvider : IDiagnosticsSnapshotProvider {
        public DiagnosticSnapshot Capture() {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string programs = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var snapshot = new DiagnosticSnapshot();
            snapshot.CodeExecutableFound = File.Exists(Path.Combine(home, ".local", "bin", "claude.exe")) || File.Exists(Path.Combine(roaming, "npm", "claude.cmd")) || File.Exists(Path.Combine(local, "Microsoft", "WinGet", "Links", "claude.exe"));
            snapshot.DesktopExecutableFound = File.Exists(Path.Combine(local, "AnthropicClaude", "claude.exe")) || File.Exists(Path.Combine(local, "Programs", "Claude", "Claude.exe")) || File.Exists(Path.Combine(local, "Claude", "Claude.exe")) || File.Exists(Path.Combine(programs, "Claude", "Claude.exe")) || HasStorePackage(local);
            snapshot.CodeConfigFound = File.Exists(Path.Combine(home, ".claude.json")) || Directory.Exists(Path.Combine(home, ".claude"));
            snapshot.DesktopConfigFound = Directory.Exists(Path.Combine(roaming, "Claude")) || Directory.Exists(Path.Combine(local, "Claude-Data"));
            // Only named proxy values are checked for presence and immediately discarded.
            // No auth/token variables or configuration-file contents are accessed.
            snapshot.ProxyVariableNames = DiagnosticsService.ProxyVariables.Where(name => !String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name))).ToArray();
            return snapshot;
        }
        private static bool HasStorePackage(string local) {
            string packages = Path.Combine(local, "Packages");
            if (!Directory.Exists(packages)) return false;
            try { return Directory.EnumerateDirectories(packages, "Claude_*").Take(1).Any() || Directory.EnumerateDirectories(packages, "AnthropicPBC.Claude_*").Take(1).Any(); }
            catch (IOException) { return false; } catch (UnauthorizedAccessException) { return false; }
        }
    }

    internal sealed class OfficialConnectionProbe : IDiagnosticsConnectionProbe {
        private const int DnsTimeoutMs = 4000, HttpsTimeoutMs = 5000;
        public async Task<ConnectionProbeResult> ProbeAsync(string host, CancellationToken cancellation) {
            if (!DiagnosticsService.OfficialHosts.Contains(host, StringComparer.Ordinal)) throw new ArgumentException("unsupported_host");
            cancellation.ThrowIfCancellationRequested();
            var result = new ConnectionProbeResult();
            try {
                var addresses = await Bound(Dns.GetHostAddressesAsync(host), DnsTimeoutMs, cancellation, null).ConfigureAwait(false);
                if (addresses == null || addresses.Length == 0) throw new SocketException();
                result.Dns = ProbeOutcome.Success;
            } catch (OperationCanceledException) { throw; }
            catch (TimeoutException) { result.Dns = ProbeOutcome.Timeout; result.Failure = ConnectionFailure.Dns; return result; }
            catch { result.Dns = ProbeOutcome.Failed; result.Failure = ConnectionFailure.Dns; return result; }
            try {
            cancellation.ThrowIfCancellationRequested();
            var request = (HttpWebRequest)WebRequest.Create(new Uri("https://" + host + "/"));
            request.Method = "HEAD"; request.AllowAutoRedirect = false; request.Timeout = HttpsTimeoutMs; request.ReadWriteTimeout = HttpsTimeoutMs;
            request.UserAgent = "WhaleEnvironmentHelper-Diagnostics"; request.UseDefaultCredentials = false; request.Credentials = null; request.PreAuthenticate = false;
            request.CookieContainer = null; request.KeepAlive = false;
            // Use a request-local system proxy object without inheriting stored credentials.
            // There are no global proxy/TLS mutations or certificate-validation callbacks.
            request.Proxy = WebRequest.GetSystemWebProxy(); if (request.Proxy != null) request.Proxy.Credentials = null;
                using (cancellation.Register(request.Abort)) {
                    using (var response = (HttpWebResponse)await Bound(request.GetResponseAsync(), HttpsTimeoutMs, cancellation, request.Abort).ConfigureAwait(false)) {
                        result.Https = ProbeOutcome.Success; result.HttpStatusCode = (int)response.StatusCode;
                    }
                }
            } catch (OperationCanceledException) { throw; }
            catch (TimeoutException) { result.Https = ProbeOutcome.Timeout; result.Failure = ConnectionFailure.Https; }
            catch (WebException error) {
                using (var response = error.Response as HttpWebResponse) {
                    cancellation.ThrowIfCancellationRequested();
                    if (response != null) { result.Https = ProbeOutcome.Success; result.HttpStatusCode = (int)response.StatusCode; }
                    else {
                        result.Https = error.Status == WebExceptionStatus.Timeout ? ProbeOutcome.Timeout : ProbeOutcome.Failed;
                        result.Failure = error.Status == WebExceptionStatus.TrustFailure || error.Status == WebExceptionStatus.SecureChannelFailure ? ConnectionFailure.Tls : error.Status == WebExceptionStatus.ProxyNameResolutionFailure ? ConnectionFailure.Proxy : ConnectionFailure.Https;
                    }
                }
            } catch { cancellation.ThrowIfCancellationRequested(); result.Https = ProbeOutcome.Failed; result.Failure = ConnectionFailure.Https; }
            return result;
        }
        private static async Task<T> Bound<T>(Task<T> operation, int milliseconds, CancellationToken cancellation, Action abort) {
            using (var delayCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellation)) {
                var delay = Task.Delay(milliseconds, delayCancellation.Token);
                var completed = await Task.WhenAny(operation, delay).ConfigureAwait(false);
                if (completed == operation) {
                    delayCancellation.Cancel();
                    T value = await operation.ConfigureAwait(false);
                    if (cancellation.IsCancellationRequested) {
                        var disposable = (object)value as IDisposable; if (disposable != null) disposable.Dispose();
                        cancellation.ThrowIfCancellationRequested();
                    }
                    return value;
                }
                if (abort != null) try { abort(); } catch { }
                // DNS cannot be cancelled in this framework; observe its eventual failure.
                // HTTPS responses that race with timeout are disposed rather than leaked.
                ObserveLateCompletion(operation);
                cancellation.ThrowIfCancellationRequested(); throw new TimeoutException();
            }
        }
        private static void ObserveLateCompletion<T>(Task<T> operation) {
            operation.ContinueWith(t => { if (t.IsFaulted) { var ignored = t.Exception; } else if (t.Status == TaskStatus.RanToCompletion) { var disposable = (object)t.Result as IDisposable; if (disposable != null) disposable.Dispose(); } }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }
    }
}
