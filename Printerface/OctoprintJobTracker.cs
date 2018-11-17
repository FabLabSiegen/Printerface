using System;
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
        public OctoprintJobInfo GetInfo()
        {
            OctoprintJobInfo result = new OctoprintJobInfo();
            string jobInfo = Connection.Get("api/job");
            JObject data = JsonConvert.DeserializeObject<JObject>(jobInfo);
            JToken job = data.Value<JToken>("job");
            result.EstimatedPrintTime = job.Value<int?>("estimatedPrintTime") ?? -1;
            JToken filament = job.Value<JToken>("filament");
            if(filament.HasValues)
            result.Filament = new OctoprintFilamentInfo
            {
                Lenght = filament.Value<int?>("length") ?? -1,
                Volume = filament.Value<int?>("volume") ?? -1
            };
            JToken file = job.Value<JToken>("file");
            result.File = new OctoprintFile
            {
                Name = file.Value<String>("name") ?? "",
                Origin = file.Value<String>("origin") ?? "",
                Size = file.Value<int?>("size") ?? -1,
                Date = file.Value<int?>("date") ?? -1
            };
            return result;
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
    public class OctoprintFilamentInfo
    {
        public int Lenght { get; set; }
        public double Volume { get; set; }
    }
    public class OctoprintJobInfo
    {
        public OctoprintFile File { get; set; }
        public int EstimatedPrintTime { get; set; }
        public OctoprintFilamentInfo Filament { get; set; }
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
