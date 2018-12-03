using System;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace OctoprintClient
{
    /// <summary>
    /// Tracks Jobs, can get Progress or other information, start, stop or pause jobs.
    /// </summary>
    public class OctoprintJobTracker : OctoprintTracker
    {

        /// <summary>
        /// Initializes a Jobtracker, this shouldn't be done directly and is part of the Connection it needs anyway
        /// </summary>
        /// <param name="con">The Octoprint connection it connects to.</param>
        public OctoprintJobTracker(OctoprintConnection con) : base(con)
        {
        }


        /// <summary>
        /// Action for Eventhandling the Websocket Job info
        /// </summary>
        public event Action<OctoprintJobInfo> JobinfoHandlers;
        public bool JobListens()
        {
            return JobinfoHandlers != null;
        }
        public void CallJob(OctoprintJobInfo i)
        {
            JobinfoHandlers.Invoke(i);
        }

        /// <summary>
        /// Action for Eventhandling the Websocket Progress info
        /// </summary>
        public event Action<OctoprintJobProgress> ProgressinfoHandlers;
        public bool ProgressListens()
        {
            return ProgressinfoHandlers != null;
        }
        public void CallProgress(OctoprintJobProgress p) {
            ProgressinfoHandlers.Invoke(p);
        }


        /// <summary>
        /// Gets info of the current job
        /// </summary>
        /// <returns>The info.</returns>
        public OctoprintJobInfo GetInfo()
        {
            //OctoprintJobInfo result = new OctoprintJobInfo();
            string jobInfo = Connection.Get("api/job");
            JObject data = JsonConvert.DeserializeObject<JObject>(jobInfo);
            JToken job = data.Value<JToken>("job");
            OctoprintJobInfo result = new OctoprintJobInfo(job);
            return result;
        }

        /// <summary>
        /// Gets the progress of the current job
        /// </summary>
        /// <returns>The progress.</returns>
        public OctoprintJobProgress GetProgress()
        {
            string jobInfo = Connection.Get("api/job");
            JObject data = JsonConvert.DeserializeObject<JObject>(jobInfo);
            JToken progress = data.Value<JToken>("progress");
            OctoprintJobProgress result = new OctoprintJobProgress(progress);
            return result;
        }

        /// <summary>
        /// Posts a command with a certain <paramref name="action"/>.
        /// </summary>
        /// <returns>The Http Result</returns>
        /// <param name="command">The Command to execute on the Job.</param>
        /// <param name="action">The exact action withing the command to take.</param>
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

        /// <summary>
        /// Starts the job.
        /// </summary>
        /// <returns>The Http Result</returns>
        public string StartJob()
        {
            return Post("start", "");
        }

        /// <summary>
        /// Cancels the job.
        /// </summary>
        /// <returns>The Http Result</returns>
        public string CancelJob()
        {
            return Post("cancel", "");
        }

        /// <summary>
        /// Restarts the job.
        /// </summary>
        /// <returns>The Http Result</returns>
        public string RestartJob()
        {
            return Post("restart", "");
        }


        /// <summary>
        /// Pauses the job.
        /// </summary>
        /// <returns>The Http Result</returns>
        public string PauseJob()
        {
            return Post("pause", "pause");
        }

        /// <summary>
        /// Resumes the job.
        /// </summary>
        /// <returns>The Http Result</returns>
        public string ResumeJob()
        {
            return Post("pause", "resume");
        }

        /// <summary>
        /// Pauses the job if it runs, resumes the Job if it is paused.
        /// </summary>
        /// <returns>The Http Result</returns>
        public string ToggleJob()
        {
            return Post("pause", "toggle");
        }
    }
    public class OctoprintFilamentInfo
    {
        public int Lenght { get; set; }
        public double Volume { get; set; }
        public override string ToString()
        {
            return "Length: " + Lenght + "\nVolume: "+Volume;
        }
    }
    public class OctoprintJobInfo
    {

        public OctoprintJobInfo(JToken job)
        {
            EstimatedPrintTime = job.Value<int?>("estimatedPrintTime") ?? -1;
            JToken filament = job.Value<JToken>("filament");
            if (filament.HasValues)
                Filament = new OctoprintFilamentInfo
                {
                    Lenght = filament.Value<int?>("length") ?? -1,
                    Volume = filament.Value<int?>("volume") ?? -1
                };
            JToken file = job.Value<JToken>("file");
            File = new OctoprintFile((JObject)file);
        }

        public OctoprintFile File { get; set; }
        public int EstimatedPrintTime { get; set; }
        public OctoprintFilamentInfo Filament { get; set; }
        public override string ToString()
        {
            return "EstimatedPrinttime: " + EstimatedPrintTime + "\nAt File: " + File + "\nUsing Fillament: \n" + Filament;
        }
    }
    public class OctoprintJobProgress
    {
        public OctoprintJobProgress(JToken progress)
        {
            Completion = progress.Value<double?>("completion") ?? -1.0;
            Filepos = progress.Value<int?>("filepos") ?? -1;
            PrintTime = progress.Value<int?>("printTime") ?? -1;
            PrintTimeLeft = progress.Value<int?>("printTimeLeft")??-1;
        }

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
