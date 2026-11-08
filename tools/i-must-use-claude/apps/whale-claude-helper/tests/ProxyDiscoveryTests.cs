using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WhaleClaudeHelper;

internal static class ProxyDiscoveryTests {
    private static int checks;
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
    private enum ServerMode { Http, Socks, Mixed, Authentication, WrongHttp, WrongBinary, Stalled, SlowHeader, IncompleteHeader, OversizeHeader, Fragmented }

    // Every fixture binds loopback and returns canned bytes. It never resolves a name,
    // connects upstream, reads real proxy settings, or changes registry/configuration.
    private sealed class Fixture : IDisposable {
        private readonly TcpListener listener;
        private readonly CancellationTokenSource stop = new CancellationTokenSource();
        private readonly List<TcpClient> clients = new List<TcpClient>();
        private readonly List<Task> workers = new List<Task>();
        private readonly Task accept;
        private readonly ServerMode mode;
        internal readonly ProxyEndpoint Endpoint;
        internal int Accepted, HttpRequests, SocksRequests, CredentialBytes, Closed;
        internal string LastHttpRequest;

        internal Fixture(ServerMode fixtureMode, bool ipv6 = false) {
            mode = fixtureMode;
            listener = new TcpListener(ipv6 ? IPAddress.IPv6Loopback : IPAddress.Loopback, 0);
            listener.Start();
            var local = (IPEndPoint)listener.LocalEndpoint;
            Endpoint = new ProxyEndpoint { Host = local.Address.ToString(), Port = local.Port, Source = "Synthetic loopback fixture" };
            accept = AcceptAsync();
        }

        private async Task AcceptAsync() {
            try {
                while (!stop.IsCancellationRequested) {
                    TcpClient client = await listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    lock (clients) clients.Add(client);
                    Interlocked.Increment(ref Accepted);
                    Task worker = ServeAsync(client);
                    lock (workers) workers.Add(worker);
                }
            } catch (ObjectDisposedException) { }
              catch (SocketException) { }
        }

