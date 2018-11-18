using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OctoprintClient

{
    public enum HttpVerb
    {
        GET,
        POST,
        PUT,
        DELETE
    }
    public delegate void JobInfoHandler(OctoprintJobInfo Info);
    public delegate void ProgressInfoHandler(OctoprintJobProgress Info);
    public delegate void PrinterStateHandler(OctoprintPrinterState Info);
    public delegate void CurrentZHandler(float CurrentZ);
    public class OctoprintConnection
    {
        public string EndPoint { get; set; }
        public string ApiKey { get; set; }
        public HttpVerb HttpMethod { get; set; }
        ClientWebSocket WebSocket { get; set; }
        public volatile bool listening = false;
        public int WebSocketBufferSize = 4096;//if the buffer is to small the websocket might run into problems more often
        //private CancellationTokenSource source;
        //private CancellationToken token;
        public event JobInfoHandler JobinfoHandlers;
        public event ProgressInfoHandler ProgressHandlers;
        public event PrinterStateHandler PrinterstateHandlers;
        public event CurrentZHandler CurrentZHandlers;
        //public OctoprintConnection e = null;

        public OctoprintPosTracker Position { get; set; }
        public OctoprintFileTracker Files { get; set; }
        public OctoprintJobTracker Jobs { get; set; }
        public OctoprintPrinterTracker Printers { get; set; }

        //posdata

        public OctoprintConnection(string eP, string aK)
        {
            EndPoint = eP;
            ApiKey = aK;
            HttpMethod = HttpVerb.GET;
            Position = new OctoprintPosTracker(this);
            Files = new OctoprintFileTracker(this);
            Jobs = new OctoprintJobTracker(this);
            Printers = new OctoprintPrinterTracker(this);
            //source = new CancellationTokenSource();
            //token = source.Token;
            var canceltoken = CancellationToken.None;
            WebSocket = new ClientWebSocket();
            WebSocket.ConnectAsync(new Uri("ws://"+EndPoint.Replace("https://", "").Replace("http://", "")+"sockjs/websocket"), canceltoken).GetAwaiter().GetResult();
        }
        public string Get(string location)
        {
            string strResponseValue = string.Empty;
            Debug.WriteLine("This was searched:");
            Debug.WriteLine(EndPoint + location + "?apikey=" + ApiKey);
            WebClient wc = new WebClient();
            wc.Headers.Add("X-Api-Key", ApiKey);
            Stream downStream = wc.OpenRead(EndPoint + location);
            using (StreamReader sr = new StreamReader(downStream))
            {
                strResponseValue = sr.ReadToEnd();
            }
            return strResponseValue;
        }

        public string PostString(string location, string arguments)
        {
            string strResponseValue = string.Empty;
            Debug.WriteLine("This was searched:");
            Debug.WriteLine(EndPoint + location + "?apikey=" + ApiKey);
            WebClient wc = new WebClient();
            wc.Headers.Add("X-Api-Key", ApiKey);
            strResponseValue = wc.UploadString(EndPoint + location, arguments);
            return strResponseValue;
        }

        public string PostJson(string location, JObject arguments)
        {
            string strResponseValue = string.Empty;
            Debug.WriteLine("This was searched:");
            Debug.WriteLine(EndPoint + location + "?apikey=" + ApiKey);
            String argumentString = string.Empty;
            argumentString = JsonConvert.SerializeObject(arguments);
            //byte[] byteArray = Encoding.UTF8.GetBytes(argumentString);
            HttpWebRequest request = WebRequest.CreateHttp(EndPoint + location);// + "?apikey=" + apiKey);
            request.Method = "POST";
            request.Headers["X-Api-Key"]=ApiKey;
            request.ContentType = "application/json";
            using (var streamWriter = new StreamWriter(request.GetRequestStream()))
            {
                streamWriter.Write(argumentString);
            }
            HttpWebResponse httpResponse;
            httpResponse = (HttpWebResponse)request.GetResponse();

            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                var result = streamReader.ReadToEnd();
            }
            return strResponseValue;
        }

        public string Delete(string location)
        {
            string strResponseValue = string.Empty;
            Debug.WriteLine("This was deleted:");
            Debug.WriteLine(EndPoint + location + "?apikey=" + ApiKey);
            HttpWebRequest request = WebRequest.CreateHttp(EndPoint + location);// + "?apikey=" + apiKey);
            request.Method = "DELETE";
            request.Headers["X-Api-Key"]=ApiKey;
            HttpWebResponse httpResponse;
            httpResponse = (HttpWebResponse)request.GetResponse();
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                var result = streamReader.ReadToEnd();
            }
            return strResponseValue;
        }
        public string PostMultipart(string packagestring,string location)
        {
            Debug.WriteLine("A Multipart was posted to:");
            Debug.WriteLine(EndPoint + location + "?apikey=" + ApiKey);
            string strResponseValue = String.Empty;
            var webClient = new WebClient();
            string boundary = "------------------------" + DateTime.Now.Ticks.ToString("x");
            webClient.Headers.Add("Content-Type", "multipart/form-data; boundary=" + boundary);
            webClient.Headers.Add("X-Api-Key", ApiKey);
            packagestring.Replace("{0}", boundary);
            string package = packagestring.Replace("{0}", boundary);

            var nfile = webClient.Encoding.GetBytes(package);
            byte[] resp = webClient.UploadData(EndPoint+location, "POST", nfile);
            return strResponseValue;
        }
        public void WebsocketStop()
        {
            listening = false;
        }
        public void WebsocketStart()
        {
            if (!listening)
            {
                listening = true;
                Thread syncthread = new Thread(new ThreadStart(WebsocketSync));
                syncthread.Start();
            }
        }
        public void WebsocketSync()
        {
            string temporarystorage ="";
            var buffer = new byte[4096];
            CancellationToken cancellation = CancellationToken.None;
            //var awaiter = task.GetAwaiter();
            WebSocketReceiveResult received;// = WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellation).GetAwaiter().GetResult();
            while (!WebSocket.CloseStatus.HasValue&&listening)
            {
                received = WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellation).GetAwaiter().GetResult();
                string text = System.Text.Encoding.UTF8.GetString(buffer, 0, received.Count);
                JObject obj=null;// = JObject(text);
                //JObject.Parse(text);
                try
                {
                    obj = JObject.Parse(text);
                }catch{
                    temporarystorage += text;
                    try
                    {
                        obj=JObject.Parse(temporarystorage);
                        temporarystorage = "";
                    }
                    catch
                    {
                        Debug.WriteLine("had to read something in more lines");
                    }
                }
                if (obj != null){
                    JToken current = obj.Value<JToken>("current");

                    if (current!=null)
                    {
                        JToken progress = current.Value<JToken>("progress");
                        if (progress!= null && ProgressHandlers!=null)
                        {
                            OctoprintJobProgress jobprogress = new OctoprintJobProgress();
                            jobprogress.Completion = progress.Value<double?>("completion") ?? -1.0;
                            jobprogress.Filepos = progress.Value<int?>("filepos") ?? -1;
                            jobprogress.PrintTime = progress.Value<int?>("printTime") ?? -1;
                            jobprogress.PrintTimeLeft = progress.Value<int?>("printTimeLeft") ?? -1;
                            ProgressHandlers(jobprogress);
                            Console.WriteLine("hey, i tried to post");
                        }

                        JToken job = current.Value<JToken>("job");
                        if (job != null && JobinfoHandlers!=null)
                        {
                            OctoprintJobInfo jobInfo = new OctoprintJobInfo();
                            jobInfo.EstimatedPrintTime = job.Value<int?>("estimatedPrintTime") ?? -1;
                            JToken filament = job.Value<JToken>("filament");
                            if (filament.HasValues)
                                jobInfo.Filament = new OctoprintFilamentInfo
                                {
                                    Lenght = filament.Value<int?>("length") ?? -1,
                                    Volume = filament.Value<int?>("volume") ?? -1
                                };
                            JToken file = job.Value<JToken>("file");
                            jobInfo.File = new OctoprintFile
                            {
                                Name = file.Value<String>("name") ?? "",
                                Origin = file.Value<String>("origin") ?? "",
                                Size = file.Value<int?>("size") ?? -1,
                                Date = file.Value<int?>("date") ?? -1
                            };
                            JobinfoHandlers(jobInfo);
                            Console.WriteLine("hey, i tried to post");
                        }

                        JToken printerinfo = current.Value<JToken>("state");
                        if (printerinfo != null && PrinterstateHandlers!=null)
                        {
                            JToken stateflags = printerinfo.Value<JToken>("flags");
                            OctoprintPrinterState opstate = new OctoprintPrinterState()
                            {
                                Text = printerinfo.Value<String>("text"),
                                Flags = new OctoprintPrinterFlags()
                                {
                                    Operational = stateflags.Value<bool?>("operational") ?? false,
                                    Paused = stateflags.Value<bool?>("paused") ?? false,
                                    Printing = stateflags.Value<bool?>("printing") ?? false,
                                    Cancelling = stateflags.Value<bool?>("canceling") ?? false,
                                    SDReady = stateflags.Value<bool?>("sdReady") ?? false,
                                    Error = stateflags.Value<bool?>("error") ?? false,
                                    Ready = stateflags.Value<bool?>("ready") ?? false,
                                    ClosedOrError = stateflags.Value<bool?>("closedOrError") ?? false
                                }
                            };
                            PrinterstateHandlers(opstate);
                            Console.WriteLine("hey, i tried to post");
                        }

                        float? currentz = current.Value<float>("currentZ");
                        if (currentz != null&&CurrentZHandlers!=null)
                        {
                            CurrentZHandlers((float)currentz);
                            Console.WriteLine("hey, i tried to post");
                        }

                    }
                }

            }
        }
    }
    public class OctoprintTracker{
        protected OctoprintConnection Connection { get; set; }
        public OctoprintTracker(OctoprintConnection con)
        {
            Connection = con;
        }
    }
}