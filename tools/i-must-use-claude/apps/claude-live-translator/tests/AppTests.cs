using System;
using System.Text;
using ClaudeCompanion;

internal static class AppTests {
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); }
    public static int Main(string[] args) {
        Console.OutputEncoding = new UTF8Encoding(false);
        var a = new Snapshot { Conversation = "a", MessageId = "1", Text = "hello" };
        var growth = new Snapshot { Conversation = "a", MessageId = "1", Text = "hello world" };
        var other = new Snapshot { Conversation = "b", MessageId = "1", Text = "hello" };
        Check(ResultGate.CanShow(1,1,a,growth), "stream growth allowed");
        Check(!ResultGate.CanShow(1,2,a,a), "paused result rejected");
        Check(!ResultGate.CanShow(1,1,a,other), "other chat rejected");
        Check(!ResultGate.CanShow(1,1,a,null), "missing chat rejected");
        Check(!ResultGate.CanShow(1,1,a,new Snapshot { Conversation="a", MessageId="1",Text="hello",Direction="zh_en" }), "old direction translation rejected");
        Check(SelectionStatus.Describe("password_denied",true).Contains("密码"), "password selections are not read");
        Check(SelectionStatus.Describe("no_selection",true).Contains("上一段"), "unreadable selection labels previous result");
        Check(SelectionStatus.Describe("scan_limit",false).Contains("页面内容较多"), "large page failure has a useful explanation");
        Check(SelectionRetry.CanRetry("no_selection",0), "one retry allows the provider to publish a fresh selection");
        Check(!SelectionRetry.CanRetry("no_selection",1), "selection retry is bounded");
        Check(!SelectionRetry.CanRetry("password_denied",0), "password refusal never retried");
        Check(!SelectionRetry.CanRetry("foreground_changed",0), "changed window never retried");
        Check(!SelectionRetry.CanRetry("ambiguous_selection",0), "ambiguous selection never retried");
        var safeDiagnostics=SelectionDiagnostics.SafeFields(new System.Collections.Generic.Dictionary<string,object>{
            {"status","scan_limit"},{"text","PRIVATE SENTINEL"},{"sourceTitle","PRIVATE TITLE"},
            {"diagnostics",new System.Collections.Generic.Dictionary<string,object>{{"phase","window"},{"filterPieces",3},{"text","PRIVATE NESTED"}}}
        });
        Check(!Json.Encode(safeDiagnostics).Contains("PRIVATE"), "diagnostics never contain source text or titles");
        Check(Json.Str(safeDiagnostics,"phase")=="window", "diagnostics retain failure phase");
        Check(!ResultGate.CanShow(1,1,a,new Snapshot { Conversation="a", MessageId="1",Text="hi" }), "rewritten reply rejected");
        Check(HighlightGate.CanMark(a,a), "highlight matches displayed source");
        Check(HighlightGate.CanMark(a,growth), "existing displayed-source anchors remain while reply grows");
        Check(!HighlightGate.CanMark(a,other), "highlight rejects other conversation");
        Check(!HighlightGate.CanMark(a,new Snapshot { Conversation="a", MessageId="2",Text="hello" }), "highlight rejects next reply before translation");
        Check(!HighlightGate.CanMark(a,new Snapshot { Conversation="a", MessageId="1",Text="different" }), "highlight rejects stale content even when message id reused");
        Check(!HighlightGate.CanMark(new Snapshot { Conversation="manual", MessageId="1",Text="hello" },new Snapshot { Conversation="manual", MessageId="1",Text="hello" }), "manual paste never highlights Claude");
        Check(HighlightGate.ReadRectangle(new System.Collections.Generic.Dictionary<string,object>{{"x",Double.NaN},{"y",0},{"width",100},{"height",20}}).IsEmpty,"invalid coordinates rejected");
        // 0.3: a translation survives read hiccups and the end of streaming; another chat or reply clears it.
        var streaming = new Snapshot { Conversation = "a", MessageId = "streaming", Text = "hello" };
        Check(ReadGate.SameReply(streaming, new Snapshot { Conversation = "a", MessageId = "2", Text = "hello world" }), "finished stream keeps translation");
        Check(ReadGate.SameReply(a, growth), "growing reply keeps translation");
        Check(!ReadGate.SameReply(a, new Snapshot { Conversation = "a", MessageId = "2", Text = "hello" }), "next reply clears");
        Check(!ReadGate.SameReply(streaming, new Snapshot { Conversation = "b", MessageId = "2", Text = "hello world" }), "other chat clears");
        Check(!ReadGate.SameReply(streaming, new Snapshot { Conversation = "a", MessageId = "2", Text = "different" }), "rewritten stream clears");
        Check(!ReadGate.SameReply(null, a), "nothing shown yet");
        Check(!ReadGate.ShouldClear(a, ""), "failed read keeps translation");
        Check(!ReadGate.ShouldClear(a, "a"), "same chat keeps translation");
        Check(ReadGate.ShouldClear(a, "b"), "other chat clears translation");
        Check(ReadGate.ShouldClear(null, ""), "nothing to keep");
        Check(WaitTicker.Describe("等", 1) == "等", "no counter at first");
        Check(WaitTicker.Describe("等", 5) == "等 已等 5 秒", "seconds shown");
        Check(WaitTicker.Describe("等", 25).StartsWith("网络有点慢", StringComparison.Ordinal), "slow network hint");
        Check(Worker.ErrorMessage("google_blocked").Contains("人机验证"), "captcha message");
        Check(Worker.ErrorMessage("unexpected_redirect").Contains("跳转"), "redirect message");
        Check(Worker.ErrorMessage("something_new").Contains("暂不可用"), "fallback message");
        Check(Worker.ErrorMessage("proxy_authentication").Contains("代理"), "proxy failure distinct from Google");
        Check(ReaderStatus.Describe("waiting", true).Contains("上一条"), "previous translation visibly labelled");
        Check(ReaderStatus.Describe("conversation_document_not_found", false).Contains("Code"), "unsupported page actionable");
        Check(ReaderStatus.Failure(new System.IO.FileNotFoundException()).Contains("ClaudeReader.exe"), "missing helper not called timeout");
        Check(ReaderStatus.Failure(new TimeoutException()).Contains("超时"), "real timeout described");
        if (args.Length == 1 && args[0] == "--offline") { Console.WriteLine("AppTests offline checks passed (Google not contacted)."); return 0; }
        using(var worker = new Worker()) {
            string zh = worker.Translate("Please keep the original file.","en_zh",false).GetAwaiter().GetResult();
            Check(System.Text.RegularExpressions.Regex.IsMatch(zh,"[\\u3400-\\u9fff]"),"English to Chinese");
            Console.WriteLine("en_zh: " + zh);
            string en = worker.Translate("请保留原始文件。", "zh_en",false).GetAwaiter().GetResult();
            Check(en.IndexOf("file",StringComparison.OrdinalIgnoreCase)>=0,"Chinese to English");
            Console.WriteLine("zh_en: " + en);
            string mixed = worker.Translate("已完成更新。 Everything is ready. Please keep the original files. 接下来检查结果。","en_zh",false).GetAwaiter().GetResult();
            Check(mixed.Contains("已完成更新") && mixed.IndexOf("Everything", StringComparison.OrdinalIgnoreCase) < 0 && mixed.IndexOf("Please keep", StringComparison.OrdinalIgnoreCase) < 0, "mixed Chinese does not disable English translation");
            Console.WriteLine("mixed_en_zh: " + mixed);
            string paragraphs = worker.Translate("Please keep the original file.\n\nCheck the preview before exporting.","en_zh",false).GetAwaiter().GetResult();
            Check(paragraphs.Contains("\n\n"), "Google round trip preserves paragraph breaks");
            string code = "    print('hello')\nhttps://example.com/a.b?q=1\n`ENDPOINT_SENTINEL`";
            string result = worker.Translate(code,"en_zh",false).GetAwaiter().GetResult();
            Check(result==code, "code and URL preserved");
        }
        Console.WriteLine("AppTests passed: stale-result guards and real Google worker UTF-8 in both directions."); return 0;
    }
}