        private async Task ServeAsync(TcpClient client) {
            using (client) {
                try {
                    NetworkStream stream = client.GetStream();
                    var first = new byte[1];
                    if (await stream.ReadAsync(first, 0, 1).ConfigureAwait(false) == 0) return;
                    if (mode == ServerMode.Stalled) { await DrainAsync(stream, false).ConfigureAwait(false); return; }
                    if (mode == ServerMode.WrongBinary) { await SendAsync(stream, new byte[] { 4, 0 }).ConfigureAwait(false); return; }
                    if (mode == ServerMode.WrongHttp) { await SendAsync(stream, Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 4\r\n\r\npage")).ConfigureAwait(false); return; }
                    if (first[0] == (byte)'C') {
                        string request = await ReadRequestAsync(stream, first[0]).ConfigureAwait(false);
                        LastHttpRequest = request;
                        Interlocked.Increment(ref HttpRequests);
                        if (mode == ServerMode.Socks) return;
                        if (mode == ServerMode.SlowHeader) {
                            foreach (byte b in Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n")) {
                                await SendAsync(stream, new[] { b }).ConfigureAwait(false);
                                await Task.Delay(45, stop.Token).ConfigureAwait(false);
                            }
                            return;
                        }
                        if (mode == ServerMode.IncompleteHeader) { await SendAsync(stream, Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n")).ConfigureAwait(false); return; }
                        if (mode == ServerMode.OversizeHeader) { await SendAsync(stream, Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nX-Large: " + new string('x', 9000) + "\r\n\r\n")).ConfigureAwait(false); return; }
                        string response = mode == ServerMode.Authentication
                            ? "HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Basic realm=PRIVATE_CHALLENGE_DO_NOT_LOG\r\n\r\n"
                            : "HTTP/1.1 200 Connection Established\r\nProxy-Agent: synthetic-fixture\r\n\r\n";
                        byte[] bytes = Encoding.ASCII.GetBytes(response);
                        if (mode == ServerMode.Fragmented) {
                            await stream.WriteAsync(bytes, 0, 10).ConfigureAwait(false);
                            await Task.Delay(20, stop.Token).ConfigureAwait(false);
                            await stream.WriteAsync(bytes, 10, bytes.Length - 10).ConfigureAwait(false);
                        } else await SendAsync(stream, bytes).ConfigureAwait(false);
                        await DrainAsync(stream, mode == ServerMode.Authentication).ConfigureAwait(false);
                    } else if (first[0] == 5) {
                        byte[] remaining = await ReadExactlyAsync(stream, 3).ConfigureAwait(false);
                        if (remaining[0] != 2 || remaining[1] != 0 || remaining[2] != 2) throw new IOException("Unexpected fixture SOCKS methods");
                        Interlocked.Increment(ref SocksRequests);
                        if (mode == ServerMode.Http || mode == ServerMode.SlowHeader || mode == ServerMode.IncompleteHeader || mode == ServerMode.OversizeHeader) return;
                        byte[] response = { 5, mode == ServerMode.Authentication ? (byte)2 : (byte)0 };
                        if (mode == ServerMode.Fragmented) {
                            await stream.WriteAsync(response, 0, 1).ConfigureAwait(false);
                            await Task.Delay(20, stop.Token).ConfigureAwait(false);
                            await stream.WriteAsync(response, 1, 1).ConfigureAwait(false);
                        } else await SendAsync(stream, response).ConfigureAwait(false);
                        await DrainAsync(stream, mode == ServerMode.Authentication).ConfigureAwait(false);
                    }
                } catch (IOException) { }
                  catch (SocketException) { }
                  catch (ObjectDisposedException) { }
                  catch (OperationCanceledException) { }
                finally { Interlocked.Increment(ref Closed); }
            }
        }

        private async Task DrainAsync(NetworkStream stream, bool countCredentials) {
            var buffer = new byte[256];
            int read;
            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0) if (countCredentials) Interlocked.Add(ref CredentialBytes, read);
        }

        internal async Task WaitForClosedAsync() {
            var watch = Stopwatch.StartNew();
            while (Volatile.Read(ref Closed) < 2 && watch.ElapsedMilliseconds < 1000) await Task.Delay(10).ConfigureAwait(false);
        }

        public void Dispose() {
            stop.Cancel();
            listener.Stop();
            lock (clients) foreach (TcpClient client in clients) client.Close();
            try { accept.Wait(1000); } catch (AggregateException) { }
            Task[] pending;
            lock (workers) pending = workers.ToArray();
            try { Task.WaitAll(pending, 1000); } catch (AggregateException) { }
            stop.Dispose();
        }
    }

    private static Task SendAsync(NetworkStream stream, byte[] bytes) { return stream.WriteAsync(bytes, 0, bytes.Length); }
    private static async Task<byte[]> ReadExactlyAsync(NetworkStream stream, int count) {
        var bytes = new byte[count];
        int at = 0;
        while (at < count) {
            int read = await stream.ReadAsync(bytes, at, count - at).ConfigureAwait(false);
            if (read == 0) throw new IOException("Fixture received truncated request");
            at += read;
        }
        return bytes;
    }
    private static async Task<string> ReadRequestAsync(NetworkStream stream, byte first) {
        var bytes = new List<byte> { first };
        while (bytes.Count < 1024) {
            byte[] next = await ReadExactlyAsync(stream, 1).ConfigureAwait(false);
            bytes.Add(next[0]);
            int n = bytes.Count;
            if (n >= 4 && bytes[n - 4] == 13 && bytes[n - 3] == 10 && bytes[n - 2] == 13 && bytes[n - 1] == 10) return Encoding.ASCII.GetString(bytes.ToArray());
        }
        throw new IOException("Fixture request too large");
    }

    private static void EndpointTests() {
        ProxyEndpoint endpoint;
        foreach (string text in new[] { "127.0.0.1:54321", "127.8.9.10:1", "localhost:65535", "[::1]:43127", "[0:0:0:0:0:0:0:1]:89", "[::ffff:127.0.0.1]:42137" }) Check(ProxyDiscovery.TryParseEndpoint(text, out endpoint), "accept local endpoint " + text);
        foreach (string text in new[] { "http://127.0.0.1:42", "socks5://127.0.0.1:42", "user:secret@127.0.0.1:42", "example.com:80", "192.168.1.1:80", "0.0.0.0:80", "[::]:80", "[2001:db8::1]:80", "127.0.0.1:0", "127.0.0.1:65536", "127.0.0.1:-1", "127.0.0.1:+1", "127.0.0.1:1.0", "127.0.0.1:1/path", "127.0.0.1:1?x=y", "127.0.0.1\r\n:80", "2130706433:80", "127.1:80", "0177.0.0.1:80", "[::1%1]:80", "::1:80", "127.0.0.1: 2 3", "localhost.:42" }) Check(!ProxyDiscovery.TryParseEndpoint(text, out endpoint), "reject unsafe endpoint " + text);
        Check(ProxyDiscovery.TryCreateEndpoint("::1", 45678, out endpoint) && endpoint.ToString() == "[::1]:45678", "manual IPv6 host and arbitrary port");
        Check(ProxyDiscovery.TryCreateEndpoint("LOCALHOST", " 43219 ", out endpoint) && endpoint.Host == "127.0.0.1", "localhost uses numeric loopback without DNS");
        Check(!ProxyDiscovery.TryCreateEndpoint("http://localhost", 42, out endpoint), "host field rejects URL");
    }

    private static void MetadataTests() {
        var metadata = new[] {
            new ProxyListenerMetadata { Host = "127.0.0.1", Port = 43219, ProcessName = "mihomo" },
            new ProxyListenerMetadata { Host = "::1", Port = 54321, ProcessName = "sing-box" },
            new ProxyListenerMetadata { Host = "0.0.0.0", Port = 32123, ProcessName = "privoxy" },
            new ProxyListenerMetadata { Host = "::", Port = 32124, ProcessName = "tor" },
            new ProxyListenerMetadata { Host = "127.0.0.1", Port = 65432, ProcessName = "unrelated-database" },
            new ProxyListenerMetadata { Host = "192.168.1.10", Port = 32125, ProcessName = "mihomo" },
            new ProxyListenerMetadata { Host = "127.0.0.1", Port = 0, ProcessName = "gost" },
            new ProxyListenerMetadata { Host = "127.0.0.1", Port = 41873, ProcessName = "FlClashCore" },
            new ProxyListenerMetadata { Host = "127.0.0.1", Port = 41874, ProcessName = "clash-verge" },
            new ProxyListenerMetadata { Host = "::1", Port = 41875, ProcessName = "verge-mihomo" }
        };
        var found = ProxyDiscovery.DiscoverFromMetadata("http=localhost:43219;https=[::1]:54321;socks=127.0.0.1:43220;ftp=127.0.0.1:43221;https=remote.example:443", true, metadata);
        Check(found.Count == 8 && found.Select(x => x.ToString()).Distinct().Count() == 8, "metadata merges protocol entries and deduplicates listeners");
        Check(found.Any(x => x.Port == 41873 && x.Source.EndsWith("FlClashCore", StringComparison.Ordinal)), "FlClashCore discovered case-insensitively on arbitrary port");
        Check(found.Any(x => x.Port == 41874 && x.Source.EndsWith("clash-verge", StringComparison.Ordinal)), "clash-verge listener metadata discovered");
        Check(found.Any(x => x.Port == 41875 && x.Host == "::1" && x.Source.EndsWith("verge-mihomo", StringComparison.Ordinal)), "verge-mihomo IPv6 core listener discovered");
        Check(found.Any(x => x.Host == "127.0.0.1" && x.Port == 32123) && found.Any(x => x.Host == "::1" && x.Port == 32124), "known wildcard listeners become loopback candidates");
        Check(found.All(x => x.Port != 7890 && x.Port != 65432 && x.Port != 32125 && x.Port != 43221), "no fixed ports, unrelated services, remote bindings or ftp");
        Check(ProxyDiscovery.DiscoverFromMetadata("127.0.0.1:44444", false, null).Count == 0, "disabled system proxy ignored");
        Check(ProxyDiscovery.DiscoverFromMetadata("http://user:SECRET@127.0.0.1:44444", true, null).Count == 0, "credential-bearing configured URL ignored");
        Check(ProxyDiscovery.DiscoverFromMetadata(new string('x', 4097), true, null).Count == 0, "oversized proxy setting ignored");
        var many = Enumerable.Range(10000, 100).Select(p => new ProxyListenerMetadata { Host = "127.0.0.1", Port = p, ProcessName = "gost" });
        Check(ProxyDiscovery.DiscoverFromMetadata(null, false, many).Count == 32, "candidate enumeration capped");
        Check(ProxyDiscovery.DiscoverFromMetadata(null, false, BoundedMetadata()).Count == 0, "metadata inspection capped before late candidate");
    }

    private static IEnumerable<ProxyListenerMetadata> BoundedMetadata() {
        for (int i = 0; i < 129; i++) yield return new ProxyListenerMetadata { Host = "127.0.0.1", Port = 12345, ProcessName = i == 128 ? "gost" : "unrelated" };
        throw new Exception("Enumeration should have stopped");
    }

    private static async Task ProbeTestsAsync() {
        foreach (ServerMode mode in new[] { ServerMode.Http, ServerMode.Socks, ServerMode.Mixed, ServerMode.Fragmented, ServerMode.Authentication, ServerMode.WrongHttp, ServerMode.WrongBinary, ServerMode.IncompleteHeader, ServerMode.OversizeHeader }) {
            using (var fixture = new Fixture(mode)) {
                Check(fixture.Endpoint.Port != 7890, "OS-selected arbitrary port");
                ProxyProbeResult result = await ProxyDiscovery.ProbeAsync(fixture.Endpoint, CancellationToken.None, 400).ConfigureAwait(false);
                bool expectHttp = mode == ServerMode.Http || mode == ServerMode.Mixed || mode == ServerMode.Fragmented;
                bool expectSocks = mode == ServerMode.Socks || mode == ServerMode.Mixed || mode == ServerMode.Fragmented;
                Check(result.Http == expectHttp && result.Socks5 == expectSocks, "protocol classification " + mode);
                Check(result.AuthenticationRequired == (mode == ServerMode.Authentication), "authentication classification " + mode);
                Check(!result.Detail.Contains("PRIVATE_CHALLENGE") && result.Detail.Contains("do not verify Codex authentication"), "fixed diagnostics do not expose server text " + mode);
                await fixture.WaitForClosedAsync().ConfigureAwait(false);
                Check(fixture.Accepted == 2 && fixture.Closed == 2, "exactly two bounded connections closed " + mode);
                if (expectHttp || mode == ServerMode.Authentication) Check(fixture.LastHttpRequest == "CONNECT example.com:443 HTTP/1.1\r\nHost: example.com:443\r\n\r\n", "explicit CONNECT request, no credentials or app payload " + mode);
                if (mode == ServerMode.Authentication) Check(fixture.CredentialBytes == 0, "authentication requirement never triggers credential transmission");
            }
        }
        using (var fixture = new Fixture(ServerMode.Stalled)) {
            var watch = Stopwatch.StartNew();
            ProxyProbeResult result = await ProxyDiscovery.ProbeAsync(fixture.Endpoint, CancellationToken.None).ConfigureAwait(false);
            Check(!result.Http && !result.Socks5 && result.Detail.Contains("timed out"), "stalled services time out");
            Check(watch.ElapsedMilliseconds < 2400, "production timeout remains bounded with parallel protocol checks");
            await fixture.WaitForClosedAsync().ConfigureAwait(false);
            Check(fixture.Closed == 2, "timeout closes both sockets");
        }
        using (var fixture = new Fixture(ServerMode.SlowHeader)) {
            var watch = Stopwatch.StartNew();
            ProxyProbeResult result = await ProxyDiscovery.ProbeAsync(fixture.Endpoint, CancellationToken.None, 180).ConfigureAwait(false);
            Check(!result.Http && watch.ElapsedMilliseconds < 850, "slow trickle cannot reset whole-header deadline");
        }
        int closedPort;
        var closedListener = new TcpListener(IPAddress.Loopback, 0);
        closedListener.Start(); closedPort = ((IPEndPoint)closedListener.LocalEndpoint).Port; closedListener.Stop();
        ProxyProbeResult closed = await ProxyDiscovery.ProbeAsync(new ProxyEndpoint { Host = "127.0.0.1", Port = closedPort }, CancellationToken.None, 250).ConfigureAwait(false);
        Check(!closed.Http && !closed.Socks5 && !closed.AuthenticationRequired, "closed arbitrary port is unavailable");
        using (var fixture = new Fixture(ServerMode.Stalled)) using (var cancellation = new CancellationTokenSource()) {
            Task<ProxyProbeResult> task = ProxyDiscovery.ProbeAsync(fixture.Endpoint, cancellation.Token);
            var acceptedWatch = Stopwatch.StartNew();
            while (fixture.Accepted < 2 && acceptedWatch.ElapsedMilliseconds < 1000) await Task.Delay(10).ConfigureAwait(false);
            Check(fixture.Accepted == 2, "cancellation fixture accepted both protocol sockets");
            var watch = Stopwatch.StartNew();
            cancellation.Cancel();
            bool cancelled = false;
            try { await task.ConfigureAwait(false); } catch (OperationCanceledException) { cancelled = true; }
            Check(cancelled && watch.ElapsedMilliseconds < 700, "in-flight cancellation is prompt and propagates");
            await fixture.WaitForClosedAsync().ConfigureAwait(false);
            Check(fixture.Closed == 2, "cancellation closes both sockets");
        }
        using (var fixture = new Fixture(ServerMode.Mixed)) using (var cancellation = new CancellationTokenSource()) {
            cancellation.Cancel();
            bool cancelled = false;
            try { await ProxyDiscovery.ProbeAsync(fixture.Endpoint, cancellation.Token).ConfigureAwait(false); } catch (OperationCanceledException) { cancelled = true; }
            Check(cancelled && fixture.Accepted == 0, "pre-cancelled probe opens no sockets");
        }
        bool rejected = false;
        try { await ProxyDiscovery.ProbeAsync(new ProxyEndpoint { Host = "8.8.8.8", Port = 80 }, CancellationToken.None).ConfigureAwait(false); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "probe revalidates mutable endpoint before opening sockets");
        if (Socket.OSSupportsIPv6) using (var fixture = new Fixture(ServerMode.Mixed, true)) {
            ProxyProbeResult result = await ProxyDiscovery.ProbeAsync(fixture.Endpoint, CancellationToken.None, 400).ConfigureAwait(false);
            Check(result.Http && result.Socks5 && result.Endpoint.ToString().StartsWith("[::1]:", StringComparison.Ordinal), "IPv6 mixed proxy on arbitrary port");
        } else Console.WriteLine("SKIP IPv6 runtime fixture: OS has no IPv6 socket support");
    }

    private static void Main() {
        try {
            EndpointTests();
            MetadataTests();
            ProbeTestsAsync().GetAwaiter().GetResult();
            Console.WriteLine("PASS " + checks + " checks; synthetic loopback sockets and injected metadata only; no real proxy discovery, upstream access, credentials or configuration changes.");
        } catch (Exception error) {
            Console.Error.WriteLine("FAIL after " + checks + " checks: " + error.GetType().Name + ": " + error.Message);
            Environment.ExitCode = 1;
        }
    }
}
