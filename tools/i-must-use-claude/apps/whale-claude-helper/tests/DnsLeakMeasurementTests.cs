using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using WhaleClaudeHelper;

// All measurements use an injected HTTP transport, virtual clock and delay.
// No request, local configuration read, client process or DNS lookup is performed.
internal static class DnsLeakMeasurementTests {
    private const string Token = "0123456789abcdef0123456789abcdef";
    private const string OtherToken = "abcdef0123456789abcdef0123456789";
    private const string Secret = "fixture-secret-must-not-be-displayed-4a71";
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 512 * 1024 };
    private static int checks, failures;

    private sealed class AssertionFailure : Exception { internal AssertionFailure(string label) : base(label) { } }
    private sealed class Call {
        internal Uri Uri;
        internal int Timeout;
        internal CancellationToken Cancellation;
    }
    private sealed class CaptureProgress : IProgress<DnsLeakProgress> {
        internal readonly List<DnsLeakProgress> Items = new List<DnsLeakProgress>();
        public void Report(DnsLeakProgress value) { Items.Add(value); }
    }
    private sealed class Fixture {
        internal readonly DateTime Start = new DateTime(2026, 11, 2, 5, 0, 0, DateTimeKind.Utc);
        internal DateTime Now;
        internal string Session = Token;
        internal Func<string> SessionFactory;
        internal Func<Uri, DnsLeakHttpReply> Handler;
        internal Action<int, CancellationToken> DelayHook;
        internal readonly List<Call> Calls = new List<Call>();
        internal readonly List<int> Delays = new List<int>();
        internal readonly CaptureProgress Progress = new CaptureProgress();
        internal readonly DnsLeakMeasurementService Service;
        internal Fixture() {
            Now = Start;
            SessionFactory = delegate { return Session; };
            Handler = DefaultReply;
            Service = new DnsLeakMeasurementService(
                delegate(Uri uri, int timeout, CancellationToken cancellation) {
                    Calls.Add(new Call { Uri = uri, Timeout = timeout, Cancellation = cancellation });
                    Now = Now.AddMilliseconds(20);
                    return Handler(uri);
                },
                delegate(int milliseconds, CancellationToken cancellation) {
                    Delays.Add(milliseconds);
                    cancellation.ThrowIfCancellationRequested();
                    Now = Now.AddMilliseconds(milliseconds);
                    if (DelayHook != null) DelayHook(milliseconds, cancellation);
                },
                delegate { return Now; },
                delegate { return SessionFactory(); });
        }
        internal DnsLeakHttpReply DefaultReply(Uri uri) {
            if (IsProbe(uri)) return Reply(403, "Forbidden " + Secret);
            if (IsResult(uri)) return Reply(200, ResultBody(Session, "8.8.8.8", "1.1.1.1"));
            if (IsMetadata(uri)) {
                string ip = Uri.UnescapeDataString(uri.AbsolutePath.Substring("/api/geoip/".Length));
                return Reply(200, Json.Serialize(new { country = "United States", region = "fixture region", city = "fixture city", isp = ip == "1.1.1.1" ? "Cloudflare, Inc." : "Google LLC", country_code = "US" }));
            }
            throw new AssertionFailure("unexpected outbound endpoint in fake transport");
        }
        internal DnsLeakMeasurementResult Measure(CancellationToken cancellation) {
            return Service.MeasureAsync(cancellation, Progress).GetAwaiter().GetResult();
        }
        internal DnsLeakMeasurementResult Measure() { return Measure(CancellationToken.None); }
    }

    private static bool IsProbe(Uri uri) { return uri.AbsolutePath == "/pixel.gif"; }
    private static bool IsResult(Uri uri) { return uri.AbsolutePath.StartsWith("/api/dns/result/", StringComparison.Ordinal); }
    private static bool IsMetadata(Uri uri) { return uri.AbsolutePath.StartsWith("/api/geoip/", StringComparison.Ordinal); }
    private static DnsLeakHttpReply Reply(int status, string body) { return new DnsLeakHttpReply { StatusCode = status, Body = body, Failure = null }; }
    private static DnsLeakHttpReply Failed(string code) { return new DnsLeakHttpReply { StatusCode = 0, Body = Secret, Failure = code }; }
    private static string ResultBody(string token, params string[] addresses) { return Json.Serialize(new { token = token, dns_servers = addresses }); }
    private static void Check(bool condition, string label) { if (!condition) throw new AssertionFailure(label); checks++; }
    private static void Safe(string text, string label) {
        Check(text != null && !text.Contains(Secret) && !text.Contains("fixture-subscription.invalid"), label + " hides raw fixture secret");
    }
    private static void SafeResult(DnsLeakMeasurementResult result, string label) {
        Safe(result.Format(false), label + " Chinese"); Safe(result.Format(true), label + " English");
    }
    private static void Run(string label, Action body) {
        try { body(); Console.WriteLine("PASS " + label); }
        catch (Exception error) {
            failures++;
            // Never print exception details from the backend or fake HTTP payload.
            Console.WriteLine("FAIL " + label + ": " + (error is AssertionFailure ? error.Message : error.GetType().Name));
        }
    }

    private static void HappyMeasurementAndRoute() {
        var f = new Fixture(); var result = f.Measure();
        Check(result.Status == "measured", "matching session and resolver observation is measured");
        Check(result.Rows != null && result.Rows.Length == 2, "two unique resolvers survive repeated result polls");
        Check(result.AttemptedProbes == 5 && result.CompletedProbes == 5 && result.ProbeFailures == 0, "HTTP 403 pixel responses still count as completed DNS challenges");
        var probes = f.Calls.Where(c => IsProbe(c.Uri)).ToArray();
        Check(probes.Length == 5 && probes.Select(c => c.Uri.AbsoluteUri).Distinct().Count() == 5, "five unique probe labels are used");
        for (int index = 1; index <= 5; index++) Check(probes.Any(c => c.Uri.Host == Token + "-" + index + ".d.ip.net.coffee"), "probe host contains matching session and fresh numbered label");
        var polls = f.Calls.Where(c => IsResult(c.Uri)).ToArray();
        Check(polls.Length >= 1 && polls.Length <= 3, "bounded root-domain result polling");
        Check(polls.All(c => c.Uri.Host == "ip.net.coffee" && c.Uri.AbsolutePath == "/api/dns/result/" + Token), "polling uses root host and exact current token without creating another session subdomain");
        Check(f.Calls.All(c => c.Uri.Scheme == "https" && c.Uri.UserInfo == "" && c.Uri.Fragment == ""), "all requests use HTTPS without credentials or fragments");
        Check(f.Calls.All(c => IsProbe(c.Uri) || IsResult(c.Uri) || IsMetadata(c.Uri)), "no exit-IP request or alternate provider is contacted");
        Check(f.Calls.All(c => c.Timeout > 0 && c.Timeout <= 45000), "transport calls receive bounded timeouts");
        Check(f.Delays.Contains(600) && f.Delays.Contains(2000), "probe spacing and authority observation wait use injected delay");
        Check(result.StartedUtc == f.Start && result.CollectedUtc >= result.StartedUtc && result.CollectedUtc <= f.Now, "UTC evidence times come from injected clock");
        Check(result.Rows.Any(row => row.Address == "8.8.8.8" && row.Country == "United States" && row.Organization == "Google LLC"), "available metadata is mapped to resolver row");
        Check(result.Rows.All(row => String.IsNullOrEmpty(row.Asn)), "absent ASN is not invented");
        SafeResult(result, "successful result with secret-bearing pixel response");
        Check(f.Progress.Items.Any(p => p.Stage == "probes") && f.Progress.Items.Any(p => p.Stage == "results") && f.Progress.Items.Any(p => p.Stage == "metadata"), "progress reports probes, result collection and metadata stages");
        Check(f.Progress.Items.All(p => p.Completed >= 0 && p.Total >= 0 && p.Completed <= p.Total), "progress counts remain within each stage bounds");
    }

    private static void EmptyIsNotSafe() {
        var f = new Fixture(); f.Handler = delegate(Uri uri) { return IsResult(uri) ? Reply(200, ResultBody(Token)) : f.DefaultReply(uri); };
        var result = f.Measure();
        Check(result.Status == "no_evidence" && result.Rows != null && result.Rows.Length == 0, "empty observations produce no_evidence instead of passing a leak test");
        Check(!f.Calls.Any(c => IsMetadata(c.Uri)), "empty observation does not request metadata");
        Check(result.Format(true).IndexOf("No DNS leaks detected", StringComparison.OrdinalIgnoreCase) < 0 && !result.Format(false).Contains("未检测到 DNS 泄露"), "empty observation does not use a green success claim");
        SafeResult(result, "no evidence");
        var late = new Fixture(); int polls = 0;
        late.Handler = delegate(Uri uri) { return IsResult(uri) ? Reply(200, ++polls == 1 ? ResultBody(Token) : ResultBody(Token, "8.8.8.8")) : late.DefaultReply(uri); };
        result = late.Measure();
        Check(polls >= 2 && result.Status == "measured" && result.Rows.Length == 1 && result.Rows[0].Address == "8.8.8.8", "initial empty poll does not discard late authoritative observation");
    }

    private static void RejectUnboundOrInvalidResponses() {
        var payloads = new[] {
            ResultBody(OtherToken, "8.8.8.8"),
            "{\"dns_servers\":[\"8.8.8.8\"],\"error\":\"" + Secret + "\"}",
            "{invalid-json-" + Secret,
            ResultBody(Token, "8.8.8.8", "https://fixture-subscription.invalid/" + Secret),
            ResultBody(Token, "999.999.999.999"),
            "{\"token\":\"" + Token + "\",\"dns_servers\":{\"8.8.8.8\":1},\"error\":\"" + Secret + "\"}"
        };
        foreach (string payload in payloads) {
            var f = new Fixture(); f.Handler = delegate(Uri uri) { return IsResult(uri) ? Reply(200, payload) : f.DefaultReply(uri); };
            var result = f.Measure();
            Check(result.Status == "service_error", "unbound or malformed observation is a service error");
            Check(result.Rows != null && result.Rows.Length == 0, "invalid whole observation batch is not partially trusted");
            Check(!f.Calls.Any(c => IsMetadata(c.Uri)), "invalid resolver cannot reach metadata request construction");
            SafeResult(result, "invalid observation");
        }
        foreach (string address in new[] { "0.0.0.0", "127.0.0.1", "10.0.0.1", "100.64.0.1", "169.254.1.1", "192.0.2.1", "224.0.0.1", "240.0.0.1", "::", "::1", "fc00::1", "fe80::1", "ff02::1", "2001:db8::1", "::ffff:127.0.0.1" }) {
            var f = new Fixture(); f.Handler = delegate(Uri uri) { return IsResult(uri) ? Reply(200, ResultBody(Token, address)) : f.DefaultReply(uri); };
            var result = f.Measure();
            Check(result.Status == "service_error" && result.Rows.Length == 0, "private, special or reserved resolver address cannot become public DNS evidence");
            Check(!f.Calls.Any(c => IsMetadata(c.Uri)), "special address never reaches metadata transport");
        }
    }

    private static void ResponseBoundsAndDeduplication() {
        var oversized = new Fixture(); oversized.Handler = delegate(Uri uri) { return IsResult(uri) ? Reply(200, new string('x', 128 * 1024 + 1) + Secret) : oversized.DefaultReply(uri); };
        var result = oversized.Measure(); Check(result.Status == "service_error" && result.Rows.Length == 0, "oversized result body is rejected"); SafeResult(result, "oversized response");
        var deep = new Fixture(); string nested = "{\"token\":\"" + Token + "\",\"dns_servers\":[\"8.8.8.8\"],\"padding\":" + new string('[', 22) + "0" + new string(']', 22) + "}";
        deep.Handler = delegate(Uri uri) { return IsResult(uri) ? Reply(200, nested) : deep.DefaultReply(uri); };
        result = deep.Measure(); Check(result.Status == "service_error" && result.Rows.Length == 0, "deep JSON cannot bypass bounded parser"); SafeResult(result, "deep response");
        var tooMany = new Fixture(); var addresses = Enumerable.Range(1, 33).Select(n => "8.8.0." + n).ToArray();
        tooMany.Handler = delegate(Uri uri) { return IsResult(uri) ? Reply(200, ResultBody(Token, addresses)) : tooMany.DefaultReply(uri); };
        result = tooMany.Measure();
        Check((result.Status == "service_error" || result.Status == "partial") && result.Rows.Length <= 32, "resolver count overflow is bounded and never reported fully measured");
        Check(tooMany.Calls.Count(c => IsMetadata(c.Uri)) <= 32, "metadata work is bounded by resolver limit"); SafeResult(result, "too many resolvers");
        var duplicates = new Fixture(); duplicates.Handler = delegate(Uri uri) { return IsResult(uri) ? Reply(200, ResultBody(Token, "8.8.8.8", "8.8.8.8", "2001:4860:4860::8888", "2001:4860:4860:0:0:0:0:8888")) : duplicates.DefaultReply(uri); };
        result = duplicates.Measure();
        Check(result.Status == "measured" && result.Rows.Length == 2, "duplicate IPv4 and equivalent IPv6 addresses are normalized and deduplicated");
        Check(result.Rows.Any(row => row.Address == "2001:4860:4860::8888"), "IPv6 evidence uses canonical address form");
        Check(duplicates.Calls.Where(c => IsMetadata(c.Uri)).All(c => c.Uri.Host == "ip.net.coffee"), "IPv6 metadata remains on the fixed provider host");
    }

    private static void DuplicateJsonKeysAreRejected() {
        string[] bodies = {
            "{\"token\":\"" + Token + "\",\"token\":\"" + Token + "\",\"dns_servers\":[\"8.8.8.8\"],\"error\":\"" + Secret + "\"}",
            "{\"token\":\"" + Token + "\",\"dns_servers\":[\"8.8.8.8\"],\"dns_servers\":[\"8.8.8.8\"],\"error\":\"" + Secret + "\"}"
        };
        foreach (string body in bodies) {
            var f = new Fixture(); f.Handler = delegate(Uri uri) { return IsResult(uri) ? Reply(200, body) : f.DefaultReply(uri); };
            var result = f.Measure();
            Check(result.Status == "service_error" && result.Rows.Length == 0, "duplicate token or resolver keys invalidate the whole response");
            Check(!f.Calls.Any(c => IsMetadata(c.Uri)), "ambiguous result keys do not reach metadata requests");
            SafeResult(result, "duplicate result keys");
        }
        foreach (string metadata in new[] {
            "{\"country\":\"United States\",\"country\":\"United States\",\"isp\":\"Google LLC\",\"error\":\"" + Secret + "\"}",
            "{\"country\":\"United States\",\"isp\":\"Google LLC\",\"extra\":{\"ordinary\":1,\"ordinary\":2},\"error\":\"" + Secret + "\"}"
        }) {
            var f = new Fixture(); f.Handler = delegate(Uri uri) { return IsMetadata(uri) ? Reply(200, metadata) : f.DefaultReply(uri); };
            var result = f.Measure();
            Check(result.Rows.Length == 2, "duplicate metadata keys do not erase authoritative IP evidence");
            Check(result.Rows.All(row => String.IsNullOrEmpty(row.Country) && String.IsNullOrEmpty(row.Organization) && String.IsNullOrEmpty(row.Asn)), "ambiguous metadata including ordinary nested keys stays unknown");
            SafeResult(result, "duplicate metadata keys");
        }
    }

    private static void EmptyThenFailureAndRecovery() {
        foreach (string fault in new[] { "429", "500", "wrong-token" }) {
            var f = new Fixture(); int polls = 0;
            f.Handler = delegate(Uri uri) {
                if (!IsResult(uri)) return f.DefaultReply(uri);
                if (++polls == 1) return Reply(200, ResultBody(Token));
                return fault == "wrong-token" ? Reply(200, ResultBody(OtherToken, "8.8.8.8")) : Reply(Int32.Parse(fault), Secret);
            };
            var result = f.Measure();
            Check(polls >= 2 && result.Status == "service_error" && result.Rows.Length == 0, "later unrecovered result failure overrides an earlier empty observation");
            SafeResult(result, "empty then result failure");
        }
        var recovered = new Fixture(); int seen = 0;
        recovered.Handler = delegate(Uri uri) {
            if (!IsResult(uri)) return recovered.DefaultReply(uri);
            return ++seen == 1 ? Reply(200, ResultBody(OtherToken, "8.8.8.8")) : Reply(200, ResultBody(Token));
        };
        var completed = recovered.Measure();
        Check(seen >= 2 && completed.Status == "no_evidence" && completed.Rows.Length == 0, "a subsequent valid result can clear an earlier provider failure");
        SafeResult(completed, "valid empty recovery");
    }

    private static void PartialProbeAndMetadataFailures() {
        var partial = new Fixture(); int probes = 0;
        partial.Handler = delegate(Uri uri) { if (IsProbe(uri) && ++probes == 2) return Failed("network_error"); return partial.DefaultReply(uri); };
        var result = partial.Measure();
        Check(result.Status == "partial" && result.Rows.Length == 2, "partial challenge transport failure retains observed resolvers");
        Check(result.AttemptedProbes == 5 && result.CompletedProbes == 4 && result.ProbeFailures == 1, "failed probe counts remain factual"); SafeResult(result, "partial probes");
        var metadata = new Fixture(); metadata.Handler = delegate(Uri uri) { return IsMetadata(uri) ? Reply(503, "service error " + Secret) : metadata.DefaultReply(uri); };
        result = metadata.Measure();
        Check((result.Status == "measured" || result.Status == "partial") && result.Rows.Length == 2, "metadata failure cannot erase authoritative IP evidence");
        Check(result.Rows.All(row => String.IsNullOrEmpty(row.Country) && String.IsNullOrEmpty(row.Organization) && String.IsNullOrEmpty(row.Asn)), "unavailable metadata stays unknown rather than fabricated"); SafeResult(result, "metadata failure");
    }

    private static void ServiceAndNetworkErrors() {
        foreach (int status in new[] { 429, 500, 503 }) {
            var f = new Fixture(); f.Handler = delegate(Uri uri) { return IsResult(uri) ? Reply(status, Secret) : f.DefaultReply(uri); };
            var result = f.Measure(); Check(result.Status == "service_error" && result.Rows.Length == 0, "provider HTTP failure is not a DNS safety verdict"); SafeResult(result, "provider failure");
        }
        var network = new Fixture(); network.Handler = delegate(Uri uri) { return Failed("network_error"); };
        var failed = network.Measure(); Check(failed.Status == "network_error" && failed.Rows.Length == 0, "network failures are distinct from no DNS evidence"); SafeResult(failed, "network failure");
        var timed = new Fixture(); timed.Handler = delegate(Uri uri) { return Failed("timeout"); };
        failed = timed.Measure(); Check(failed.Status == "timeout" && failed.Rows.Length == 0, "transport timeout is distinct from successful empty observation"); SafeResult(failed, "transport timeout");
    }

    private static void CancellationAndDeadlineRetainEvidence() {
        var early = new Fixture(); using (var cancelled = new CancellationTokenSource()) {
            cancelled.Cancel(); var result = early.Measure(cancelled.Token);
            Check(result.Status == "cancelled" && early.Calls.Count == 0, "pre-cancelled measurement does not send requests"); SafeResult(result, "early cancellation");
        }
        var later = new Fixture(); using (var cancellation = new CancellationTokenSource()) {
            int polls = 0;
            later.Handler = delegate(Uri uri) {
                if ((IsResult(uri) && ++polls > 1) || IsMetadata(uri)) { cancellation.Cancel(); return Failed("cancelled"); }
                return later.DefaultReply(uri);
            };
            var result = later.Measure(cancellation.Token);
            Check(result.Status == "cancelled" && result.Rows.Length == 2, "cancellation after observation retains already collected resolver evidence"); SafeResult(result, "late cancellation");
        }
        var deadline = new Fixture(); int seenPolls = 0;
        deadline.Handler = delegate(Uri uri) {
            if ((IsResult(uri) && ++seenPolls > 1) || IsMetadata(uri)) { deadline.Now = deadline.Start.AddSeconds(46); return Failed("timeout"); }
            return deadline.DefaultReply(uri);
        };
        var timed = deadline.Measure();
        Check(timed.Status == "timeout" && timed.Rows.Length == 2, "total deadline retains earlier valid observation");
        Check(timed.CollectedUtc >= timed.StartedUtc, "timed-out evidence has coherent timestamps"); SafeResult(timed, "deadline result");
        var beforeEvidence = new Fixture(); beforeEvidence.DelayHook = delegate(int ms, CancellationToken ct) { beforeEvidence.Now = beforeEvidence.Start.AddSeconds(46); };
        timed = beforeEvidence.Measure();
        Check(timed.Status == "timeout" && timed.Rows.Length == 0, "deadline before observation produces timeout without evidence");
        Check(beforeEvidence.Calls.Count <= 1, "deadline prevents additional challenge calls");
    }

    private static void SessionIsolationAndConstruction() {
        // Construction only: no MeasureAsync is called on the production instance.
        Check(new DnsLeakMeasurementService() != null, "default service can be constructed without starting a measurement");
        var f = new Fixture(); int sessions = 0;
        f.SessionFactory = delegate { f.Session = ++sessions == 1 ? Token : OtherToken; return f.Session; };
        var first = f.Measure(); int split = f.Calls.Count; var second = f.Measure();
        Check(first.Status == "measured" && second.Status == "measured", "independent measurements both complete");
        Check(f.Calls.Take(split).Where(c => IsProbe(c.Uri)).All(c => c.Uri.Host.StartsWith(Token + "-", StringComparison.Ordinal)) && f.Calls.Skip(split).Where(c => IsProbe(c.Uri)).All(c => c.Uri.Host.StartsWith(OtherToken + "-", StringComparison.Ordinal)), "sessions do not reuse prior challenge hostnames");
        Check(f.Calls.Skip(split).Where(c => IsResult(c.Uri)).All(c => c.Uri.AbsolutePath.EndsWith("/" + OtherToken, StringComparison.Ordinal)), "later measurement polls only its own token");
    }

    private static void IgnoredTransportCancellationIsBounded() {
        var f = new Fixture();
        using (var entered = new ManualResetEvent(false))
        using (var release = new ManualResetEvent(false))
        using (var finished = new ManualResetEvent(false))
        using (var cancellation = new CancellationTokenSource()) {
            f.Handler = delegate(Uri uri) {
                entered.Set();
                try {
                    // This intentionally ignores CancellationToken to simulate stuck DNS/PAC.
                    release.WaitOne();
                    return Reply(403, "late response " + Secret);
                } finally { finished.Set(); }
            };
            Task<DnsLeakMeasurementResult> measurement = Task.Run(delegate { return f.Measure(cancellation.Token); });
            DnsLeakMeasurementResult result = null; string returnedState = null;
            try {
                Check(entered.WaitOne(1500), "blocking fake transport has started before cancellation");
                var watch = Stopwatch.StartNew(); cancellation.CancelAfter(50);
                Check(measurement.Wait(1500) && watch.ElapsedMilliseconds < 1500, "caller cancellation returns promptly even when transport ignores its token");
                result = measurement.Result;
                Check(result.Status == "cancelled" && result.Rows.Length == 0, "blocked first probe produces cancelled without fabricated evidence");
                Check(!f.Calls.Any(c => IsResult(c.Uri) || IsMetadata(c.Uri)), "cancellation does not issue later polling or metadata requests");
                returnedState = Json.Serialize(new { result.Status, result.AttemptedProbes, result.CompletedProbes, result.ProbeFailures, Count = result.Rows.Length, result.CollectedUtc });
                SafeResult(result, "blocked transport cancellation");
            } finally {
                release.Set();
                Check(finished.WaitOne(1500), "late fake transport is explicitly released for test cleanup");
                Check(measurement.Wait(1500), "measurement task finishes after fixture release");
            }
            Thread.Sleep(30);
            Check(returnedState == Json.Serialize(new { result.Status, result.AttemptedProbes, result.CompletedProbes, result.ProbeFailures, Count = result.Rows.Length, result.CollectedUtc }), "late transport response cannot mutate a returned measurement");
            Check(f.Calls.Count == 1, "late transport completion cannot resume the cancelled workflow");
        }
    }

    public static int Main() {
        Run("authoritative observation and request scope", HappyMeasurementAndRoute);
        Run("empty result is not a pass", EmptyIsNotSafe);
        Run("session binding and invalid response rejection", RejectUnboundOrInvalidResponses);
        Run("response bounds and IP normalization", ResponseBoundsAndDeduplication);
        Run("duplicate JSON keys", DuplicateJsonKeysAreRejected);
        Run("empty response followed by failure or recovery", EmptyThenFailureAndRecovery);
        Run("partial probes and metadata failures", PartialProbeAndMetadataFailures);
        Run("service and network failures", ServiceAndNetworkErrors);
        Run("cancellation and total deadline", CancellationAndDeadlineRetainEvidence);
        Run("new sessions and idle constructor", SessionIsolationAndConstruction);
        Run("bounded cancellation of uncooperative transport", IgnoredTransportCancellationIsBounded);
        Console.WriteLine("DnsLeakMeasurementTests: " + checks + " checks, " + failures + " failed groups. Fake transport, fake clock and fake delay only; no network or real configuration access.");
        return failures == 0 ? 0 : 1;
    }
}
