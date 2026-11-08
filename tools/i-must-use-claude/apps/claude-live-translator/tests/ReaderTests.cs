using System;

// Pure string/JSON tests only. These tests never enumerate windows or inspect UI.
internal static class ReaderTests
{
    private static void Equal(string expected, string actual, string label)
    {
        if (!String.Equals(expected, actual, StringComparison.Ordinal))
            throw new Exception(label + " expected <" + expected + "> actual <" + actual + ">");
    }

    public static int Main()
    {
      try {
        Equal("8", ClaudeReader.MessageId("第 8 条消息，共 10 条消息"), "localized message id");
        Equal("12", ClaudeReader.MessageId("Message 12 of 20"), "English message id");
        Equal("43", ClaudeReader.MessageId("消息 43"), "Claude Code message id");
        Equal("44", ClaudeReader.MessageId("Message 44"), "English Claude Code message id");
        Equal("streaming", ClaudeReader.MessageId("正在流式传输消息"), "streaming id");
        if (!ClaudeReader.IsClaudeConversationUrl("https://claude.ai/epitaxy/local_f9b274ea-8352-4e34-9a09-18e51d84e555")) throw new Exception("Code conversation URL should be accepted");
        if (!ClaudeReader.IsClaudeConversationUrl("https://claude.ai/chat/12345678-1234-1234-1234-123456789abc")) throw new Exception("Chat conversation URL should be accepted");
        if (ClaudeReader.IsClaudeConversationUrl("https://claude.ai.evil.example/epitaxy/local_12345678-1234-1234-1234-123456789abc")) throw new Exception("Untrusted host should be rejected");
        Equal("code:f9b274ea-8352-4e34-9a09-18e51d84e555", ClaudeReader.ConversationIdFromUrl("https://claude.ai/epitaxy/local_f9b274ea-8352-4e34-9a09-18e51d84e555"), "Code ID is namespaced from standard chat");
        Equal("chat:12345678-1234-1234-1234-123456789abc", ClaudeReader.ConversationIdFromUrl("https://claude.ai/chat/12345678-1234-1234-1234-123456789abc"), "standard chat ID namespace");
        Equal(null, ClaudeReader.ConversationIdFromUrl("Codex conversation review - Claude Code"), "title is not a conversation ID");
        Equal("First paragraph\n\nSecond paragraph", ClaudeReader.CombineCodeReply("First paragraph", new[] {
            new ClaudeReader.ReplySegment { Kind = "group", Node = new ClaudeReader.CodeNode { Kind = "exclude", Text = "tool execution" } },
            new ClaudeReader.ReplySegment { Kind = "status", Node = new ClaudeReader.CodeNode { Kind = "exclude", Text = "running tool" } },
            new ClaudeReader.ReplySegment { Kind = "text", Node = new ClaudeReader.CodeNode { Kind = "inline", Text = "Second paragraph" } },
            new ClaudeReader.ReplySegment { Kind = "toolbar", Node = new ClaudeReader.CodeNode { Kind = "exclude", Text = "toolbar" } },
            new ClaudeReader.ReplySegment { Kind = "article", IsArticle = true },
            new ClaudeReader.ReplySegment { Kind = "text", Node = new ClaudeReader.CodeNode { Kind = "inline", Text = "later unrelated text" } }
        }), "segmented Code reply with tool/UI exclusion");
        Equal("Hello world\n\nParagraph\n\n```\nconst x = 1;\n```", ClaudeReader.RenderCodeNode(new ClaudeReader.CodeNode {
            Kind = "container", Children = new[] {
                new ClaudeReader.CodeNode { Kind = "inline", Text = "Hello" },
                new ClaudeReader.CodeNode { Kind = "inline", Text = "world" },
                new ClaudeReader.CodeNode { Kind = "exclude", Text = "tool secret must not leak", Children = new[] { new ClaudeReader.CodeNode { Kind = "inline", Text = "nested tool secret" } } },
                new ClaudeReader.CodeNode { Kind = "exclude", Text = "status secret" },
                new ClaudeReader.CodeNode { Kind = "exclude", Text = "button label" },
                new ClaudeReader.CodeNode { Kind = "block", Text = "Paragraph" },
                new ClaudeReader.CodeNode { Kind = "code", Children = new[] { new ClaudeReader.CodeNode { Kind = "inline", Text = "const x = 1;" } } }
            }
        }), "Code semantic rendering filters tool subtrees and fences code");
        Equal("First paragraph\n\nSecond paragraph", ClaudeReader.RenderCodeNode(new ClaudeReader.CodeNode {
            Kind = "container", Children = new[] {
                new ClaudeReader.CodeNode { Kind = "inline", Text = "First paragraph" },
                new ClaudeReader.CodeNode { Kind = "inline", Text = "Second paragraph", SeparatorBefore = ClaudeReader.ClassifyTextGap("\r\n") }
            }
        }), "TextPattern paragraph gap becomes double newline");
        Equal("Hello, world", ClaudeReader.RenderCodeNode(new ClaudeReader.CodeNode {
            Kind = "container", Children = new[] {
                new ClaudeReader.CodeNode { Kind = "inline", Text = "Hello" },
                new ClaudeReader.CodeNode { Kind = "inline", Text = ",", SeparatorBefore = ClaudeReader.ClassifyTextGap("") },
                new ClaudeReader.CodeNode { Kind = "inline", Text = "world", SeparatorBefore = ClaudeReader.ClassifyTextGap(" ") }
            }
        }), "same sentence Text Hyperlink Text gap");
        Equal("Run `x` now", ClaudeReader.RenderCodeNode(new ClaudeReader.CodeNode {
            Kind = "container", Children = new[] {
                new ClaudeReader.CodeNode { Kind = "inline", Text = "Run" },
                new ClaudeReader.CodeNode { Kind = "inline_code", Text = "x", SeparatorBefore = " " },
                new ClaudeReader.CodeNode { Kind = "inline", Text = "now", SeparatorBefore = " " }
            }
        }), "inline code stays in its sentence");
        Equal("First\n\nSecond", ClaudeReader.RenderCodeNode(new ClaudeReader.CodeNode {
            Kind = "container", Children = new[] {
                new ClaudeReader.CodeNode { Kind = "inline", Text = "First" },
                new ClaudeReader.CodeNode { Kind = "exclude", Text = "tool output" },
                new ClaudeReader.CodeNode { Kind = "inline", Text = "Second", SeparatorBefore = ClaudeReader.ClassifyTextGap("tool output") }
            }
        }), "filtered tool gap separates paragraphs without copying gap text");
        Equal("Item one\nItem two", ClaudeReader.RenderCodeNode(new ClaudeReader.CodeNode {
            Kind = "list", Children = new[] {
                new ClaudeReader.CodeNode { Kind = "listitem", Text = "Item one" },
                new ClaudeReader.CodeNode { Kind = "listitem", Text = "Item two" }
            }
        }), "list items remain separate lines");
        Equal("exclude", ClaudeReader.CodeKind(System.Windows.Automation.ControlType.Group, "group"), "tool groups are excluded");
        Equal("exclude", ClaudeReader.CodeKind(System.Windows.Automation.ControlType.StatusBar, "status"), "status subtrees are excluded");
        Equal("code", ClaudeReader.CodeKind(System.Windows.Automation.ControlType.Group, "code block"), "semantic code groups are retained");
        Equal("exclude", ClaudeReader.CodeKind(System.Windows.Automation.ControlType.Button, "code"), "buttons are excluded even when labeled code");
        Equal("answer", ClaudeReader.CleanCode("\uFFFCanswer\uFFFC"), "object replacement markers removed");
        ClaudeReader.GeometryRect clipped;
        if(!ClaudeReader.TryClipRect(new ClaudeReader.GeometryRect(-5,5,20,10),new ClaudeReader.GeometryRect(0,0,12,12),new ClaudeReader.GeometryRect(2,2,8,8),out clipped))throw new Exception("valid highlight should clip");
        Equal("{\"x\":2,\"y\":5,\"width\":8,\"height\":5}",ClaudeReader.RectJson(clipped),"clipped highlight coordinates");
        if(ClaudeReader.TryClipRect(new ClaudeReader.GeometryRect(20,20,2,2),new ClaudeReader.GeometryRect(0,0,10,10),new ClaudeReader.GeometryRect(0,0,10,10),out clipped))throw new Exception("outside highlight must be discarded");
        if(ClaudeReader.TryClipRect(new ClaudeReader.GeometryRect(Double.NaN,0,1,1),new ClaudeReader.GeometryRect(0,0,10,10),new ClaudeReader.GeometryRect(0,0,10,10),out clipped))throw new Exception("NaN highlight must be discarded");
        if(ClaudeReader.TryClipRect(new ClaudeReader.GeometryRect(0,0,20000000,1),new ClaudeReader.GeometryRect(0,0,10,10),new ClaudeReader.GeometryRect(0,0,10,10),out clipped))throw new Exception("abnormal highlight must be discarded");
        if(!ClaudeReader.HighlightCountAllowed(512)||ClaudeReader.HighlightCountAllowed(513))throw new Exception("highlight cap should fail closed");
        Equal("0",ClaudeReader.NormalizeRuntimeId(null).Length.ToString(),"missing runtime id is empty");
        Equal("0",ClaudeReader.NormalizeRuntimeId(new int[0]).Length.ToString(),"empty runtime id stays empty");
        Equal("7,8,9",String.Join(",",ClaudeReader.NormalizeRuntimeId(new[]{7,8,9})),"runtime id is preserved");
        Equal("0",ClaudeReader.NormalizeRuntimeId(new int[33]).Length.ToString(),"abnormal runtime id is rejected");
        if(!ClaudeReader.RuntimeAnchorCountAllowed(512)||ClaudeReader.RuntimeAnchorCountAllowed(513))throw new Exception("runtime anchor cap should fail closed");
        var visibleScore=ClaudeReader.ScoreVisibleRects(new[] {
            new ClaudeReader.GeometryRect(10,10,10,10),
            new ClaudeReader.GeometryRect(200,200,10,10) // excluded tool geometry lies outside the visible feed
        },new ClaudeReader.GeometryRect(0,0,100,100));
        Equal("100",visibleScore.VisibleArea.ToString("0"),"only visible body rects contribute area");
        Equal("0",ClaudeReader.ChooseCandidateIndex(new[] {
            new ClaudeReader.CandidateMetrics(2500,10), // visible older assistant
            new ClaudeReader.CandidateMetrics(0,Double.MaxValue) // screen-off latest assistant
        }).ToString(),"visible historical reply wins over offscreen newest");
        Equal("1",ClaudeReader.ChooseCandidateIndex(new[] {
            new ClaudeReader.CandidateMetrics(500,30),
            new ClaudeReader.CandidateMetrics(500,10),
            new ClaudeReader.CandidateMetrics(500,10)
        }).ToString(),"center tie-break is stable");
        Equal("-1",ClaudeReader.ChooseCandidateIndex(new[] { new ClaudeReader.CandidateMetrics(0,Double.MaxValue) }).ToString(),"no visible reply has no selection");
        Equal("Answer\nCopy the text", ClaudeReader.Clean("Claude 已回复：Answer\nCopy the text", "Claude 已回复："), "normal text containing copy");
        Equal("Answer\nAnswer", ClaudeReader.Clean("Claude 已回复：Answer\nAnswer", "Claude 已回复："), "repeated legitimate text");
        Equal("\"quote\\\" slash\\\\ newline\\n\"", ClaudeReader.Q("quote\" slash\\ newline\n"), "JSON escaping");
        Console.WriteLine("ReaderTests passed");
        return 0;
      } catch(Exception e) { Console.WriteLine("ReaderTests failed: "+e.Message); return 1; }
    }
}
