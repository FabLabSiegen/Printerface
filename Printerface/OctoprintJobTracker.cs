using System;
using System.Collections.Generic;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace OctoprintClient
{
    public class OctoprintJobTracker : OctoprintTracker
    {
        public OctoprintJobTracker(OctoprintConnection con) : base(con)
        {
        }
        public OctoprintJobProgress GetProgress()
        {
            string jobInfo = Connection.Get("api/job");
            JObject data = JsonConvert.DeserializeObject<JObject>(jobInfo);
            OctoprintJobProgress result = new OctoprintJobProgress();
            JToken progress = data.Value<JToken>("progress");
            result.Completion = progress.Value<double?>("completion") ?? -1.0;
            result.Filepos = progress.Value<int?>("filepos") ?? -1;
            result.PrintTime = progress.Value<int?>("printTime") ?? -1;
            result.PrintTimeLeft = progress.Value<int?>("printTimeLeft")??-1;
            return result;
        }
        private string Post(string command, string action)
        { 
            string returnValue = string.Empty;
            JObject data = new JObject
            {
                { "command", command }
            };
            if (action != "")
            {
                data.Add("action",action);
            }
            try
            {
                returnValue =Connection.PostJson("api/job", data);
            }
            catch (WebException e)
            {
                switch (((HttpWebResponse)e.Response).StatusCode)
                {
                    case HttpStatusCode.Conflict:
                        return "409 Current jobstate is incompatible with this type of interaction";

                    default:
                        return "unknown webexception occured";
                }

            }
            return returnValue;
        }
        public string StartJob()
        {
            return Post("start", "");
        }
        public string CancelJob()
        {
            return Post("cancel", "");
        }
        public string RestartJob()
        {
            return Post("restart", "");
        }
        public string PauseJob()
        {
            return Post("pause", "pause");
        }
        public string ResumeJob()
        {
            return Post("pause", "resume");
        }
        public string ToggleJob()
        {
            return Post("pause", "toggle");
        }
    }
    public class OctoprintJobProgress
    {
        public Double Completion { get; set; }
        public int Filepos { get; set; }
        public int PrintTime { get; set; }
        public int PrintTimeLeft { get; set; }
        public override string ToString()
        {
            if (Filepos != -1)
                return "Completion: " + Completion + "\nFilepos: " + Filepos + "\nPrintTime: " + PrintTime + "\nPrintTimeLeft: " + PrintTimeLeft + "\n";
            else
                return "No Job found running";
        }
    }
}
