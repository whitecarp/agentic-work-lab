using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Automation;
using System.Windows.Automation.Text;

// Read-only Claude Desktop UIA reader. No input injection, configuration access, or persistence.
internal static class ClaudeReader
{
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr extra);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hwnd,out NativeRect rect);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd,out NativeRect rect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hwnd,ref NativePoint point);
    [DllImport("user32.dll")] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);
    private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr extra);
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X,Y; }
    internal struct GeometryRect { public double X,Y,Width,Height; public GeometryRect(double x,double y,double width,double height){X=x;Y=y;Width=width;Height=height;} }
    private const int MaxHighlightRects=512;
    private const double MaxGeometryCoordinate=10000000;

    private sealed class WindowInfo { public IntPtr Handle; public string Title; }
    private sealed class Candidate { public AutomationElement Article,Heading; public string HeadingName; public bool Streaming; public IList<AutomationElement> BodyElements; public CandidateMetrics Metrics; }
    internal struct CandidateMetrics { public double VisibleArea,CenterDistance; public CandidateMetrics(double visibleArea,double centerDistance){VisibleArea=visibleArea;CenterDistance=centerDistance;} }

    public static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        try
        {
            if (args.Length == 1 && args[0] == "--list") { Console.WriteLine(ListJson()); return 0; }
            if (args.Length == 2 && args[0] == "--read") { Console.WriteLine(ReadJson(args[1])); return 0; }
            if (args.Length == 2 && args[0] == "--debug") { Console.WriteLine(DebugJson(args[1])); return 0; }
            Console.WriteLine("{\"ok\":false,\"status\":\"usage\",\"error\":\"Use --list, --read <handle>, or --debug <handle>\"}"); return 2;
        }
        catch (Exception e)
        {
            Console.WriteLine("{\"ok\":false,\"conversation\":null,\"messageId\":null,\"text\":\"\",\"streaming\":false,\"status\":\"error\",\"highlights\":[],\"windowBounds\":null,\"error\":" + Q(e.GetType().Name) + "}");
            return 1;
        }
    }

    private static List<WindowInfo> EnumerateClaudeWindows()
    {
        var result = new List<WindowInfo>();
        EnumWindows(delegate(IntPtr h, IntPtr x) {
            if (!IsWindowVisible(h)) return true;
            uint pid; GetWindowThreadProcessId(h, out pid);
            try {
                using (Process p = Process.GetProcessById((int)pid)) {
                    if (!String.Equals(p.ProcessName, "claude", StringComparison.OrdinalIgnoreCase)) return true;
                }
                AutomationElement el = AutomationElement.FromHandle(h);
                string title = el.Current.Name ?? "";
                if (title.Length > 0) result.Add(new WindowInfo { Handle = h, Title = title });
            } catch { }
            return true;
        }, IntPtr.Zero);
        return result;
    }

    private static string DebugJson(string handleArg) {
        AutomationElement root; IntPtr hwnd; string error; if(!GetClaudeRoot(handleArg,out root,out hwnd,out error)) return "{\"ok\":false,\"status\":"+Q(error)+"}";
        var counts = new Dictionary<string,int>(); var named = new Dictionary<string,int>(); int visited=0;
        var all=root.FindAll(TreeScope.Descendants,Condition.TrueCondition);
        foreach(AutomationElement e in all) {
            if(++visited>10000) break;
            string type=SafeLocalizedType(e); if(String.IsNullOrEmpty(type)) type="unknown";
            int n; counts.TryGetValue(type,out n); counts[type]=n+1;
            if(!String.IsNullOrEmpty(SafeName(e))) { named.TryGetValue(type,out n); named[type]=n+1; }
        }
        var b=new StringBuilder("{\"ok\":true,\"structure\":["); bool first=true;
        foreach(KeyValuePair<string,int> kv in counts) { if(!first)b.Append(','); first=false; int nameCount; named.TryGetValue(kv.Key,out nameCount); b.Append("{\"type\":").Append(Q(kv.Key)).Append(",\"count\":").Append(kv.Value).Append(",\"nameCount\":").Append(nameCount).Append('}'); }
        return b.Append("],\"truncated\":").Append(visited>10000?"true":"false").Append('}').ToString();
    }

    private static bool GetClaudeRoot(string handleArg,out AutomationElement root,out IntPtr hwnd,out string error) {
        root=null; hwnd=IntPtr.Zero; error="window_not_found"; long raw;
        if(handleArg.StartsWith("0x",StringComparison.OrdinalIgnoreCase)) raw=Convert.ToInt64(handleArg.Substring(2),16); else raw=Int64.Parse(handleArg,CultureInfo.InvariantCulture);
        hwnd=new IntPtr(raw); uint pid; GetWindowThreadProcessId(hwnd,out pid); if(pid==0)return false;
        using(Process proc=Process.GetProcessById((int)pid)) if(!String.Equals(proc.ProcessName,"claude",StringComparison.OrdinalIgnoreCase)){error="not_claude";return false;}
        root=AutomationElement.FromHandle(hwnd); error=null; return true;
    }

    private static string ListJson()
    {
        var list = EnumerateClaudeWindows(); var b = new StringBuilder("{\"windows\":[");
        for (int i=0;i<list.Count;i++) { if(i>0)b.Append(','); b.Append("{\"handle\":").Append(Q("0x"+list[i].Handle.ToInt64().ToString("X",CultureInfo.InvariantCulture))).Append(",\"title\":").Append(Q(list[i].Title)).Append('}'); }
        return b.Append("]}").ToString();
    }

    private static string ReadJson(string handleArg)
    {
        AutomationElement root; IntPtr hwnd; string error; if(!GetClaudeRoot(handleArg,out root,out hwnd,out error)) return Failure(error);
        AutomationElement doc = FindClaudeDocument(root);
        if (doc == null) return Failure("conversation_document_not_found");
        AutomationElement feed = FindFeed(doc);
        if (feed == null) return Failure("message_feed_not_found");
        var articles = FindArticles(feed);
        if (articles.Count == 0) return Failure("messages_not_found");
        string conversationUrl = DocumentValueUrl(doc);
        bool codeMode=IsClaudeCodeUrl(conversationUrl);
        Candidate selected=FindVisibleAssistant(doc,feed,articles,codeMode,hwnd);
        if(selected==null) {
            AutomationElement latest=articles[articles.Count-1], latestHeading=FindHeading(latest);
            string latestHeadingName=latestHeading==null?"":SafeName(latestHeading), latestName=SafeName(latest);
            bool latestStreaming=Has(latestName,"正在流式传输消息","streaming message");
            bool latestUser=Has(latestHeadingName,"您说：","You said:");
            return Result(false,ConversationId(doc),MessageId(latestName),"",latestStreaming,latestUser?"waiting":"visible_reply_not_found");
        }
        AutomationElement last=selected.Article, heading=selected.Heading;
        string articleName=SafeName(last), headingName=selected.HeadingName;
        bool streaming=selected.Streaming;
        bool omitHeading=HasAssistantHeading(headingName);
        AutomationElement excludedHeading=omitHeading?heading:null;
        var highlightElements=new List<AutomationElement>(selected.BodyElements);
        string text = codeMode ? ExtractCodeArticle(doc,last,headingName,excludedHeading,null) : ExtractArticleText(doc, last, headingName);
        // Claude Code may expose the first paragraph inside the Article, with the
        // remaining visible answer as direct Text siblings of that Article.
        if (codeMode) text = AppendFollowingReplyText(doc,feed, last, text,excludedHeading,null);
        if (codeMode) text = CleanCode(text);
        if (String.IsNullOrWhiteSpace(text)) return Result(false, ConversationId(doc), MessageId(articleName), "", streaming, "message_text_unavailable");
        if (text.Length > 32000) return Result(false, ConversationId(doc), MessageId(articleName), "", streaming, "message_too_long_split_and_paste");
        GeometryRect windowBounds; IList<GeometryRect> highlights=BuildHighlights(hwnd,doc,feed,highlightElements,out windowBounds);
        int[][] highlightAnchors=GetRuntimeAnchors(highlightElements);
        int[] documentRuntimeId=SafeRuntimeId(doc),articleRuntimeId=SafeRuntimeId(last);
        return Result(true, ConversationId(doc), MessageId(articleName), text, streaming, streaming ? "streaming" : "complete",windowBounds,highlights,highlightAnchors,documentRuntimeId,articleRuntimeId,SafeRuntimeId(feed),conversationUrl);
    }

    private static AutomationElement FindClaudeDocument(AutomationElement root)
    {
        foreach (AutomationElement e in root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document))) {
            string n=SafeName(e); string url=DocumentValueUrl(e);
            if (IsClaudeConversationUrl(url) || IsClaudeConversationUrl(n)) return e;
        }
        return null;
    }
    internal static bool IsClaudeConversationUrl(string value) { string id; bool code; return TryParseConversationUrl(value,out id,out code); }
    internal static bool TryParseConversationUrl(string value,out string id,out bool code) {
        id=null; code=false; Match m=Regex.Match(value??"", @"^https://claude\.ai/(chat/([0-9a-f-]{20,})|epitaxy/local_([0-9a-f-]{20,}))(?:[/?#]|$)", RegexOptions.IgnoreCase);
        if(!m.Success)return false; code=m.Groups[3].Success; id=m.Groups[code?3:2].Value; return true;
    }
    private static bool IsClaudeCodeUrl(string value) { string id; bool code; return TryParseConversationUrl(value,out id,out code)&&code; }
    private static string DocumentValueUrl(AutomationElement doc) { string value=""; object p; try{if(doc.TryGetCurrentPattern(ValuePattern.Pattern,out p))value=((ValuePattern)p).Current.Value??"";}catch{} return value; }
    private static AutomationElement FindFeed(AutomationElement doc) {
        foreach(AutomationElement e in doc.FindAll(TreeScope.Descendants,Condition.TrueCondition)) if(Has(SafeName(e),"对话消息","Conversation messages")) return e;
        return null;
    }
    private static readonly Regex LocalizedArticleName = new Regex(@"^(?:第\s*\d+\s*条消息，共\s*\d+\s*条(?:消息)?|消息\s*\d+)$", RegexOptions.Compiled);
    private static readonly Regex EnglishArticleName = new Regex(@"^Message\s+\d+(?:\s+of\s+\d+)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static bool IsArticle(AutomationElement e) {
        string role = SafeLocalizedType(e); string aria = SafeAriaRole(e); string name = SafeName(e);
        return String.Equals(role,"文章",StringComparison.OrdinalIgnoreCase) || String.Equals(role,"article",StringComparison.OrdinalIgnoreCase) ||
               String.Equals(aria,"article",StringComparison.OrdinalIgnoreCase) ||
               LocalizedArticleName.IsMatch(name) || EnglishArticleName.IsMatch(name) ||
               String.Equals(name,"正在流式传输消息",StringComparison.OrdinalIgnoreCase) || String.Equals(name,"streaming message",StringComparison.OrdinalIgnoreCase);
    }
    private static List<AutomationElement> FindArticles(AutomationElement feed) {
        var result = new List<AutomationElement>();
        AutomationElementCollection all = feed.FindAll(TreeScope.Descendants, Condition.TrueCondition);
        foreach (AutomationElement e in all) if (IsArticle(e) && !HasArticleAncestor(e, feed)) result.Add(e);
        return result;
    }
    private static bool HasArticleAncestor(AutomationElement e, AutomationElement feed) {
        try { AutomationElement p = TreeWalker.ControlViewWalker.GetParent(e); while(p != null && !p.Equals(feed)) { if(IsArticle(p)) return true; p = TreeWalker.ControlViewWalker.GetParent(p); } } catch {}
        return false;
    }
    private static AutomationElement FindHeading(AutomationElement e) {
        AutomationElementCollection all = e.FindAll(TreeScope.Descendants, Condition.TrueCondition);
        AutomationElement roleHeading = null;
        foreach(AutomationElement n in all) {
            string name = SafeName(n), role = SafeLocalizedType(n), aria = SafeAriaRole(n);
            if (StartsWithAny(name,"Claude 已回复：","您说：","Claude responded:","You said:")) return n;
            if(roleHeading==null && (String.Equals(role,"heading",StringComparison.OrdinalIgnoreCase) || String.Equals(aria,"heading",StringComparison.OrdinalIgnoreCase))) roleHeading=n;
        }
        return roleHeading;
    }
    private static bool StartsWithAny(string s, params string[] prefixes) { foreach(string prefix in prefixes) if(s.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)) return true; return false; }
    private static bool HasAssistantHeading(string heading) { return StartsWithAny(heading??"","Claude 已回复：","Claude responded:"); }
    private static Candidate FindVisibleAssistant(AutomationElement doc,AutomationElement feed,IList<AutomationElement> articles,bool codeMode,IntPtr hwnd) {
        GeometryRect windowBounds,clientBounds,feedBounds;
        if(!TryGetWindowAndClientBounds(hwnd,out windowBounds,out clientBounds))return null;
        try { System.Windows.Rect r=feed.Current.BoundingRectangle; feedBounds=new GeometryRect(r.X,r.Y,r.Width,r.Height); } catch{return null;}
        GeometryRect viewport; if(!TryIntersect(feedBounds,clientBounds,out viewport))return null;
        var candidates=new List<Candidate>();
        foreach(AutomationElement article in articles) {
            string articleName=SafeName(article); bool streaming=Has(articleName,"正在流式传输消息","streaming message");
            AutomationElement heading=FindHeading(article); string headingName=heading==null?"":SafeName(heading);
            bool assistant=Has(headingName,"Claude 已回复：","Claude responded:")||streaming;
            bool user=Has(headingName,"您说：","You said:"); if(!assistant||user)continue;
            if(!IsPotentiallyVisible(article,feed,viewport,clientBounds,codeMode))continue;
            AutomationElement excluded=HasAssistantHeading(headingName)?heading:null;
            var elements=new List<AutomationElement>();
            if(codeMode)CollectCodeCandidateElements(doc,feed,article,excluded,elements);
            else CollectChatHighlights(article,excluded,elements,0);
            CandidateMetrics metrics=ScoreVisibleElements(doc,elements,viewport,clientBounds);
            if(metrics.VisibleArea<=0)continue;
            candidates.Add(new Candidate{Article=article,Heading=heading,HeadingName=headingName,Streaming=streaming,BodyElements=elements,Metrics=metrics});
        }
        int best=ChooseCandidateIndex(GetMetrics(candidates)); return best<0?null:candidates[best];
    }
    private static bool IsPotentiallyVisible(AutomationElement article,AutomationElement feed,GeometryRect viewport,GeometryRect clientBounds,bool codeMode) {
        if(ElementMayIntersect(article,viewport,clientBounds))return true;
        if(!codeMode)return false;
        AutomationElementCollection children;try{children=feed.FindAll(TreeScope.Children,Condition.TrueCondition);}catch{return true;}
        int index=-1;for(int i=0;i<children.Count;i++)if(children[i].Equals(article)){index=i;break;}
        if(index<0)return true;
        for(int i=index+1;i<children.Count;i++){AutomationElement e=children[i];if(IsArticle(e))break;if(ElementMayIntersect(e,viewport,clientBounds))return true;}
        return false;
    }
    private static bool ElementMayIntersect(AutomationElement element,GeometryRect viewport,GeometryRect clientBounds) {
        try { System.Windows.Rect r=element.Current.BoundingRectangle; GeometryRect clipped; return TryClipRect(new GeometryRect(r.X,r.Y,r.Width,r.Height),viewport,clientBounds,out clipped); } catch{return false;}
    }
    private static IList<CandidateMetrics> GetMetrics(IList<Candidate> candidates) { var list=new List<CandidateMetrics>(); foreach(Candidate c in candidates)list.Add(c.Metrics);return list; }
    private static void CollectCodeCandidateElements(AutomationElement doc,AutomationElement feed,AutomationElement article,AutomationElement excludedHeading,IList<AutomationElement> elements) {
        ReadCodeChildren(doc,article,0,"\n\n",excludedHeading,elements);
        AutomationElementCollection children=feed.FindAll(TreeScope.Children,Condition.TrueCondition); int index=-1;
        for(int i=0;i<children.Count;i++)if(children[i].Equals(article)){index=i;break;}
        if(index<0)return;
        for(int i=index+1;i<children.Count;i++) {
            AutomationElement e=children[i]; string name=SafeName(e); ControlType type; try{type=e.Current.ControlType;}catch{break;}
            if(IsArticle(e))break;
            if(Has(name,"消息操作","Message actions")||(type==ControlType.Group&&ContainsMessageToolbar(e)))break;
            ReadCodeNode(doc,e,0,excludedHeading,elements);
        }
    }
    private static CandidateMetrics ScoreVisibleElements(AutomationElement doc,IList<AutomationElement> elements,GeometryRect viewport,GeometryRect clientBounds) {
        if(elements==null||elements.Count==0)return new CandidateMetrics(0,Double.MaxValue);
        object pattern; try{if(!doc.TryGetCurrentPattern(TextPattern.Pattern,out pattern))return new CandidateMetrics(0,Double.MaxValue);}catch{return new CandidateMetrics(0,Double.MaxValue);}
        var visible=new List<GeometryRect>(); var seen=new HashSet<string>(StringComparer.Ordinal);
        foreach(AutomationElement element in elements)try {
            System.Windows.Rect[] rects=((TextPattern)pattern).RangeFromChild(element).GetBoundingRectangles(); if(rects==null)continue;
            foreach(System.Windows.Rect r in rects) {
                GeometryRect clipped; if(!TryClipRect(new GeometryRect(r.X,r.Y,r.Width,r.Height),viewport,clientBounds,out clipped))continue;
                string key=FormatNumber(clipped.X)+","+FormatNumber(clipped.Y)+","+FormatNumber(clipped.Width)+","+FormatNumber(clipped.Height);if(!seen.Add(key))continue;
                visible.Add(clipped);
            }
        } catch{}
        return ScoreVisibleRects(visible,viewport);
    }
    private static bool TryIntersect(GeometryRect a,GeometryRect b,out GeometryRect result) {
        result=new GeometryRect(); if(!ValidRect(a)||!ValidRect(b))return false;
        double x=Math.Max(a.X,b.X),y=Math.Max(a.Y,b.Y),right=Math.Min(a.X+a.Width,b.X+b.Width),bottom=Math.Min(a.Y+a.Height,b.Y+b.Height);
        if(right<=x||bottom<=y)return false;result=new GeometryRect(x,y,right-x,bottom-y);return ValidRect(result);
    }
    internal static int ChooseCandidateIndex(IList<CandidateMetrics> candidates) {
        if(candidates==null)return -1;int best=-1;
        for(int i=0;i<candidates.Count;i++) {
            CandidateMetrics m=candidates[i]; if(!Finite(m.VisibleArea)||m.VisibleArea<=0||!Finite(m.CenterDistance))continue;
            if(best<0||m.VisibleArea>candidates[best].VisibleArea||(m.VisibleArea==candidates[best].VisibleArea&&m.CenterDistance<candidates[best].CenterDistance))best=i;
        }
        return best;
    }
    internal static CandidateMetrics ScoreVisibleRects(IList<GeometryRect> rects,GeometryRect viewport) {
        double area=0,weightedDistance=0,cx=viewport.X+viewport.Width/2,cy=viewport.Y+viewport.Height/2;
        if(rects==null||!ValidRect(viewport))return new CandidateMetrics(0,Double.MaxValue);
        foreach(GeometryRect rect in rects){GeometryRect clipped;if(!TryClipRect(rect,viewport,viewport,out clipped))continue;double a=clipped.Width*clipped.Height;area+=a;double dx=clipped.X+clipped.Width/2-cx,dy=clipped.Y+clipped.Height/2-cy;weightedDistance+=a*(Math.Abs(dx)+Math.Abs(dy));}
        return new CandidateMetrics(area,area>0?weightedDistance/area:Double.MaxValue);
    }
    private static bool IsWithinElement(AutomationElement element,AutomationElement ancestor) {
        if(element==null||ancestor==null)return false;
        try { AutomationElement current=element; while(current!=null){if(current.Equals(ancestor))return true;current=TreeWalker.RawViewWalker.GetParent(current);} } catch{}
        return false;
    }
    private static void CollectChatHighlights(AutomationElement element,AutomationElement excludedHeading,IList<AutomationElement> output,int depth) {
        if(element==null||depth>60||IsWithinElement(element,excludedHeading)||output.Count>MaxHighlightRects)return;
        ControlType type; try{type=element.Current.ControlType;}catch{return;}
        string name=SafeName(element);
        if(type==ControlType.Button||type==ControlType.ToolBar||type==ControlType.StatusBar||type==ControlType.ProgressBar||Has(name,"消息操作","Message actions"))return;
        if((type==ControlType.Text||type==ControlType.Hyperlink)&&!String.IsNullOrWhiteSpace(name)){output.Add(element);return;}
        try{foreach(AutomationElement child in element.FindAll(TreeScope.Children,Condition.TrueCondition))CollectChatHighlights(child,excludedHeading,output,depth+1);}catch{}
    }
    private static string ExtractArticleText(AutomationElement doc,AutomationElement article,string heading) {
        object p; try { if(doc.TryGetCurrentPattern(TextPattern.Pattern,out p)) {
            TextPatternRange range = ((TextPattern)p).RangeFromChild(article);
            AutomationElement toolbar = FindToolbar(article);
            if(toolbar != null) { TextPatternRange toolbarRange = ((TextPattern)p).RangeFromChild(toolbar); range.MoveEndpointByRange(TextPatternRangeEndpoint.End,toolbarRange,TextPatternRangeEndpoint.Start); }
            return Clean(range.GetText(32001),heading);
        } } catch {}
        var parts=new List<string>(); CollectLeaves(article,parts,0);
        return Clean(String.Join("\n",parts.ToArray()),heading);
    }
    private static string ExtractCodeArticle(AutomationElement doc,AutomationElement article,string heading,AutomationElement headingElement,IList<AutomationElement> highlights) {
        CodeNode node=ReadCodeChildren(doc,article,0,"\n\n",headingElement,highlights);
        return Clean(RenderCodeNode(node),heading);
    }
    private static string AppendFollowingReplyText(AutomationElement doc,AutomationElement feed,AutomationElement article,string initial,AutomationElement headingElement,IList<AutomationElement> highlights) {
        try {
            AutomationElementCollection children=feed.FindAll(TreeScope.Children,Condition.TrueCondition); int index=-1;
            for(int i=0;i<children.Count;i++) if(children[i].Equals(article)){index=i;break;}
            if(index<0)return initial;
            var following=new List<ReplySegment>();
            for(int i=index+1;i<children.Count;i++) {
                AutomationElement e=children[i]; string name=SafeName(e); ControlType type; try{type=e.Current.ControlType;}catch{break;}
                bool isArticle=IsArticle(e);
                bool toolbarBoundary=Has(name,"消息操作","Message actions") || (type==ControlType.Group && ContainsMessageToolbar(e));
                CodeNode node=toolbarBoundary?null:ReadCodeNode(doc,e,0,headingElement,highlights);
                if(node!=null)node.SeparatorBefore=GetRangeSeparator(doc,article,e,"\n\n");
                following.Add(new ReplySegment{Kind=toolbarBoundary?"toolbar":"node",Node=node,IsArticle=isArticle});
                if(node!=null)article=e;
                if(isArticle)break;
                if(toolbarBoundary)break;
            }
            return CombineCodeReply(initial,following);
        } catch { return initial; }
    }
    private static bool ContainsMessageToolbar(AutomationElement group) {
        try { foreach(AutomationElement e in group.FindAll(TreeScope.Descendants,Condition.TrueCondition)) if(Has(SafeName(e),"消息操作","Message actions"))return true; } catch {} return false;
    }
    internal sealed class ReplySegment { public string Kind; public CodeNode Node; public bool IsArticle; }
    internal static string CombineCodeReply(string initial,IList<ReplySegment> following) {
        var root=new CodeNode{Kind="container",Children=new List<CodeNode>()};
        if(!String.IsNullOrWhiteSpace(initial))root.Children.Add(new CodeNode{Kind="block",Text=initial});
        if(following!=null)foreach(ReplySegment item in following) {
            if(item==null)continue; if(item.IsArticle || String.Equals(item.Kind,"toolbar",StringComparison.OrdinalIgnoreCase))break;
            if(item.Node!=null)root.Children.Add(item.Node);
        }
        return RenderCodeNode(root);
    }
    internal sealed class CodeNode { public string Kind,Text,SeparatorBefore; public IList<CodeNode> Children; }
    private static CodeNode ReadCodeChildren(AutomationElement doc,AutomationElement parent,int depth,string fallbackSeparator,AutomationElement headingElement,IList<AutomationElement> highlights) {
        var children=new List<CodeNode>(); AutomationElement previous=null;
        try { foreach(AutomationElement child in parent.FindAll(TreeScope.Children,Condition.TrueCondition)) {
            CodeNode n=ReadCodeNode(doc,child,depth+1,headingElement,highlights);
            if(n==null)continue;
            n.SeparatorBefore=previous==null?null:GetRangeSeparator(doc,previous,child,fallbackSeparator);
            children.Add(n); previous=child;
        } } catch{}
        return new CodeNode{Kind="container",Children=children};
    }
    private static CodeNode ReadCodeNode(AutomationElement doc,AutomationElement element,int depth,AutomationElement headingElement,IList<AutomationElement> highlights) {
        if(depth>50||IsWithinElement(element,headingElement))return null;
        ControlType type; try{type=element.Current.ControlType;}catch{return null;}
        string role=SafeLocalizedType(element), aria=SafeAriaRole(element), automationId="";
        try { object v=element.GetCurrentPropertyValue(AutomationElement.AutomationIdProperty,true); if(v!=AutomationElement.NotSupported&&v!=null)automationId=v.ToString(); } catch{}
        string semantic=(role+" "+aria+" "+automationId).ToLowerInvariant();
        string kind=CodeKind(type,semantic);
        if(kind=="exclude")return null;
        string value="";
        if(type==ControlType.Text||type==ControlType.Hyperlink) value=SafeName(element);
        string childFallback=kind=="list"||kind=="code"?"\n":(kind=="block"?" ":" ");
        int before=highlights==null?0:highlights.Count;
        CodeNode descendants=ReadCodeChildren(doc,element,depth,childFallback,headingElement,highlights);
        var children=descendants.Children;
        if(highlights!=null) {
            if((type==ControlType.Text||type==ControlType.Hyperlink)&&children.Count==0&&!String.IsNullOrWhiteSpace(value)&&highlights.Count<=MaxHighlightRects)highlights.Add(element);
            else if(kind=="code"&&highlights.Count==before&&highlights.Count<=MaxHighlightRects)highlights.Add(element);
        }
        return new CodeNode{Kind=kind,Text=value,Children=children};
    }
    internal static string CodeKind(ControlType type,string semantic) {
        semantic=(semantic??"").ToLowerInvariant();
        bool code=semantic.Contains("code")||semantic.Contains("preformatted")||semantic.Contains("codeblock")||semantic.Contains("code block");
        if(type==ControlType.Group&&code)return "code";
        if(type==ControlType.Group||type==ControlType.StatusBar||type==ControlType.ProgressBar||type==ControlType.Button||type==ControlType.ToolBar)return "exclude";
        if(semantic.Contains("inline code")||semantic.Contains("inlinecode")||semantic.Contains("内联代码"))return "inline_code";
        if(code)return "code";
        if(type==ControlType.List)return "list";
        if(type==ControlType.ListItem)return "listitem";
        if(semantic.Contains("paragraph")||semantic.Contains("段落")||semantic.Contains("heading")||semantic.Contains("标题")||type==ControlType.DataItem||type==ControlType.Table)return "block";
        if(type==ControlType.Text||type==ControlType.Hyperlink)return "inline";
        return "container";
    }
    internal static string RenderCodeNode(CodeNode node) {
        if(node==null||node.Kind=="exclude")return "";
        string content=String.IsNullOrWhiteSpace(node.Text)?"":node.Text.Trim(); string previousKind=null;
        if(node.Children!=null)foreach(CodeNode child in node.Children){string rendered=RenderCodeNode(child);if(String.IsNullOrWhiteSpace(rendered))continue; string separator=child.SeparatorBefore??DefaultCodeSeparator(node.Kind,previousKind,child.Kind); content=String.IsNullOrEmpty(content)?rendered:content+separator+rendered; previousKind=child.Kind;}
        content=content.Trim();
        if(node.Kind=="code")return String.IsNullOrWhiteSpace(content)?"":"```\n"+content+"\n```";
        if(node.Kind=="inline_code")return String.IsNullOrWhiteSpace(content)?"":"`"+content+"`";
        return content;
    }
    internal static string DefaultCodeSeparator(string parentKind,string previousKind,string nextKind) {
        if(parentKind=="code")return "\n";
        if(parentKind=="list"||previousKind=="listitem"||nextKind=="listitem")return "\n";
        if(previousKind=="block"||previousKind=="code"||nextKind=="block"||nextKind=="code"||nextKind=="list")return "\n\n";
        return " ";
    }
    internal static string ClassifyTextGap(string gap) {
        gap=(gap??"").Replace("\uFFFC","");
        if(gap.IndexOf('\n')>=0||gap.IndexOf('\r')>=0)return "\n\n";
        if(String.IsNullOrWhiteSpace(gap))return gap.Length==0?"":" ";
        return "\n\n";
    }
    private static string GetRangeSeparator(AutomationElement doc,AutomationElement previous,AutomationElement next,string fallback) {
        try {
            object pattern; if(!doc.TryGetCurrentPattern(TextPattern.Pattern,out pattern))return fallback;
            TextPattern text=(TextPattern)pattern; TextPatternRange left=text.RangeFromChild(previous),right=text.RangeFromChild(next),gap=left.Clone();
            gap.MoveEndpointByRange(TextPatternRangeEndpoint.Start,left,TextPatternRangeEndpoint.End);
            gap.MoveEndpointByRange(TextPatternRangeEndpoint.End,right,TextPatternRangeEndpoint.Start);
            return ClassifyTextGap(gap.GetText(4096));
        } catch { return fallback; }
    }
    internal static bool TryClipRect(GeometryRect rect,GeometryRect feed,GeometryRect window,out GeometryRect clipped) {
        clipped=new GeometryRect(); if(!ValidRect(rect)||!ValidRect(feed)||!ValidRect(window))return false;
        double left=Math.Max(rect.X,Math.Max(feed.X,window.X)),top=Math.Max(rect.Y,Math.Max(feed.Y,window.Y));
        double right=Math.Min(rect.X+rect.Width,Math.Min(feed.X+feed.Width,window.X+window.Width));
        double bottom=Math.Min(rect.Y+rect.Height,Math.Min(feed.Y+feed.Height,window.Y+window.Height));
        if(right<=left||bottom<=top)return false;
        clipped=new GeometryRect(left,top,right-left,bottom-top); return ValidRect(clipped);
    }
    private static bool ValidRect(GeometryRect r) {
        return Finite(r.X)&&Finite(r.Y)&&Finite(r.Width)&&Finite(r.Height)&&r.Width>0&&r.Height>0&&Math.Abs(r.X)<=MaxGeometryCoordinate&&Math.Abs(r.Y)<=MaxGeometryCoordinate&&r.Width<=MaxGeometryCoordinate&&r.Height<=MaxGeometryCoordinate&&Math.Abs(r.X+r.Width)<=MaxGeometryCoordinate&&Math.Abs(r.Y+r.Height)<=MaxGeometryCoordinate;
    }
    private static bool Finite(double v) { return !Double.IsNaN(v)&&!Double.IsInfinity(v); }
    private static bool TryGetWindowAndClientBounds(IntPtr hwnd,out GeometryRect windowBounds,out GeometryRect clientBounds) {
        windowBounds=new GeometryRect(); clientBounds=new GeometryRect(); IntPtr previous=IntPtr.Zero;
        try {
            previous=SetThreadDpiAwarenessContext(new IntPtr(-4)); if(previous==IntPtr.Zero)return false;
            NativeRect w,r; if(!GetWindowRect(hwnd,out w)||w.Right<=w.Left||w.Bottom<=w.Top)return false;
            if(!GetClientRect(hwnd,out r)||r.Right<=r.Left||r.Bottom<=r.Top)return false;
            NativePoint a=new NativePoint{X=r.Left,Y=r.Top},b=new NativePoint{X=r.Right,Y=r.Bottom};
            if(!ClientToScreen(hwnd,ref a)||!ClientToScreen(hwnd,ref b))return false;
            windowBounds=new GeometryRect(w.Left,w.Top,w.Right-w.Left,w.Bottom-w.Top);
            clientBounds=new GeometryRect(a.X,a.Y,b.X-a.X,b.Y-a.Y); return ValidRect(windowBounds)&&ValidRect(clientBounds);
        } catch { return false; }
        finally { if(previous!=IntPtr.Zero)try{SetThreadDpiAwarenessContext(previous);}catch{} }
    }
    private static IList<GeometryRect> BuildHighlights(IntPtr hwnd,AutomationElement doc,AutomationElement feed,IList<AutomationElement> elements,out GeometryRect windowBounds) {
        windowBounds=new GeometryRect(); var result=new List<GeometryRect>();
        if(elements==null)return result;
        GeometryRect clientBounds; if(!TryGetWindowAndClientBounds(hwnd,out windowBounds,out clientBounds))return result;
        if(elements.Count>MaxHighlightRects)return result;
        if(elements.Count==0)return result;
        GeometryRect feedBounds; try { System.Windows.Rect r=feed.Current.BoundingRectangle; feedBounds=new GeometryRect(r.X,r.Y,r.Width,r.Height); } catch { return result; }
        if(!ValidRect(feedBounds))return result;
        object pattern; try{if(!doc.TryGetCurrentPattern(TextPattern.Pattern,out pattern))return result;}catch{return result;}
        var seen=new HashSet<string>(StringComparer.Ordinal);
        foreach(AutomationElement element in elements) {
            try {
                TextPatternRange range=((TextPattern)pattern).RangeFromChild(element); System.Windows.Rect[] raw=range.GetBoundingRectangles();
                if(raw==null||raw.Length==0)continue;
                for(int i=0;i<raw.Length;i++) {
                    GeometryRect clipped; if(!TryClipRect(new GeometryRect(raw[i].X,raw[i].Y,raw[i].Width,raw[i].Height),feedBounds,clientBounds,out clipped))continue;
                    string key=FormatNumber(clipped.X)+","+FormatNumber(clipped.Y)+","+FormatNumber(clipped.Width)+","+FormatNumber(clipped.Height);
                    if(!seen.Add(key))continue;
                    result.Add(clipped); if(result.Count>MaxHighlightRects)return new List<GeometryRect>();
                }
            } catch {}
        }
        return result;
    }
    private static string FormatNumber(double value) { return value.ToString("0.###",CultureInfo.InvariantCulture); }
    private static void AppendRect(StringBuilder b,GeometryRect r) { b.Append("{\"x\":").Append(FormatNumber(r.X)).Append(",\"y\":").Append(FormatNumber(r.Y)).Append(",\"width\":").Append(FormatNumber(r.Width)).Append(",\"height\":").Append(FormatNumber(r.Height)).Append('}'); }
    internal static bool HighlightCountAllowed(int count) { return count>=0&&count<=MaxHighlightRects; }
    internal static string RectJson(GeometryRect rect) { var b=new StringBuilder(); AppendRect(b,rect); return b.ToString(); }
    internal static int[] NormalizeRuntimeId(int[] runtimeId) {
        if(runtimeId==null||runtimeId.Length==0||runtimeId.Length>32)return new int[0];
        return (int[])runtimeId.Clone();
    }
    private static int[] SafeRuntimeId(AutomationElement element) { try{return NormalizeRuntimeId(element.GetRuntimeId());}catch{return new int[0];} }
    private static int[][] GetRuntimeAnchors(IList<AutomationElement> elements) {
        if(elements==null||elements.Count==0||!RuntimeAnchorCountAllowed(elements.Count))return new int[0][];
        var ids=new int[elements.Count][]; for(int i=0;i<elements.Count;i++)ids[i]=SafeRuntimeId(elements[i]); return ids;
    }
    internal static bool RuntimeAnchorCountAllowed(int count) { return count>=0&&count<=MaxHighlightRects; }
    internal static string CombineReplyText(IList<string> parts) {
        var clean=new List<string>(); foreach(string part in parts)if(!String.IsNullOrWhiteSpace(part))clean.Add(part.Trim());
        return Clean(String.Join("\n",clean.ToArray()),"");
    }
    internal static string CleanCode(string text) { return Clean((text??"").Replace("\uFFFC",""),""); }
    private static AutomationElement FindToolbar(AutomationElement article) {
        foreach(AutomationElement e in article.FindAll(TreeScope.Descendants,Condition.TrueCondition)) if(Has(SafeName(e),"消息操作","Message actions")) return e;
        return null;
    }
    private static void CollectLeaves(AutomationElement e,List<string> parts,int depth) {
        if(depth>40)return; AutomationElementCollection kids; try { kids=e.FindAll(TreeScope.Children,Condition.TrueCondition); } catch{return;}
        string name=SafeName(e); ControlType type; try { type=e.Current.ControlType; } catch{return;}
        if(type==ControlType.ToolBar || type==ControlType.Button || type==ControlType.StatusBar || type==ControlType.ProgressBar || Has(name,"消息操作","Message actions"))return;
        if(kids.Count==0) { if(type==ControlType.Text || type==ControlType.ListItem || type==ControlType.DataItem || type==ControlType.Hyperlink) AddPart(parts,name); return; }
        if(type==ControlType.ListItem || type==ControlType.DataItem) AddPart(parts,name);
        foreach(AutomationElement child in kids) CollectLeaves(child,parts,depth+1);
    }
    private static void AddPart(List<string> parts,string s) { if(!String.IsNullOrWhiteSpace(s)) parts.Add(s.Trim()); }
    internal static string Clean(string s,string heading) { if(s==null)return ""; s=s.Replace("\r\n","\n").Replace('\r','\n');
        if(!String.IsNullOrEmpty(heading)) { string trimmed=s.TrimStart(); if(trimmed.StartsWith(heading,StringComparison.Ordinal)) { int offset=s.Length-trimmed.Length; s=s.Remove(offset,heading.Length); } }
        return Regex.Replace(s,@"\n{3,}","\n\n").Trim(); }
    internal static string ConversationIdFromUrl(string value) { string id; bool code; return TryParseConversationUrl(value,out id,out code)?(code?"code:":"chat:")+id:null; }
    private static string ConversationId(AutomationElement doc) { return ConversationIdFromUrl(DocumentValueUrl(doc)); }
    internal static string MessageId(string name) { Match m=Regex.Match(name??"",@"(?:第\s*)?(\d+)(?:\s*条消息|\s+of\s+\d+)|^消息\s*(\d+)$|^Message\s*(\d+)$",RegexOptions.IgnoreCase); return m.Success?(m.Groups[1].Success?m.Groups[1].Value:(m.Groups[2].Success?m.Groups[2].Value:m.Groups[3].Value)):(Has(name??"","streaming message","正在流式传输消息")?"streaming":null); }
    private static bool Has(string s,params string[] terms){foreach(string t in terms)if(s.IndexOf(t,StringComparison.OrdinalIgnoreCase)>=0)return true;return false;}
    private static string SafeName(AutomationElement e){try{return e.Current.Name??"";}catch{return "";}}
    private static string SafeLocalizedType(AutomationElement e){try{return e.Current.LocalizedControlType??"";}catch{return "";}}
    private static string SafeAriaRole(AutomationElement e){try{AutomationProperty p=AutomationProperty.LookupById(30101);object v=e.GetCurrentPropertyValue(p,true);return v==AutomationElement.NotSupported?"":(v==null?"":v.ToString());}catch{return "";}}
    private static string Failure(string status){return Result(false,null,null,"",false,status);}
    private static string Result(bool ok,string conversation,string messageId,string text,bool streaming,string status){return Result(ok,conversation,messageId,text,streaming,status,new GeometryRect(),new List<GeometryRect>(),new int[0][],new int[0],new int[0],new int[0],"");}
    private static string Result(bool ok,string conversation,string messageId,string text,bool streaming,string status,GeometryRect windowBounds,IList<GeometryRect> highlights){
        return Result(ok,conversation,messageId,text,streaming,status,windowBounds,highlights,new int[0][],new int[0],new int[0],new int[0],"");
    }
    private static string Result(bool ok,string conversation,string messageId,string text,bool streaming,string status,GeometryRect windowBounds,IList<GeometryRect> highlights,int[][] highlightAnchors,int[] documentRuntimeId,int[] articleRuntimeId,int[] feedRuntimeId,string conversationUrl){
        var b=new StringBuilder("{\"ok\":").Append(ok?"true":"false").Append(",\"conversation\":").Append(conversation==null?"null":Q(conversation)).Append(",\"messageId\":").Append(messageId==null?"null":Q(messageId)).Append(",\"text\":").Append(Q(text)).Append(",\"streaming\":").Append(streaming?"true":"false").Append(",\"status\":").Append(Q(status)).Append(",\"highlights\":[");
        if(highlights!=null)for(int i=0;i<highlights.Count&&i<MaxHighlightRects;i++){if(i>0)b.Append(',');AppendRect(b,highlights[i]);}
        b.Append("],\"windowBounds\":"); if(ValidRect(windowBounds))AppendRect(b,windowBounds);else b.Append("null");
        b.Append(",\"highlightAnchors\":["); if(highlightAnchors!=null&&highlightAnchors.Length<=MaxHighlightRects)for(int i=0;i<highlightAnchors.Length;i++){if(i>0)b.Append(',');AppendIntArray(b,NormalizeRuntimeId(highlightAnchors[i]));}
        b.Append("],\"documentRuntimeId\":");AppendIntArray(b,NormalizeRuntimeId(documentRuntimeId));b.Append(",\"articleRuntimeId\":");AppendIntArray(b,NormalizeRuntimeId(articleRuntimeId));b.Append(",\"feedRuntimeId\":");AppendIntArray(b,NormalizeRuntimeId(feedRuntimeId)).Append(",\"conversationUrl\":").Append(Q(conversationUrl??""));
        return b.Append('}').ToString();
    }
    private static StringBuilder AppendIntArray(StringBuilder b,int[] values){b.Append('[');if(values!=null)for(int i=0;i<values.Length;i++){if(i>0)b.Append(',');b.Append(values[i].ToString(CultureInfo.InvariantCulture));}return b.Append(']');}
    internal static string Q(string s){if(s==null)return "null";var b=new StringBuilder("\"");foreach(char c in s){switch(c){case '"':b.Append("\\\"");break;case '\\':b.Append("\\\\");break;case '\n':b.Append("\\n");break;case '\r':b.Append("\\r");break;case '\t':b.Append("\\t");break;default:if(c<32)b.Append("\\u").Append(((int)c).ToString("x4"));else b.Append(c);break;}}return b.Append('"').ToString();}
}
