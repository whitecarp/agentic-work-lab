using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Automation;
using System.Windows.Automation.Text;

// Read-only helper for the existing UIA selection in the current foreground window.
internal static class SelectionReader
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint processId);
    [DllImport("user32.dll")] private static extern bool SetProcessDPIAware();
    private const int MaxTextLength=32000;
    private const int MaxTreeElements=1024;
    private const int MaxSelectableElements=128;
    private const int MaxPatternProbes=128;
    private const int MaxScanMilliseconds=500;
    private const int MaxClaudeTreeElements=8192;
    private const int MaxClaudeScanMilliseconds=1200;

    internal sealed class SelectionResult { public bool Ok; public string Status,Text,Route,Source,Phase,PointOutcome,PointTypes,FocusOutcome,FocusTypes,LastReadStatus; public int NodesScanned,PatternProviders,SelectionCandidates,FilterVisited,FilterPieces,PointAncestors,PointPatterns,PointRanges,PointLength,FocusAncestors,FocusPatterns,FocusRanges,FocusLength,ReadFailures; }
    internal struct ReadPoint { public double X,Y; public ReadPoint(double x,double y){X=x;Y=y;} }
    private sealed class AncestorProbe { public int Ancestors,Patterns,Ranges,Length; public string Status="not-tried",Types=""; }
    private sealed class ReadTrace { public AncestorProbe Point=new AncestorProbe(),Focus=new AncestorProbe(); }
    private sealed class ClaudeContext { public AutomationElement Document,Feed,Root; public TextPattern Pattern; public string Url; public bool Detected; public readonly List<ClaudeContext> Documents=new List<ClaudeContext>(); }
    private sealed class ScanState { public int Visited,Selectable,ReadFailures; public bool Limited; public readonly List<SelectionResult> Selections=new List<SelectionResult>(); public string Error,LastReadStatus; }
    private sealed class ClaudeFilterState { public int Visited,Length; public bool Limited; public string Error; public readonly List<SafePiece> Pieces=new List<SafePiece>(); }
    private sealed class SafePiece { public string Text; public bool Code,Block; public TextPatternRange Range; }
    internal sealed class ClaudeTextPiece { public string Text,SeparatorBefore; public bool Code,Block; }

    public static int Main(string[] args) {
        Console.OutputEncoding=new UTF8Encoding(false);
        try{SetProcessDPIAware();}catch{}
        if((args.Length!=2&&args.Length!=5)||args[0]!="--read"||(args.Length==5&&args[2]!="--point")) { Console.WriteLine("{\"ok\":false,\"status\":\"usage\",\"text\":\"\",\"sourceHandle\":null,\"sourceTitle\":\"\"}");return 2; }
        try { ReadPoint? point=null;if(args.Length==5){double x,y;if(!Double.TryParse(args[3],NumberStyles.Float,CultureInfo.InvariantCulture,out x)||!Double.TryParse(args[4],NumberStyles.Float,CultureInfo.InvariantCulture,out y)||Double.IsNaN(x)||Double.IsInfinity(x)||Double.IsNaN(y)||Double.IsInfinity(y)){Console.WriteLine(Result(false,"invalid_point","",null,""));return 0;}point=new ReadPoint(x,y);} Console.WriteLine(ReadJson(args[1],point));return 0; }
        catch(Exception e) { Console.WriteLine("{\"ok\":false,\"status\":\"error\",\"text\":\"\",\"sourceHandle\":null,\"sourceTitle\":\"\",\"error\":"+Q(e.GetType().Name)+"}");return 1; }
    }

    private static string ReadJson(string handleArg,ReadPoint? point) {
        IntPtr hwnd;
        if(!TryHandle(handleArg,out hwnd))return Result(false,"invalid_handle","",null,"");
        if(GetForegroundWindow()!=hwnd)return Result(false,"not_foreground","",Handle(hwnd),"");
        uint pid;GetWindowThreadProcessId(hwnd,out pid);if(pid==0)return Result(false,"window_not_found","",Handle(hwnd),"");
        AutomationElement root;
        try { root=AutomationElement.FromHandle(hwnd); }
        catch { return Result(false,"uia_read_failed","",Handle(hwnd),""); }
        if(GetForegroundWindow()!=hwnd)return Result(false,"foreground_changed","",Handle(hwnd),"");

        ClaudeContext claude=TryGetClaudeContext(hwnd,root);
        SelectionResult selection=ReadFocusedThenWindow(root,claude,point);
        if(GetForegroundWindow()!=hwnd)return Result(false,"foreground_changed","",Handle(hwnd),"");
        return Result(selection.Ok,selection.Status,selection.Text,Handle(hwnd),"",selection);
    }

    private static SelectionResult ReadFocusedThenWindow(AutomationElement root,ClaudeContext claude,ReadPoint? point) {
        var trace=new ReadTrace();
        if(point.HasValue) {
            try { AutomationElement atPoint=AutomationElement.FromPoint(new System.Windows.Point(point.Value.X,point.Value.Y)); if(atPoint==null)trace.Point.Status="element-not-found";else if(!IsWithinWindow(atPoint,root))trace.Point.Status="outside-window";else {SelectionResult fromPoint=ReadAncestorSelection(atPoint,root,claude,trace.Point);if(fromPoint!=null)return AttachTrace(SetDiagnostics(fromPoint,claude,"point",0,trace.Point.Patterns,1),trace);}} catch(Exception e){trace.Point.Status="frompoint-"+e.GetType().Name;}
        }
        AutomationElement focused=null;try{focused=AutomationElement.FocusedElement;}catch{}
        if(focused==null)trace.Focus.Status="element-not-found";else if(IsWithinWindow(focused,root)){SelectionResult fromFocus=ReadAncestorSelection(focused,root,claude,trace.Focus);if(fromFocus!=null)return AttachTrace(SetDiagnostics(fromFocus,claude,"focus",0,trace.Focus.Patterns,1),trace);}else trace.Focus.Status="outside-window";

        var state=new ScanState();var watch=Stopwatch.StartNew();
        AutomationElement child;try{child=TreeWalker.ControlViewWalker.GetFirstChild(root);}catch{return AttachTrace(SetDiagnostics(Empty("uia_read_failed"),claude,"window",state.Visited,state.Selectable,0),trace);}
        while(child!=null) {
            if(state.Visited>=MaxTreeElements||watch.ElapsedMilliseconds>MaxScanMilliseconds){state.Limited=true;break;}
            ScanSelectableTree(child,state,watch,0,claude);
            if(state.Limited)break;
            try{child=TreeWalker.ControlViewWalker.GetNextSibling(child);}catch{state.Error="uia_read_failed";break;}
        }
        SelectionResult result;
        if(state.Limited)result=Empty("scan_limit");
        else if(state.Error!=null)result=Empty(state.Error);
        else if(state.Selections.Count==0)result=Empty("no_selection");
        else if(state.Selections.Count>1)result=ResolveSelections(state.Selections);
        else result=state.Selections[0];
        result.ReadFailures=state.ReadFailures;result.LastReadStatus=state.LastReadStatus;
        return AttachTrace(SetDiagnostics(result,claude,"window",state.Visited,state.Selectable,state.Selections.Count),trace);
    }
    private static SelectionResult ReadAncestorSelection(AutomationElement start,AutomationElement root,ClaudeContext claude,AncestorProbe probe) {
        AutomationElement current=start;
        for(int depth=0;current!=null&&depth<40;depth++) {
            SelectionResult found=ReadElementSelection(current,claude,probe);
            probe.Status=found.Status;
            if(found.Status!="no_selection"&&found.Status!="pattern_unavailable")return found;
            if(current.Equals(root))break;
            try{current=TreeWalker.RawViewWalker.GetParent(current);}catch{break;}
        }
        if(probe.Status=="not-tried")probe.Status="ancestor-end";
        return null;
    }

    private static void ScanSelectableTree(AutomationElement element,ScanState state,Stopwatch watch,int depth,ClaudeContext claude) {
        if(state.Limited||depth>80)return;
        if(state.Visited++>=MaxTreeElements||watch.ElapsedMilliseconds>MaxScanMilliseconds){state.Limited=true;return;}
        ControlType type;try{type=element.Current.ControlType;}catch{return;}
        bool hasTextPattern=false;object textPattern;
        if(IsSelectionProviderType(type)&&state.Selectable<MaxPatternProbes)try{hasTextPattern=element.TryGetCurrentPattern(TextPattern.Pattern,out textPattern);}catch{}
        if(hasTextPattern) {
            if(state.Selectable++>=MaxSelectableElements){state.Limited=true;return;}
            SelectionResult result=ReadElementSelection(element,claude);
            if(!result.Ok&&result.Status!="no_selection"&&result.Status!="pattern_unavailable"){state.ReadFailures++;state.LastReadStatus=result.Status;}
            if(result.Status!="password_denied") {
                if(result.Status=="ambiguous_selection"){state.Error="ambiguous_selection";return;}
                if(result.Status=="text_too_long"){state.Error="text_too_long";return;}
                if(result.Ok&&!ContainsSelection(state.Selections,result.Text))state.Selections.Add(result);
                if(result.Ok&&type==ControlType.Document)return; // A Document selection covers its text subtree; avoid scanning its descendants.
            }
        }
        if(state.Limited||state.Error!=null)return;
        AutomationElement child;try{child=TreeWalker.ControlViewWalker.GetFirstChild(element);}catch{return;}
        while(child!=null) {
            ScanSelectableTree(child,state,watch,depth+1,claude);
            if(state.Limited||state.Error!=null)return;
            try{child=TreeWalker.ControlViewWalker.GetNextSibling(child);}catch{state.Error="uia_read_failed";return;}
        }
    }
    private static bool IsSelectionProviderType(ControlType type){return type==ControlType.Document||type==ControlType.Edit||type==ControlType.Pane||type==ControlType.Custom||type==ControlType.Group||type==ControlType.Text||type==ControlType.List;}
    private static SelectionResult SetDiagnostics(SelectionResult result,ClaudeContext claude,string source,int nodes,int providers,int candidates){result.Route=claude==null?"generic":"claude-safe";result.Source=source;result.NodesScanned=nodes;result.PatternProviders=providers;result.SelectionCandidates=candidates;if(String.IsNullOrEmpty(result.Phase))result.Phase=source;return result;}
    private static SelectionResult AttachTrace(SelectionResult result,ReadTrace trace){result.PointOutcome=trace.Point.Status;result.PointTypes=trace.Point.Types;result.PointAncestors=trace.Point.Ancestors;result.PointPatterns=trace.Point.Patterns;result.PointRanges=trace.Point.Ranges;result.PointLength=trace.Point.Length;result.FocusOutcome=trace.Focus.Status;result.FocusTypes=trace.Focus.Types;result.FocusAncestors=trace.Focus.Ancestors;result.FocusPatterns=trace.Focus.Patterns;result.FocusRanges=trace.Focus.Ranges;result.FocusLength=trace.Focus.Length;return result;}
    private static bool ContainsSelection(IList<SelectionResult> selections,string text){foreach(SelectionResult item in selections)if(String.Equals(item.Text,text,StringComparison.Ordinal))return true;return false;}
    internal static SelectionResult ResolveSelections(IList<SelectionResult> selections) {
        if(selections==null||selections.Count==0)return Empty("no_selection");
        SelectionResult first=selections[0];
        foreach(SelectionResult item in selections)if(!String.Equals(first.Text,item.Text,StringComparison.Ordinal))return Empty("ambiguous_selection");
        return first;
    }

    private static SelectionResult ReadElementSelection(AutomationElement element,ClaudeContext claude,AncestorProbe probe=null) {
        if(probe!=null){probe.Ancestors++;try{string type=element.Current.ControlType.ProgrammaticName??"unknown";if(probe.Types.Length<500)probe.Types+=(probe.Types.Length==0?"":"|")+type;}catch{probe.Types+="|unknown";}}
        bool password;try{password=element.Current.IsPassword;}catch{return Empty("pattern_unavailable");}
        if(password)return Empty("password_denied");
        object pattern;try{if(!element.TryGetCurrentPattern(TextPattern.Pattern,out pattern))return Empty("pattern_unavailable");}
        catch{return Empty("pattern_unavailable");}
        if(probe!=null)probe.Patterns++;
        TextPatternRange[] ranges;try{ranges=((TextPattern)pattern).GetSelection();}catch{return Empty("pattern_unavailable");}
        if(probe!=null)probe.Ranges+=ranges==null?0:ranges.Length;
        if(ranges==null||ranges.Length==0)return Empty("no_selection");
        if(ranges.Length!=1)return Empty("ambiguous_selection");
        if(claude!=null){if(probe!=null){try{probe.Length=ranges[0].GetText(MaxTextLength+1).Length;}catch{}}ClaudeContext scoped=GetClaudeContextForSource(element,claude);if(scoped==null)return Empty("claude_document_not_found");return ReadClaudeSelection(element,(TextPattern)pattern,ranges[0],scoped);}
        string text;try{text=ranges[0].GetText(MaxTextLength+1);}catch{return Empty("uia_read_failed");}
        if(probe!=null)probe.Length=text==null?0:text.Length;
        return GuardSelection(text,false,true,1);
    }

    private static ClaudeContext TryGetClaudeContext(IntPtr hwnd,AutomationElement root) {
        ClaudeContext detected=new ClaudeContext();
        try {
            uint pid;GetWindowThreadProcessId(hwnd,out pid);if(pid==0)return null;
            using(Process process=Process.GetProcessById((int)pid))if(!String.Equals(process.ProcessName,"claude",StringComparison.OrdinalIgnoreCase))return null;
        } catch{return null;}
        detected.Detected=true;detected.Root=root;
        try {
            foreach(AutomationElement e in root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Document))) {
                string candidate="";try{object p;if(e.TryGetCurrentPattern(ValuePattern.Pattern,out p))candidate=((ValuePattern)p).Current.Value??"";}catch{}
                if(!IsClaudeConversationUrl(candidate))continue;
                AutomationElement feed=null;
                try{foreach(AutomationElement child in e.FindAll(TreeScope.Descendants,Condition.TrueCondition))if(Has(SafeName(child),"对话消息","Conversation messages")){feed=child;break;}}catch{feed=null;}
                object pattern=null;try{e.TryGetCurrentPattern(TextPattern.Pattern,out pattern);}catch{}
                detected.Documents.Add(new ClaudeContext{Document=e,Feed=feed,Pattern=pattern as TextPattern,Url=candidate});
            }
            return detected;
        }catch{return detected;}
    }
    private static ClaudeContext GetClaudeContextForSource(AutomationElement source,ClaudeContext detected) {
        if(detected==null||!detected.Detected)return null;
        AutomationElement current=source;
        for(int depth=0;current!=null&&depth<96;depth++) {
            foreach(ClaudeContext candidate in detected.Documents)if(current.Equals(candidate.Document))return candidate;
            if(current.Equals(detected.Root))break;
            try{current=TreeWalker.RawViewWalker.GetParent(current);}catch{return null;}
        }
        return null;
    }
    internal static bool IsClaudeConversationUrl(string value) {
        return Regex.IsMatch(value??"",@"^https://claude\.ai/(?:chat/[0-9a-z-]{20,}|epitaxy/local_[0-9a-z-]{20,})(?:[/?#]|$)",RegexOptions.IgnoreCase);
    }
    private static SelectionResult ReadClaudeSelection(AutomationElement source,TextPattern sourcePattern,TextPatternRange sourceRange,ClaudeContext context) {
        if(context==null||context.Document==null||context.Feed==null)return Empty("claude_filter_unavailable");
        if(IsWithinWindow(context.Feed,source))return FilterClaudeSelection(sourcePattern,sourceRange,context.Feed);
        if(context.Pattern==null)return Empty("claude_filter_unavailable");
        try { TextPatternRange[] selected=context.Pattern.GetSelection();if(selected==null||selected.Length!=1)return Empty(selected!=null&&selected.Length>1?"ambiguous_selection":"no_selection");return FilterClaudeSelection(context.Pattern,selected[0],context.Feed); }
        catch{return Empty("claude_filter_unavailable");}
    }
    private static SelectionResult FilterClaudeSelection(TextPattern pattern,TextPatternRange selection,AutomationElement feed) {
        var state=new ClaudeFilterState();var watch=Stopwatch.StartNew();
        try {
            AutomationElement[] children=feed.FindAll(TreeScope.Children,Condition.TrueCondition).Cast<AutomationElement>().ToArray();
            int first=0,last=children.Length-1;
            int localFirst,localLast;
            if(TryGetClaudeChildSpan(selection,feed,children,out localFirst,out localLast)){first=localFirst;last=localLast;}
            for(int i=first;i<=last;i++) {
                if(watch.ElapsedMilliseconds>MaxClaudeScanMilliseconds){state.Limited=true;break;}
                AutomationElement child=children[i];
                bool article=IsArticle(child);
                WalkClaudeNode(pattern,selection,child,state,watch,0,false,false,article);
                if(state.Limited)break;
            }
        }catch{return Empty("uia_read_failed");}
        if(state.Limited)return ClaudeFilterResult(Empty("scan_limit"),state);
        if(state.Error!=null)return ClaudeFilterResult(Empty(state.Error),state);
        if(state.Pieces.Count==0)return ClaudeFilterResult(Empty("no_selection"),state);
        string text=RenderSafePieces(state.Pieces);
        return ClaudeFilterResult(GuardSelection(text,false,true,1),state);
    }
    private static bool TryGetClaudeChildSpan(TextPatternRange selection,AutomationElement feed,AutomationElement[] children,out int first,out int last) {
        first=last=-1;
        try {
            TextPatternRange startRange=selection.Clone();startRange.MoveEndpointByRange(TextPatternRangeEndpoint.End,selection,TextPatternRangeEndpoint.Start);
            TextPatternRange endRange=selection.Clone();endRange.MoveEndpointByRange(TextPatternRangeEndpoint.Start,selection,TextPatternRangeEndpoint.End);
            AutomationElement start=startRange.GetEnclosingElement(),end=endRange.GetEnclosingElement();
            int a=GetFeedChildIndex(start,feed,children),b=GetFeedChildIndex(end,feed,children);
            if(a<0||b<a)return false;first=a;last=b;return true;
        }catch{return false;}
    }
    private static int GetFeedChildIndex(AutomationElement element,AutomationElement feed,AutomationElement[] children) {
        AutomationElement current=element;
        for(int depth=0;current!=null&&depth<96;depth++) {
            AutomationElement parent;try{parent=TreeWalker.RawViewWalker.GetParent(current);}catch{return -1;}
            if(parent==null)return -1;
            if(parent.Equals(feed)){for(int i=0;i<children.Length;i++)if(children[i].Equals(current))return i;return -1;}
            current=parent;
        }
        return -1;
    }
    internal static bool TryCandidateSpan(int start,int end,int count,out int first,out int last) {first=last=-1;if(count<0||start<0||end<start||end>=count)return false;first=start;last=end;return true;}
    private static SelectionResult ClaudeFilterResult(SelectionResult result,ClaudeFilterState state){result.Phase="claude-filter";result.FilterVisited=state.Visited;result.FilterPieces=state.Pieces.Count;return result;}
    private static void WalkClaudeNode(TextPattern pattern,TextPatternRange selection,AutomationElement element,ClaudeFilterState state,Stopwatch watch,int depth,bool inCode,bool inBlock,bool articleRoot) {
        if(depth>80||state.Limited)return;
        if(++state.Visited>MaxClaudeTreeElements||watch.ElapsedMilliseconds>MaxClaudeScanMilliseconds){state.Limited=true;return;}
        ControlType type;try{type=element.Current.ControlType;}catch{return;}
        string name=SafeName(element),semantic=GetSemantic(element);
        if(IsAuthorHeading(name)||Has(name,"消息操作","Message actions"))return;
        bool code=IsCodeSemantic(semantic);
        if(!articleRoot&&!IsClaudeSafeKind(type,semantic))return;
        TextPatternRange elementRange=null;
        try { elementRange=pattern.RangeFromChild(element); if(!RangesIntersect(selection,elementRange))return; } catch{}
        var kids=new List<AutomationElement>();try{foreach(AutomationElement child in element.FindAll(TreeScope.Children,Condition.TrueCondition))kids.Add(child);}catch{}
        if(IsClaudeTextLeaf(type,semantic)&&kids.Count==0) {
            TextPatternRange intersection=IntersectRanges(selection,elementRange);
            if(intersection!=null) {
                string text;try{text=CleanClaudeText(intersection.GetText(MaxTextLength+1));}catch{return;}
                if(text.Length>0) {
                    state.Length+=text.Length;if(state.Length>MaxTextLength){state.Error="text_too_long";return;}
                    state.Pieces.Add(new SafePiece{Text=text,Code=code||inCode,Block=IsBlockSemantic(semantic)||inBlock,Range=intersection});
                }
            }
            return;
        }
        foreach(AutomationElement child in kids)WalkClaudeNode(pattern,selection,child,state,watch,depth+1,inCode||code,inBlock||IsBlockSemantic(semantic),false);
    }
    internal static bool IsClaudeTextLeaf(ControlType type,string semantic) {
        semantic=(semantic??"").ToLowerInvariant();
        return type==ControlType.Text||type==ControlType.Hyperlink||type==ControlType.ListItem||type==ControlType.DataItem||type==ControlType.Header||semantic.Contains("heading")||semantic.Contains("标题")||semantic.Contains("paragraph")||semantic.Contains("段落")||semantic.Contains("list item")||semantic.Contains("列表项")||IsCodeSemantic(semantic);
    }
    private static string GetSemantic(AutomationElement element) {
        string role=SafeLocalizedType(element),aria="",id="";
        try{object v=element.GetCurrentPropertyValue(AutomationProperty.LookupById(30101),true);if(v!=AutomationElement.NotSupported&&v!=null)aria=v.ToString();}catch{}
        try{object v=element.GetCurrentPropertyValue(AutomationElement.AutomationIdProperty,true);if(v!=AutomationElement.NotSupported&&v!=null)id=v.ToString();}catch{}
        return (role+" "+aria+" "+id).ToLowerInvariant();
    }
    internal static bool IsClaudeSafeKind(ControlType type,string semantic) {
        semantic=(semantic??"").ToLowerInvariant();
        if(type==ControlType.Button||type==ControlType.StatusBar||type==ControlType.ProgressBar||type==ControlType.ToolBar)return false;
        if(type==ControlType.Group)return IsCodeSemantic(semantic);
        return true;
    }
    private static bool IsCodeSemantic(string semantic){semantic=(semantic??"").ToLowerInvariant();return semantic.Contains("code")||semantic.Contains("preformatted")||semantic.Contains("code block")||semantic.Contains("代码块");}
    private static bool IsBlockSemantic(string semantic){semantic=(semantic??"").ToLowerInvariant();return semantic.Contains("paragraph")||semantic.Contains("段落")||semantic.Contains("heading")||semantic.Contains("标题")||semantic.Contains("list item")||semantic.Contains("列表项");}
    private static bool IsAuthorHeading(string name){return (name??"").StartsWith("您说：",StringComparison.Ordinal)|| (name??"").StartsWith("You said:",StringComparison.OrdinalIgnoreCase)||(name??"").StartsWith("Claude 已回复：",StringComparison.Ordinal)||(name??"").StartsWith("Claude responded:",StringComparison.OrdinalIgnoreCase);}
    private static bool IsArticle(AutomationElement e){string role=SafeLocalizedType(e),aria="",name=SafeName(e);try{object v=e.GetCurrentPropertyValue(AutomationProperty.LookupById(30101),true);if(v!=AutomationElement.NotSupported&&v!=null)aria=v.ToString();}catch{}return String.Equals(role,"article",StringComparison.OrdinalIgnoreCase)||String.Equals(role,"文章",StringComparison.OrdinalIgnoreCase)||String.Equals(aria,"article",StringComparison.OrdinalIgnoreCase)||Regex.IsMatch(name,@"^(?:第\s*\d+\s*条消息|消息\s*\d+|Message\s+\d+(?:\s+of\s+\d+)?|streaming message|正在流式传输消息)",RegexOptions.IgnoreCase);}
    private static bool RangesIntersect(TextPatternRange selected,TextPatternRange leaf){try{return selected.CompareEndpoints(TextPatternRangeEndpoint.Start,leaf,TextPatternRangeEndpoint.End)<0&&selected.CompareEndpoints(TextPatternRangeEndpoint.End,leaf,TextPatternRangeEndpoint.Start)>0;}catch{return false;}}
    private static TextPatternRange IntersectRanges(TextPatternRange selected,TextPatternRange leaf) {
        try {
            TextPatternRange intersection=leaf.Clone();
            if(selected.CompareEndpoints(TextPatternRangeEndpoint.Start,leaf,TextPatternRangeEndpoint.Start)>0)intersection.MoveEndpointByRange(TextPatternRangeEndpoint.Start,selected,TextPatternRangeEndpoint.Start);
            if(selected.CompareEndpoints(TextPatternRangeEndpoint.End,leaf,TextPatternRangeEndpoint.End)<0)intersection.MoveEndpointByRange(TextPatternRangeEndpoint.End,selected,TextPatternRangeEndpoint.End);
            return intersection.CompareEndpoints(TextPatternRangeEndpoint.Start,intersection,TextPatternRangeEndpoint.End)<0?intersection:null;
        }catch{return null;}
    }
    private static string RenderSafePieces(IList<SafePiece> pieces) {
        var output=new List<ClaudeTextPiece>();TextPatternRange previous=null;SafePiece previousPiece=null;
        foreach(SafePiece piece in pieces) {
            string separator="";
            if(previous!=null) {
                string gap="";try{TextPatternRange g=previous.Clone();g.MoveEndpointByRange(TextPatternRangeEndpoint.Start,previous,TextPatternRangeEndpoint.End);g.MoveEndpointByRange(TextPatternRangeEndpoint.End,piece.Range,TextPatternRangeEndpoint.Start);gap=g.GetText(4096);}catch{}
                separator=ClassifyClaudeGap(gap);
                if(separator.Length==0&&(piece.Block||(previousPiece!=null&&previousPiece.Block)))separator="\n\n";
            }
            output.Add(new ClaudeTextPiece{Text=piece.Text,SeparatorBefore=separator,Code=piece.Code,Block=piece.Block});previous=piece.Range;previousPiece=piece;
        }
        return ComposeClaudePieces(output);
    }
    internal static string ComposeClaudePieces(IList<ClaudeTextPiece> pieces) {
        if(pieces==null)return "";var b=new StringBuilder();bool inCode=false;ClaudeTextPiece previous=null;
        foreach(ClaudeTextPiece piece in pieces) {
            if(piece==null||String.IsNullOrEmpty(piece.Text))continue;string separator=piece.SeparatorBefore??"";
            if(piece.Code&&!inCode){if(b.Length>0)b.Append("\n\n");b.Append("```\n");inCode=true;separator="";}
            else if(!piece.Code&&inCode){b.Append("\n```");inCode=false;if(separator.Length==0)separator=piece.Block?"\n\n":" ";}
            else if(separator.Length==0&&previous!=null&&(piece.Block||previous.Block))separator="\n\n";
            if(piece.Code&&separator.Length>0)separator=separator.Replace("\n\n","\n");
            b.Append(separator).Append(piece.Text);previous=piece;
        }
        if(inCode)b.Append("\n```");return b.ToString().Trim();
    }
    internal static string CleanClaudeText(string text){return (text??"").Replace("\uFFFC","");}
    internal static string ClassifyClaudeGap(string gap){gap=CleanClaudeText(gap??"");if(gap.IndexOf('\n')>=0||gap.IndexOf('\r')>=0)return "\n\n";if(String.IsNullOrWhiteSpace(gap))return gap.Length==0?"":" ";return "\n\n";}
    private static bool IsWithinWindow(AutomationElement element,AutomationElement root) {
        AutomationElement current=element;
        for(int depth=0;current!=null&&depth<64;depth++) {
            try{if(current.Equals(root))return true;current=TreeWalker.RawViewWalker.GetParent(current);}catch{return false;}
        }
        return false;
    }
    private static SelectionResult Empty(string status){return new SelectionResult{Ok=false,Status=status,Text=""};}
    internal static bool TryIntersectionOffsets(int selectionStart,int selectionEnd,int leafStart,int leafEnd,out int start,out int end){start=end=0;if(selectionStart>selectionEnd||leafStart>leafEnd)return false;start=Math.Max(selectionStart,leafStart);end=Math.Min(selectionEnd,leafEnd);return end>start;}
    private static bool Has(string value,params string[] terms){foreach(string term in terms)if((value??"").IndexOf(term,StringComparison.OrdinalIgnoreCase)>=0)return true;return false;}
    private static string SafeName(AutomationElement element){try{return element.Current.Name??"";}catch{return "";}}
    private static string SafeLocalizedType(AutomationElement element){try{return element.Current.LocalizedControlType??"";}catch{return "";}}
    internal static SelectionResult GuardSelection(string raw,bool password,bool foregroundStillSame,int rangeCount) {
        if(!foregroundStillSame)return Empty("foreground_changed");
        if(password)return Empty("password_denied");
        if(rangeCount>1)return Empty("ambiguous_selection");
        if(rangeCount<=0)return Empty("no_selection");
        string text=(raw??"").Trim();
        if(text.Length==0)return Empty("no_selection");
        if(text.Length>MaxTextLength)return Empty("text_too_long");
        return new SelectionResult{Ok=true,Status="selected",Text=text};
    }
    private static bool TryHandle(string value,out IntPtr hwnd) {
        hwnd=IntPtr.Zero;try{long raw=value.StartsWith("0x",StringComparison.OrdinalIgnoreCase)?Convert.ToInt64(value.Substring(2),16):Int64.Parse(value,CultureInfo.InvariantCulture);hwnd=new IntPtr(raw);return hwnd!=IntPtr.Zero;}catch{return false;}
    }
    private static string Handle(IntPtr hwnd){return "0x"+hwnd.ToInt64().ToString("X",CultureInfo.InvariantCulture);}
    private static string Result(bool ok,string status,string text,string handle,string title){return "{\"ok\":"+(ok?"true":"false")+",\"status\":"+Q(status)+",\"text\":"+Q(ok?text:"")+",\"sourceHandle\":"+(handle==null?"null":Q(handle))+",\"sourceTitle\":\"\",\"diagnostics\":{\"route\":\"unresolved\",\"source\":\"\",\"phase\":\"unresolved\",\"nodesScanned\":0,\"patternProviders\":0,\"selectionCandidates\":0,\"filterVisited\":0,\"filterPieces\":0,\"pointOutcome\":\"not-tried\",\"pointTypes\":\"\",\"pointAncestors\":0,\"pointPatterns\":0,\"pointRanges\":0,\"pointLength\":0,\"focusOutcome\":\"not-tried\",\"focusTypes\":\"\",\"focusAncestors\":0,\"focusPatterns\":0,\"focusRanges\":0,\"focusLength\":0,\"readFailures\":0,\"lastReadStatus\":\"\"}}";}
    private static string Result(bool ok,string status,string text,string handle,string title,SelectionResult diagnostic){return "{\"ok\":"+(ok?"true":"false")+",\"status\":"+Q(status)+",\"text\":"+Q(ok?text:"")+",\"sourceHandle\":"+(handle==null?"null":Q(handle))+",\"sourceTitle\":\"\",\"diagnostics\":{\"route\":"+Q(diagnostic.Route??"unknown")+",\"source\":"+Q(diagnostic.Source??"")+",\"phase\":"+Q(diagnostic.Phase??"unknown")+",\"nodesScanned\":"+diagnostic.NodesScanned+",\"patternProviders\":"+diagnostic.PatternProviders+",\"selectionCandidates\":"+diagnostic.SelectionCandidates+",\"filterVisited\":"+diagnostic.FilterVisited+",\"filterPieces\":"+diagnostic.FilterPieces+",\"pointOutcome\":"+Q(diagnostic.PointOutcome??"not-tried")+",\"pointTypes\":"+Q(diagnostic.PointTypes??"")+",\"pointAncestors\":"+diagnostic.PointAncestors+",\"pointPatterns\":"+diagnostic.PointPatterns+",\"pointRanges\":"+diagnostic.PointRanges+",\"pointLength\":"+diagnostic.PointLength+",\"focusOutcome\":"+Q(diagnostic.FocusOutcome??"not-tried")+",\"focusTypes\":"+Q(diagnostic.FocusTypes??"")+",\"focusAncestors\":"+diagnostic.FocusAncestors+",\"focusPatterns\":"+diagnostic.FocusPatterns+",\"focusRanges\":"+diagnostic.FocusRanges+",\"focusLength\":"+diagnostic.FocusLength+",\"readFailures\":"+diagnostic.ReadFailures+",\"lastReadStatus\":"+Q(diagnostic.LastReadStatus??"")+"}}";}
    internal static string Q(string value){if(value==null)return "null";var b=new StringBuilder("\"");foreach(char c in value){switch(c){case '"':b.Append("\\\"");break;case '\\':b.Append("\\\\");break;case '\n':b.Append("\\n");break;case '\r':b.Append("\\r");break;case '\t':b.Append("\\t");break;default:if(c<32)b.Append("\\u").Append(((int)c).ToString("x4"));else b.Append(c);break;}}return b.Append('"').ToString();}
}
