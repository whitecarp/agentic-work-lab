using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace WhaleClaudeHelper {
    internal sealed class DnsLeakResolver {
        internal string Address = "", Country = "", Organization = "", Asn = "";
    }
    internal sealed class DnsLeakProgress {
        internal string Stage;
        internal int Completed, Total;
    }
    internal sealed class DnsLeakMeasurementResult {
        internal string Status = "no_evidence";
        internal DateTime StartedUtc, CollectedUtc;
        internal int AttemptedProbes, CompletedProbes, ProbeFailures, MetadataFailures;
        internal DnsLeakResolver[] Rows = new DnsLeakResolver[0];
        internal string Format(bool english) {
            string title;
            switch (Status) {
                case "measured": title = english ? "DNS resolver evidence collected" : "已取得 DNS 解析器观测"; break;
                case "partial": title = english ? "Partial DNS resolver evidence" : "已取得部分 DNS 解析器观测"; break;
                case "timeout": title = english ? "The DNS measurement timed out" : "DNS 检测已超时"; break;
                case "cancelled": title = english ? "The DNS measurement was cancelled" : "DNS 检测已取消"; break;
                case "service_error": title = english ? "The DNS observation service could not provide a valid result" : "DNS 观测服务未能提供有效结果"; break;
                case "network_error": title = english ? "The DNS observation service could not be reached" : "无法连接 DNS 观测服务"; break;
                default: title = english ? "No resolver evidence was received; the result is inconclusive" : "未收到解析器观测，无法判定"; break;
            }
            var output = new StringBuilder(title);
            output.AppendLine();
            output.AppendLine(english ? "Scope: this helper's HTTPS request path using the current default proxy/TUN route. It does not test a particular browser's own DoH settings or every application." : "范围：助手自身通过当前默认代理/TUN 路由发出的 HTTPS 请求；不等于已测试某个浏览器独立的 DoH 设置或全部应用。");
            output.AppendLine((english ? "Probe requests completed: " : "触发请求已结束：") + CompletedProbes + "/" + AttemptedProbes +
                (english ? "; transport failures: " : "；传输失败：") + ProbeFailures + (english ? "; observed resolvers: " : "；观测解析器：") + Rows.Length + ".");
            output.AppendLine(english ? "A completed pixel request is only a trigger attempt; the resolver rows are the observation evidence." : "像素请求结束只表示已尝试触发；下方解析器列表才是观测证据。");
            if (Rows.Length != 0) {
                output.AppendLine(english ? "Resolver IP | Country | Organization | ASN" : "解析器 IP | 国家/地区 | 服务商 | ASN");
                foreach (var row in Rows) output.AppendLine(row.Address + " | " + Known(row.Country, english) + " | " + Known(row.Organization, english) + " | " + Known(row.Asn, english));
            }
            if (MetadataFailures != 0) output.AppendLine(english ? "Some location/provider lookups were unavailable; their resolver IP evidence was retained." : "部分归属地或服务商查询不可用，对应解析器 IP 证据已保留。");
            output.AppendLine(english ? "Compare the observed resolvers with your intended DNS provider. An empty list, missing metadata, or a completed request is not proof that no DNS leaks exist." : "请核对这些解析器是否符合预期；空列表、缺失归属信息或请求完成都不能证明没有 DNS 泄露。");
            output.AppendLine(english ? "Provider: Net.Coffee public webpage protocol (not a guaranteed stable API). The service sees the random test domains, current network exit and resolver sources. No CuteCloud configuration, account, subscription or credentials were sent. This helper did not change any settings." : "来源：Net.Coffee 公开网页协议（不是有稳定性保证的 API）。服务方会看到随机测试域名、本次网络出口及解析器来源；本助手未发送 CuteCloud 配置、账户、订阅或凭据，也未修改设置。");
            output.Append((english ? "Collected UTC: " : "采集 UTC：") + CollectedUtc.ToString("yyyy-MM-dd HH:mm:ss'Z'", CultureInfo.InvariantCulture));
            return output.ToString();
        }
        private static string Known(string value, bool english) { return String.IsNullOrEmpty(value) ? (english ? "unknown" : "未知") : value; }
    }

    // The transport DTO is intentionally small so offline fixtures never need the
    // real network, account state, browser or CuteCloud installation.
    internal sealed class DnsLeakHttpReply {
        internal int StatusCode;
        internal string Body = "", Failure;
    }

    internal sealed class DnsLeakMeasurementService {
        internal const int ProbeCount = 5;
        internal const int RequestTimeoutMilliseconds = 6000;
        internal const int OverallTimeoutMilliseconds = 45000;
        internal const int MaximumResponseBytes = 128 * 1024;
        internal const int MaximumResolvers = 32;
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
        private readonly Func<Uri, int, CancellationToken, DnsLeakHttpReply> transport;
        private readonly Action<int, CancellationToken> delay;
        private readonly Func<DateTime> utcNow;
        private readonly Func<string> sessionFactory;

        // Construction has no filesystem, network or process side effects.
        internal DnsLeakMeasurementService() : this(SendPublicRequest, Delay, delegate { return DateTime.UtcNow; }, NewSession) { }
        internal DnsLeakMeasurementService(Func<Uri, int, CancellationToken, DnsLeakHttpReply> transport,
            Action<int, CancellationToken> delay, Func<DateTime> utcNow, Func<string> sessionFactory) {
            if (transport == null || delay == null || utcNow == null || sessionFactory == null) throw new ArgumentException("measurement_context_required");
            this.transport = transport; this.delay = delay; this.utcNow = utcNow; this.sessionFactory = sessionFactory;
        }
        internal Task<DnsLeakMeasurementResult> MeasureAsync(CancellationToken cancellationToken, IProgress<DnsLeakProgress> progress) {
            // Do not pass cancellationToken to Task.Run: even pre-cancellation returns
            // a safe result DTO rather than a cancelled Task or raw exception.
            return Task.Run(delegate {
                try { return Measure(cancellationToken, progress); }
                catch { return new DnsLeakMeasurementResult { Status = cancellationToken.IsCancellationRequested ? "cancelled" : "service_error", StartedUtc = DateTime.UtcNow, CollectedUtc = DateTime.UtcNow }; }
            });
        }

        private DnsLeakMeasurementResult Measure(CancellationToken userCancellation, IProgress<DnsLeakProgress> progress) {
            var result = new DnsLeakMeasurementResult { StartedUtc = utcNow() };
            var deadline = new Budget { DeadlineUtc = result.StartedUtc.AddMilliseconds(OverallTimeoutMilliseconds), Watch = Stopwatch.StartNew() };
            bool validObservation = false;
            string observationFailure = null;
            using (var lifetime = CancellationTokenSource.CreateLinkedTokenSource(userCancellation)) {
                lifetime.CancelAfter(OverallTimeoutMilliseconds);
                try {
                    CheckBudget(deadline, lifetime.Token);
                    string session = sessionFactory();
                    if (session == null || !Regex.IsMatch(session, @"\A[a-f0-9]{32}\z")) throw new MeasurementException("service_error");
                    for (int i = 1; i <= ProbeCount; i++) {
                        CheckBudget(deadline, lifetime.Token);
                        Report(progress, "probes", i - 1, ProbeCount);
                        result.AttemptedProbes++;
                        var reply = Request(new Uri("https://" + session + "-" + i.ToString(CultureInfo.InvariantCulture) + ".d.ip.net.coffee/pixel.gif"), deadline, lifetime.Token);
                        // The public webpage resolves both Image.onload and Image.onerror.
                        // 403/404 still trigger authoritative DNS; HTTP success is not a DNS verdict.
                        if (reply.Failure == null && reply.StatusCode >= 100 && reply.StatusCode <= 599) result.CompletedProbes++;
                        else result.ProbeFailures++;
                        CheckBudget(deadline, lifetime.Token);
                        Report(progress, "probes", i, ProbeCount);
                        if (i != ProbeCount) Pause(600, deadline, lifetime.Token);
                    }
                    Pause(2000, deadline, lifetime.Token);
                    for (int attempt = 1; attempt <= 3; attempt++) {
                        CheckBudget(deadline, lifetime.Token); Report(progress, "results", attempt - 1, 3);
                        var reply = Request(new Uri("https://ip.net.coffee/api/dns/result/" + session), deadline, lifetime.Token);
                        CheckBudget(deadline, lifetime.Token);
                        if (reply.Failure != null) observationFailure = PreferFailure(observationFailure, reply.Failure);
                        else if (reply.StatusCode != 200) observationFailure = PreferFailure(observationFailure, "service_error");
                        else {
                            try {
                                result.Rows = ParseObservation(reply.Body, session); validObservation = true; observationFailure = null;
                            } catch { observationFailure = PreferFailure(observationFailure, "service_error"); }
                        }
                        Report(progress, "results", attempt, 3);
                        if (validObservation && result.Rows.Length != 0) break;
                        // Rate limits and server failures are not retried aggressively.
                        if (reply.StatusCode == 429 || reply.StatusCode >= 500) break;
                        if (attempt != 3) Pause(2000, deadline, lifetime.Token);
                    }
                    if (result.Rows.Length != 0) {
                        for (int index = 0; index < result.Rows.Length; index++) {
                            CheckBudget(deadline, lifetime.Token); Report(progress, "metadata", index, result.Rows.Length);
                            var row = result.Rows[index];
                            var reply = Request(new Uri("https://ip.net.coffee/api/geoip/" + Uri.EscapeDataString(row.Address)), deadline, lifetime.Token);
                            CheckBudget(deadline, lifetime.Token);
                            if (reply.Failure == null && reply.StatusCode == 200) {
                                try { FillMetadata(row, reply.Body); } catch { result.MetadataFailures++; }
                            } else result.MetadataFailures++;
                            Report(progress, "metadata", index + 1, result.Rows.Length);
                            if (reply.StatusCode == 429) { result.MetadataFailures += result.Rows.Length - index - 1; break; }
                        }
                    }
                    if (result.Rows.Length != 0) result.Status = result.CompletedProbes == ProbeCount ? "measured" : "partial";
                    else if (observationFailure != null) result.Status = observationFailure;
                    else if (validObservation) result.Status = "no_evidence";
                    else result.Status = observationFailure ?? "service_error";
                } catch (OperationCanceledException) {
                    result.Status = userCancellation.IsCancellationRequested ? "cancelled" : "timeout";
                } catch (MeasurementException error) { result.Status = error.Code; }
                catch { result.Status = userCancellation.IsCancellationRequested ? "cancelled" : "service_error"; }
                result.CollectedUtc = utcNow();
            }
            return result;
        }

        private DnsLeakHttpReply Request(Uri uri, Budget deadline, CancellationToken token) {
            CheckBudget(deadline, token);
            int remaining = Math.Max(1, (int)Math.Min(RequestTimeoutMilliseconds, Remaining(deadline)));
            using (var attempt = CancellationTokenSource.CreateLinkedTokenSource(token)) {
                CancellationToken attemptToken = attempt.Token;
                // DNS/PAC native work may outlast HttpWebRequest.Timeout/Abort. Isolate it:
                // only this bounded wait may consume its DTO, and late work has no result/UI reference.
                Task<DnsLeakHttpReply> work = Task.Run(delegate { return transport(uri, remaining, attemptToken); });
                work.ContinueWith(delegate(Task<DnsLeakHttpReply> failed) { var observed = failed.Exception; }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                try {
                    if (!work.Wait(remaining, token)) { attempt.Cancel(); return new DnsLeakHttpReply { Failure = "timeout" }; }
                    DnsLeakHttpReply reply = work.GetAwaiter().GetResult();
                    if (reply == null) return new DnsLeakHttpReply { Failure = "network_error" };
                    if (reply.Failure != null && reply.Failure != "timeout" && reply.Failure != "network_error" && reply.Failure != "cancelled" && reply.Failure != "service_error") return new DnsLeakHttpReply { Failure = "network_error" };
                    if (reply.Body != null && (reply.Body.Length > MaximumResponseBytes || Utf8.GetByteCount(reply.Body) > MaximumResponseBytes)) return new DnsLeakHttpReply { Failure = "service_error" };
                    return reply;
                } catch (OperationCanceledException) { attempt.Cancel(); throw; }
                catch { attempt.Cancel(); return new DnsLeakHttpReply { Failure = "network_error" }; }
            }
        }
        private double Remaining(Budget budget) { return Math.Min((budget.DeadlineUtc - utcNow()).TotalMilliseconds, OverallTimeoutMilliseconds - budget.Watch.ElapsedMilliseconds); }
        private void CheckBudget(Budget deadline, CancellationToken token) {
            token.ThrowIfCancellationRequested(); if (Remaining(deadline) <= 0) throw new MeasurementException("timeout");
        }
        private void Pause(int milliseconds, Budget deadline, CancellationToken token) {
            CheckBudget(deadline, token);
            if (Remaining(deadline) < milliseconds) throw new MeasurementException("timeout");
            delay(milliseconds, token); CheckBudget(deadline, token);
        }
        private static void Report(IProgress<DnsLeakProgress> progress, string stage, int completed, int total) {
            if (progress == null) return;
            try { progress.Report(new DnsLeakProgress { Stage = stage, Completed = completed, Total = total }); } catch { /* A closed UI cannot expose transport exceptions or change results. */ }
        }
        private static string PreferFailure(string previous, string next) {
            if (next == "service_error" || previous == "service_error") return "service_error";
            if (next == "timeout" || previous == "timeout") return "timeout";
            return "network_error";
        }
        private static DnsLeakResolver[] ParseObservation(string body, string session) {
            var document = ParseObject(body); object tokenValue, addressesValue;
            if (!document.TryGetValue("token", out tokenValue) || !(tokenValue is string) || !String.Equals((string)tokenValue, session, StringComparison.Ordinal) ||
                !document.TryGetValue("dns_servers", out addressesValue)) throw new InvalidDataException();
            object[] addresses = addressesValue as object[];
            if (addresses == null || addresses.Length > MaximumResolvers) throw new InvalidDataException();
            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (object value in addresses) {
                string normalized = PublicAddress(value as string); if (normalized == null) throw new InvalidDataException(); unique.Add(normalized);
            }
            return unique.OrderBy(x => x, StringComparer.Ordinal).Select(x => new DnsLeakResolver { Address = x }).ToArray();
        }
        private static void FillMetadata(DnsLeakResolver row, string body) {
            var document = ParseObject(body);
            row.Country = ReadMetadata(document, "country"); row.Organization = ReadMetadata(document, "isp");
            // The observed public endpoint currently has no ASN field. Never infer one from an organization string.
            row.Asn = "";
        }
        private static string ReadMetadata(Dictionary<string, object> document, string key) {
            object value; if (!document.TryGetValue(key, out value) || !(value is string)) return "";
            string text = (string)value; if (text.Length > 160 || text.Any(c => Char.IsControl(c) || Char.GetUnicodeCategory(c) == UnicodeCategory.Format)) return "";
            return text.Trim();
        }
        private static Dictionary<string, object> ParseObject(string body) {
            if (body == null || body.Length > MaximumResponseBytes || Utf8.GetByteCount(body) > MaximumResponseBytes) throw new InvalidDataException();
            CheckJsonDepth(body);
            var serializer = new JavaScriptSerializer { MaxJsonLength = MaximumResponseBytes, RecursionLimit = 16 };
            var parsed = serializer.DeserializeObject(body) as Dictionary<string, object>;
            if (parsed == null) throw new InvalidDataException(); return parsed;
        }
        private static void CheckJsonDepth(string json) {
            var containers = new Stack<char>(); var keys = new Stack<HashSet<string>>();
            bool quoted = false, escaped = false; int stringStart = -1;
            for (int index = 0; index < json.Length; index++) {
                char c = json[index];
                if (escaped) { escaped = false; continue; }
                if (quoted) {
                    if (c == '\\') escaped = true;
                    else if (c == '"') {
                        quoted = false; int next = index + 1; while (next < json.Length && Char.IsWhiteSpace(json[next])) next++;
                        if (next < json.Length && json[next] == ':' && containers.Count != 0 && containers.Peek() == '{') {
                            string name = new JavaScriptSerializer().Deserialize<string>(json.Substring(stringStart, index - stringStart + 1));
                            if (!keys.Peek().Add(name)) throw new InvalidDataException();
                        }
                    }
                    continue;
                }
                if (c == '"') { quoted = true; stringStart = index; }
                else if (c == '{' || c == '[') { containers.Push(c); keys.Push(c == '{' ? new HashSet<string>(StringComparer.Ordinal) : null); if (containers.Count > 16) throw new InvalidDataException(); }
                else if (c == '}' || c == ']') { if (containers.Count == 0 || containers.Pop() != (c == '}' ? '{' : '[')) throw new InvalidDataException(); keys.Pop(); }
            }
            if (containers.Count != 0 || quoted || escaped) throw new InvalidDataException();
        }
        private static string PublicAddress(string text) {
            if (String.IsNullOrEmpty(text) || text.Length > 64 || text != text.Trim() || text.IndexOf('%') >= 0) return null;
            IPAddress address; if (!IPAddress.TryParse(text, out address)) return null;
            if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
            byte[] bytes = address.GetAddressBytes();
            if (address.AddressFamily == AddressFamily.InterNetwork) {
                // Reject alternate numeric forms, private/shared/link-local/reserved and documentation ranges.
                if (text.IndexOf(':') < 0 && (!Regex.IsMatch(text, @"\A(?:0|[1-9][0-9]{0,2})(?:\.(?:0|[1-9][0-9]{0,2})){3}\z") || text != address.ToString())) return null;
                if (bytes[0] == 0 || bytes[0] == 10 || bytes[0] == 127 || bytes[0] >= 224 ||
                    (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127) || (bytes[0] == 169 && bytes[1] == 254) ||
                    (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) || (bytes[0] == 192 && bytes[1] == 168) ||
                    (bytes[0] == 192 && bytes[1] == 0 && (bytes[2] == 0 || bytes[2] == 2)) ||
                    (bytes[0] == 198 && (bytes[1] == 18 || bytes[1] == 19 || (bytes[1] == 51 && bytes[2] == 100))) ||
                    (bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113)) return null;
            } else if (address.AddressFamily == AddressFamily.InterNetworkV6) {
                if ((bytes[0] & 0xe0) != 0x20 || (bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0d && bytes[3] == 0xb8)) return null;
            } else return null;
            return address.ToString();
        }

        private static string NewSession() {
            byte[] random = new byte[16]; using (var generator = RandomNumberGenerator.Create()) generator.GetBytes(random);
            return BitConverter.ToString(random).Replace("-", "").ToLowerInvariant();
        }
        private static void Delay(int milliseconds, CancellationToken token) {
            if (token.WaitHandle.WaitOne(milliseconds)) token.ThrowIfCancellationRequested();
        }
        private static DnsLeakHttpReply SendPublicRequest(Uri uri, int timeout, CancellationToken cancellationToken) {
            if (!AllowedUri(uri)) return new DnsLeakHttpReply { Failure = "service_error" };
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = "GET"; request.AllowAutoRedirect = false; request.Timeout = timeout; request.ReadWriteTimeout = Math.Min(timeout, 5000);
            request.UserAgent = "DafeiyuEnvironmentHelper-DnsCheck/1.0"; request.Accept = "application/json,image/*;q=0.5";
            request.Credentials = null; request.UseDefaultCredentials = false; request.PreAuthenticate = false;
            request.Proxy = new CredentialFreeProxy(WebRequest.DefaultWebProxy);
            request.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
            using (var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)) {
                lifetime.CancelAfter(timeout);
                using (lifetime.Token.Register(delegate { try { request.Abort(); } catch { } })) {
                    try {
                        using (var response = (HttpWebResponse)request.GetResponse()) {
                            return ReadResponse(response, uri.Host != "ip.net.coffee", lifetime.Token);
                        }
                    } catch (WebException error) {
                        try {
                            if (cancellationToken.IsCancellationRequested) return new DnsLeakHttpReply { Failure = "cancelled" };
                            if (lifetime.IsCancellationRequested || error.Status == WebExceptionStatus.Timeout) return new DnsLeakHttpReply { Failure = "timeout" };
                            var response = error.Response as HttpWebResponse;
                            if (response != null) return new DnsLeakHttpReply { StatusCode = (int)response.StatusCode };
                            return new DnsLeakHttpReply { Failure = "network_error" };
                        } finally { if (error.Response != null) error.Response.Dispose(); }
                    } catch (OperationCanceledException) { return new DnsLeakHttpReply { Failure = cancellationToken.IsCancellationRequested ? "cancelled" : "timeout" }; }
                    catch (InvalidDataException) { return new DnsLeakHttpReply { Failure = "service_error" }; }
                    catch { return new DnsLeakHttpReply { Failure = "network_error" }; }
                }
            }
        }
        private static DnsLeakHttpReply ReadResponse(HttpWebResponse response, bool triggerOnly, CancellationToken token) {
            var reply = new DnsLeakHttpReply { StatusCode = (int)response.StatusCode };
            if (triggerOnly || response.StatusCode != HttpStatusCode.OK) return reply;
            if (response.ContentLength > MaximumResponseBytes) throw new InvalidDataException();
            using (Stream stream = response.GetResponseStream()) using (var buffer = new MemoryStream()) {
                byte[] chunk = new byte[4096]; int count;
                while ((count = stream.Read(chunk, 0, chunk.Length)) != 0) {
                    token.ThrowIfCancellationRequested(); if (buffer.Length + count > MaximumResponseBytes) throw new InvalidDataException(); buffer.Write(chunk, 0, count);
                }
                reply.Body = Utf8.GetString(buffer.ToArray());
            }
            return reply;
        }
        private static bool AllowedUri(Uri uri) {
            if (uri == null || uri.Scheme != "https" || !uri.IsDefaultPort || uri.UserInfo != "" || uri.Fragment != "" || uri.Query != "") return false;
            if (Regex.IsMatch(uri.Host, @"\A[a-f0-9]{32}-[1-5]\.d\.ip\.net\.coffee\z")) return uri.AbsolutePath == "/pixel.gif";
            if (uri.Host != "ip.net.coffee") return false;
            if (Regex.IsMatch(uri.AbsolutePath, @"\A/api/dns/result/[a-f0-9]{32}\z")) return true;
            const string geo = "/api/geoip/";
            return uri.AbsolutePath.StartsWith(geo, StringComparison.Ordinal) && PublicAddress(Uri.UnescapeDataString(uri.AbsolutePath.Substring(geo.Length))) != null;
        }
        private sealed class CredentialFreeProxy : IWebProxy {
            private readonly IWebProxy proxy;
            internal CredentialFreeProxy(IWebProxy proxy) { this.proxy = proxy; }
            public ICredentials Credentials { get { return null; } set { } }
            public Uri GetProxy(Uri destination) {
                Uri selected = proxy == null ? destination : proxy.GetProxy(destination);
                if (selected != null && selected.UserInfo != "") throw new InvalidOperationException("proxy_credentials_not_supported");
                return selected;
            }
            public bool IsBypassed(Uri host) { return proxy == null || proxy.IsBypassed(host); }
        }
        private sealed class MeasurementException : Exception {
            internal readonly string Code; internal MeasurementException(string code) { Code = code; }
        }
        private sealed class Budget { internal DateTime DeadlineUtc; internal Stopwatch Watch; }
    }
}
