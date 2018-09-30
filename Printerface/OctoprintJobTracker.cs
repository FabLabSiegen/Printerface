using System;
using System.Collections.Generic;
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
            string jobInfo = connection.MakeRequest("api/job");
            JObject data = JsonConvert.DeserializeObject<JObject>(jobInfo);
            OctoprintJobProgress result = new OctoprintJobProgress();
            JToken progress = data.Value<JToken>("progress");
            result.Completion = progress.Value<double?>("completion") ?? -1.0;
            result.Filepos = progress.Value<int?>("filepos") ?? -1;
            result.PrintTime = progress.Value<int?>("printTime") ?? -1;
            result.PrintTimeLeft = progress.Value<int?>("printTimeLeft")??-1;
            return result;
        }
    }
    public class OctoprintJobProgress
    {
        public Double Completion { get; set; }
        public int Filepos { get; set; }
        public int PrintTime { get; set; }
        public int PrintTimeLeft { get; set; }
    }
}
