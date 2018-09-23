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
            OctoprintJobProgress result = new OctoprintJobProgress { Completion = (double)data["progress"]["completion"], Filepos = (int)data["progress"]["filepos"], PrintTime = (int)data["progress"]["printTime"], PrintTimeLeft = (int)data["progress"]["printTimeLeft"] };
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
