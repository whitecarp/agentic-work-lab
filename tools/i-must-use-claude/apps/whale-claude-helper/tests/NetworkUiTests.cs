using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using WhaleClaudeHelper;

// Offline, hidden-window rendering. Never calls Show/ShowDialog/Application.Run,
// a real service, a browser launch, or any network/configuration operation.
internal static class NetworkUiTests {
    private static int checks;
    private static readonly BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    private static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
    private static T Field<T>(object target,string name){return (T)target.GetType().GetField(name,Private).GetValue(target);}
    private static void SetField(object target,string name,object value){target.GetType().GetField(name,Private).SetValue(target,value);}
    private static object Invoke(object target,string name,params object[] arguments){return target.GetType().GetMethod(name,Private).Invoke(target,arguments);}
    private static void CompleteDemoTask(object target,string name){((Task)Invoke(target,name)).GetAwaiter().GetResult();}
    private static void PumpUntil(Func<bool> done){var until=DateTime.UtcNow.AddSeconds(8);while(!done()){if(DateTime.UtcNow>until)throw new Exception("offline UI action timed out");Application.DoEvents();Thread.Sleep(1);}Application.DoEvents();}
    private static void Click(Button button){typeof(Button).GetMethod("OnClick",Private).Invoke(button,new object[]{EventArgs.Empty});}
    private static ScoreReport FixtureReport(string status){string[] ids={"timezone","language","fonts","vendorFonts","webrtcLeak","cnBrowser","deviceVendor","intlLocale","timezoneOffset","emoji"};return ScoreReport.Parse(new JavaScriptSerializer().Serialize(new ScoreWire{version=1,browserId="Other",fingerprintSource="offline-ui-fixture",signals=ids.Select(id=>new SignalWire{id=id,status=id=="webrtcLeak"?status:"unavailable",risk=id=="webrtcLeak"&&status=="measured"?(double?)0.5:null,display=id=="webrtcLeak"?"offline WebRTC fixture":"Not collected"}).ToArray()}));}
    private static DnsLeakMeasurementResult FixtureDns(string status){return new DnsLeakMeasurementResult{Status=status,StartedUtc=new DateTime(2026,11,1,12,0,0,DateTimeKind.Utc),CollectedUtc=new DateTime(2026,11,1,12,0,8,DateTimeKind.Utc),AttemptedProbes=5,CompletedProbes=4,ProbeFailures=1,MetadataFailures=1,Rows=new[]{new DnsLeakResolver{Address="203.0.113.53",Country="CN",Organization="OFFLINE FIXTURE · Example resolver",Asn="AS64500"},new DnsLeakResolver{Address="2001:db8:1234:5678:90ab:cdef:1111:0053",Country="ZZ",Organization="OFFLINE FIXTURE · Long organization name for wrapped layout verification only · not a live provider result",Asn="AS64501"},new DnsLeakResolver{Address="192.0.2.53",Country="",Organization="",Asn=""}}};}
    private static void Layout(Control control){control.CreateControl();control.PerformLayout();foreach(Control child in control.Controls)Layout(child);control.PerformLayout();}
    private static void PrepareHiddenRender(Form form){
        // Create the native form hidden, then enable only WinForms' managed visibility
        // bit so DrawToBitmap includes children. No ShowWindow/Show/ShowDialog call.
        IntPtr handle=form.Handle;typeof(Control).GetMethod("SetState",Private).Invoke(form,new object[]{2,true});
        Check(!IsWindowVisible(handle),"native form remains hidden before rendering");Layout(form);
    }
    private static void ScalePreview(Form form,float scale){if(scale==1f)return;var fonts=new Dictionary<Control,Font>();Action<Control> collect=null;collect=delegate(Control control){fonts[control]=control.Font;foreach(Control child in control.Controls)collect(child);};collect(form);form.Scale(new SizeF(scale,scale));foreach(var pair in fonts)pair.Key.Font=new Font(pair.Value.FontFamily,pair.Value.Size*scale,pair.Value.Style);Layout(form);}
    private static void Capture(Form form,string path){Layout(form);using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));int ink=0;for(int y=60;y<bitmap.Height-20;y+=3)for(int x=20;x<bitmap.Width-20;x+=3){var color=bitmap.GetPixel(x,y);if(color.R<160&&color.G<180&&color.B<190)ink++;}Check(ink>300,"render contains real UI content: "+Path.GetFileName(path));bitmap.Save(path,ImageFormat.Png);}Check(!IsWindowVisible(form.Handle),"native form remained hidden: "+Path.GetFileName(path));Check(new FileInfo(path).Length>1000,"render exists: "+Path.GetFileName(path));}
    private static void CheckButtons(Control control){foreach(Control child in control.Controls){var button=child as Button;if(button!=null&&button.Visible){var size=TextRenderer.MeasureText(button.Text,button.Font,new Size(Int32.MaxValue,Int32.MaxValue),TextFormatFlags.NoPadding|TextFormatFlags.SingleLine);Check(size.Width+12<=button.Width,"button text fits: "+button.Text);}CheckButtons(child);}}
    private static void CheckProxyGroupLabel(Form form){
        var label=(Label)form.Controls.Find("proxyGroupLabel",true).Single();
        var natural=label.GetPreferredSize(Size.Empty);
        var line=TextRenderer.MeasureText(label.Text,label.Font,new Size(Int32.MaxValue,Int32.MaxValue),TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix);
        Check(label.MaximumSize.Width==0,"proxy group label is not constrained by its own AutoSize column");
        Check(label.Width>=natural.Width&&label.Width>=line.Width,"proxy group text fits in one line without clipping");
        Check(label.Height>=line.Height&&label.Height<=line.Height+4,"proxy group text does not wrap into a vertical word column");
        var row=(TableLayoutPanel)label.Parent;
        Check(row.GetColumnWidths()[0]>=label.Width+label.Margin.Horizontal,"proxy group column preserves label and margin width");
    }
    [STAThread]
    private static void Main(string[] args){try{Run(args);}catch(Exception error){Console.Error.WriteLine("FAIL "+error.GetType().Name+": "+error.Message);Environment.ExitCode=1;}}
    private static void Run(string[] args){
        string output=args.Length>0?Path.GetFullPath(args[0]):Path.Combine(Path.GetTempPath(),"WhaleNetworkUi-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(output);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        foreach(bool english in new[]{false,true}){
            Console.WriteLine("Checking "+(english?"English":"Chinese")+" native DNS workflow and lifecycle.");
            Ui.English=english;
            foreach(string status in new[]{"timeout","server_error","unsupported"}){string result=NetworkAssistantForm.FormatWebRtcReport(FixtureReport(status),english);Check(result.Contains(english?"Unknown":"未知"),status+" WebRTC receipt remains unknown");Check(result.Contains(english?"not a leak verdict":"不是泄露结论"),status+" receipt does not claim a leak verdict");}
            Check(new[]{"measured","partial","timeout","service_error","network_error","no_evidence","cancelled"}.Select(status=>NetworkAssistantForm.DnsStatusText(status,english)).Distinct().Count()==7,"all native DNS statuses have distinct labels");
            var trace=new List<string>();IProgress<DnsLeakProgress> oldProgress=null;
            using(var form=new NetworkAssistantForm(false,delegate{trace.Add("LocalDNS");return new NetworkDnsSnapshot{Available=false,Code="missing",ProxyGroups=new string[0]};},delegate(CancellationToken token,IProgress<DnsLeakProgress> progress){trace.Add("NativeDNS");oldProgress=progress;progress.Report(new DnsLeakProgress{Stage="probes",Completed=4,Total=5});return Task.FromResult(FixtureDns("partial"));},delegate(CancellationToken token){trace.Add("WebRTC");return Task.FromResult(FixtureReport("timeout"));})){
                Click(Field<Button>(form,"inspect"));PumpUntil(()=>trace.Count==2&&!Field<bool>(form,"busy")&&!Field<bool>(form,"checkingNetwork"));
                Check(trace.SequenceEqual(new[]{"LocalDNS","NativeDNS"}),"main button checks local then native DNS without starting a browser or WebRTC");
                Check(!Field<NetworkDnsSnapshot>(form,"snapshot").Available,"combined action retains unavailable DNS result");
                Check(Field<DnsLeakMeasurementResult>(form,"dnsReport").Status=="partial","combined action retains native partial result");
                Check(Field<ScoreReport>(form,"webRtcReport")==null,"main button never invokes optional browser measurement");
                Check(Field<Label>(form,"state").Text.Contains(english?"Local configuration remains unknown":"本机配置仍未知"),"overall state does not hide unknown local configuration");
                var table=Field<DataGridView>(form,"dnsResolvers");Check(table.Rows.Count==3&&table.ReadOnly,"native resolver table contains read-only evidence rows");
                Check(table.Rows[2].Cells["Country"].Value.ToString()==(english?"Unknown":"未知"),"missing resolver metadata remains unknown");
                Check(!Field<TextBox>(form,"dnsSummary").Text.Contains("203.0.113.53"),"summary does not duplicate the resolver table");
                string beforeProgress=Field<Label>(form,"dnsProgress").Text;oldProgress.Report(new DnsLeakProgress{Stage="metadata",Completed=999,Total=999});Application.DoEvents();Check(Field<Label>(form,"dnsProgress").Text==beforeProgress,"late progress after completion is ignored");
                Click(Field<Button>(form,"refreshDns"));PumpUntil(()=>trace.Count==3&&!Field<bool>(form,"busy"));
                Check(trace.SequenceEqual(new[]{"LocalDNS","NativeDNS","LocalDNS"}),"light DNS refresh triggers neither external DNS nor WebRTC");
                Click(Field<Button>(form,"webRtcStart"));PumpUntil(()=>trace.Count==4&&!Field<bool>(form,"busy"));Check(trace[3]=="WebRTC","browser measurement runs only from its own optional button");
                Check(Field<ScoreReport>(form,"webRtcReport").Get("webrtcLeak").Status=="timeout","optional WebRTC result is retained independently");
                Invoke(form,"MarkDnsResultStale");Check(Field<bool>(form,"dnsReportStale")&&Field<Label>(form,"dnsStatus").Text.Contains(english?"Earlier result":"此前结果"),"processing marks earlier DNS results for recheck");Check(table.Rows.Count==3,"stale evidence stays visible without masquerading as fresh");
                Click(Field<Button>(form,"inspect"));PumpUntil(()=>trace.Count==6&&!Field<bool>(form,"busy")&&!Field<bool>(form,"checkingNetwork"));Check(!Field<bool>(form,"dnsReportStale"),"new native measurement clears the stale marker");
                Check(Field<NetworkDnsService>(form,"service")==null,"combined test uses no real backend or system reads");
                Check(Field<NetworkDnsPlan>(form,"currentPlan")==null&&!Field<Button>(form,"apply").Enabled,"measurement never creates or applies a repair plan");
            }
            foreach(bool closeWindow in new[]{false,true}){
                var lateWebRtc=new TaskCompletionSource<ScoreReport>();CancellationToken receivedToken=CancellationToken.None;
                using(var form=new NetworkAssistantForm(false,null,null,delegate(CancellationToken token){receivedToken=token;return lateWebRtc.Task;})){
                    var previous=FixtureReport("timeout");SetField(form,"webRtcReport",previous);Field<TextBox>(form,"webRtcResults").Text=NetworkAssistantForm.FormatWebRtcReport(previous,english);
                    Click(Field<Button>(form,"webRtcStart"));PumpUntil(()=>Field<bool>(form,"measuringWebRtc"));
                    if(closeWindow){var closing=new FormClosingEventArgs(CloseReason.UserClosing,false);typeof(Form).GetMethod("OnFormClosing",Private).Invoke(form,new object[]{closing});Check(!closing.Cancel,"closing cancels optional WebRTC without blocking closure");}else Click(Field<Button>(form,"webRtcCancel"));
                    Check(receivedToken.IsCancellationRequested,"optional WebRTC receives this run's cancellation");lateWebRtc.SetResult(FixtureReport("measured"));PumpUntil(()=>!Field<bool>(form,"busy"));
                    Check(Object.ReferenceEquals(Field<ScoreReport>(form,"webRtcReport"),previous),"late successful WebRTC result after cancellation cannot replace the prior receipt");
                    Check(Field<TextBox>(form,"webRtcResults").Text==NetworkAssistantForm.FormatWebRtcReport(previous,english),"cancelled optional WebRTC preserves displayed prior results");
                    Check(Field<Label>(form,"state").Text.Contains(english?"cancelled":"已取消"),"late result does not overwrite cancellation status");Check(Field<CancellationTokenSource>(form,"measurementCancellation")==null,"late optional WebRTC completion still releases cancellation source");
                }
                IProgress<DnsLeakProgress> pendingProgress=null;int browserCalls=0;
                using(var form=new NetworkAssistantForm(false,delegate{return new NetworkDnsSnapshot{Available=false,Code="missing"};},delegate(CancellationToken token,IProgress<DnsLeakProgress> progress){pendingProgress=progress;var pending=new TaskCompletionSource<DnsLeakMeasurementResult>();token.Register(()=>pending.TrySetResult(FixtureDns("cancelled")));return pending.Task;},delegate(CancellationToken token){browserCalls++;return Task.FromResult(FixtureReport("timeout"));})){
                    Click(Field<Button>(form,"inspect"));PumpUntil(()=>Field<bool>(form,"measuringDns")&&pendingProgress!=null);
                    if(closeWindow){var closing=new FormClosingEventArgs(CloseReason.UserClosing,false);typeof(Form).GetMethod("OnFormClosing",Private).Invoke(form,new object[]{closing});Check(!closing.Cancel,"closing cancels native DNS rather than blocking closure");}else Click(Field<Button>(form,"webRtcCancel"));
                    PumpUntil(()=>!Field<bool>(form,"busy")&&!Field<bool>(form,"checkingNetwork"));
                    Check(Field<DnsLeakMeasurementResult>(form,"dnsReport").Status=="cancelled","cancelled native result is distinct");Check(Field<CancellationTokenSource>(form,"measurementCancellation")==null,"cancelled measurement releases cancellation source");Check(browserCalls==0,"cancellation starts no browser or further check");
                    string before=Field<Label>(form,"dnsProgress").Text;pendingProgress.Report(new DnsLeakProgress{Stage="metadata",Completed=7,Total=7});Application.DoEvents();Check(Field<Label>(form,"dnsProgress").Text==before,"late progress after cancellation is ignored");
                    Check(Field<NetworkDnsService>(form,"service")==null,"cancellation test used no real backend");form.Dispose();pendingProgress.Report(new DnsLeakProgress{Stage="results",Completed=1,Total=1});Application.DoEvents();Check(form.IsDisposed,"late progress after dispose is harmless");
                }
            }
            using(var entered=new ManualResetEventSlim(false))using(var release=new ManualResetEventSlim(false)){
                int dnsCalls=0;
                using(var form=new NetworkAssistantForm(false,delegate{entered.Set();release.Wait();return new NetworkDnsSnapshot{Available=false,Code="missing"};},delegate(CancellationToken token,IProgress<DnsLeakProgress> progress){dnsCalls++;return Task.FromResult(FixtureDns("measured"));},delegate(CancellationToken token){throw new Exception("browser must not start");})){
                    Click(Field<Button>(form,"inspect"));PumpUntil(()=>entered.IsSet);Click(Field<Button>(form,"webRtcCancel"));release.Set();PumpUntil(()=>!Field<bool>(form,"checkingNetwork"));
                    Check(dnsCalls==0,"cancellation during local inspection prevents the native DNS stage");Check(Field<DnsLeakMeasurementResult>(form,"dnsReport").Status=="cancelled","pre-probe cancellation has a cancelled receipt");
                }
            }
            IProgress<DnsLeakProgress> firstRunProgress=null,secondRunProgress=null;int nativeRuns=0;var secondRun=new TaskCompletionSource<DnsLeakMeasurementResult>();
            using(var form=new NetworkAssistantForm(false,delegate{return new NetworkDnsSnapshot{Available=false,Code="missing"};},delegate(CancellationToken token,IProgress<DnsLeakProgress> progress){nativeRuns++;if(nativeRuns==1){firstRunProgress=progress;return Task.FromResult(FixtureDns("partial"));}secondRunProgress=progress;return secondRun.Task;},null)){
                Click(Field<Button>(form,"inspect"));PumpUntil(()=>nativeRuns==1&&!Field<bool>(form,"checkingNetwork"));Click(Field<Button>(form,"inspect"));PumpUntil(()=>nativeRuns==2&&Field<bool>(form,"measuringDns"));
                string before=Field<Label>(form,"dnsProgress").Text;firstRunProgress.Report(new DnsLeakProgress{Stage="metadata",Completed=99,Total=99});Application.DoEvents();Check(Field<Label>(form,"dnsProgress").Text==before,"progress from a prior run cannot update the active run");
                secondRunProgress.Report(new DnsLeakProgress{Stage="probes",Completed=1,Total=5});PumpUntil(()=>Field<Label>(form,"dnsProgress").Text.Contains("1 / 5"));Check(Field<Label>(form,"dnsProgress").Text.Contains("1 / 5"),"current-run progress is displayed");secondRun.SetResult(FixtureDns("measured"));PumpUntil(()=>!Field<bool>(form,"checkingNetwork"));Check(Field<DnsLeakMeasurementResult>(form,"dnsReport").Status=="measured","current run supplies its own terminal result");
            }
            using(var form=new NetworkAssistantForm(true)){
                foreach(string status in new[]{"measured","partial","timeout","service_error","network_error","no_evidence","cancelled"}){var result=FixtureDns(status);if(status!="measured"&&status!="partial")result.Rows=new DnsLeakResolver[0];SetField(form,"dnsReport",result);Invoke(form,"RenderDnsResult");Check(Field<Label>(form,"dnsStatus").Text==NetworkAssistantForm.DnsStatusText(status,english),status+" is rendered distinctly");Check(Field<DataGridView>(form,"dnsResolvers").Rows.Count==result.Rows.Length,status+" shows only actual evidence rows");}
            }
            using(var form=new NetworkAssistantForm(true)){
                Check(Field<NetworkDnsService>(form,"service")==null,"demo constructor does not create service");
                Check(Field<ComboBox>(form,"groups").Items.Count==0,"demo does not invent proxy groups");
                CompleteDemoTask(form,"CheckNetworkAsync");CompleteDemoTask(form,"PreviewAsync");CompleteDemoTask(form,"MeasureWebRtcAsync");
                Check(Field<NetworkDnsService>(form,"service")==null,"demo actions do not create service");
                Check(Field<NetworkDnsPlan>(form,"currentPlan")==null,"demo never creates an applicable plan");
                Check(!Field<Button>(form,"apply").Enabled&&!Field<Button>(form,"undo").Enabled,"demo mutation buttons disabled");
                Check(!Field<bool>(form,"busy")&&!Field<bool>(form,"measuringWebRtc")&&!Field<bool>(form,"measuringDns"),"demo has no pending measurement");Check(Field<DnsLeakMeasurementResult>(form,"dnsReport")==null,"demo does not fabricate a DNS result");Check(typeof(NetworkAssistantForm).GetMethod("OpenExternal",Private)==null,"external DNS browser-launch entry is removed");
                var marker=(NetworkDnsPlan)FormatterServices.GetUninitializedObject(typeof(NetworkDnsPlan));SetField(form,"currentPlan",marker);Invoke(form,"Build");
                Check(Field<NetworkDnsPlan>(form,"currentPlan")==null,"language/layout rebuild invalidates the plan");
                SetField(form,"currentPlan",marker);var groups=Field<ComboBox>(form,"groups");groups.Items.Add("fixture group from test only");groups.SelectedIndex=0;
                Check(Field<NetworkDnsPlan>(form,"currentPlan")==null,"changing group invalidates the plan");
                SetField(form,"currentPlan",marker);CompleteDemoTask(form,"InspectAsync");Check(Field<NetworkDnsPlan>(form,"currentPlan")==null,"checking invalidates the plan");
            }
            using(var form=new NetworkAssistantForm(false)){Check(Field<NetworkDnsService>(form,"service")==null,"normal constructor does not read backend automatically");Check(!Field<Button>(form,"apply").Enabled&&!Field<Button>(form,"undo").Enabled,"normal mutation buttons disabled before inspection");}
            foreach(float scale in new[]{1f,1.5f}){
                Console.WriteLine("Rendering hidden "+(english?"English":"Chinese")+" layout at "+scale+"x.");
                using(var form=new NetworkAssistantForm(true)){
                    form.AutoScaleMode=AutoScaleMode.None;PrepareHiddenRender(form);ScalePreview(form,scale);Layout(form);CheckProxyGroupLabel(form);
                    var tabs=Field<TabControl>(form,"tabs");
                    for(int index=0;index<tabs.TabPages.Count;index++){tabs.SelectedIndex=index;Layout(form);string name=(english?"en":"zh")+"-"+(scale==1f?"100":"150")+"-tab"+index;Capture(form,Path.Combine(output,name+".png"));CheckButtons(tabs.TabPages[index]);var scroll=tabs.TabPages[index].Controls.OfType<ScrollableControl>().FirstOrDefault();if(scroll!=null&&scroll.VerticalScroll.Visible){scroll.AutoScrollPosition=new Point(0,scroll.VerticalScroll.Maximum);Layout(form);Capture(form,Path.Combine(output,name+"-bottom.png"));scroll.AutoScrollPosition=Point.Empty;}}
                    tabs.SelectedIndex=0;SetField(form,"dnsReport",FixtureDns("partial"));Invoke(form,"RenderDnsResult");Field<Label>(form,"dnsStatus").Text="OFFLINE FIXTURE · "+Field<Label>(form,"dnsStatus").Text;Layout(form);
                    var resolverTable=Field<DataGridView>(form,"dnsResolvers");var resultScroll=tabs.TabPages[0].Controls.OfType<ScrollableControl>().First();resultScroll.ScrollControlIntoView(resolverTable.Parent.Parent);Layout(form);
                    Check(resolverTable.Rows.Count==3,"fixture render contains three resolver rows");Check(resolverTable.Rows[1].Cells["Address"].Value.ToString()=="2001:db8:1234:5678:90ab:cdef:1111:0053","long IPv6 evidence is retained exactly");
                    int addressWidth=resolverTable.Rows.Cast<DataGridViewRow>().Max(row=>TextRenderer.MeasureText(row.Cells["Address"].Value.ToString(),resolverTable.Font).Width);Check(resolverTable.Columns["Address"].Width>=addressWidth,"long IPv6 address column fits without truncation");Check(resolverTable.Columns["Organization"].DefaultCellStyle.WrapMode==DataGridViewTriState.True&&resolverTable.ScrollBars==ScrollBars.Both,"long organizations wrap and table remains scrollable");
                    Capture(form,Path.Combine(output,(english?"en":"zh")+"-"+(scale==1f?"100":"150")+"-dns-fixture.png"));resultScroll.AutoScrollPosition=Point.Empty;
                    Check(Field<NetworkDnsService>(form,"service")==null,"rendering does not create backend");
                    string exactPlan=new NetworkDnsPlan(new object(),"fixture-before","fixture-yaml","fixture-after","Fixture outbound group","C:\\FixtureOnly\\preferences.json",new[]{"www.baidu.com","geosite:cn"},3,4).Format(english);
                    using(var confirmationOwner=new NetworkAssistantForm(true))
                    using(var dialog=(Form)Invoke(confirmationOwner,"CreateConfirmationDialog",english?"Confirm DNS changes":"确认 DNS 改动",exactPlan,false)){
                        var acknowledgment=(CheckBox)dialog.Controls.Find("confirmAcknowledgment",true)[0];var confirm=(Button)dialog.Controls.Find("confirmAction",true)[0];
                        Check(!acknowledgment.Checked&&!confirm.Enabled,"confirmation starts unchecked and disabled");Check(((TextBox)dialog.Controls.Find("exactPlanText",true)[0]).Text==exactPlan,"confirmation contains exact complete plan");
                        dialog.AutoScaleMode=AutoScaleMode.None;PrepareHiddenRender(dialog);ScalePreview(dialog,scale);Capture(dialog,Path.Combine(output,(english?"en":"zh")+"-"+(scale==1f?"100":"150")+"-confirmation.png"));CheckButtons(dialog);
                        acknowledgment.Checked=true;Check(confirm.Enabled,"explicit acknowledgment enables confirmation");Check(dialog.CancelButton!=null,"confirmation has an escape/cancel action");
                    }
                }
                using(var form=new OptimizerForm(true)){form.AutoScaleMode=AutoScaleMode.None;PrepareHiddenRender(form);ScalePreview(form,scale);Capture(form,Path.Combine(output,(english?"en":"zh")+"-"+(scale==1f?"100":"150")+"-main.png"));Check(Field<LinkLabel>(form,"network").Text==(english?"Network & DNS":"网络与 DNS"),"main window has visible network entry");foreach(string field in new[]{"maintenance","codexProxy","closeApps","updates"}){var entry=Field<LinkLabel>(form,field);Check(entry!=null&&entry.Enabled,"existing 0.6 entry preserved: "+field);}Check(Theme.BlueDark==Color.FromArgb(172,72,37),"existing orange theme preserved");}
            }
        }
        Console.WriteLine("PASS "+checks+" offline network UI checks. Hidden 100%/150% layout renders are simulated scaling, not actual Windows DPI acceptance. No browser, network, backend reads, or system changes.");
        Console.WriteLine(output);
    }
}
