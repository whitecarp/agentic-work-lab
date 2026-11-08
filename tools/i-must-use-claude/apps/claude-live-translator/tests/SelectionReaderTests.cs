using System;

// Pure selection guard tests; no UIA windows, clipboard, or input are used.
internal static class SelectionReaderTests
{
    private static void Equal(string expected,string actual,string label) {
        if(!String.Equals(expected,actual,StringComparison.Ordinal))throw new Exception(label+" expected <"+expected+"> actual <"+actual+">");
    }
    public static int Main() {
        try {
            var selected=SelectionReader.GuardSelection("  selected words\r\n",false,true,1);
            Equal("true",selected.Ok.ToString().ToLowerInvariant(),"valid selected text");
            Equal("selected words",selected.Text,"trim selection boundaries");
            Equal("no_selection",SelectionReader.GuardSelection(" \r\n ",false,true,1).Status,"whitespace selection");
            Equal("",SelectionReader.GuardSelection("secret",true,true,1).Text,"password text is never returned");
            Equal("password_denied",SelectionReader.GuardSelection("secret",true,true,1).Status,"password rejected");
            Equal("foreground_changed",SelectionReader.GuardSelection("selected",false,false,1).Status,"changed foreground rejected");
            Equal("ambiguous_selection",SelectionReader.GuardSelection("selected",false,true,2).Status,"multiple ranges rejected");
            Equal("no_selection",SelectionReader.GuardSelection("selected",false,true,0).Status,"zero ranges rejected");
            Equal("text_too_long",SelectionReader.GuardSelection(new string('x',32001),false,true,1).Status,"long selection rejected");
            Equal("",SelectionReader.GuardSelection(new string('x',32001),false,true,1).Text,"long selection not returned");
            Equal("\"\\\"quoted\\\"\\n\"",SelectionReader.Q("\"quoted\"\n"),"JSON escaping");
            if(!SelectionReader.IsClaudeConversationUrl("https://claude.ai/epitaxy/local_f9b274ea-8352-4e34-9a09-18e51d84e555"))throw new Exception("Claude Code route detection");
            if(!SelectionReader.IsClaudeConversationUrl("https://claude.ai/chat/12345678-1234-1234-1234-123456789abc"))throw new Exception("Claude Chat route detection");
            if(SelectionReader.IsClaudeConversationUrl("https://claude.ai.evil.example/epitaxy/local_12345678-1234-1234-1234-123456789abc"))throw new Exception("Claude host must be exact");
            if(SelectionReader.IsClaudeSafeKind(System.Windows.Automation.ControlType.Group,"group"))throw new Exception("tool Group must be excluded");
            if(!SelectionReader.IsClaudeSafeKind(System.Windows.Automation.ControlType.Group,"code block"))throw new Exception("semantic code Group must be allowed");
            if(SelectionReader.IsClaudeSafeKind(System.Windows.Automation.ControlType.Button,"text"))throw new Exception("Button must be excluded");
            if(SelectionReader.IsClaudeSafeKind(System.Windows.Automation.ControlType.StatusBar,"status"))throw new Exception("Status must be excluded");
            if(!SelectionReader.IsClaudeTextLeaf(System.Windows.Automation.ControlType.ListItem,"list item"))throw new Exception("list item leaf must be collected");
            if(!SelectionReader.IsClaudeTextLeaf(System.Windows.Automation.ControlType.Text,"heading"))throw new Exception("heading text leaf must be collected");
            int start,end;
            if(!SelectionReader.TryIntersectionOffsets(10,20,5,15,out start,out end)||start!=10||end!=15)throw new Exception("selection is clipped to safe leaf bounds");
            if(SelectionReader.TryIntersectionOffsets(0,5,5,10,out start,out end))throw new Exception("touching ranges have empty intersection");
            int first,last;
            if(!SelectionReader.TryCandidateSpan(4,6,20,out first,out last)||first!=4||last!=6)throw new Exception("Claude feed traversal is restricted to selected endpoint children");
            if(SelectionReader.TryCandidateSpan(4,20,20,out first,out last))throw new Exception("out of bounds Claude child span is rejected");
            if(SelectionReader.TryCandidateSpan(6,4,20,out first,out last))throw new Exception("reversed Claude child span is rejected");
            Equal("\n\n",SelectionReader.ClassifyClaudeGap("hidden tool text"),"filtered gap becomes paragraph break");
            Equal(" ",SelectionReader.ClassifyClaudeGap("  "),"inline whitespace preserved");
            Equal("",SelectionReader.CleanClaudeText("\uFFFC"),"U+FFFC removed");
            Equal("First\n\nSecond",SelectionReader.ComposeClaudePieces(new[] {
                new SelectionReader.ClaudeTextPiece{Text="First"},
                new SelectionReader.ClaudeTextPiece{Text="Second",SeparatorBefore="\n\n"}
            }),"paragraph boundaries survive Claude filtering");
            Equal("https://example.test/a?q=1",SelectionReader.ComposeClaudePieces(new[] {
                new SelectionReader.ClaudeTextPiece{Text="https://example.test/a?q=1"}
            }),"URLs remain intact");
            Equal("Before\n\n```\nconst x = 1;\n```\n\nAfter",SelectionReader.ComposeClaudePieces(new[] {
                new SelectionReader.ClaudeTextPiece{Text="Before"},
                new SelectionReader.ClaudeTextPiece{Text="const x = 1;",Code=true},
                new SelectionReader.ClaudeTextPiece{Text="After",SeparatorBefore="\n\n"}
            }),"code remains fenced around selected intersections");
            Equal("已运行 1 个命令",SelectionReader.GuardSelection("已运行 1 个命令",false,true,1).Text,"quoted tool words are not blacklisted");
            Equal("complete selection",SelectionReader.ResolveSelections(new[] {
                new SelectionReader.SelectionResult{Ok=true,Status="selected",Text="complete selection"},
                new SelectionReader.SelectionResult{Ok=true,Status="selected",Text="complete selection"}
            }).Text,"identical provider selections deduplicate");
            Equal("ambiguous_selection",SelectionReader.ResolveSelections(new[] {
                new SelectionReader.SelectionResult{Ok=true,Status="selected",Text="first"},
                new SelectionReader.SelectionResult{Ok=true,Status="selected",Text="second"}
            }).Status,"independent provider selections remain ambiguous");
            Equal("ambiguous_selection",SelectionReader.ResolveSelections(new[] {
                new SelectionReader.SelectionResult{Ok=true,Status="selected",Text="selected tail"},
                new SelectionReader.SelectionResult{Ok=true,Status="selected",Text="tail"}
            }).Status,"containing but different provider selections are not assumed identical");
            Console.WriteLine("SelectionReaderTests passed");return 0;
        } catch(Exception e) { Console.WriteLine("SelectionReaderTests failed: "+e.Message);return 1; }
    }
}
