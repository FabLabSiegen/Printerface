using System;
using System.IO;
using System.Net;

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
            Position.GetCurrentPosSync();
            Files = new OctoprintFileTracker(this);
            Jobs = new OctoprintJobTracker(this);
            Printers = new OctoprintPrinterTracker(this);
        }
        public string MakeRequest(string location)
        {
            string strResponseValue = string.Empty;
			Console.WriteLine("This was searched:");
			Console.WriteLine(EndPoint + location + "?apikey=" + ApiKey);
            WebClient wc = new WebClient();
            //wc.Headers.Add("X-Api-Key", ApiKey);
            using (Stream downStream = wc.OpenRead(EndPoint + location + "?apikey=" + ApiKey)){
                using (StreamReader sr = new StreamReader(downStream)){
                    strResponseValue = sr.ReadToEnd();
                }
            }
            /*try{
				HttpWebRequest request = WebRequest.CreateHttp(EndPoint + location);// + "?apikey=" + apiKey);
    	        request.Method = HttpMethod.ToString();
				request.Headers["X-Api-Key"]=ApiKey;
            	using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            	{
                	if (response.StatusCode != HttpStatusCode.OK)
                	{
                    	throw new ApplicationException("Response Status Code:" + response.StatusCode.ToString());
                	}
                	using (Stream responseStream = response.GetResponseStream())
                	{
                    	if (responseStream != null)
                    	{
                        	using (StreamReader reader = new StreamReader(responseStream))
                        	{
                            	strResponseValue = reader.ReadToEnd();
                        	}
                    	}
                	}
            	}
			}
			catch(Exception e){
                Console.WriteLine("couldn't connect");
                Console.WriteLine(e.Source);
                Console.WriteLine(e.StackTrace);
				Console.WriteLine(e.Message);
                Console.WriteLine(e.GetType());
			}*/
            return strResponseValue;
        }
    }
    public class OctoprintTracker{
        protected OctoprintConnection connection { get; set; }
        public OctoprintTracker(OctoprintConnection con)
        {
            connection = con;
        }
    }
}