using System;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Diagnostics;

namespace OctoprintClient
{
    public class OctoprintPosTracker:OctoprintTracker
    {
        private string GCodeString { get; set; }
        private float Xpos { get; set; }
        private float Ypos { get; set; }
        private float Zpos { get; set; }
        private float FeedrateBuffer { get; set; }
        private float[] MaxFeedRateBuffer { get; set; }
        private List<float[]> MovesBuffer { get; set; }
        private float[] BufferPos { get; set; }
        private static int GcodePos { get; set; }
        private static int LastSyncPos { get; set; }
        private Thread syncthread;
        private volatile bool threadstop=false;
        private bool threadrunning=false;
        private Stopwatch watch = Stopwatch.StartNew();
        public OctoprintPosTracker(OctoprintConnection con):base(con)
        {
            BufferPos = new float[] { 0, 0, 0, 0 };
            MaxFeedRateBuffer = new float[] { 200, 200, 12 };
            MovesBuffer = new List<float[]>();
            //syncthread = new Thread(new ThreadStart(AutoSync));
            //syncthread.Start();
        }
        ~OctoprintPosTracker(){

            threadstop = true;
            try
            {
                if (syncthread != null){
                    syncthread.Join();
                }
            }
            catch (ThreadStateException)
            {

            }
            threadrunning = false;
            watch.Stop();
        }
        public float[] GetPosAsync()
        {
            if (!threadrunning)
            {
                Console.WriteLine("starting thread at 56");
                StartThread();
            }
            float[] PosResult = { 0, 0, 0 };
            long millisecondsPassed = watch.ElapsedMilliseconds;
            float secondsPassed = (float)millisecondsPassed / (float)1000.0;
            //Console.WriteLine(secondsPassed);
            for (int i = 0; i < MovesBuffer.Count - 1; i++)
            {
                //Console.WriteLine("reading movesbuffer");

                if (secondsPassed >= MovesBuffer[i + 1][3])
                    secondsPassed -= MovesBuffer[i + 1][3];
                else
                {
                    float factor = 0;
                    if (Math.Abs(MovesBuffer[i + 1][3]) < 0.1)
                        factor = secondsPassed / (MovesBuffer[i + 1][3]);
                    PosResult = new float[] { MovesBuffer[i][0] + (MovesBuffer[i + 1][0] - MovesBuffer[i][0]) * factor,
                        MovesBuffer[i][1] + (MovesBuffer[i + 1][1] - MovesBuffer[i][1]) * factor,
                        MovesBuffer[i][2] + (MovesBuffer[i + 1][2] - MovesBuffer[i][2]) * factor };
                    //Console.WriteLine(movesBuffer[i][0]+"/"+movesBuffer[i][1]+"/"+movesBuffer[i][2]);
                    //Console.WriteLine(PosResult[0]+"/"+PosResult[1]+"/"+PosResult[2]);
                    break;
                }
            }
            //Console.WriteLine("Returning");
            return PosResult;
            //Timersince Sync
            //get Pos from Buffer
        }
        public float[] GetCurrentPosSync()
        {
            float[] coordinateResponseValue = { 0, 0, 0 };
            //string jobInfo = Connection.Get("api/job");
            //JObject data = JsonConvert.DeserializeObject<JObject>(jobInfo);
            //JToken progressdata = data.Value<JToken>("progress");
            OctoprintJobProgress progress = Connection.Jobs.GetProgress();
            OctoprintJobInfo info = Connection.Jobs.GetInfo();
            if (GCodeString == null)
            {
                if (info.File.Name != "")
                {
                    GetGCode(info.File.Origin + "/" + info.File.Name);
                }
                ////Console.WriteLine(data["progress"]["filepos"]);
                //try
                //{
                //    JToken jobdata = data.Value<JToken>("job");
                //    if(jobdata != null)
                //    {
                //        JToken filedata = jobdata.Value<JToken>("file");
                //        if (filedata != null)
                //        {
                //            string filelocation = filedata.Value<String>("origin") + "/" + filedata.Value<String>("name");
                //            if(progressdata!=null)
                //                GetGCode(filelocation, progressdata.Value<int?>("filepos")??-1);
                //        }
                //    }
                //}
                //catch (Exception e)
                //{

                //    Debug.WriteLine("Printer is currently not active");
                //    Debug.WriteLine(e.Message);
                //    GCodeString = null;
                //    return coordinateResponseValue;
                //}
            }
            //if (progressdata != null)
            //{

                string[] linesLeft = GCodeString.Substring(progress.Filepos).Split(new[] { '\r', '\n' });
                if (GcodePos != (progress.Filepos))
                {
                    if (GCodeString.Length > (progress.Filepos))
                    {
                        string currline = linesLeft[0];
                        ReadLineForwards(currline);
                    }
                    GcodePos = progress.Filepos;


                }
                if (MovesBuffer.Count == 0)
                {
                    BufferPos = new float[] { Xpos, Ypos, Zpos };
                    for (int i = 0; i < linesLeft.Length; i++)
                    {
                        ReadLineToBuffer(linesLeft[i]);
                    }
                }
            //}

            coordinateResponseValue[0] = Xpos;
            coordinateResponseValue[1] = Ypos;
            coordinateResponseValue[2] = Zpos;
            return coordinateResponseValue;
        }
        public void SetPos(float? x=null, float? y=null, float? z=null)
        {
            if (x.HasValue)
            {
                Xpos = (float)x;
            }
            if (y.HasValue)
            {
                Ypos = (float)y;
            }
            if (z.HasValue)
            {
                Zpos = (float)z;
            }
        }
        public void Move(float? x=null, float? y=null, float? z=null)
        {

            if (x.HasValue)
            {
                Xpos += (float)x;
            }
            if (y.HasValue)
            {
                Ypos += (float)y;
            }
            if (z.HasValue)
            {
                Zpos += (float)z;
            }
        }
        private void GetGCode(string location)
        {
            Console.WriteLine("get gcode location "+ location );
            using (var wc = new System.Net.WebClient())
            {
                try
                {
                    //wc.Headers.Add("X-Api-Key", Connection.ApiKey);
                    Console.WriteLine("downloading: " + Connection.EndPoint + "downloads/files/" + location + "?apikey=" + Connection.ApiKey);
                    GCodeString = wc.DownloadString(Connection.EndPoint + "downloads/files/" + location+ "?apikey=" + Connection.ApiKey);
                }
                catch (Exception e)
                {
                    Console.WriteLine("download failed with");
                    Console.WriteLine(e);
                    return;
                }
            }
            Console.WriteLine("got this long a String:" + GCodeString.Length);
            if (GCodeString.Length == 0)
            {
                GCodeString = null;
                return;
            }
            //Console.WriteLine("got "+GCodeString);
            OctoprintJobProgress progress = Connection.Jobs.GetProgress();
            if (progress.Filepos == 0)
                return;
            string[] preloadString = new string[0];
            if (GCodeString.Length > 0)
            {
                preloadString = GCodeString.Substring(0, Math.Max(Math.Min(GCodeString.Length, progress.Filepos) - 1,0)).Split(new[] { '\r', '\n' });
            }
            for (int i = preloadString.Length - 1; i >= 0 && (Math.Abs(Zpos) < 0.001 || Math.Abs(Ypos) < 0.001 || Math.Abs(Xpos) < 0.001); i -= 1)
            {
                ReadLineBackwards(preloadString[i]);
            }

            string[] linesLeft = GCodeString.Substring(progress.Filepos).Split(new[] { '\r', '\n' });
            if (GcodePos != progress.Filepos)
            {
                if (GCodeString.Length > progress.Filepos)
                {
                    string currline = linesLeft[0];
                    ReadLineForwards(currline);
                }
                Console.WriteLine("setting Gcodepos to new value " + progress.Filepos);
                GcodePos = progress.Filepos;
            }
            if (MovesBuffer.Count == 0)
            {
                BufferPos = new float[] { Xpos, Ypos, Zpos };
                for (int i = 0; i < linesLeft.Length; i++)
                {
                    ReadLineToBuffer(linesLeft[i]);
                }
            }
            else
            {
                Debug.WriteLine("bufferpos isn't 0");
            }
            //GcodePos = progress.Filepos - 1;
        }
        private float[] ReadLine(string currline)
        {
            float[] lineResponseValue = { -1, -1, -1, -1 };
            if (currline.Length > 1 && currline.Substring(0, 2) == "G1")
            {
                foreach (string part in currline.Split(new[] { ' ', ';' }))
                {
                    if (part.Length > 1)
                    {
                        switch (part[0])
                        {
                            case 'X':
                                lineResponseValue[0] = float.Parse(part.Substring(1), CultureInfo.InvariantCulture.NumberFormat);
                                break;
                            case 'Y':
                                lineResponseValue[1] = float.Parse(part.Substring(1), CultureInfo.InvariantCulture.NumberFormat);
                                break;
                            case 'Z':
                                lineResponseValue[2] = float.Parse(part.Substring(1), CultureInfo.InvariantCulture.NumberFormat);
                                break;
                            case 'F':
                                lineResponseValue[3] = float.Parse(part.Substring(1), CultureInfo.InvariantCulture.NumberFormat) / 2;//Divided by 6 for conversion from mm/minute to cm/second
                                break;
                        }
                    }
                }

            }
            else if (currline.Length > 3 && currline.Substring(0, 4) == "M203")
            {
                foreach (string part in currline.Split(new[] { ' ', ';' }))
                {
                    if (part.Length > 1)
                    {
                        switch (part[0])
                        {
                            case 'X':
                                MaxFeedRateBuffer[0] = float.Parse(part.Substring(1), CultureInfo.InvariantCulture.NumberFormat);
                                break;
                            case 'Y':
                                MaxFeedRateBuffer[1] = float.Parse(part.Substring(1), CultureInfo.InvariantCulture.NumberFormat);
                                break;
                            case 'Z':
                                MaxFeedRateBuffer[2] = float.Parse(part.Substring(1), CultureInfo.InvariantCulture.NumberFormat);
                                break;
                        }
                    }
                }
            }
            return lineResponseValue;
        }
        private void ReadLineForwards(string currline)
        {
            float[] coords = ReadLine(currline);
            if (coords[0] > 0) Xpos = coords[0];
            if (coords[1] > 0) Ypos = coords[1];
            if (coords[2] > 0) Zpos = coords[2];
            if (coords[3] > 0) FeedrateBuffer = coords[3];
        }
        private void ReadLineToBuffer(string currline)
        {
            float[] coords = ReadLine(currline);
            if (coords[3] > 0)
                FeedrateBuffer = coords[3];
            //Console.WriteLine("new feedrate"+feedrateBuffer);
            if (coords[0] > 0 || coords[1] > 0 || coords[2] > 0)
            {
                if (coords[0] < 0)
                    coords[0] = BufferPos[0];
                if (coords[1] < 0)
                    coords[1] = BufferPos[1];
                if (coords[2] < 0)
                    coords[2] = BufferPos[2];
                float[] distances = { Math.Abs(coords[0] - BufferPos[0]), Math.Abs(coords[1] - BufferPos[1]), Math.Abs(coords[2] - BufferPos[2]) };
                float[] speed = { Math.Min((FeedrateBuffer / 60), MaxFeedRateBuffer[0]), Math.Min((FeedrateBuffer / 60), MaxFeedRateBuffer[1]), Math.Min((FeedrateBuffer / 60), MaxFeedRateBuffer[2]) };
                float[] times = { (float)distances[0] / (float)speed[0], (float)distances[1] / (float)speed[1], (float)distances[2] / (float)speed[2] };
                float time = times.Max();
                MovesBuffer.Add(new float[] { coords[0], coords[1], coords[2], time });
                BufferPos = new float[] { coords[0], coords[1], coords[2] };
            }
        }
        private void ReadLineBackwards(string currline)
        {

            float[] coords = ReadLine(currline);
            if (coords[0] > 0 && Math.Abs(Xpos) < 0.0001) Xpos = coords[0];
            if (coords[1] > 0 && Math.Abs(Ypos) < 0.0001) Ypos = coords[1];
            if (coords[2] > 0 && Math.Abs(Zpos) < 0.0001) Zpos = coords[2];
            if (coords[3] > 0 && Math.Abs(FeedrateBuffer) < 0.0001) FeedrateBuffer = coords[3];
        }
        public void Syncpos()
        {
            //string JobStatusString = Connection.Get("api/job");
            //JObject JobStatus = JsonConvert.DeserializeObject<JObject>(JobStatusString);
            //JToken progressdata = JobStatus.Value<JToken>("progress");
            //if (GCodeString == null)
            //{
            //    //Console.WriteLine(data["progress"]["filepos"]);
            //    try
            //    {
            //        JToken jobdata = JobStatus.Value<JToken>("job");
            //        if (jobdata != null)
            //        {
            //            JToken filedata = jobdata.Value<JToken>("file");
            //            if (filedata != null)
            //            {
            //                string filelocation = filedata.Value<String>("origin") + "/" + filedata.Value<String>("name");
            //                if (progressdata != null)
            //                    GetGCode(filelocation, progressdata.Value<int?>("filepos") ?? -1);
            //            }
            //        }
            //    }
            //    catch (Exception e)
            //    {

            //        Console.WriteLine("Printer is currently not active");
            //        Console.WriteLine(e.Message);
            //        GCodeString = null;
            //    }
            //}
            //else
            //{
            //    if (JobStatus.Value<JToken>("job") == null)
            //    {
            //        GCodeString = null;
            //    }
            //}

            OctoprintJobProgress progress = Connection.Jobs.GetProgress();
            OctoprintJobInfo info = Connection.Jobs.GetInfo();
            Console.WriteLine("about to read gcode from" + info.File.Origin);
            if (GCodeString == null)
            {
                if (info.File.Name != "" &&info.File.Origin=="local")
                {
                    GetGCode(info.File.Origin + "/" + info.File.Name);

                }
            }
            if (GCodeString == null)
            {
                return;
            }
            Console.WriteLine("readgcode");
            int bitspassed = (progress.Filepos - 1) - LastSyncPos;
            string[] linesPassed = { };
            Console.WriteLine("bitspassed: ");
            Console.WriteLine(bitspassed);
            Console.WriteLine(" GCodeString==null: " + GCodeString.Length);
            Console.WriteLine(" LastSyncPos = " + LastSyncPos);
            if (bitspassed > 0 && GCodeString != null && GCodeString.Length > LastSyncPos + bitspassed)
                linesPassed = GCodeString.Substring(LastSyncPos, bitspassed).Split(new[] { '\r', '\n' });
            else
                Console.WriteLine("SomethingWrong");
            int count = 0;
            float secondspassed = 0;
            foreach (string line in linesPassed)
            {
                if (ReadLine(line)[0] < 0 || ReadLine(line)[1] < 0 || ReadLine(line)[2] < 0)
                {
                    if (MovesBuffer.Count <= count)
                        Console.WriteLine("count to high");
                    else
                        secondspassed += MovesBuffer[count][3];
                    count++;
                }
            }
            if (count > 1 && MovesBuffer.Count >= count)
            {
                MovesBuffer.RemoveRange(0, count);
                LastSyncPos = progress.Filepos;
                //Console.WriteLine("10 seconds passed in: "+secondspassed+" seconds and the next move is this long: "+movesBuffer[0][3]);
                //movesBuffer.RemoveAt(0);
            }
        }
        public Boolean IsReady()
        {
            //Console.WriteLine("Gcodepos "+GcodePos+" Lastsyncpos "+LastSyncPos);
            if (GcodePos != 0 && LastSyncPos == 0)
            {
                LastSyncPos = GcodePos;
                return false;
            }
            else if (GcodePos != 0)
                return true;
            else return false;
        }

