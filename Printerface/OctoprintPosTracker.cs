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
    /// <summary>
    /// Octoprint position tracker. tracks the Position and guesses the position if needed.
    /// </summary>
    public class OctoprintPosTracker:OctoprintTracker
    {
        /// <summary>
        /// The GCode in the form of a String.
        /// </summary>
        private string GCodeString { get; set; }

        /// <summary>
        /// The X position.
        /// </summary>
        private float Xpos { get; set; }
        /// <summary>
        /// The Y position.
        /// </summary>
        private float Ypos { get; set; }
        /// <summary>
        /// The Z position.
        /// </summary>
        private float Zpos { get; set; }

        /// <summary>
        /// Buffers changes in the feedrate for guessing
        /// </summary>
        private float FeedrateBuffer { get; set; }
        private float[] MaxFeedRateBuffer { get; set; }

        /// <summary>
        /// Buffers movements for guessing
        /// </summary>
        private List<float[]> MovesBuffer { get; set; }

        /// <summary>
        /// Buffers positions for guessing
        /// </summary>
        private float[] BufferPos { get; set; }

        /// <summary>
        /// The Position in bytes within the GCode
        /// </summary>
        private static int GcodePos { get; set; }

        /// <summary>
        /// The position synced last.
        /// </summary>
        private static int LastSyncPos { get; set; }

        /// <summary>
        /// the Thread for syncing
        /// </summary>
        private Thread syncthread;

        /// <summary>
        /// used to stop the thread
        /// </summary>
        private volatile bool threadstop;

        /// <summary>
        /// used to keep track of the passed time for guessing the position
        /// </summary>
        private Stopwatch watch = Stopwatch.StartNew();

        /// <summary>
        /// Initializes a Positiontracker, this shouldn't be done directly and is part of the Connection it needs anyway
        /// </summary>
        /// <param name="con">The Octoprint connection it connects to.</param>
        public OctoprintPosTracker(OctoprintConnection con):base(con)
        {
            BufferPos = new float[] { 0, 0, 0, 0 };
            MaxFeedRateBuffer = new float[] { 200, 200, 12 };
            MovesBuffer = new List<float[]>();
            //syncthread = new Thread(new ThreadStart(AutoSync));
            //syncthread.Start();
        }

        /// <summary>
        /// Releases unmanaged resources and performs other cleanup operations before the
        /// <see cref="T:OctoprintClient.OctoprintPosTracker"/> is reclaimed by garbage collection.
        /// </summary>
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
            watch.Stop();
        }

        /// <summary>
        /// Gets the position asynchroniously, guesses between Sync intervals, starts thread if it isn't allready running.
        /// </summary>
        /// <returns>The position of the Printhead.</returns>
        public float[] GetPosAsync()
        {
            if (!syncthread.IsAlive)
            {
                StartThread();
            }
            float[] PosResult = { 0, 0, 0 };
            long millisecondsPassed = watch.ElapsedMilliseconds;
            float secondsPassed = (float)millisecondsPassed / (float)1000.0;
            for (int i = 0; i < MovesBuffer.Count - 1; i++)
            {
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
                    break;
                }
            }
            return PosResult;
            //Timersince Sync
            //get Pos from Buffer
        }

        /// <summary>
        /// Gets the current position Synchroniously at this exact time, should not be used many times per seconds though, that's why GetPosAsync exists.
        /// </summary>
        /// <returns>The current Position of the Printhead.</returns>
        public float[] GetCurrentPosSync()
        {
            float[] coordinateResponseValue = { 0, 0, 0 };
            OctoprintJobProgress progress = Connection.Jobs.GetProgress();
            OctoprintJobInfo info = Connection.Jobs.GetInfo();
            if (GCodeString == null)
            {
                if (info.File.Name != "")
                {
                    GetGCode(info.File.Origin + "/" + info.File.Name);
                }
            }
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

        /// <summary>
        /// Sets the internal representation of the Printhead-Position, doesn't post to octoprint though
        /// </summary>
        /// <param name="x">The x coordinate.</param>
        /// <param name="y">The y coordinate.</param>
        /// <param name="z">The z coordinate.</param>
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

        /// <summary>
        /// Moves the internal representation of the Printhead-Position, doesn't post to octoprint though
        /// </summary>
        /// <param name="x">The x coordinate.</param>
        /// <param name="y">The y coordinate.</param>
        /// <param name="z">The z coordinate.</param>
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

        /// <summary>
        /// Gets the gcode from the given location
        /// </summary>
        /// <param name="location">Location the GCode file exists under, can only get this from local.</param>
        private void GetGCode(string location)
        {
            Debug.WriteLine("get gcode location "+ location );
            using (var wc = new System.Net.WebClient())
            {
                try
                {
                    Debug.WriteLine("downloading: " + Connection.EndPoint + "downloads/files/" + location + "?apikey=" + Connection.ApiKey);
                    GCodeString = wc.DownloadString(Connection.EndPoint + "downloads/files/" + location+ "?apikey=" + Connection.ApiKey);
                }
                catch (Exception e)
                {
                    Debug.WriteLine("download failed with");
                    Debug.WriteLine(e);
                    return;
                }
            }
            Debug.WriteLine("got this long a String:" + GCodeString.Length);
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
                Debug.WriteLine("setting Gcodepos to new value " + progress.Filepos);
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

        /// <summary>
        /// Reads one line of the GCode, only G1 and M203 return something
        /// </summary>
        /// <returns>The Coordinates of the G1 or M203 command</returns>
        /// <param name="currline">The line to read</param>
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

        /// <summary>
        /// Reads the line and sets the Positions.
        /// </summary>
        /// <param name="currline">The line</param>
        private void ReadLineForwards(string currline)
        {
            float[] coords = ReadLine(currline);
            if (coords[0] > 0) Xpos = coords[0];
            if (coords[1] > 0) Ypos = coords[1];
            if (coords[2] > 0) Zpos = coords[2];
            if (coords[3] > 0) FeedrateBuffer = coords[3];
        }

        /// <summary>
        /// Reads the line and adds it to the buffer for guessing later.
        /// </summary>
        /// <param name="currline">The Line to read.</param>
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

        /// <summary>
        /// Sets the position by reading what happened in the past before the GCodePos and adding things that are not set.
        /// </summary>
        /// <param name="currline">The Line to read</param>
        private void ReadLineBackwards(string currline)
        {

            float[] coords = ReadLine(currline);
            if (coords[0] > 0 && Math.Abs(Xpos) < 0.0001) Xpos = coords[0];
            if (coords[1] > 0 && Math.Abs(Ypos) < 0.0001) Ypos = coords[1];
            if (coords[2] > 0 && Math.Abs(Zpos) < 0.0001) Zpos = coords[2];
            if (coords[3] > 0 && Math.Abs(FeedrateBuffer) < 0.0001) FeedrateBuffer = coords[3];
        }

        /// <summary>
        /// Syncronizes the position with a Thread
        /// </summary>
        public void Syncpos()
        {
            OctoprintJobProgress progress = Connection.Jobs.GetProgress();
            OctoprintJobInfo info = Connection.Jobs.GetInfo();
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
            int bitspassed = (progress.Filepos - 1) - LastSyncPos;
            string[] linesPassed = { };
            if (bitspassed > 0 && GCodeString != null && GCodeString.Length > LastSyncPos + bitspassed)
                linesPassed = GCodeString.Substring(LastSyncPos, bitspassed).Split(new[] { '\r', '\n' });
            else
                Debug.WriteLine("Something Wrong in Postrackers Syncpos");
            int count = 0;
            float secondspassed = 0;
            foreach (string line in linesPassed)
            {
                if (ReadLine(line)[0] < 0 || ReadLine(line)[1] < 0 || ReadLine(line)[2] < 0)
                {
                    if (MovesBuffer.Count <= count)
                        Debug.WriteLine("count seems to high");
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

        /// <summary>
        /// Checks if the GCode allready exists
        /// </summary>
        /// <returns><c>true</c>, if GCode is downloaded, <c>false</c> otherwise.</returns>
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

        /// <summary>
        /// Home the specified axes.
        /// </summary>
        /// <returns>The Http response.</returns>
        /// <param name="axes">Axes. in string "x", "y" or "z"</param>
        public string Home(string[] axes)
        {
            return Connection.Printers.MakePrintheadHome(axes);
        }
        public string Home()
        {
            return Home(new string[]{"x","y","z" });
        }

        /// <summary>
        /// Moves the actual printhead to a position.
        /// </summary>
        /// <returns>The Http response.</returns>
        /// <param name="x">The x coordinate.</param>
        /// <param name="y">The y coordinate.</param>
        /// <param name="z">The z coordinate.</param>
        /// <param name="absolute">If set to <c>true</c> The position is set to absolute.</param>
        /// <param name="speed">Speed.</param>
        public string MoveTo(float? x=null,float? y=null, float? z=null, bool absolute=false, int? speed=null)
        {
            return Connection.Printers.MakePrintheadJog(x, y, z, absolute, speed);
        }

        /// <summary>
        /// Starts the thread.
        /// </summary>
        public void StartThread()
        {
            if (!syncthread.IsAlive)
            {
                Debug.WriteLine("making thread run");
                syncthread = new Thread(new ThreadStart(AutoSync));
                syncthread.Start();
                Sync();

            }
        }

        /// <summary>
        /// Stops the thread.
        /// </summary>
        public void StopThread()
        {
            threadstop = true;
        }

        /// <summary>
        /// Sync the position.
        /// </summary>
        private void Sync()
        {
            try
            {
                Syncpos();
            }
            catch (System.Net.WebException e)
            {
                Debug.WriteLine("something happened with the web connection"+e.Message);
            }
            watch.Reset();
            watch.Start();
        }

        /// <summary>
        /// The thread function.
        /// </summary>
        private void AutoSync()
        {
            Debug.WriteLine("autosync");
            while (threadstop == false)
            {
                if (IsReady())
                {
                    Sync();
                    Debug.WriteLine("Syncing");
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
