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
    /// <summary>
    /// is the base Class connecting your project to different parts of Octoprint.
    /// </summary>
    public class OctoprintConnection
    {
        /// <summary>
        /// The end point URL like https://192.168.1.2/
        /// </summary>
        public string EndPoint { get; set; }
        /// <summary>
        /// The end point Api Key like "ABCDE12345"
        /// </summary>
        public string ApiKey { get; set; }

        /// <summary>
        /// The Websocket Client
        /// </summary>
        ClientWebSocket WebSocket { get; set; }
        /// <summary>
        /// Defines if the WebsocketClient is listening and the Tread is running
        /// </summary>
        public volatile bool listening;
        /// <summary>
        /// The size of the web socket buffer. Should work just fine, if the Websocket sends more, it will be split in 4096 Byte and reassembled in this class.
        /// </summary>
        public int WebSocketBufferSize = 4096;

        /// <summary>
        /// Gets or sets the position. of the 3D printer, guesses it if necessary from the GCODE
        /// </summary>
        public OctoprintPosTracker Position { get; set; }
        /// <summary>
        /// Gets or sets files in the Folders of the Octoprint Server
        /// </summary>
        public OctoprintFileTracker Files { get; set; }
        /// <summary>
        /// Starts Jobs or reads progress of the Octoprint Server
        /// </summary>
        public OctoprintJobTracker Jobs { get; set; }
        /// <summary>
        /// Reads the Hardware state, Temperatures and other information.
        /// </summary>
        public OctoprintPrinterTracker Printers { get; set; }

        /// <summary>
        /// Creates a <see cref="T:OctoprintClient.OctoprintConnection"/> 
        /// </summary>
        /// <param name="eP">The endpoint Address like "http://192.168.1.2/"</param>
        /// <param name="aK">The Api Key of the User account you want to use. You can get this in the user settings</param>
        public OctoprintConnection(string eP, string aK)
        {
            EndPoint = eP;
            ApiKey = aK;
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

        /// <summary>
        /// A Get request for any String using your Account
        /// </summary>
        /// <returns>The result as a String, doesn't handle Exceptions</returns>
        /// <param name="location">The url sub-address like "http://192.168.1.2/<paramref name="location"/>"</param>
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

        /// <summary>
        /// Posts a string with the rights of your Account to a given <paramref name="location"/>..
        /// </summary>
        /// <returns>The Result if any exists. Doesn't handle exceptions</returns>
        /// <param name="location">The url sub-address like "http://192.168.1.2/<paramref name="location"/>"</param>
        /// <param name="arguments">The string to post tp the address</param>
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

        /// <summary>
        /// Posts a JSON object as a string, uses JObject from Newtonsoft.Json to a given <paramref name="location"/>.
        /// </summary>
        /// <returns>The Result if any exists. Doesn't handle exceptions</returns>
        /// <param name="location">The url sub-address like "http://192.168.1.2/<paramref name="location"/>"</param>
        /// <param name="arguments">The Newtonsoft Jobject to post tp the address</param>
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

        /// <summary>
        /// Posts a Delete request to a given <paramref name="location"/>
        /// </summary>
        /// <returns>The Result if any, shouldn't return anything.</returns>
        /// <param name="location">The url sub-address like "http://192.168.1.2/<paramref name="location"/>"</param>
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

        /// <summary>
        /// Posts a multipart reqest to a given <paramref name="location"/>
        /// </summary>
        /// <returns>The Result if any.</returns>
        /// <param name="packagestring">A packagestring should be generated elsewhere and input here as a String</param>
        /// <param name="location">The url sub-address like "http://192.168.1.2/<paramref name="location"/>"</param>
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

        /// <summary>
        /// Stops the Websocket Thread.
        /// </summary>
        public void WebsocketStop()
        {
            listening = false;
        }

        /// <summary>
        /// Starts the Websocket Thread.
        /// </summary>
        public void WebsocketStart()
        {
            if (!listening)
            {
                listening = true;
                Thread syncthread = new Thread(new ThreadStart(WebsocketSync));
                syncthread.Start();
            }
        }

        /// <summary>
        /// The Websocket Thread function,runs and never stops
        /// </summary>
        private void WebsocketSync()
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

                    if (current!=null && Jobs.ProgressListens())
                    {
                        JToken progress = current.Value<JToken>("progress");
                        if (progress!= null)
                        {
                            OctoprintJobProgress jobprogress = new OctoprintJobProgress
                            {
                                Completion = progress.Value<double?>("completion") ?? -1.0,
                                Filepos = progress.Value<int?>("filepos") ?? -1,
                                PrintTime = progress.Value<int?>("printTime") ?? -1,
                                PrintTimeLeft = progress.Value<int?>("printTimeLeft") ?? -1
                            };
                            Jobs.CallProgress(jobprogress);
                        }

                        JToken job = current.Value<JToken>("job");
                        if (job != null && Jobs.JobListens())
                        {
                            OctoprintJobInfo jobInfo = new OctoprintJobInfo
                            {
                                EstimatedPrintTime = job.Value<int?>("estimatedPrintTime") ?? -1
                            };
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
                            Jobs.CallJob(jobInfo);
                        }

                        JToken printerinfo = current.Value<JToken>("state");
                        if (printerinfo != null && Printers.StateListens())
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
                            Printers.CallPrinterState(opstate);
                        }

                        float? currentz = current.Value<float>("currentZ");
                        if (currentz != null&& Printers.ZListens() )
                        {
                            Printers.CallCurrentZ((float)currentz);
                        }

                    }
                }

            }
        }
    }
    /// <summary>
    /// The base class for the different Trackers
    /// </summary>
    public class OctoprintTracker{
        protected OctoprintConnection Connection { get; set; }
        public OctoprintTracker(OctoprintConnection con)
        {
            Connection = con;
        }
    }
}