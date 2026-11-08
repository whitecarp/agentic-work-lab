using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WhaleClaudeHelper {
    internal sealed class NetworkAssistantForm : Form {
        private readonly bool demo;
        private readonly Func<NetworkDnsSnapshot> inspectOverride;
        private readonly Func<CancellationToken,IProgress<DnsLeakProgress>,Task<DnsLeakMeasurementResult>> dnsLeakOverride;
        private readonly Func<CancellationToken,Task<ScoreReport>> webRtcOverride;
        private bool english,busy,populating,measuringWebRtc,measuringDns,checkingNetwork,dnsReportStale;
        private int measurementGeneration;
        private NetworkDnsService service;
        private NetworkDnsSnapshot snapshot;
        private NetworkDnsPlan currentPlan;
        private bool hasUndo;
        private Label state;
        private Button language,inspect,refreshDns,preview,apply,undo;
        private Button webRtcStart,webRtcCancel;
        private CancellationTokenSource measurementCancellation;
        private ComboBox groups;
        private TextBox inspectionText,planText;
        private TextBox webRtcResults;
        private ScoreReport webRtcReport;
        private DnsLeakMeasurementResult dnsReport;
        private Label dnsStatus,dnsProgress;
        private ProgressBar dnsProgressBar;
        private DataGridView dnsResolvers;
        private TextBox dnsSummary;
        private TabControl tabs;
        private string lastAction="";
        private string T(string zh,string en){return english?en:zh;}
        private NetworkDnsService Service { get { return service??(service=new NetworkDnsService()); } }

        internal NetworkAssistantForm(bool demoMode):this(demoMode,null,null,null){}
        // Injected read-only operations allow offline tests to exercise the actual combined action.
        internal NetworkAssistantForm(bool demoMode,Func<NetworkDnsSnapshot> inspectForTest,Func<CancellationToken,IProgress<DnsLeakProgress>,Task<DnsLeakMeasurementResult>> dnsLeakForTest,Func<CancellationToken,Task<ScoreReport>> webRtcForTest){
            demo=demoMode;english=Ui.English;inspectOverride=inspectForTest;dnsLeakOverride=dnsLeakForTest;webRtcOverride=webRtcForTest;
            Font=new Font("Microsoft YaHei UI",10f);
            AutoScaleDimensions=new SizeF(96f,96f);AutoScaleMode=AutoScaleMode.Dpi;
            ClientSize=new Size(930,780);MinimumSize=new Size(790,620);
            StartPosition=FormStartPosition.CenterParent;BackColor=Theme.Cream;ForeColor=Theme.Ink;Theme.SetWindowIcon(this,true);
            try{Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);}catch{}
            Build();
            FormClosing+=delegate(object sender,FormClosingEventArgs e){if(measuringWebRtc||measuringDns||checkingNetwork){if(measurementCancellation!=null)measurementCancellation.Cancel();return;}if(busy){e.Cancel=true;state.Text=T("正在完成文件核对或备份，请稍候关闭。","Finishing file checks or backup receipts; please wait before closing.");}};
        }

        private void Build(){
            string selected=groups==null?null:groups.SelectedItem as string;
            int selectedTab=tabs==null?0:tabs.SelectedIndex;
            InvalidatePlan();
            var old=Controls.Cast<Control>().ToArray();Controls.Clear();foreach(var c in old)c.Dispose();
            Text=T("大肥鱼 · 网络与 DNS","Whale Helper · Network & DNS")+(demo?" · Demo":"");
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(20,16,20,14)};
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var header=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,Margin=new Padding(0,0,0,10)};
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var title=TextLabel(T("网络检测与修复","Network check & repair"),true);title.Font=new Font(Font.FontFamily,17f,FontStyle.Bold);header.Controls.Add(title,0,0);
            language=MakeButton(T("English","中文"),false,110);language.Click+=delegate{if(busy)return;english=!english;lastAction="";Build();};header.Controls.Add(language,1,0);root.Controls.Add(header,0,0);
            state=TextLabel(demo?T("演示模式：不读取配置、不改设置、不打开网页。","Demo: no configuration reads, changes, or browser pages."):T("尚未检查。点击「检查网络」才读取配置并发起 DNS 实测请求；可选 WebRTC 按钮才打开浏览器。","Not checked. Check network reads configuration and sends DNS test requests only when clicked; only the optional WebRTC button opens a browser."),false);
            state.ForeColor=Theme.Muted;state.Margin=new Padding(0,0,0,10);root.Controls.Add(state,0,1);
            tabs=new TabControl{Dock=DockStyle.Fill,Padding=new Point(18,8)};
            tabs.TabPages.Add(BuildDnsTab(selected));tabs.TabPages.Add(BuildGuideTab());
            tabs.SelectedIndex=Math.Max(0,Math.Min(selectedTab,tabs.TabPages.Count-1));root.Controls.Add(tabs,0,2);
            var close=MakeButton(T("关闭","Close"),false,120);close.Anchor=AnchorStyles.Right;close.Margin=new Padding(0,10,0,0);close.Click+=delegate{Close();};root.Controls.Add(close,0,3);
            Controls.Add(root);RefreshButtons();
        }

        private TabPage Page(string zh,string en){return new TabPage(T(zh,en)){BackColor=Theme.Cream,Padding=new Padding(14)};}
        private Label TextLabel(string text,bool bold){
            var label=new Label{Text=text,AutoSize=true,UseMnemonic=false,Dock=DockStyle.Fill,Margin=new Padding(0,0,0,8),Font=new Font(Font.FontFamily,10f,bold?FontStyle.Bold:FontStyle.Regular)};
            Action fit=delegate{if(label.IsDisposed||label.Parent==null)return;int width=label.Parent.ClientSize.Width-label.Parent.Padding.Horizontal-label.Margin.Horizontal;var table=label.Parent as TableLayoutPanel;if(table!=null){int column=table.GetColumn(label);int[] widths=table.GetColumnWidths();if(column>=0&&column<widths.Length&&widths[column]>0)width=Math.Min(width,widths[column]-label.Margin.Horizontal);}if(width>0&&label.MaximumSize.Width!=width)label.MaximumSize=new Size(width,0);};
            label.ParentChanged+=delegate{if(label.Parent==null)return;label.Parent.SizeChanged+=delegate{fit();};label.Parent.Layout+=delegate{fit();};fit();};
            return label;
        }
        private Button MakeButton(string text,bool primary,int width){var button=Theme.Button(text,primary);button.Size=new Size(width,43);button.Margin=new Padding(0,0,8,7);return button;}
        private TextBox Output(){return new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None,BackColor=Color.White,ForeColor=Theme.Ink,WordWrap=true};}
        private Control OutputGroup(string title,TextBox box){var group=new GroupBox{Text=title,Dock=DockStyle.Fill,Padding=new Padding(12,24,12,12),Margin=new Padding(0,6,0,0)};group.Controls.Add(Theme.Card(box,new Padding(0)));return group;}

        private TabPage BuildDnsTab(string selected){
            var page=Page("网络检测与修复","Network check & repair");
            var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true};
            var layout=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=1,RowCount=10,Padding=new Padding(0,0,18,8)};
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            foreach(int height in new[]{0,0,135,350,150,0,0,215,0,0})layout.RowStyles.Add(new RowStyle(height==0?SizeType.AutoSize:SizeType.Absolute,height));
            layout.Controls.Add(TextLabel(T("「检查网络」在助手内核对本机 DNS，并通过原生 HTTPS 请求 Net.Coffee 及随机探测域名进行真实 DNS 测试，不打开浏览器。观察服务可能看到请求出口及解析器；不上传本地配置或凭据。可选 WebRTC 专项会另行打开本机浏览器页，并在网页点击后向 Google STUN 请求。","Check network inspects local DNS and runs a real DNS test through native HTTPS requests to Net.Coffee and random probe domains, without opening a browser. The observation service may see request exits and resolvers; no local configuration or credentials are uploaded. Optional WebRTC opens a local browser page and contacts Google STUN after you click on that page."),false),0,0);
            var checkActions=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Margin=new Padding(0)};
            inspect=MakeButton(T("检查网络","Check network"),true,210);inspect.Click+=async delegate{await CheckNetworkAsync();};
            refreshDns=MakeButton(T("刷新 DNS 状态","Refresh DNS status"),false,210);refreshDns.Click+=async delegate{await InspectAsync();};
            webRtcStart=MakeButton(T("WebRTC（浏览器专项）","WebRTC (browser check)"),false,280);webRtcStart.Click+=async delegate{await MeasureWebRtcAsync();};
            webRtcCancel=MakeButton(T("取消检测","Cancel check"),false,170);webRtcCancel.Click+=delegate{if(measurementCancellation!=null)measurementCancellation.Cancel();};checkActions.Controls.Add(inspect);checkActions.Controls.Add(refreshDns);checkActions.Controls.Add(webRtcStart);checkActions.Controls.Add(webRtcCancel);layout.Controls.Add(checkActions,0,1);
            inspectionText=Output();inspectionText.Text=snapshot==null?T("本机 DNS：尚未检查。文件配置不代表实际解析路径；真实 DNS 结果单独显示在下方。","Local DNS: not inspected. File configuration does not establish the actual resolver path; real DNS results are shown separately below."):snapshot.Format(english);
            webRtcResults=Output();webRtcResults.Text=webRtcReport==null?T("WebRTC：尚无实测结果；不会推测是否安全。","WebRTC: no measured result; safety is not assumed."):FormatWebRtcReport(webRtcReport,english);
            layout.Controls.Add(OutputGroup(T("本机 DNS 文件检查","Local DNS file inspection"),inspectionText),0,2);layout.Controls.Add(BuildDnsMeasurementGroup(),0,3);layout.Controls.Add(OutputGroup(T("可选 WebRTC 实测回执","Optional WebRTC receipt"),webRtcResults),0,4);
            var selectRow=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,Margin=new Padding(0)};
            selectRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));selectRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            // This AutoSize column needs an unconstrained, single-line label. The
            // wrapping TextLabel helper would feed its own column width back into layout.
            var groupLabel=new Label{Name="proxyGroupLabel",Text=T("代理组","Proxy group"),AutoSize=true,UseMnemonic=false,Anchor=AnchorStyles.Left,Font=new Font(Font.FontFamily,10f,FontStyle.Bold),Margin=new Padding(2,9,12,5)};selectRow.Controls.Add(groupLabel,0,0);
            groups=new ComboBox{Dock=DockStyle.Top,DropDownStyle=ComboBoxStyle.DropDownList,IntegralHeight=false,DropDownHeight=220,Margin=new Padding(0,6,0,7),AccessibleName=T("检查得到的实际代理组","Actual proxy groups from inspection")};
            populating=true;if(snapshot!=null&&snapshot.ProxyGroups!=null)foreach(string name in snapshot.ProxyGroups)groups.Items.Add(name);
            if(selected!=null&&groups.Items.Contains(selected))groups.SelectedItem=selected;
            populating=false;
            groups.SelectedIndexChanged+=delegate{if(populating)return;InvalidatePlan();RefreshButtons();state.Text=T("代理组已更改，请重新预览；旧计划已作废。","Proxy group changed. Preview again; the old plan was discarded.");};selectRow.Controls.Add(groups,1,0);layout.Controls.Add(selectRow,0,5);
            var actions=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Margin=new Padding(0)};
            preview=MakeButton(T("2  预览改动","2  Preview changes"),false,english?205:165);preview.Click+=async delegate{await PreviewAsync();};
            apply=MakeButton(T("3  备份并应用…","3  Back up & apply…"),true,english?215:195);apply.Click+=async delegate{await ApplyAsync();};
            undo=MakeButton(T("撤回 DNS 改动…","Undo DNS changes…"),false,english?210:190);undo.Click+=async delegate{await UndoAsync();};actions.Controls.Add(preview);actions.Controls.Add(apply);actions.Controls.Add(undo);layout.Controls.Add(actions,0,6);
            planText=Output();planText.Text=lastAction.Length>0?lastAction:T("尚无有效计划。预览不写入文件；每次检查、改组或切换语言后都需要重新预览。","No current plan. Preview does not write files. Inspecting, changing groups, or switching language requires a new preview.");
            layout.Controls.Add(OutputGroup(T("具体计划 / 操作回执","Exact plan / action receipt"),planText),0,7);
            layout.Controls.Add(TextLabel(T("仅对核实后的 CuteCloud 文件提供修复，其他客户端只指导。应用/撤回前正常退出 CuteCloud（含托盘）及核心；后端会拒绝运行中的客户端。本工具不强停、不自动启动。处理后请正常启动，再点「检查网络」重新实测。","Repairs support verified CuteCloud files only; other clients receive guidance. Before apply/undo, exit CuteCloud, its tray process, and core normally; running clients are refused. This tool never force-stops or starts them. After processing, start normally and run Check network again."),false),0,8);
            layout.Controls.Add(TextLabel(T("解析器地址、机构、ASN 与地区是观察信息，不按国别单独判安全或危险。DNS 请求结束不保证产生解析器证据；超时、服务异常或无证据均保留未知。WebRTC 候选数也不是泄露结论。","Resolver addresses, organizations, ASNs, and regions are observations; country alone is not a safe/unsafe verdict. A completed DNS probe request does not guarantee resolver evidence. Timeout, service errors, and no evidence remain unknown. WebRTC counts are not leak verdicts either."),false),0,9);
            scroll.Controls.Add(layout);page.Controls.Add(scroll);return page;
        }

        private Control BuildDnsMeasurementGroup(){
            var group=new GroupBox{Text=T("真实 DNS 检测 · 助手原生请求","Real DNS test · native helper requests"),Dock=DockStyle.Fill,Padding=new Padding(12,25,12,12),Margin=new Padding(0,6,0,0)};
            var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,28));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,76));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            dnsStatus=TextLabel("",true);layout.Controls.Add(dnsStatus,0,0);
            var progressRow=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2};progressRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,70));progressRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,30));
            dnsProgress=new Label{Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,UseMnemonic=false,ForeColor=Theme.Muted};dnsProgressBar=new ProgressBar{Dock=DockStyle.Fill,Minimum=0,Maximum=100,Margin=new Padding(8,4,0,4)};progressRow.Controls.Add(dnsProgress,0,0);progressRow.Controls.Add(dnsProgressBar,1,0);layout.Controls.Add(progressRow,0,1);
            dnsSummary=Output();dnsSummary.Margin=new Padding(0,4,0,7);layout.Controls.Add(dnsSummary,0,2);
            dnsResolvers=new DataGridView{Name="dnsResolverTable",Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AllowUserToResizeRows=false,RowHeadersVisible=false,MultiSelect=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells,BackgroundColor=Color.White,BorderStyle=BorderStyle.None,ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.AutoSize,ClipboardCopyMode=DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText};
            dnsResolvers.DefaultCellStyle.BackColor=Color.White;dnsResolvers.DefaultCellStyle.ForeColor=Theme.Ink;dnsResolvers.DefaultCellStyle.SelectionBackColor=Theme.Mist;dnsResolvers.DefaultCellStyle.SelectionForeColor=Theme.Ink;dnsResolvers.DefaultCellStyle.WrapMode=DataGridViewTriState.False;
            dnsResolvers.Columns.Add(new DataGridViewTextBoxColumn{Name="Address",HeaderText=T("解析器地址","Resolver address"),MinimumWidth=150,AutoSizeMode=DataGridViewAutoSizeColumnMode.AllCells});dnsResolvers.Columns.Add(new DataGridViewTextBoxColumn{Name="Country",HeaderText=T("地区","Region"),MinimumWidth=80,FillWeight=18});dnsResolvers.Columns.Add(new DataGridViewTextBoxColumn{Name="Organization",HeaderText=T("机构","Organization"),MinimumWidth=180,FillWeight=62});dnsResolvers.Columns.Add(new DataGridViewTextBoxColumn{Name="Asn",HeaderText="ASN",MinimumWidth=80,FillWeight=20});dnsResolvers.Columns["Organization"].DefaultCellStyle.WrapMode=DataGridViewTriState.True;layout.Controls.Add(dnsResolvers,0,3);group.Controls.Add(layout);RenderDnsResult();return group;
        }
        internal static string DnsStatusText(string status,bool inEnglish){
            switch(status){
                case "measured":return inEnglish?"Measured · resolver evidence received":"已测 · 已收到解析器证据";
                case "partial":return inEnglish?"Partial result · coverage is incomplete":"部分结果 · 覆盖不完整";
                case "timeout":return inEnglish?"Timed out · Unknown":"超时 · 未知";
                case "service_error":return inEnglish?"Observation service error · Unknown":"观察服务异常 · 未知";
                case "network_error":return inEnglish?"Network request error · Unknown":"网络请求异常 · 未知";
                case "no_evidence":return inEnglish?"No resolver evidence · Unknown":"无解析器证据 · 未知";
                case "cancelled":return inEnglish?"Cancelled · not a completed check":"已取消 · 未完成检测";
                default:return inEnglish?"Not measured":"尚未实测";
            }
        }
        private void RenderDnsResult(){
            if(dnsStatus==null||dnsStatus.IsDisposed)return;dnsResolvers.Rows.Clear();
            dnsStatus.Text=(dnsReportStale?T("此前结果 · 处理后需重新检测 | ","Earlier result · recheck after processing | "):"")+DnsStatusText(dnsReport==null?null:dnsReport.Status,english);
            dnsProgress.Text=dnsReport==null?T("等待用户开始检测","Waiting for you to start"):T("采集 UTC：","Collected UTC: ")+dnsReport.CollectedUtc.ToString("yyyy-MM-dd HH:mm:ss");
            dnsProgressBar.Value=0;
            dnsSummary.Text=dnsReport==null?T("尚未向观察服务发送请求。本机配置不替代此实测；原生 HTTPS 路径也不代表浏览器独立 DoH 或所有应用。","No observation request has been sent. Local configuration is not a substitute for this test; native HTTPS does not represent independent browser DoH or every app."):T("请求已结束：","Requests ended: ")+dnsReport.CompletedProbes+" / "+dnsReport.AttemptedProbes+T("；传输失败：","; transport failures: ")+dnsReport.ProbeFailures+T("；解析器证据：","; resolver observations: ")+(dnsReport.Rows==null?0:dnsReport.Rows.Length)+"\r\n"+T("范围：助手原生 HTTPS / 当前默认代理或 TUN 路径，不代表浏览器独立 DoH 或所有应用。请求结束不是 DNS 命中；地区不单独决定安全性。","Scope: this helper's native HTTPS/default proxy or TUN path, not independent browser DoH or every app. Ended requests are not DNS hits; region alone does not determine safety.");
            if(dnsReport!=null&&dnsReport.Rows!=null)foreach(var row in dnsReport.Rows)dnsResolvers.Rows.Add(row.Address??"",String.IsNullOrEmpty(row.Country)?T("未知","Unknown"):row.Country,String.IsNullOrEmpty(row.Organization)?T("未知","Unknown"):row.Organization,String.IsNullOrEmpty(row.Asn)?T("未知","Unknown"):row.Asn);
        }
        private void MarkDnsResultStale(){if(dnsReport==null)return;dnsReportStale=true;RenderDnsResult();}

        private TabPage BuildGuideTab(){
            var page=Page("排查顺序","Troubleshooting");var flow=GuideFlow(page);
            AddGuide(flow,T("1  先确认代理协议与端口","1  Verify proxy protocol and port"),T("系统代理需要真实的 HTTP 或 mixed 监听，并验证协议能用。7890 只是常见例子，不要照抄；1053 可能是 DNS 监听端口，不能直接当作 HTTP 代理。选择当前配置中实际存在的代理组。","A system proxy needs a real HTTP or mixed listener whose protocol works. Port 7890 is only an example; do not copy it blindly. Port 1053 may be a DNS listener, not an HTTP proxy. Choose a group that actually exists in the current configuration."));
            AddGuide(flow,T("2  分清主 DNS、Fallback 和域名策略","2  Separate primary DNS, fallback, and policies"),T("主 DNS、Fallback 与按域名策略各有用途；只改一处不一定覆盖所有查询。保留内网域名解析和代理节点的引导解析，避免 DNS 又依赖尚未解析的节点而循环。其他客户端按自己的字段与说明手动预览，不套用 CuteCloud 文件路径。","Primary resolvers, fallback, and per-domain policies serve different purposes. Changing one may not cover every lookup. Preserve local-domain and proxy-node bootstrap resolution to avoid a DNS/proxy dependency loop. For other clients, review their own fields and documentation; do not reuse CuteCloud paths."));
            AddGuide(flow,T("3  预览后正常退出，再备份写入","3  Preview, exit normally, then back up and write"),T("核对具体计划。正常退出 CuteCloud，包括托盘驻留进程及核心；本工具检测到仍在运行会拒绝写入或撤回。不要强制结束进程，也不要把“文件已写”理解为“客户端已经加载”。","Review the exact plan. Exit CuteCloud normally, including its tray process and core. Apply and undo are refused while they are running. Avoid forced termination. Files written does not mean the client has loaded them."));
            AddGuide(flow,T("4  正常启动，回本页重新检测","4  Start normally and run a fresh check here"),T("自行正常启动客户端，确认覆写实际生效，再点本页「检查网络」，查看真实 DNS 解析器表。处理前的旧结果不能代表处理后状态。需要时再单独运行 WebRTC 浏览器专项；DNS 与 UDP 路径可能不同。","Start the client normally, confirm the override took effect, then run Check network here and inspect the real DNS resolver table. Earlier results do not establish the state after processing. Run the optional WebRTC browser check when needed; DNS and UDP paths may differ."));
            AddGuide(flow,T("5  仍失败时再查 DoH 与 IPv6 旁路","5  Investigate DoH and IPv6 only if checks still fail"),T("先完成正常停启及复测。仍不符合预期时，再检查浏览器安全 DNS/DoH、IPv6 或分流旁路，并逐项验证；不要盲目关闭它们。保留原设置和备份，必要时撤回本工具的 DNS 改动。","First restart normally and recheck. If results still differ from the intended route, inspect browser secure DNS/DoH, IPv6, or routing bypasses one at a time. Do not disable them blindly. Keep original settings and backups; undo this tool's DNS changes when needed."));
            AddGuide(flow,T("WebRTC 三种未知与候选数量","WebRTC unknown outcomes and candidate counts"),T("超时、STUN 探测失败、浏览器不支持都不是 0 分或安全。STUN 失败不证明服务器宕机。候选数量不直接证明泄露，0 个候选也不证明安全；未收集完成的候选结果会标明部分结果。只回传数量和摘要，不回传原始 IP。","Timeout, STUN probe failure, and unsupported remain unknown, never zero or safe. STUN failure does not prove server downtime. Counts alone do not confirm a leak; zero candidates do not prove safety. Incomplete candidate gathering is marked partial. Only counts and summaries are returned, never raw IPs."));
            AddGuide(flow,T("DNS 观察证据与请求范围","DNS evidence and request scope"),T("主检测由助手发出原生 HTTPS 请求，不打开第三方网页。Net.Coffee 及探测服务可看到请求出口和观察到的解析器，但不会收到本地配置或凭据。已测、部分结果、超时、服务异常和取消分别显示；不按国家或地区单独判安全或危险。浏览器独立 DoH 与其他应用可能走不同路径。","The main check makes native HTTPS requests without opening a third-party page. Net.Coffee and probe services may see request exits and observed resolvers, but receive no local configuration or credentials. Measured, partial, timeout, service error, and cancelled are distinct. Country or region alone is not a safe/unsafe verdict. Independent browser DoH and other apps may use different paths."));
            return page;
        }

        private FlowLayoutPanel GuideFlow(Control parent){var flow=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(0,0,12,0)};flow.SizeChanged+=delegate{foreach(Control child in flow.Controls){if(child is RoundedCard)child.Width=Math.Max(240,flow.ClientSize.Width-flow.Padding.Horizontal-SystemInformation.VerticalScrollBarWidth-4);}};parent.Controls.Add(flow);return flow;}
        private void AddGuide(FlowLayoutPanel flow,string title,string body){
            var card=new RoundedCard{Margin=new Padding(0,0,0,12),Width=790,Height=160};
            var heading=new Label{Text=title,Font=new Font(Font.FontFamily,10f,FontStyle.Bold),AutoSize=false,UseMnemonic=false,BackColor=Color.White};
            var detail=new Label{Text=body,Font=new Font(Font.FontFamily,10f),AutoSize=false,UseMnemonic=false,BackColor=Color.White};card.Controls.Add(heading);card.Controls.Add(detail);
            Action fit=delegate{int inset=Math.Max(16,(int)Math.Ceiling(heading.Font.SizeInPoints*1.6));int width=Math.Max(180,card.ClientSize.Width-inset*2);var flags=TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix;int titleHeight=TextRenderer.MeasureText(heading.Text,heading.Font,new Size(width,Int32.MaxValue),flags).Height+4;int bodyHeight=TextRenderer.MeasureText(detail.Text,detail.Font,new Size(width,Int32.MaxValue),flags).Height+6;heading.SetBounds(inset,inset,width,titleHeight);detail.SetBounds(inset,inset+titleHeight+6,width,bodyHeight);int height=inset*2+titleHeight+bodyHeight+6;if(card.Height!=height)card.Height=height;};
            card.SizeChanged+=delegate{fit();};heading.FontChanged+=delegate{fit();};detail.FontChanged+=delegate{fit();};flow.Controls.Add(card);fit();
        }

        private void InvalidatePlan(){currentPlan=null;if(planText!=null&&!planText.IsDisposed)planText.Text=T("计划已作废，请重新预览。","Plan discarded. Preview again.");}
        private void RefreshButtons(){
            bool idle=!busy&&!checkingNetwork;
            inspect.Enabled=refreshDns.Enabled=language.Enabled=idle;groups.Enabled=idle&&!demo&&snapshot!=null&&snapshot.Available&&groups.Items.Count>0;
            preview.Enabled=idle&&(demo||(snapshot!=null&&snapshot.Available&&groups.SelectedItem is string));
            apply.Enabled=idle&&!demo&&currentPlan!=null&&currentPlan.Valid;undo.Enabled=idle&&!demo&&hasUndo;
            webRtcStart.Enabled=idle;webRtcCancel.Visible=measuringWebRtc||measuringDns||checkingNetwork;webRtcCancel.Enabled=measurementCancellation!=null;
        }
        private void StartWork(string message){busy=true;RefreshButtons();state.Text=message;}
        private void EndWork(){busy=false;if(!IsDisposed)RefreshButtons();}
        private async Task CheckNetworkAsync(){
            if(busy||checkingNetwork)return;
            if(demo){await InspectAsync();dnsStatus.Text=T("演示：未发送 DNS 请求","Demo: no DNS requests sent");dnsSummary.Text=T("主检测会在助手内显示 DNS 解析器证据。当前未读取系统、未联网，也未打开浏览器。","The main check would show DNS resolver evidence inside the helper. No system read, network request, or browser launch occurred.");return;}
            checkingNetwork=true;measurementCancellation=new CancellationTokenSource();var token=measurementCancellation.Token;int generation=++measurementGeneration;DateTime started=DateTime.UtcNow;RefreshButtons();
            try{await InspectAsync();if(IsDisposed)return;token.ThrowIfCancellationRequested();await MeasureDnsAsync(token,generation);}
            catch(OperationCanceledException){if(!IsDisposed){dnsReport=DnsFailure("cancelled",started);dnsReportStale=false;RenderDnsResult();state.Text=T("网络检测已取消，没有继续其他检测。","Network check cancelled; no further check was started.");}}
            finally{checkingNetwork=measuringDns=false;measurementGeneration++;measurementCancellation.Dispose();measurementCancellation=null;EndWork();}
        }
        private static DnsLeakMeasurementResult DnsFailure(string status,DateTime started){return new DnsLeakMeasurementResult{Status=status,StartedUtc=started,CollectedUtc=DateTime.UtcNow,Rows=new DnsLeakResolver[0]};}
        private async Task MeasureDnsAsync(CancellationToken token,int generation){
            measuringDns=true;DateTime started=DateTime.UtcNow;StartWork(T("正在进行原生 DNS 实测，可随时取消；不会打开浏览器。","Running the native DNS test. You can cancel; no browser is opened."));
            dnsStatus.Text=T("DNS 实测进行中；下方旧结果不是本轮结果","DNS test in progress; any previous rows are not this run's result");dnsProgressBar.Value=0;
            var progress=new Progress<DnsLeakProgress>(update=>{
                if(IsDisposed||!measuringDns||measurementGeneration!=generation||token.IsCancellationRequested||update==null)return;
                string stage=update.Stage=="probes"?T("探测请求已结束","Probe requests ended"):update.Stage=="metadata"?T("查询归属信息","Looking up metadata"):T("等待观察结果","Waiting for observations");
                dnsProgress.Text=stage+" "+update.Completed+" / "+update.Total;dnsProgressBar.Value=update.Total>0?Math.Max(0,Math.Min(100,(int)(100.0*update.Completed/update.Total))):0;
            });
            try{var result=dnsLeakOverride==null?await new DnsLeakMeasurementService().MeasureAsync(token,progress):await dnsLeakOverride(token,progress);if(IsDisposed)return;dnsReport=result??DnsFailure("service_error",started);if(token.IsCancellationRequested)dnsReport.Status="cancelled";dnsReportStale=false;RenderDnsResult();state.Text=(snapshot!=null&&snapshot.Available?T("本机文件已核对；","Local files inspected; "):T("本机配置仍未知或不支持；","Local configuration remains unknown or unsupported; "))+DnsStatusText(dnsReport.Status,english)+T("。未自动判定安全，也未修改配置。",". No safety verdict or configuration change was made.");}
            catch(OperationCanceledException){if(!IsDisposed){dnsReport=DnsFailure("cancelled",started);dnsReportStale=false;RenderDnsResult();state.Text=T("DNS 实测已取消，没有继续其他检测。","DNS test cancelled; no further check was started.");}}
            catch{if(!IsDisposed){dnsReport=DnsFailure("service_error",started);dnsReportStale=false;RenderDnsResult();state.Text=T("DNS 实测服务异常，结果未知；没有显示原始错误或修改配置。","DNS observation failed; the result is unknown. No raw error was displayed or configuration changed.");}}
            finally{measuringDns=false;}
        }
        private async Task InspectAsync(){
            if(busy)return;InvalidatePlan();lastAction="";
            if(demo){inspectionText.Text=T("演示：此按钮会只读检查 CuteCloud 文件、运行状态和实际代理组；当前没有读取任何系统数据。","Demo: this button would inspect CuteCloud files, running state, and actual proxy groups. No system data was read.");RefreshButtons();return;}
            StartWork(T("正在只读检查…","Inspecting without changing files…"));
            try{var result=await Task.Run(()=>inspectOverride==null?Service.Inspect():inspectOverride());bool availableUndo=inspectOverride==null&&await Task.Run(()=>Service.HasUndo);if(IsDisposed)return;snapshot=result;hasUndo=availableUndo;inspectionText.Text=result.Format(english);populating=true;groups.Items.Clear();if(result.ProxyGroups!=null)foreach(string name in result.ProxyGroups)groups.Items.Add(name);groups.SelectedIndex=-1;populating=false;state.Text=result.Available?T("本机 DNS 文件已核对；真实 DNS 状态请看独立实测回执。","Local DNS files inspected; see the separate measured DNS receipt for actual observations."):T("本机 DNS 配置未知或不支持；不阻断真实 DNS 实测。","Local DNS configuration is unknown or unsupported; real DNS testing can still run.");}
            catch{if(IsDisposed)return;snapshot=null;hasUndo=false;populating=true;groups.Items.Clear();populating=false;inspectionText.Text=T("未能完成检查；未显示未过滤的配置或错误文本。","Inspection did not complete; raw configuration and error text are not displayed.");state.Text=T("本机检查未完成，未写入文件。","Local inspection incomplete; no files written.");}
            finally{EndWork();}
        }
        private async Task PreviewAsync(){
            if(busy)return;InvalidatePlan();lastAction="";
            if(demo){planText.Text=T("演示：预览会展示具体文件和改动；当前没有读取配置、生成可应用计划或写入文件。","Demo: preview would show exact files and changes. No configuration was read, applicable plan created, or file written.");return;}
            string group=groups.SelectedItem as string;if(snapshot==null||!snapshot.Available||String.IsNullOrEmpty(group)){RefreshButtons();return;}
            StartWork(T("正在生成只读预览…","Preparing a read-only preview…"));
            try{var plan=await Task.Run(()=>Service.Preview(group));if(IsDisposed)return;currentPlan=plan;planText.Text=plan.Format(english);state.Text=plan.Valid?T("预览已生成，尚未写入。正常退出 CuteCloud 及核心后，点击「备份并应用」。","Preview ready; nothing written. Exit CuteCloud and its core normally, then choose Back up & apply."):T("无法生成可应用计划，请查看说明。","No applicable plan; review the details.");}
            catch{InvalidatePlan();planText.Text=T("预览未完成；没有可应用计划，也未写入。","Preview failed; there is no applicable plan and nothing was written.");}
            finally{EndWork();}
        }
        private async Task ApplyAsync(){
            if(busy||demo||currentPlan==null||!currentPlan.Valid)return;
            var plan=currentPlan;
            if(!ConfirmAction(T("确认备份并应用 DNS 改动","Confirm backup and DNS changes"),plan.Format(english),false))return;
            if(!Object.ReferenceEquals(currentPlan,plan))return;
            InvalidatePlan();MarkDnsResultStale();StartWork(T("正在核对退出状态、备份并写入…","Checking stopped state, backing up, and writing…"));
            try{var result=await Task.Run(()=>Service.Apply(plan));bool availableUndo=await Task.Run(()=>Service.HasUndo);if(IsDisposed)return;hasUndo=availableUndo;lastAction=result.Format(english);planText.Text=lastAction;state.Text=result.Ok?T("处理完成，请查看回执；客户端生效尚待确认。请正常启动客户端，再回本页点击「检查网络」复测。","Processing complete; review the receipt. Client activation is unconfirmed. Start the client normally, then return here and click Check network to retest."):T("未完成应用，请查看回执。不能据此判断网络安全。","Apply did not complete. Review the receipt; network safety is unconfirmed.");}
            catch{planText.Text=T("操作未正常返回。请先重新检查并核对备份，不要假定已全部完成。","The operation did not return normally. Inspect again and check backups; do not assume it completed.");state.Text=T("操作结果未确认。","Operation outcome unconfirmed.");}
            finally{EndWork();}
        }
        private async Task UndoAsync(){
            if(busy||demo||!hasUndo)return;InvalidatePlan();
            if(!ConfirmAction(T("确认撤回 DNS 改动","Confirm undo of DNS changes"),T("将请求后端核对本工具最近的 DNS 备份及当前文件。仅在可安全恢复时撤回；后续独立修改可能导致拒绝。撤回后仍需正常启动客户端并复测。","The backend will verify this tool's latest DNS backup and current files. It restores only when safe; later independent changes may cause refusal. Restart the client normally and recheck afterward."),true)){RefreshButtons();return;}
            MarkDnsResultStale();StartWork(T("正在核对备份并撤回…","Checking backups and undoing…"));
            try{var result=await Task.Run(()=>Service.Undo());bool availableUndo=await Task.Run(()=>Service.HasUndo);if(IsDisposed)return;hasUndo=availableUndo;lastAction=result.Format(english);planText.Text=lastAction;state.Text=result.Ok?T("撤回文件已写入。请正常启动后复测，当前尚未确认生效。","Restored files written. Start normally and recheck; activation is unconfirmed."):T("撤回未完成，请查看回执。","Undo did not complete; review the receipt.");}
            catch{planText.Text=T("撤回结果未确认。请重新检查并保留备份。","Undo outcome is unconfirmed. Inspect again and keep backups.");}
            finally{EndWork();}
        }
        internal static string FormatWebRtcReport(ScoreReport report,bool inEnglish){
            var signal=report.Get("webrtcLeak");string status;
            switch(signal.Status){
                case "measured":status=inEnglish?"Measured":"已测";break;
                case "timeout":status=inEnglish?"Timed out · Unknown":"超时 · 未知";break;
                case "server_error":status=inEnglish?"STUN probe failed · Unknown; server downtime is unconfirmed":"STUN 探测失败 · 未知；不能据此判断服务器宕机";break;
                case "unsupported":status=inEnglish?"Browser unsupported · Unknown":"浏览器不支持 · 未知";break;
                default:status=inEnglish?"Check incomplete · Unknown":"检测未完成 · 未知";break;
            }
            return (inEnglish?"Collected: ":"采集时间：")+report.CollectedUtc.ToString("yyyy-MM-dd HH:mm:ss")+" UTC\r\n"+status+"\r\n"+signal.Display+"\r\n"+(inEnglish?"Candidate counts are not a leak verdict. Unknown is not zero or safe.":"候选数量不是泄露结论；未知不等于 0 分或安全。");
        }
        private async Task MeasureWebRtcAsync(){
            if(busy||checkingNetwork)return;
            if(demo){webRtcResults.Text=T("演示模式没有打开网页、读取浏览器或向 STUN 发送请求。","Demo opened no page, read no browser, and sent no STUN request.");return;}
            InvalidatePlan();measuringWebRtc=true;measurementCancellation=new CancellationTokenSource();var token=measurementCancellation.Token;StartWork(T("请在打开的 WebRTC 专用页面点击「开始 WebRTC 检测」；可随时取消。","Click Start WebRTC check in the WebRTC-only page. You can cancel here."));
            try{ScoreReport report;if(webRtcOverride!=null)report=await webRtcOverride(token);else using(var host=new ScoreMeasurementHost())report=await host.MeasureWebRtcAsync(token);token.ThrowIfCancellationRequested();if(IsDisposed)return;webRtcReport=report;webRtcResults.Text=FormatWebRtcReport(report,english);state.Text=T("WebRTC 回执已收到。DNS 保留其独立采集时间与状态；未自动判定安全。","WebRTC receipt received. DNS retains its own collection time and status; no safety verdict was inferred.");}
            catch(OperationCanceledException){if(!IsDisposed)state.Text=T("WebRTC 检测已取消，已有回执保留。","WebRTC check cancelled; any earlier receipt is kept.");}
            catch(TimeoutException){if(!IsDisposed)state.Text=T("等待网页回传超时；没有新结果，不能判安全。","Waiting for the page timed out. No new result; safety is unknown.");}
            catch{if(!IsDisposed)state.Text=T("未收到有效 WebRTC 结果；保持未知。","No valid WebRTC result received; it remains unknown.");}
            finally{measuringWebRtc=false;measurementCancellation.Dispose();measurementCancellation=null;EndWork();}
        }
        private bool ConfirmAction(string title,string exactPlan,bool restoring){
            using(var dialog=CreateConfirmationDialog(title,exactPlan,restoring))return dialog.ShowDialog(this)==DialogResult.OK&&((CheckBox)dialog.Controls.Find("confirmAcknowledgment",true)[0]).Checked;
        }
        private Form CreateConfirmationDialog(string title,string exactPlan,bool restoring){
                var dialog=new Form{Icon=this.Icon,ShowInTaskbar=false,Text=title,Font=Font,BackColor=Theme.Cream,ForeColor=Theme.Ink,ClientSize=new Size(800,650),MinimumSize=new Size(660,520),StartPosition=FormStartPosition.CenterParent,AutoScaleDimensions=new SizeF(96,96),AutoScaleMode=AutoScaleMode.Dpi};
                dialog.Icon=Icon;
                var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(20)};layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
                layout.Controls.Add(TextLabel(T("请核对下列具体计划。正常退出 CuteCloud（含托盘）与核心；程序仍在运行时，后端会拒绝操作。不会强停或自动启动。","Review the exact plan below. Exit CuteCloud, its tray process, and core normally. The backend refuses while they are running. Nothing is force-stopped or automatically started."),true),0,0);
                var text=Output();text.Name="exactPlanText";text.Text=exactPlan;layout.Controls.Add(Theme.Card(text,new Padding(0,8,0,8)),0,1);
                var check=new CheckBox{Name="confirmAcknowledgment",AutoSize=true,Dock=DockStyle.Top,Text=T("我已正常退出客户端及核心，并确认执行以上文件改动。","I exited the client and core normally and confirm the file changes above."),Margin=new Padding(0,12,0,12)};layout.Controls.Add(check,0,2);
                layout.SizeChanged+=delegate{check.MaximumSize=new Size(Math.Max(180,layout.ClientSize.Width-layout.Padding.Horizontal),0);};
                var actions=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Top,FlowDirection=FlowDirection.RightToLeft};
                var confirm=MakeButton(restoring?T("确认撤回","Confirm undo"):T("确认备份并应用","Confirm backup & apply"),true,english?260:230);confirm.Name="confirmAction";confirm.Enabled=false;confirm.DialogResult=DialogResult.OK;check.CheckedChanged+=delegate{confirm.Enabled=check.Checked;};
                var cancel=MakeButton(T("取消","Cancel"),false,120);cancel.DialogResult=DialogResult.Cancel;actions.Controls.Add(confirm);actions.Controls.Add(cancel);layout.Controls.Add(actions,0,3);dialog.Controls.Add(layout);dialog.CancelButton=cancel;
                return dialog;
        }
    }
}