        public string Home(string[] axes)
        {
            return Connection.Printers.MakePrintheadHome(axes);
        }
        public string Home()
        {
            return Home(new string[]{"x","y","z" });
        }

        public string MoveTo(float? x=null,float? y=null, float? z=null, bool? absolute=null, int? speed=null)
        {
            return Connection.Printers.MakePrintheadJog(x, y, z, absolute, speed);
        }

        public void StartThread()
        {
            if (!threadrunning)
            {
                Console.WriteLine("make thread run");
                threadrunning = true;
                syncthread = new Thread(new ThreadStart(AutoSync));
                syncthread.Start();
                Sync();

            }
        }
        public void StopThread()
        {
            threadstop = true;
        }
        private void Sync()
        {
            try
            {
                Syncpos();
            }
            catch (System.Net.WebException e)
            {
                Console.WriteLine("something happened with the web connection"+e.Message);
            }
            watch.Reset();
            watch.Start();
        }
        private void AutoSync()
        {
            Console.WriteLine("autosync");
            while (threadstop == false)
            {
                if (IsReady())
                {
                    Sync();
                    Console.WriteLine("Syncing");
                    //Console.WriteLine("buffer is this long: " + movesBuffer.Count);
                    Thread.Sleep(10000);
                }
                else
                    Thread.Sleep(50);
                {

                }
            }
        }
    }
}
