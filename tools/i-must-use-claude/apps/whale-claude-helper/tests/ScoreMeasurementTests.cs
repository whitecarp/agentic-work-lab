using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Web.Script.Serialization;
using WhaleClaudeHelper;

// Payload/formatting and pre-cancelled host tests: never construct a form,
// open a listener/browser, read profiles, or invoke an optimization service.
internal static class ScoreMeasurementTests {
    private static int checks;
    private static readonly string[] Ids={"timezone","language","fonts","vendorFonts","webrtcLeak","cnBrowser","deviceVendor","intlLocale","timezoneOffset","emoji"};
    private static readonly string[] WebRtcStatuses={"timeout","server_error","unsupported"};
    private static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
    private static ScoreWire Payload(string id,string status,double? risk){
        return new ScoreWire{
            version=1,browserId="Chrome",fingerprintSource="isolated-test-browser",
            signals=Ids.Select(x=>new SignalWire{
                id=x,status=x==id?status:"measured",risk=x==id?risk:1.0,
                display="fixture detail",fonts=x=="fonts"||x=="vendorFonts"?new string[0]:null
            }).ToArray()
        };
    }
    private static ScoreReport Parse(ScoreWire wire){return ScoreReport.Parse(new JavaScriptSerializer().Serialize(wire));}
    private static ScoreReport Report(string id,string status,double? risk){return Parse(Payload(id,status,risk));}
    private static void Reject(ScoreWire wire,string name){
        bool rejected=false;
        try{Parse(wire);}catch(InvalidDataException){rejected=true;}
        Check(rejected,name);
    }
    private static void UnknownWebRtc(string status,ScoreReport complete){
        var report=Report("webrtcLeak",status,null);
        var signal=report.Get("webrtcLeak");
        Check(signal.Status==status,status+": status is retained");
        Check(!signal.Risk.HasValue&&!signal.Points.HasValue,status+": unknown is not zero");
        Check(signal.Weight==10,status+": original WebRTC weight is retained");
        Check(report.MeasuredWeight==90,status+": coverage excludes the WebRTC weight");
        Check(report.MeasuredScore==90,status+": measured subtotal only includes known signals");
        Check(!report.IsComplete,status+": report remains incomplete");
        Check(!report.CanCompare(complete)&&!complete.CanCompare(report)&&!report.CanCompare(report),status+": no comparable delta with an incomplete report");
        Check(report.CanOptimize,status+": existing local actionable checks remain available");
        Check(OptimizerForm.ScoreText(report,false)=="已测小计 90；覆盖权重 90 / 100",status+": Chinese incomplete score text");
        Check(OptimizerForm.ScoreText(report,true)=="Measured subtotal 90; measured weight 90 / 100",status+": English incomplete score text");
        foreach(double risk in new[]{0.0,0.5,1.0})Reject(Payload("webrtcLeak",status,risk),status+": rejects non-null risk "+risk);
    }
    private static void Main(){
        using(var cancelled=new CancellationTokenSource())using(var host=new ScoreMeasurementHost()){
            cancelled.Cancel();
            foreach(bool webRtcOnly in new[]{false,true}){
                bool stopped=false;try{(webRtcOnly?host.MeasureWebRtcAsync(cancelled.Token):host.MeasureAsync(cancelled.Token)).GetAwaiter().GetResult();}catch(OperationCanceledException){stopped=true;}
                Check(stopped,"pre-cancelled "+(webRtcOnly?"WebRTC":"full")+" host stops before loading page resources or opening a browser");
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                Check((int)typeof(ScoreMeasurementHost).GetField("active",flags).GetValue(host)==0&&typeof(ScoreMeasurementHost).GetField("listener",flags).GetValue(host)==null,"pre-cancelled host releases active state without creating a listener");
            }
        }
        var zero=Report("webrtcLeak","measured",0.0);
        var half=Report("webrtcLeak","measured",0.5);
        Check(zero.Get("webrtcLeak").Points==0&&zero.Get("webrtcLeak").Risk==0.0,"measured zero remains a real zero");
        Check(zero.IsComplete&&zero.MeasuredWeight==100&&zero.MeasuredScore==90,"measured zero completes full coverage");
        Check(half.Get("webrtcLeak").Points==5&&half.MeasuredScore==95&&half.IsComplete,"measured half risk scores five WebRTC points");
        Check(zero.CanCompare(half)&&half.CanCompare(zero),"complete matching-browser reports can compare");
        Check(OptimizerForm.ScoreText(zero,false)=="90 / 100"&&OptimizerForm.ScoreText(half,true)=="95 / 100","complete report score presentation");
        Check(OptimizerForm.ReportText(zero,false).Contains("WebRTC: 0 / 10 — fixture detail"),"Chinese measured zero details");
        Check(OptimizerForm.ReportText(half,true).Contains("webrtcLeak: 5 / 10 — fixture detail"),"English measured half-risk details");
        Check(!zero.CanCompare(null),"null report cannot compare");

        foreach(string status in WebRtcStatuses){
            UnknownWebRtc(status,zero);
            foreach(string id in Ids.Where(x=>x!="webrtcLeak"))Reject(Payload(id,status,null),status+": rejects non-WebRTC id "+id);
        }
        foreach(string status in new[]{"unavailable","error"}){
            UnknownWebRtc(status,zero);
            foreach(string id in Ids){
                var report=Report(id,status,null);
                Check(report.Get(id).Status==status&&!report.Get(id).Points.HasValue&&!report.IsComplete,status+": legacy status accepted for "+id);
                Reject(Payload(id,status,0.0),status+": legacy non-null risk rejected for "+id);
            }
        }
        string[] zh={"检测超时","STUN 探测失败","浏览器不支持"};
        string[] en={"Timed out","STUN probe failed","Browser unsupported"};
        for(int i=0;i<WebRtcStatuses.Length;i++){
            var report=Report("webrtcLeak",WebRtcStatuses[i],null);
            string chinese=OptimizerForm.ReportText(report,false),english=OptimizerForm.ReportText(report,true);
            Check(chinese.Contains("WebRTC: 未知 — "+zh[i]),WebRtcStatuses[i]+": localized Chinese unknown detail");
            Check(english.Contains("webrtcLeak: Unknown — "+en[i]),WebRtcStatuses[i]+": localized English unknown detail");
            Check(chinese.Contains("fixture detail")&&english.Contains("fixture detail"),WebRtcStatuses[i]+": diagnostic detail retained");
            if(WebRtcStatuses[i]=="server_error"){
                Check(chinese.Contains("不能据此判断服务器宕机"),"Chinese STUN failure does not claim server outage");
                Check(english.Contains("server availability is unconfirmed"),"English STUN failure does not claim server outage");
            }
        }
        foreach(string status in new[]{"unavailable","error"}){
            var report=Report("webrtcLeak",status,null);
            Check(OptimizerForm.ReportText(report,false).Contains("WebRTC: 未知 — fixture detail"),status+": legacy Chinese unknown detail unchanged");
            Check(OptimizerForm.ReportText(report,true).Contains("webrtcLeak: Unknown — fixture detail"),status+": legacy English unknown detail unchanged");
        }
        foreach(string status in new[]{null,"","unknown","timed_out","Timeout","server-error"})Reject(Payload("webrtcLeak",status,null),"unrecognized status rejected: "+status);
        foreach(string id in Ids){
            Reject(Payload(id,"measured",null),"measured requires risk for "+id);
            Reject(Payload(id,"measured",-0.1),"negative risk rejected for "+id);
            Reject(Payload(id,"measured",1.1),"risk over one rejected for "+id);
        }
        var changedBrowser=Payload("webrtcLeak","measured",0.0);changedBrowser.browserId="Edge";
        Check(!zero.CanCompare(Parse(changedBrowser)),"complete reports from different browsers cannot compare");
        var changedFingerprint=Payload("webrtcLeak","measured",0.0);changedFingerprint.fingerprintSource="different-fixture-browser";
        Check(!zero.CanCompare(Parse(changedFingerprint)),"complete reports with different fingerprints cannot compare");
        var duplicate=Payload("webrtcLeak","measured",0.0);duplicate.signals[0].id="webrtcLeak";
        Reject(duplicate,"duplicate or missing signal rejected");
        Console.WriteLine("PASS "+checks+" isolated score measurement checks; no UI, network, browser, profile or system changes.");
    }
}
