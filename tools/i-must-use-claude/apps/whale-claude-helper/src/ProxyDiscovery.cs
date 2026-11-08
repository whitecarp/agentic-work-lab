using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace WhaleClaudeHelper {
    internal sealed class ProxyEndpoint {
        public string Host;
        public int Port;
        public string Source;
        public override string ToString() {
            return (Host != null && Host.IndexOf(':') >= 0 ? "[" + Host + "]" : Host) + ":" + Port.ToString(CultureInfo.InvariantCulture);
        }
    }

    internal sealed class ProxyProbeResult {
        public ProxyEndpoint Endpoint;
        public bool Http;
        public bool Socks5;
        public string Detail;
        public bool AuthenticationRequired;
    }

    // Metadata only: no command lines, executable contents, subscriptions, or credentials.
    internal sealed class ProxyListenerMetadata {
        public string Host;
        public int Port;
        public string ProcessName;
    }

    internal static class ProxyDiscovery {
        private const int MaximumCandidates = 32;
        private const int MaximumListeners = 4096;
        private const int StepTimeoutMilliseconds = 1500;
        private const int MaximumHttpHeaderBytes = 8192;
        private static readonly HashSet<string> ProxyProcessNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "clash", "clash-meta", "clash-win64", "clash-windows-amd64", "mihomo", "mihomo-windows-amd64",
            "flclashcore", "clash-verge", "verge-mihomo",
            "sing-box", "singbox", "xray", "v2ray", "v2rayn", "v2rayw", "naive", "hiddify",
            "hysteria", "hysteria2", "trojan", "ss-local", "sslocal", "shadowsocks", "shadowsocksr",
            "privoxy", "polipo", "gost", "brook", "3proxy", "tor", "cntlm", "tinyproxy", "outline-client"
        };

        public static bool TryCreateEndpoint(string host, string port, out ProxyEndpoint endpoint) {
            endpoint = null;
            int number;
            foreach (char c in port ?? "") if (Char.IsControl(c)) return false;
            string text = (port ?? "").Trim();
            if (text.Length == 0 || text.Length > 5) return false;
            foreach (char c in text) if (c < '0' || c > '9') return false;
            return Int32.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out number) && TryCreateEndpoint(host, number, out endpoint);
        }

        public static bool TryCreateEndpoint(string host, int port, out ProxyEndpoint endpoint) {
            endpoint = null;
            if (port < 1 || port > 65535 || String.IsNullOrWhiteSpace(host)) return false;
            foreach (char c in host) if (Char.IsControl(c)) return false;
            string value = host.Trim();
            if (value.Length > 64) return false;
            if (value.StartsWith("[", StringComparison.Ordinal) && value.EndsWith("]", StringComparison.Ordinal)) value = value.Substring(1, value.Length - 2);
            if (String.Equals(value, "localhost", StringComparison.OrdinalIgnoreCase)) value = "127.0.0.1";
            if (value.IndexOfAny(new[] { '/', '\\', '@', '%', '?', '#', '[', ']' }) >= 0) return false;
            foreach (char c in value) if (Char.IsWhiteSpace(c) || Char.IsControl(c)) return false;
            IPAddress address;
            if (!IPAddress.TryParse(value, out address)) return false;
            // Do not accept the legacy integer, octal, or abbreviated IPv4 forms accepted by IPAddress.
            if (address.AddressFamily == AddressFamily.InterNetwork && !IsDottedDecimal(value)) return false;
            if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
            if (!IPAddress.IsLoopback(address)) return false;
            endpoint = new ProxyEndpoint { Host = address.ToString(), Port = port, Source = "Manual local endpoint" };
            return true;
        }

        public static bool TryParseEndpoint(string text, out ProxyEndpoint endpoint) {
            endpoint = null;
            foreach (char c in text ?? "") if (Char.IsControl(c)) return false;
            string value = (text ?? "").Trim();
            if (value.Length == 0 || value.Length > 80) return false;
            string host, port;
            if (value[0] == '[') {
                int end = value.IndexOf(']');
                if (end < 0 || end + 1 >= value.Length || value[end + 1] != ':') return false;
                host = value.Substring(0, end + 1);
                port = value.Substring(end + 2);
            } else {
                int colon = value.IndexOf(':');
                if (colon < 1 || colon != value.LastIndexOf(':')) return false;
                host = value.Substring(0, colon);
                port = value.Substring(colon + 1);
            }
            return TryCreateEndpoint(host, port, out endpoint);
        }

        private static bool IsDottedDecimal(string value) {
            string[] parts = value.Split('.');
            if (parts.Length != 4) return false;
            foreach (string part in parts) {
                if (part.Length < 1 || part.Length > 3 || (part.Length > 1 && part[0] == '0')) return false;
                foreach (char c in part) if (c < '0' || c > '9') return false;
                int number;
                if (!Int32.TryParse(part, out number) || number > 255) return false;
            }
            return true;
        }

        // Discovery never opens a socket. Call ProbeAsync only after an explicit UI action.
        public static List<ProxyEndpoint> Discover() {
            string configured = null;
            bool enabled = false;
            try {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings", false)) {
                    if (key != null) {
                        object enable = key.GetValue("ProxyEnable", 0);
                        enabled = enable is int && (int)enable != 0;
                        configured = key.GetValue("ProxyServer", "") as string;
                    }
                }
            } catch (System.Security.SecurityException) { }
              catch (UnauthorizedAccessException) { }
              catch (IOException) { }
            return DiscoverFromMetadata(configured, enabled, ReadProxyListeners());
        }

        internal static List<ProxyEndpoint> DiscoverFromMetadata(string systemProxy, bool systemProxyEnabled, IEnumerable<ProxyListenerMetadata> listeners) {
            var result = new List<ProxyEndpoint>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (systemProxyEnabled && !String.IsNullOrEmpty(systemProxy) && systemProxy.Length <= 4096) {
                foreach (string entry in systemProxy.Split(';')) {
                    string value = entry.Trim();
                    int equals = value.IndexOf('=');
                    if (equals >= 0) {
                        string scheme = value.Substring(0, equals).Trim();
                        if (!String.Equals(scheme, "http", StringComparison.OrdinalIgnoreCase) && !String.Equals(scheme, "https", StringComparison.OrdinalIgnoreCase) && !String.Equals(scheme, "socks", StringComparison.OrdinalIgnoreCase)) continue;
                        value = value.Substring(equals + 1).Trim();
                    }
                    ProxyEndpoint endpoint;
                    if (TryParseEndpoint(value, out endpoint)) AddCandidate(result, seen, endpoint, "Current-user system proxy");
                    if (result.Count >= MaximumCandidates) return result;
                }
            }
            int inspected = 0;
            if (listeners != null) foreach (ProxyListenerMetadata listener in listeners) {
                if (++inspected > 128 || result.Count >= MaximumCandidates) break;
                if (listener == null || !ProxyProcessNames.Contains(listener.ProcessName ?? "")) continue;
                string host = listener.Host;
                // A known proxy listening on all interfaces is reachable through loopback.
                if (host == "0.0.0.0") host = "127.0.0.1";
                else if (host == "::") host = "::1";
                ProxyEndpoint endpoint;
                if (TryCreateEndpoint(host, listener.Port, out endpoint)) AddCandidate(result, seen, endpoint, "Listening proxy process: " + listener.ProcessName);
            }
            return result;
        }

        private static void AddCandidate(List<ProxyEndpoint> result, HashSet<string> seen, ProxyEndpoint endpoint, string source) {
            if (result.Count >= MaximumCandidates || !seen.Add(endpoint.ToString())) return;
            endpoint.Source = source;
            result.Add(endpoint);
        }

        public static Task<ProxyProbeResult> ProbeAsync(ProxyEndpoint endpoint, CancellationToken token) {
            return ProbeAsync(endpoint, token, StepTimeoutMilliseconds);
        }

        // The shorter deadline is used only by isolated loopback fixtures.
        internal static async Task<ProxyProbeResult> ProbeAsync(ProxyEndpoint endpoint, CancellationToken token, int stepTimeoutMilliseconds) {
            token.ThrowIfCancellationRequested();
            ProxyEndpoint validated;
            if (endpoint == null || !TryCreateEndpoint(endpoint.Host, endpoint.Port, out validated)) throw new ArgumentException("A numeric loopback address or localhost and a port from 1 to 65535 are required.", "endpoint");
            if (stepTimeoutMilliseconds < 1 || stepTimeoutMilliseconds > StepTimeoutMilliseconds) throw new ArgumentOutOfRangeException("stepTimeoutMilliseconds");
            validated.Source = endpoint.Source;
            var httpTask = ProbeHttpAsync(validated, token, stepTimeoutMilliseconds);
            var socksTask = ProbeSocksAsync(validated, token, stepTimeoutMilliseconds);
            ProtocolResult[] protocols = await Task.WhenAll(httpTask, socksTask).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            return new ProxyProbeResult {
                Endpoint = validated, Http = protocols[0].Accepted, Socks5 = protocols[1].Accepted,
                AuthenticationRequired = protocols[0].AuthenticationRequired || protocols[1].AuthenticationRequired,
                Detail = "HTTP CONNECT: " + protocols[0].Detail + "; SOCKS5: " + protocols[1].Detail + ". Protocol checks do not verify Codex authentication or a full application response."
            };
        }

        private sealed class ProtocolResult {
            internal bool Accepted;
            internal bool AuthenticationRequired;
            internal string Detail;
        }

        private static async Task<ProtocolResult> ProbeHttpAsync(ProxyEndpoint endpoint, CancellationToken token, int timeout) {
            using (var client = NewClient(endpoint)) {
                try {
                    await Deadline(client.ConnectAsync(IPAddress.Parse(endpoint.Host), endpoint.Port), client, token, timeout).ConfigureAwait(false);
                    NetworkStream stream = client.GetStream();
                    byte[] request = Encoding.ASCII.GetBytes("CONNECT example.com:443 HTTP/1.1\r\nHost: example.com:443\r\n\r\n");
                    await Deadline(stream.WriteAsync(request, 0, request.Length, token), client, token, timeout).ConfigureAwait(false);
                    string header = await Deadline(ReadHttpHeaderAsync(stream, token), client, token, timeout).ConfigureAwait(false);
                    return ParseConnectResponse(header);
                } catch (OperationCanceledException) { throw; }
                  catch (TimeoutException) { return Status("timed out"); }
                  catch (SocketException) { return Status("connection unavailable"); }
                  catch (IOException) { return Status("no valid CONNECT response"); }
                  catch (ObjectDisposedException) { token.ThrowIfCancellationRequested(); return Status("connection closed"); }
            }
        }

        private static async Task<ProtocolResult> ProbeSocksAsync(ProxyEndpoint endpoint, CancellationToken token, int timeout) {
            using (var client = NewClient(endpoint)) {
                try {
                    await Deadline(client.ConnectAsync(IPAddress.Parse(endpoint.Host), endpoint.Port), client, token, timeout).ConfigureAwait(false);
                    NetworkStream stream = client.GetStream();
                    // Offer no authentication and username/password only to identify the requirement.
                    // No authentication request or credential is ever transmitted.
                    byte[] greeting = { 5, 2, 0, 2 };
                    await Deadline(stream.WriteAsync(greeting, 0, greeting.Length, token), client, token, timeout).ConfigureAwait(false);
                    byte[] answer = await Deadline(ReadExactlyAsync(stream, 2, token), client, token, timeout).ConfigureAwait(false);
                    if (answer[0] != 5) return Status("not a SOCKS5 greeting");
                    if (answer[1] == 0) return new ProtocolResult { Accepted = true, Detail = "no-auth greeting confirmed" };
                    if (answer[1] == 2) return new ProtocolResult { AuthenticationRequired = true, Detail = "authentication required (credentials were not sent)" };
                    if (answer[1] == 255) return Status("no supported authentication method");
                    return Status("invalid or unoffered authentication method");
                } catch (OperationCanceledException) { throw; }
                  catch (TimeoutException) { return Status("timed out"); }
                  catch (SocketException) { return Status("connection unavailable"); }
                  catch (IOException) { return Status("no valid SOCKS5 greeting"); }
                  catch (ObjectDisposedException) { token.ThrowIfCancellationRequested(); return Status("connection closed"); }
            }
        }

        private static TcpClient NewClient(ProxyEndpoint endpoint) {
            return new TcpClient(IPAddress.Parse(endpoint.Host).AddressFamily) { NoDelay = true };
        }

        private static ProtocolResult Status(string detail) { return new ProtocolResult { Detail = detail }; }

        private static async Task<byte[]> ReadExactlyAsync(NetworkStream stream, int count, CancellationToken token) {
            var buffer = new byte[count];
            int offset = 0;
            while (offset < count) {
                int read = await stream.ReadAsync(buffer, offset, count - offset, token).ConfigureAwait(false);
                if (read == 0) throw new IOException("end_of_stream");
                offset += read;
            }
            return buffer;
        }

        private static async Task<string> ReadHttpHeaderAsync(NetworkStream stream, CancellationToken token) {
            var buffer = new byte[MaximumHttpHeaderBytes];
            int count = 0;
            while (count < buffer.Length) {
                int read = await stream.ReadAsync(buffer, count, Math.Min(512, buffer.Length - count), token).ConfigureAwait(false);
                if (read == 0) throw new IOException("incomplete_http_header");
                int previous = count;
                count += read;
                if (buffer[0] != (byte)'H') throw new IOException("not_http");
                for (int i = Math.Max(0, previous - 3); i + 3 < count; i++) {
                    if (buffer[i] == 13 && buffer[i + 1] == 10 && buffer[i + 2] == 13 && buffer[i + 3] == 10) return Encoding.ASCII.GetString(buffer, 0, i + 4);
                }
            }
            throw new IOException("http_header_too_large");
        }

        private static ProtocolResult ParseConnectResponse(string header) {
            string[] lines = header.Split(new[] { "\r\n" }, StringSplitOptions.None);
            if (lines.Length < 3) return Status("invalid CONNECT response");
            string[] status = lines[0].Split(new[] { ' ' }, 3);
            if (status.Length < 2 || (status[0] != "HTTP/1.0" && status[0] != "HTTP/1.1") || status[1].Length != 3) return Status("invalid CONNECT response");
            if (status[1] == "407") return new ProtocolResult { AuthenticationRequired = true, Detail = "authentication required (credentials were not sent)" };
            if (status[1] != "200") return Status("CONNECT was not accepted");
            for (int i = 1; i < lines.Length && lines[i].Length > 0; i++) {
                int colon = lines[i].IndexOf(':');
                if (colon <= 0) return Status("invalid CONNECT headers");
                string name = lines[i].Substring(0, colon).Trim();
                string value = lines[i].Substring(colon + 1).Trim();
                // A normal HTTP 200 page with a body is not evidence of a CONNECT tunnel.
                if (String.Equals(name, "Transfer-Encoding", StringComparison.OrdinalIgnoreCase)) return Status("HTTP response did not establish a tunnel");
                if (String.Equals(name, "Content-Length", StringComparison.OrdinalIgnoreCase) && value != "0") return Status("HTTP response did not establish a tunnel");
            }
            return new ProtocolResult { Accepted = true, Detail = "tunnel accepted (200)" };
        }

        private static async Task Deadline(Task operation, TcpClient client, CancellationToken token, int timeout) {
            await Deadline(AsResult(operation), client, token, timeout).ConfigureAwait(false);
        }

        private static async Task<bool> AsResult(Task operation) {
            await operation.ConfigureAwait(false);
            return true;
        }

        private static async Task<T> Deadline<T>(Task<T> operation, TcpClient client, CancellationToken token, int timeout) {
            using (var timer = CancellationTokenSource.CreateLinkedTokenSource(token)) {
                Task delay = Task.Delay(timeout, timer.Token);
                Task completed = await Task.WhenAny(operation, delay).ConfigureAwait(false);
                if (completed != operation) {
                    client.Close();
                    ObserveFault(operation);
                    token.ThrowIfCancellationRequested();
                    throw new TimeoutException("proxy_step_timeout");
                }
                timer.Cancel();
                if (token.IsCancellationRequested) {
                    client.Close();
                    ObserveFault(operation);
                    token.ThrowIfCancellationRequested();
                }
                return await operation.ConfigureAwait(false);
            }
        }

        private static void ObserveFault(Task task) {
            task.ContinueWith(t => { var ignored = t.Exception; }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        private static List<ProxyListenerMetadata> ReadProxyListeners() {
            var result = new List<ProxyListenerMetadata>();
            var names = new Dictionary<int, string>();
            Process[] processes;
            try { processes = Process.GetProcesses(); } catch { return result; }
            for (int i = 0; i < processes.Length; i++) {
                using (Process process = processes[i]) {
                    if (i >= 512) continue;
                    try { if (ProxyProcessNames.Contains(process.ProcessName)) names[process.Id] = process.ProcessName; } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { }
                }
            }
            if (names.Count == 0) return result;
            ReadTcpListeners(2, names, result);
            ReadTcpListeners(23, names, result);
            return result;
        }

        private static void ReadTcpListeners(int family, Dictionary<int, string> names, List<ProxyListenerMetadata> result) {
            IntPtr table = IntPtr.Zero;
            try {
                int size = 0;
                uint first = GetExtendedTcpTable(IntPtr.Zero, ref size, true, family, 3, 0);
                if ((first != 122 && first != 0) || size < 4 || size > 1024 * 1024) return;
                table = Marshal.AllocHGlobal(size);
                int capacity = size;
                if (GetExtendedTcpTable(table, ref size, true, family, 3, 0) != 0 || size > capacity) return;
                int count = Marshal.ReadInt32(table);
                int rowSize = Marshal.SizeOf(family == 2 ? typeof(TcpRow4) : typeof(TcpRow6));
                if (count < 0 || count > MaximumListeners || 4L + (long)count * rowSize > size) return;
                for (int i = 0; i < count && result.Count < 128; i++) {
                    IntPtr rowPointer = IntPtr.Add(table, 4 + i * rowSize);
                    uint pid, port;
                    IPAddress address;
                    if (family == 2) {
                        TcpRow4 row = (TcpRow4)Marshal.PtrToStructure(rowPointer, typeof(TcpRow4));
                        pid = row.Pid; port = row.LocalPort; address = new IPAddress((long)row.LocalAddress);
                    } else {
                        TcpRow6 row = (TcpRow6)Marshal.PtrToStructure(rowPointer, typeof(TcpRow6));
                        pid = row.Pid; port = row.LocalPort; address = new IPAddress(row.LocalAddress, row.LocalScope);
                    }
                    string processName;
                    if (pid > Int32.MaxValue || !names.TryGetValue((int)pid, out processName)) continue;
                    if (!IPAddress.IsLoopback(address) && !address.Equals(IPAddress.Any) && !address.Equals(IPAddress.IPv6Any)) continue;
                    result.Add(new ProxyListenerMetadata { Host = address.ToString(), Port = (ushort)IPAddress.NetworkToHostOrder((short)(port & 65535)), ProcessName = processName });
                }
            } catch (DllNotFoundException) { }
              catch (EntryPointNotFoundException) { }
              catch (System.ComponentModel.Win32Exception) { }
            finally { if (table != IntPtr.Zero) Marshal.FreeHGlobal(table); }
        }

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedTcpTable(IntPtr table, ref int size, [MarshalAs(UnmanagedType.Bool)] bool order, int family, int tableClass, uint reserved);

        [StructLayout(LayoutKind.Sequential)]
        private struct TcpRow4 { internal uint State, LocalAddress, LocalPort, RemoteAddress, RemotePort, Pid; }

        [StructLayout(LayoutKind.Sequential)]
        private struct TcpRow6 {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] internal byte[] LocalAddress;
            internal uint LocalScope, LocalPort;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] internal byte[] RemoteAddress;
            internal uint RemoteScope, RemotePort, State, Pid;
        }
    }
}
