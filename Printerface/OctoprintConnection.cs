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
        public void Listen()
        {
            if (!listening)
            {
                listening = true;
                Thread syncthread = new Thread(new ThreadStart(WebsocketRead));
                syncthread.Start();
            }
        }
        public void StopListening()
        {

            listening = false;
        }
        private void WebsocketRead()
        {
            var buffer = new byte[4096];
            CancellationToken cancellation = CancellationToken.None;
            //var awaiter = task.GetAwaiter();
            WebSocketReceiveResult received = WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellation).GetAwaiter().GetResult();
            while (!WebSocket.CloseStatus.HasValue && listening)
            {
                string text = System.Text.Encoding.UTF8.GetString(buffer, 0, received.Count);
                Console.WriteLine(text);
                received = WebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellation).GetAwaiter().GetResult();
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