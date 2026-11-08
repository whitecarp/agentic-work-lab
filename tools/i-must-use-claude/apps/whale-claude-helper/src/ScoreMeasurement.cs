using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace WhaleClaudeHelper {
    internal sealed class MeasuredSignal {
        public string Id { get; private set; }
        public string Status { get; private set; }
        public double? Risk { get; private set; }
        public string Display { get; private set; }
        public ReadOnlyCollection<string> Fonts { get; private set; }
        public int Weight { get; private set; }
        public int? Points { get { return Risk.HasValue ? (int?)Math.Floor(Risk.Value * Weight + .5) : null; } }
        internal MeasuredSignal(string id,string status,double? risk,string display,string[] fonts,int weight) {
            Id=id;Status=status;Risk=risk;Display=display;Fonts=Array.AsReadOnly(fonts);Weight=weight;
        }
    }
    internal sealed class ScoreReport {
        public DateTime CollectedUtc { get; private set; }
        public string BrowserId { get; private set; }
        public string BrowserFingerprint { get; private set; }
        public ReadOnlyCollection<MeasuredSignal> Signals { get; private set; }
        public int MeasuredScore { get { return Signals.Sum(x=>x.Points??0); } }
        public int MeasuredWeight { get { return Signals.Where(x=>x.Points.HasValue).Sum(x=>x.Weight); } }
        public bool IsComplete { get { return Signals.All(x=>x.Points.HasValue); } }
        public bool IsFresh { get { var age=DateTime.UtcNow-CollectedUtc;return age>=TimeSpan.Zero && age<TimeSpan.FromMinutes(10); } }
        public bool CanOptimize { get { return IsFresh && new[]{"timezone","timezoneOffset","language","intlLocale","fonts","vendorFonts"}.All(id=>Get(id).Risk.HasValue); } }
        public MeasuredSignal Get(string id) { return Signals.Single(x=>x.Id==id); }
        public bool Hit(string id) { return Get(id).Risk.GetValueOrDefault()>0; }
        public bool CanCompare(ScoreReport other) { return other!=null && IsComplete && other.IsComplete && BrowserId==other.BrowserId && BrowserFingerprint==other.BrowserFingerprint; }
        private ScoreReport() {}
        internal static ScoreReport Parse(string json) {
            var serializer=new JavaScriptSerializer{MaxJsonLength=65536,RecursionLimit=10};
            var wire=serializer.Deserialize<ScoreWire>(json);
            string[] ids={"timezone","language","fonts","vendorFonts","webrtcLeak","cnBrowser","deviceVendor","intlLocale","timezoneOffset","emoji"};
            int[] weights={24,18,14,10,10,8,6,4,3,3};
            if(wire==null||wire.version!=1||!new[]{"Chrome","Edge","Firefox","Other"}.Contains(wire.browserId)||String.IsNullOrWhiteSpace(wire.fingerprintSource)||wire.fingerprintSource.Length>2048||wire.signals==null||wire.signals.Length!=10)throw new InvalidDataException();
            var signals=new List<MeasuredSignal>();
            for(int i=0;i<ids.Length;i++) {
                var matches=wire.signals.Where(x=>x!=null&&x.id==ids[i]).ToArray();
                if(matches.Length!=1)throw new InvalidDataException();
                var s=matches[0];
                bool knownStatus=new[]{"measured","unavailable","error"}.Contains(s.status);
                bool webRtcStatus=s.id=="webrtcLeak"&&new[]{"timeout","server_error","unsupported"}.Contains(s.status);
                if((!knownStatus&&!webRtcStatus)||s.display==null||s.display.Length>256||s.display.Any(Char.IsControl))throw new InvalidDataException();
                if(s.status=="measured") { if(!s.risk.HasValue||Double.IsNaN(s.risk.Value)||Double.IsInfinity(s.risk.Value)||s.risk<0||s.risk>1)throw new InvalidDataException(); }
                else if(s.risk.HasValue)throw new InvalidDataException();
                var fonts=s.fonts??new string[0];
                if(fonts.Length>64||fonts.Any(f=>f==null||!OptimizerService.MatchesSignal(f))||fonts.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=fonts.Length)throw new InvalidDataException();
                if(s.id!="fonts"&&s.id!="vendorFonts"&&fonts.Length!=0)throw new InvalidDataException();
                if((s.id=="fonts"||s.id=="vendorFonts")&&s.status=="measured"&&s.fonts==null)throw new InvalidDataException();
                signals.Add(new MeasuredSignal(s.id,s.status,s.risk,s.display,fonts,weights[i]));
            }
            string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(wire.fingerprintSource))).Replace("-","");
            return new ScoreReport{CollectedUtc=DateTime.UtcNow,BrowserId=wire.browserId,BrowserFingerprint=hash,Signals=signals.AsReadOnly()};
        }
    }
    internal sealed class ScoreWire { public int version{get;set;}public string browserId{get;set;}public string fingerprintSource{get;set;}public SignalWire[] signals{get;set;} }
    internal sealed class SignalWire { public string id{get;set;}public string status{get;set;}public string display{get;set;}public double? risk{get;set;}public string[] fonts{get;set;} }

    // Only a user-started measurement opens the browser. No browser/profile files are read.
    // Bound to loopback with an unguessable single-use route; no listening LAN endpoint.
    internal sealed class ScoreMeasurementHost : IDisposable {
        private TcpListener listener;
        private int active;
        public Task<ScoreReport> MeasureAsync(CancellationToken cancellation) {
            return StartMeasurement(cancellation,false);
        }
        public Task<ScoreReport> MeasureWebRtcAsync(CancellationToken cancellation) {
            return StartMeasurement(cancellation,true);
        }
        private Task<ScoreReport> StartMeasurement(CancellationToken cancellation,bool webRtcOnly) {
            if(Interlocked.CompareExchange(ref active,1,0)!=0)throw new InvalidOperationException();
            return Task.Run(()=>Run(cancellation,webRtcOnly));
        }
        private ScoreReport Run(CancellationToken cancellation,bool webRtcOnly) {
            var deadline=Stopwatch.StartNew();
            try {
                cancellation.ThrowIfCancellationRequested();
                byte[] bytes=new byte[24];using(var random=RandomNumberGenerator.Create())random.GetBytes(bytes);
                string token=BitConverter.ToString(bytes).Replace("-","");
                string page;
                using(var resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("ScorePage")) {
                    if(resource==null)throw new InvalidDataException();
                    using(var reader=new StreamReader(resource,Encoding.UTF8))page=reader.ReadToEnd().Replace("__TOKEN__",token).Replace("__WEBRTC_ONLY__",webRtcOnly?"true":"false");
                }
                listener=new TcpListener(IPAddress.Loopback,0);listener.Start(8);
                string authority="127.0.0.1:"+((IPEndPoint)listener.LocalEndpoint).Port;
                string origin="http://"+authority;
                cancellation.ThrowIfCancellationRequested();
                Process.Start(new ProcessStartInfo(origin+"/measure/"+token){UseShellExecute=true});
                while(deadline.Elapsed<TimeSpan.FromMinutes(2)) {
                    cancellation.ThrowIfCancellationRequested();
                    if(!listener.Pending()){Thread.Sleep(60);continue;}
                    using(var client=listener.AcceptTcpClient()) {
                        client.ReceiveTimeout=2000;client.SendTimeout=2000;
                        try {
                            using(var stream=client.GetStream()) {
                                string header=ReadHeader(stream,cancellation);
                                var lines=header.Split(new[]{"\r\n"},StringSplitOptions.None);
                                var request=lines[0].Split(' ');
                                var headers=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
                                foreach(string line in lines.Skip(1).Where(x=>x.Length>0)) {
                                    int colon=line.IndexOf(':');if(colon<1||headers.ContainsKey(line.Substring(0,colon)))throw new InvalidDataException();
                                    headers.Add(line.Substring(0,colon),line.Substring(colon+1).Trim());
                                }
                                if(request.Length!=3||!headers.ContainsKey("Host")||headers["Host"]!=authority||headers.ContainsKey("Transfer-Encoding"))throw new InvalidDataException();
                                if(request[0]=="GET"&&request[1]=="/measure/"+token) { Reply(stream,200,"text/html; charset=utf-8",page);continue; }
                                if(request[0]!="POST"||request[1]!="/result/"+token||!headers.ContainsKey("Origin")||headers["Origin"]!=origin||!headers.ContainsKey("Content-Type")||!headers["Content-Type"].StartsWith("application/json",StringComparison.OrdinalIgnoreCase)) { Reply(stream,404,"text/plain","Not found");continue; }
                                int length;
                                if(!headers.ContainsKey("Content-Length")||!Int32.TryParse(headers["Content-Length"],out length)||length<1||length>65536)throw new InvalidDataException();
                                var body=new byte[length];int read=0;var reading=Stopwatch.StartNew();
                                while(read<length) { cancellation.ThrowIfCancellationRequested();if(reading.ElapsedMilliseconds>5000)throw new InvalidDataException();int n=stream.Read(body,read,length-read);if(n==0)throw new EndOfStreamException();read+=n; }
                                var report=ScoreReport.Parse(new UTF8Encoding(false,true).GetString(body));
                                Reply(stream,200,"application/json","{\"ok\":true}");return report;
                            }
                        } catch(IOException) {} catch(ArgumentException) {} catch(InvalidOperationException) {} catch(FormatException) {} catch(InvalidDataException) {}
                    }
                }
                throw new TimeoutException();
            } finally { if(listener!=null)listener.Stop();listener=null;Interlocked.Exchange(ref active,0); }
        }
        private static string ReadHeader(Stream stream,CancellationToken cancellation) {
            var bytes=new List<byte>();var watch=Stopwatch.StartNew();
            while(bytes.Count<8192) {
                cancellation.ThrowIfCancellationRequested();if(watch.ElapsedMilliseconds>5000)throw new InvalidDataException();
                int b=stream.ReadByte();if(b<0)throw new EndOfStreamException();bytes.Add((byte)b);
                int n=bytes.Count;if(n>=4&&bytes[n-4]==13&&bytes[n-3]==10&&bytes[n-2]==13&&bytes[n-1]==10)return Encoding.ASCII.GetString(bytes.ToArray());
            }
            throw new InvalidDataException();
        }
        private static void Reply(Stream stream,int status,string type,string body) {
            byte[] data=Encoding.UTF8.GetBytes(body);
            string headers="HTTP/1.1 "+status+(status==200?" OK":" Not Found")+"\r\nContent-Type: "+type+"\r\nContent-Length: "+data.Length+"\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nX-Frame-Options: DENY\r\nConnection: close\r\n\r\n";
            byte[] h=Encoding.ASCII.GetBytes(headers);stream.Write(h,0,h.Length);stream.Write(data,0,data.Length);stream.Flush();
        }
        public void Dispose(){if(listener!=null)listener.Stop();}
    }
}
