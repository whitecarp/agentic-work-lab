using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WhaleClaudeHelper {
    // Pure fixtures: never construct or invoke WindowsDiagnosticSnapshotProvider or
    // OfficialConnectionProbe. No files, environment variables or network are read.
    internal static class DiagnosticsServiceTests {
        private static int passed;
        public static int Main() {
            try { Run().GetAwaiter().GetResult(); Console.WriteLine("PASS: " + passed + " diagnostics fixture cases; no machine/network probes."); return 0; }
            catch (Exception error) { Console.Error.WriteLine("FAIL: " + error.Message); return 1; }
        }
        private static async Task Run() {
            await LocalOnlyAndInertConstruction();
            await ProxyOutputIsAllowlisted();
            await RestrictedAndServerErrorAreDifferent();
            await TimeoutAndTlsAreDifferent();
            await DnsFailureSkipsHttps();
            await PublicEntryResponsesAreReachable();
            await RateLimitIsReachable();
            await MissingPathsAreNotHardFailures();
            await ExceptionsAreSanitized();
            await NullResultsAreSafe();
            await CancellationBeforeRun();
            await CancellationDuringProbe();
            DemoIsStatic();
        }
        private static async Task LocalOnlyAndInertConstruction() {
            var snapshot = new FixtureSnapshot { Value = Installed() }; var probe = new FixtureProbe();
            var service = new DiagnosticsService(snapshot, probe);
            Require(snapshot.Calls == 0 && probe.Hosts.Count == 0, "Construction must be inert.");
            var report = await service.RunAsync(false, CancellationToken.None);
            Require(snapshot.Calls == 1 && probe.Hosts.Count == 0, "Local-only must not call a transport.");
            Require(!report.IsDemo && !report.NetworkRequested, "Local report flags.");
            Require(Find(report, "claude-code").Status == DiagnosticStatus.Pass, "Code presence fixture.");
            Require(Find(report, "claude-desktop").DetailEn.Contains("no application was launched"), "Presence is not execution verification.");
            Require(Find(report, "network-not-requested").Status == DiagnosticStatus.Warn, "Network remains opt-in.");
            Require(Find(report, "scope").DetailEn.Contains("account-region eligibility"), "Scope must disclaim account eligibility.");
            passed++;
        }
        private static async Task ProxyOutputIsAllowlisted() {
            var value = Installed();
            value.ProxyVariableNames = new[] { "https_proxy", "HTTPS_PROXY", "NO_PROXY", "UNKNOWN", "https://secret-user:secret-password@fixture.invalid", null };
            var report = await Service(value, new FixtureProbe()).RunAsync(false, CancellationToken.None);
            var check = Find(report, "proxy-variables");
            Require(check.Status == DiagnosticStatus.Warn && check.DetailEn.Contains("HTTPS_PROXY, NO_PROXY"), "Proxy name normalization.");
            Require(!AllText(report).Contains("secret-") && !AllText(report).Contains("fixture.invalid") && !AllText(report).Contains("UNKNOWN"), "Never display provider values or arbitrary names.");
            passed++;
        }
        private static async Task RestrictedAndServerErrorAreDifferent() {
            var probe = new FixtureProbe(); probe.Results["claude.ai"] = Http(403); probe.Results["api.anthropic.com"] = Http(503);
            var report = await Service(Installed(), probe).RunAsync(true, CancellationToken.None);
            Require(report.NetworkRequested, "Network request flag.");
            Require(probe.Hosts.OrderBy(x => x).SequenceEqual(new[] { "api.anthropic.com", "claude.ai" }), "Only two fixed official hosts may be passed to transport.");
            var blocked = Find(report, "claude.ai-https"); var server = Find(report, "api.anthropic.com-https");
            Require(blocked.Status == DiagnosticStatus.Warn && blocked.DetailEn.Contains("reachable") && blocked.DetailEn.Contains("403"), "403 is reachable/restricted.");
            Require(server.Status == DiagnosticStatus.Fail && server.DetailEn.Contains("server error"), "503 is a server/gateway failure.");
            passed++;
        }
        private static async Task TimeoutAndTlsAreDifferent() {
            var probe = new FixtureProbe();
            probe.Results["claude.ai"] = new ConnectionProbeResult { Dns = ProbeOutcome.Success, Https = ProbeOutcome.Timeout, Failure = ConnectionFailure.Https };
            probe.Results["api.anthropic.com"] = new ConnectionProbeResult { Dns = ProbeOutcome.Success, Https = ProbeOutcome.Failed, Failure = ConnectionFailure.Tls };
            var report = await Service(Installed(), probe).RunAsync(true, CancellationToken.None);
            Require(Find(report, "claude.ai-https").DetailEn.Contains("timed out"), "HTTPS timeout classification.");
            Require(Find(report, "api.anthropic.com-https").DetailEn.Contains("TLS") && Find(report, "api.anthropic.com-https").Status == DiagnosticStatus.Fail, "TLS classification.");
            Require(Find(report, "claude.ai-dns").Status == DiagnosticStatus.Pass, "HTTPS failures preserve successful DNS.");
            passed++;
        }
        private static async Task DnsFailureSkipsHttps() {
            var probe = new FixtureProbe();
            probe.Results["claude.ai"] = new ConnectionProbeResult { Dns = ProbeOutcome.Timeout, Failure = ConnectionFailure.Dns };
            probe.Results["api.anthropic.com"] = new ConnectionProbeResult { Dns = ProbeOutcome.Failed, Failure = ConnectionFailure.Dns };
            var report = await Service(Installed(), probe).RunAsync(true, CancellationToken.None);
            Require(Find(report, "claude.ai-dns").Status == DiagnosticStatus.Fail && Find(report, "claude.ai-dns").DetailEn.Contains("timed out"), "DNS timeout classification.");
            Require(Find(report, "claude.ai-https").Status == DiagnosticStatus.Warn && Find(report, "api.anthropic.com-https").DetailEn.Contains("Not run"), "No inferred HTTPS success after DNS failure.");
            passed++;
        }
        private static async Task PublicEntryResponsesAreReachable() {
            var probe = new FixtureProbe(); probe.Results["claude.ai"] = Http(302); probe.Results["api.anthropic.com"] = Http(404);
            var report = await Service(Installed(), probe).RunAsync(true, CancellationToken.None);
            Require(Find(report, "claude.ai-https").Status == DiagnosticStatus.Pass && Find(report, "api.anthropic.com-https").Status == DiagnosticStatus.Pass, "Redirect/public-root 404 prove response only.");
            Require(Find(report, "claude.ai-https").DetailEn.Contains("no login, model call or redirect"), "Public-entry scope is explicit.");
            passed++;
        }
        private static async Task RateLimitIsReachable() {
            var probe = new FixtureProbe(); probe.Results["claude.ai"] = Http(429); probe.Results["api.anthropic.com"] = Http(401);
            var report = await Service(Installed(), probe).RunAsync(true, CancellationToken.None);
            Require(Find(report, "claude.ai-https").Status == DiagnosticStatus.Warn && Find(report, "claude.ai-https").DetailEn.Contains("rate-limiting"), "429 is rate limiting.");
            Require(Find(report, "api.anthropic.com-https").Status == DiagnosticStatus.Warn, "401 is restricted, not unreachable.");
            passed++;
        }
        private static async Task MissingPathsAreNotHardFailures() {
            var report = await Service(new DiagnosticSnapshot(), new FixtureProbe()).RunAsync(false, CancellationToken.None);
            Require(Find(report, "claude-code").Status == DiagnosticStatus.Warn && Find(report, "claude-code-config").Status == DiagnosticStatus.Warn, "Missing common paths are inconclusive.");
            Require(Find(report, "proxy-variables").Status == DiagnosticStatus.Pass && Find(report, "proxy-variables").DetailEn.Contains("does not rule out"), "Absent proxy variables do not prove direct connectivity.");
            passed++;
        }
        private static async Task ExceptionsAreSanitized() {
            var snapshot = new FixtureSnapshot { Error = new InvalidOperationException("fixture-secret-account-token") };
            var probe = new FixtureProbe { Error = new InvalidOperationException("fixture-secret-proxy-url") };
            var report = await new DiagnosticsService(snapshot, probe).RunAsync(true, CancellationToken.None);
            Require(Find(report, "local-unavailable").Status == DiagnosticStatus.Warn, "Local exceptions are handled.");
            Require(!AllText(report).Contains("fixture-secret"), "Exception text must never enter the report.");
            Require(report.Checks.Any(x => x.Id == "scope"), "Error report keeps scope.");
            passed++;
        }
        private static async Task NullResultsAreSafe() {
            var probe = new FixtureProbe { ReturnNull = true };
            var report = await new DiagnosticsService(new FixtureSnapshot(), probe).RunAsync(true, CancellationToken.None);
            Require(Find(report, "local-unavailable").Status == DiagnosticStatus.Warn && Find(report, "claude.ai-dns").Status == DiagnosticStatus.Fail, "Null provider/transport results fail safely.");
            passed++;
        }
        private static async Task CancellationBeforeRun() {
            var snapshot = new FixtureSnapshot { Value = Installed() }; var probe = new FixtureProbe();
            using (var cancellation = new CancellationTokenSource()) {
                cancellation.Cancel(); bool cancelled = false;
                try { await new DiagnosticsService(snapshot, probe).RunAsync(true, cancellation.Token); } catch (OperationCanceledException) { cancelled = true; }
                Require(cancelled && snapshot.Calls == 0 && probe.Hosts.Count == 0, "Pre-cancellation must prevent all probes.");
            }
            passed++;
        }
        private static async Task CancellationDuringProbe() {
            using (var cancellation = new CancellationTokenSource()) {
                var probe = new FixtureProbe { CancelOnCall = cancellation };
                bool cancelled = false;
                try { await Service(Installed(), probe).RunAsync(true, cancellation.Token); } catch (OperationCanceledException) { cancelled = true; }
                Require(cancelled && probe.Hosts.Count > 0, "In-flight cancellation must propagate, not become a failure report.");
            }
            passed++;
        }
        private static void DemoIsStatic() {
            var first = DiagnosticsService.DemoReport(); var second = DiagnosticsService.DemoReport();
            Require(first.IsDemo && !first.NetworkRequested && first.CollectedUtc == second.CollectedUtc, "Demo is static and clearly flagged.");
            Require(AllText(first) == AllText(second) && Find(first, "demo").DetailEn.Contains("no computer data"), "Demo content is fixture-only.");
            first.Checks.Clear(); Require(second.Checks.Count > 0, "Demo calls do not share mutable report state.");
            passed++;
        }
        private static DiagnosticsService Service(DiagnosticSnapshot value, FixtureProbe probe) { return new DiagnosticsService(new FixtureSnapshot { Value = value }, probe); }
        private static DiagnosticSnapshot Installed() { return new DiagnosticSnapshot { CodeExecutableFound = true, DesktopExecutableFound = true, CodeConfigFound = true, DesktopConfigFound = true }; }
        private static ConnectionProbeResult Http(int code) { return new ConnectionProbeResult { Dns = ProbeOutcome.Success, Https = ProbeOutcome.Success, HttpStatusCode = code }; }
        private static DiagnosticCheck Find(DiagnosticReport report, string id) { return report.Checks.Single(x => x.Id == id); }
        private static string AllText(DiagnosticReport report) { return String.Join("\n", report.Checks.Select(x => x.Id + x.TitleZh + x.TitleEn + x.DetailZh + x.DetailEn)); }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private sealed class FixtureSnapshot : IDiagnosticsSnapshotProvider {
            public DiagnosticSnapshot Value; public Exception Error; public int Calls;
            public DiagnosticSnapshot Capture() { Calls++; if (Error != null) throw Error; return Value; }
        }
        private sealed class FixtureProbe : IDiagnosticsConnectionProbe {
            public readonly List<string> Hosts = new List<string>();
            public readonly Dictionary<string, ConnectionProbeResult> Results = new Dictionary<string, ConnectionProbeResult>();
            public Exception Error; public bool ReturnNull; public CancellationTokenSource CancelOnCall;
            public Task<ConnectionProbeResult> ProbeAsync(string host, CancellationToken cancellation) {
                Hosts.Add(host);
                if (CancelOnCall != null) CancelOnCall.Cancel();
                cancellation.ThrowIfCancellationRequested();
                if (Error != null) throw Error;
                ConnectionProbeResult result;
                if (ReturnNull) result = null; else if (!Results.TryGetValue(host, out result)) result = Http(200);
                return Task.FromResult(result);
            }
        }
    }
}
