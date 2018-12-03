using System;
using System.Collections.Generic;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace OctoprintClient
{
    /// <summary>
    /// Tracks the hardware state and provides Commands
    /// </summary>
    public class OctoprintPrinterTracker:OctoprintTracker
    {
        /// <summary>
        /// The current State.
        /// </summary>
        private OctoprintFullPrinterState currentstate;

        /// <summary>
        /// The last time it was updated.
        /// </summary>
        private DateTime lastupdated;

        /// <summary>
        /// The milisecs untill an update might be necessary.
        /// </summary>
        public int BestBeforeMilisecs;

        /// <summary>
        /// Initializes a Printertracker, this shouldn't be done directly and is part of the Connection it needs anyway
        /// </summary>
        /// <param name="con">The Octoprint connection it connects to.</param>
        public OctoprintPrinterTracker(OctoprintConnection con):base(con)
        {
        }

        /// <summary>
        /// Action for Eventhandling the Websocket Printerstate info
        /// </summary>
        public event Action<OctoprintPrinterState> PrinterstateHandlers;
        public bool StateListens()
        {
            return PrinterstateHandlers != null;
        }
        public void CallPrinterState(OctoprintPrinterState ps)
        {
            PrinterstateHandlers.Invoke(ps);
        }

        /// <summary>
        /// Action for Eventhandling the Websocket Temperature offset info
        /// </summary>
        public event Action<List<int>> OffsetHandlers;
        public bool OffsetListens()
        {
            return OffsetHandlers != null;
        }
        public void CallOffset(List<int> LI)
        {
            OffsetHandlers.Invoke(LI);
        }


        /// <summary>
        /// Action for Eventhandling the Websocket Temperature info
        /// </summary>
        public event Action<List<OctoprintHistoricTemperatureState>> TempHandlers;
        public bool TempsListens()
        {
            return TempHandlers!=null;
        }
        public void CallTemp(List<OctoprintHistoricTemperatureState> LHT)
        {
            TempHandlers.Invoke(LHT);
        }

        /// <summary>
        /// Action for Eventhandling the Websocket CurrentZ info
        /// </summary>
        public event Action<float> CurrentZHandlers;
        public bool ZListens()
        {
            return CurrentZHandlers != null;
        }
        public void CallCurrentZ( float z )
        {
            CurrentZHandlers.Invoke(z);
        }


        /// <summary>
        /// Gets the full state of the printer if the BestBeforeMilisecs haven't passed.
        /// </summary>
        /// <returns>The full printer state.</returns>
        public OctoprintFullPrinterState GetFullPrinterState()
        {
            TimeSpan passed = DateTime.Now.Subtract(lastupdated);
            if (passed.Milliseconds > BestBeforeMilisecs)
            {
                return currentstate;
            }
            string jobInfo = "";
            try {
                jobInfo = Connection.Get("api/printer");
            } catch (WebException e)
            {
                if (((HttpWebResponse)e.Response).StatusCode == HttpStatusCode.Conflict) { 
                    OctoprintFullPrinterState returnval = new OctoprintFullPrinterState
                        {
                            PrinterState = new OctoprintPrinterState()
                        };
                    returnval.PrinterState.Text = "Error 409 is the Printer Connected at all?\n";
                    return returnval;
                }
            }
            JObject data=new JObject();
            data = JsonConvert.DeserializeObject<JObject>(jobInfo);
            OctoprintFullPrinterState result = new OctoprintFullPrinterState(data);
            currentstate = result;
            lastupdated = DateTime.Now;
            return result;
        }

        /// <summary>
        /// Gets the state of the printer current information only.
        /// </summary>
        /// <returns>The printer state.</returns>
        public OctoprintPrinterState GetPrinterState()
        {
            string jobInfo="";
            try
            {
                jobInfo = Connection.Get("api/printer?exclude=temperature,sd");
            }
            catch (WebException e)
            {
                if (((HttpWebResponse)e.Response).StatusCode == HttpStatusCode.Conflict)
                {
                    OctoprintPrinterState returnval = new OctoprintPrinterState
                    {
                        Text = "Error 409 is the Printer Connected at all?\n"
                    };
                    return returnval;
                }
            }
            JObject data = new JObject();
            data = JsonConvert.DeserializeObject<JObject>(jobInfo);
            JToken statedata = data.Value<JToken>("state");
            OctoprintPrinterState result = new OctoprintPrinterState(statedata);
            if (currentstate != null)
                currentstate.PrinterState = result;
            return result;

        }

        /// <summary>
        /// Makes the printhead jog.
        /// </summary>
        /// <returns>The Http Result</returns>
        /// <param name="x">The x coordinate.</param>
        /// <param name="y">The y coordinate.</param>
        /// <param name="z">The z coordinate.</param>
        /// <param name="absolute">If set to <c>true</c> absolute.</param>
        /// <param name="speed">Speed.</param>
        public string MakePrintheadJog(float? x, float? y, float? z, bool absolute, int? speed)
        {

            string returnValue = string.Empty;
            JObject data = new JObject
            {
                { "command", "jog" },
                { "absolute", absolute}
            };
            if (x.HasValue)
            {
                data.Add("x", x);
            }
            if (y.HasValue)
            {
                data.Add("y", y);
            }
            if (y.HasValue)
            {
                data.Add("z", z);
            }
            if (speed.HasValue)
            {
                data.Add("speed", speed);
            }
            if (absolute == true)
            {
                Connection.Position.SetPos(x, y, z);
            }
            else Connection.Position.Move(x, y, z);
            try
            {
                returnValue = Connection.PostJson("api/printer/printhead", data);
            }
            catch (WebException e)
            {
                switch (((HttpWebResponse)e.Response).StatusCode)
                {
                    case HttpStatusCode.Conflict:
                        return "409 The Printer is not operational";
                    default:
                        return "unknown webexception occured";
                }

            }
            return returnValue;
        }

        /// <summary>
        /// Homes the Printhead to the given axes
        /// </summary>
        /// <returns>The Http Result</returns>
        /// <param name="axes">Axes.</param>
        public string MakePrintheadHome(string[] axes)
        {
            float? x=null, y=null, z=null;
            JArray jaxes = new JArray();
            foreach (string axis in axes)
            {
                jaxes.Add(axis);
                if (axis == "x")
                    x = 0;
                if (axis == "y")
                    y = 0;
                if (axis == "z")
                    z = 0;
            }
            string returnValue = string.Empty;
            JObject data = new JObject
            {
                { "command", "home" },
                { "axes", jaxes}
            };
            try { 
                returnValue =Connection.PostJson("api/printer/printhead", data);
            }
            catch (WebException e)
            {
                switch (((HttpWebResponse)e.Response).StatusCode)
                {
                    case HttpStatusCode.Conflict:
                        return "409 The Printer is not operational";
                    case HttpStatusCode.BadRequest:
                        return "wrong axis defined, choose only x and/or y and/or z";
                    default:
                        return "unknown webexception occured";
                }

            }
            Connection.Position.SetPos(x, y, z);
            return returnValue;
        }

        /// <summary>
        /// Sets the feedrate.
        /// </summary>
        /// <returns>The Http Result</returns>
        /// <param name="feed">Feedrate.</param>
        public string SetFeedrate(int feed)
        {
            JObject data = new JObject
            {
                {"command", "feedrate"},
                {"factor", feed}
            };
            try
            {
                return Connection.PostJson("api/printer/printhead", data);
            }
            catch (WebException e)
            {
                switch (((HttpWebResponse)e.Response).StatusCode)
                {
                    case HttpStatusCode.Conflict:
                        return "409 The Printer is not operational";
                    default:
                        return "unknown webexception occured";
                }

            }
        }

        /// <summary>
        /// Sets the feedrate.
        /// </summary>
        /// <returns>The Http Result</returns>
        /// <param name="feed">Feedrate.</param>
        public string SetFeedrate(float feed)
        {
            JObject data = new JObject
            {
                {"command", "feedrate"},
                {"factor", feed}
            };
            try
            {
                return Connection.PostJson("api/printer/printhead", data);
            }
            catch (WebException e)
            {
                switch (((HttpWebResponse)e.Response).StatusCode)
                {
                    case HttpStatusCode.Conflict:
                        return "409 The Printer is not operational";
                    default:
                        return "unknown webexception occured";
                }

            }
        }

        /// <summary>
        /// Sets the temperature target.
        /// </summary>
        /// <returns>The Http Result</returns>
        /// <param name="targets">Target temperatures of the different tool heads.</param>
        public string SetTemperatureTarget(Dictionary<string, int> targets)
        {
            string returnValue = string.Empty;
            JObject data = new JObject
            {
                { "command", "target" },
                { "targets", JObject.FromObject(targets)}
            };

            try
            {
                return Connection.PostJson("api/printer/tool", data);
            }
            catch (WebException e)
            {
                switch (((HttpWebResponse)e.Response).StatusCode)
                {
                    case HttpStatusCode.Conflict:
                        return "409 The Printer is propably not operational";
                    case HttpStatusCode.BadRequest:
                        return "400 The values given seem to not be acceptable, is the tool named like tool{n}?";
                    default:
                        return "unknown webexception occured";
                }

            }
        }

        /// <summary>
        /// Sets the temperature target of tool0.
        /// </summary>
        /// <returns>The Http Result</returns>
        /// <param name="temp">Temperature to set the target to.</param>
        public string SetTemperatureTarget(int temp)
        {
            return SetTemperatureTarget(new Dictionary<string, int>(){ {"tool0",temp} });
        }

        /// <summary>
        /// Sets the temperature offset. the offset is only used in GCode
        /// </summary>
        /// <returns>The Http Result</returns>
        /// <param name="offsets">Offsets for the different tool heads.</param>
        public string SetTemperatureOffset(Dictionary<string, int> offsets)
        {
            string returnValue = string.Empty;
            JObject data = new JObject
            {
                { "command", "offset" },
                { "offsets", JObject.FromObject(offsets)}
            };
            try
            {
                return Connection.PostJson("api/printer/tool", data);
            }
            catch (WebException e)
            {
                switch (((HttpWebResponse)e.Response).StatusCode)
                {
                    case HttpStatusCode.Conflict:
                        return "409 The Printer is propably not operational";
                    case HttpStatusCode.BadRequest:
                        return "400 The values given seem to not be acceptable, is the tool named like tool{n}?";
                    default:
                        return "unknown webexception occured";
                }

            }
        }

        /// <summary>
        /// Sets the temperature offset. the offset is only used in GCode
        /// </summary>
        /// <returns>The Http Result</returns>
        /// <param name="temp">Temperature offset of tool0.</param>
        public string SetTemperatureOffset(int temp)
        {
            return SetTemperatureOffset(new Dictionary<string, int>() { { "tool0", temp } });
        }

        /// <summary>
        /// Selects the tool for configuring.
        /// </summary>
        /// <returns>The Http Result</returns>
        /// <param name="tool">Tool.</param>
        public string SelectTool(string tool)
        {
            JObject data = new JObject
            {
                { "command", "select" },
                { "tool", tool}
            };
            try
            {
                return Connection.PostJson("api/printer/tool", data);
            }
            catch (WebException e)
            {
                switch (((HttpWebResponse)e.Response).StatusCode)
                {
                    case HttpStatusCode.Conflict:
                        return "409 The Printer is propably not operational or currently printing";
                    case HttpStatusCode.BadRequest:
                        return "400 The values given seem to not be acceptable, is the tool named like tool{n}?";
                    default:
                        return "unknown webexception occured";
                }

            }
        }

        /// <summary>
        /// Extrudes the selected tool.
        /// </summary>
        /// <returns>The Http Result</returns>
        /// <param name="mm">the amount to extrude in mm</param>
        public string ExtrudeSelectedTool(int mm)
        {
            JObject data = new JObject
            {
                { "command", "extrude" },
                { "amount", mm}
            };
            try
            {
                return Connection.PostJson("api/printer/tool", data);
            }
            catch (WebException e)
            {
                switch (((HttpWebResponse)e.Response).StatusCode)
                {
                    case HttpStatusCode.Conflict:
                        return "409 The Printer is propably not operational or currently printing";
                    case HttpStatusCode.BadRequest:
                        return "400 The values given seem to not be acceptable";
                    default:
                        return "unknown webexception occured";
                }

            }
        }


        /// <summary>
        /// Sets the flowrate of the selected tool.
        /// </summary>
        /// <returns>The Http Result</returns>
        /// <param name="flow">Flowrate percents.</param>
        public string SetFlowrateSelectedTool(int flow)
        {
            JObject data = new JObject
            {
                {"command", "flowrate"},
                {"factor", flow}
            };
            try
            {
                return Connection.PostJson("api/printer/tool", data);
            }
            catch (WebException e)
            {
                switch (((HttpWebResponse)e.Response).StatusCode)
                {
                    case HttpStatusCode.Conflict:
                        return "409 The Printer is propably not operational";
                    case HttpStatusCode.BadRequest:
                        return "400 The values given seem to not be acceptable";
                    default:
                        return "unknown webexception occured";
                }

            }
        }

        /// <summary>
        /// Sets the flowrate of the selected tool.
        /// </summary>
        /// <returns>The Http Result</returns>
        /// <param name="flow">Flow ratio</param>
        public string SetFlowrateSelectedTool(float flow)
        {
            JObject data = new JObject
            {
                {"command", "flowrate"},
                {"factor", flow}
            };
            try
            {
                return Connection.PostJson("api/printer/tool", data);
            }
            catch (WebException e)
            {
                switch (((HttpWebResponse)e.Response).StatusCode)
                {
                    case HttpStatusCode.Conflict:
                        return "409 The Printer is propably not operational";
                    case HttpStatusCode.BadRequest:
                        return "400 The values given seem to not be acceptable";
                    default:
                        return "unknown webexception occured";
                }

            }
        }

        /// <summary>
        /// Sets the temperature target of the Bed.
        /// </summary>
        /// <returns>The Http Result</returns>
        /// <param name="temperature">Temperature.</param>
        public string SetTemperatureTargetBed(int temperature)
        {
            JObject data = new JObject
            {
                { "command", "target" },
                { "target", temperature}
            };
            try
            {
                return Connection.PostJson("api/printer/bed", data);
            }
            catch (WebException e)
            {
                switch (((HttpWebResponse)e.Response).StatusCode)
                {
                    case HttpStatusCode.Conflict:
                        return "409 The Printer is propably not operational or has no heated bed";
                    case HttpStatusCode.BadRequest:
                        return "400 The values given seem to not be acceptable";
                    default:
                        return "unknown webexception occured";
                }

            }
        }

        /// <summary>
        /// Sets the temperature offset of the Bed.
        /// </summary>
        /// <returns>The Http Result</returns>
        /// <param name="offset">Offset of the Temperature for GCode.</param>
        public string SetTemperatureOffsetBed(int offset)
        {
            JObject data = new JObject
            {
                { "command", "offset" },
                { "offset", offset}
            };
            try
            {
                return Connection.PostJson("api/printer/bed", data);
            }
            catch (WebException e)
            {
                switch (((HttpWebResponse)e.Response).StatusCode)
                {
                    case HttpStatusCode.Conflict:
                        return "409 The Printer is propably not operational or has no heated bed";
                    case HttpStatusCode.BadRequest:
                        return "400 The values given seem to not be acceptable";
                    default:
                        return "unknown webexception occured";
                }

            }
        }
    }

    public class OctoprintPrinterFlags
    {
        public OctoprintPrinterFlags(JToken stateflags)
        {
            Operational = stateflags.Value<bool?>("operational") ?? false;
            Paused = stateflags.Value<bool?>("paused") ?? false;
            Printing = stateflags.Value<bool?>("printing") ?? false;
            Cancelling = stateflags.Value<bool?>("canceling") ?? false;
            SDReady = stateflags.Value<bool?>("sdReady") ?? false;
            Error = stateflags.Value<bool?>("error") ?? false;
            Ready = stateflags.Value<bool?>("ready") ?? false;
            ClosedOrError = stateflags.Value<bool?>("closedOrError") ?? false;
        }

        public bool Operational { get; set; }
        public bool Paused { get; set; }
        public bool Printing { get; set; }
        public bool Cancelling { get; set; }
        public bool Pausing { get; set; }
        public bool SDReady { get; set; }
        public bool Error { get; set; }
        public bool Ready { get; set; }
        public bool ClosedOrError { get; set; }

        public override string ToString()
        {
            string returnval="";
            returnval+="Is operational:\t" + Operational +"\n";
            returnval += "Is paused:  " + Paused + "\n";
            returnval += "Is printing:    " + Printing + "\n";
            returnval += "Is Cancelling:  " + Cancelling + "\n";
            returnval += "Is Pausing: " + Pausing + "\n";
            returnval += "Is SD Card ready:   " + SDReady + "\n";
            returnval += "Has Error:  " + Error + "\n";
            returnval += "Is Ready:   " + Ready + "\n";
            returnval += "Is ClosedOrReady:   "+ClosedOrError+"\n";
            return returnval;
        }
    }
    public class OctoprintTemperature
    {
        public OctoprintTemperature(JToken temperature)
        {

            Actual = temperature.Value<double?>("actual") ?? -1.0;
            Target = temperature.Value<double?>("target") ?? -1.0;
            Offset = temperature.Value<double?>("offset") ?? -1.0;
        }

        public double Actual { get; set; }
        public double Target { get; set; }
        public double Offset { get; set; }

        public override string ToString()
        {
            return "Actual Temperature: "+Actual+"°C\nTarget Temperature: "+Target+"°C\nOffset: "+Offset+"°C\n";
        }
    }
    public class OctoprintTemperatureState
    {
        public List<OctoprintTemperature> Tools { get; set; }
        public OctoprintTemperature Bed { get; set; }
        public List<OctoprintHistoricTemperatureState> History { get; set; }
        public override string ToString()
        {
            string returnval = "Currently:\n";
            if(Bed!=null)
                returnval += "The Bed Temperature:\n" + Bed.ToString();
            if (Tools != null)
                foreach (OctoprintTemperature Tool in Tools)
                    returnval += "The Printhead tool:\n" + Tool.ToString();

            if (History != null) {
                returnval += "Past Temperature:\n";
                foreach(OctoprintHistoricTemperatureState State in History)
                    returnval += State.ToString();
            }
            return returnval;
        }
    }
    public class OctoprintHistoricTemperatureState
    {
        public OctoprintHistoricTemperatureState(JToken jToken)
        {
            Time = jToken.Value<int?>("time") ?? -1;
            Bed = new OctoprintTemperature(jToken.Value<JToken>("bed"));
            Tools = new List<OctoprintTemperature>();
            for (int i = 0; i < 256; i++)
            {
                JToken tooltemp = jToken.Value<JToken>("tool" + i);
                if (tooltemp != null)
                {
                    Tools.Add(new OctoprintTemperature(tooltemp));
                }
                else
                {
                    break;
                }
            }
        }
        public int Time { get; set; }
        public List<OctoprintTemperature> Tools { get; set; }
        public OctoprintTemperature Bed { get; set; }

        public override string ToString()
        {
            string returnval = "At " + Time+"\n";
            if(Bed!=null)
                returnval += "The Bed temperature: \n" + Bed.ToString();
            if(Tools!=null)
                foreach(OctoprintTemperature Tool in Tools)
                    returnval += "The Printhead tool: \n" + Tool.ToString();
            return returnval;
        }
    }
    public class OctoprintPrinterState
    {
        public OctoprintPrinterState()
        {

        }
        public OctoprintPrinterState(JToken statedata)
        {

            JToken stateflags = statedata.Value<JToken>("flags");
            Text = statedata.Value<String>("text");
            Flags = new OctoprintPrinterFlags(stateflags);
        }

        public string Text { get; set; }
        public OctoprintPrinterFlags Flags { get; set; }
        public override string ToString()
        {
            string returnval = "State: " + Text + "\n";
            if(Flags!=null)
                returnval += Flags.ToString();
            return returnval;
        }
    }
    public class OctoprintFullPrinterState
    {
        public OctoprintFullPrinterState()
        {

        }
        public OctoprintFullPrinterState(JObject data)
        {

            JToken temperaturedata = data.Value<JToken>("temperature");
            JToken bedtemperature = temperaturedata.Value<JToken>("bed");
            JToken statedata = data.Value<JToken>("state");
            TempState = new OctoprintTemperatureState()
            {
                Bed = new OctoprintTemperature(bedtemperature)
            };
            SDState = data.Value<JToken>("sd").Value<bool?>("ready") ?? false;
            PrinterState = new OctoprintPrinterState(statedata);
            TempState.Tools = new List<OctoprintTemperature>();
            for (int i = 0; i < 256; i++)
            {
                JToken tooltemp = temperaturedata.Value<JToken>("tool"+i);
                if (tooltemp != null)
                {
                    TempState.Tools.Add(new OctoprintTemperature(tooltemp));
                }
                else
                {
                    break;
                }
            }
            if (temperaturedata != null && temperaturedata.Value<JToken>("history")!=null)
            {
                TempState.History = new List<OctoprintHistoricTemperatureState>();
                foreach (JObject historydata in temperaturedata["history"])
                {

                    OctoprintHistoricTemperatureState historicTempState = new OctoprintHistoricTemperatureState(historydata);
                    TempState.History.Add(historicTempState);
                }
            }
        }

        public OctoprintTemperatureState TempState { get; set; }
        public bool SDState { get; set; }
        public OctoprintPrinterState PrinterState { get; set; }
        public override string ToString()
        {
            string returnval ="";
            if (PrinterState != null)
                returnval += PrinterState.ToString() + "SDState: " + SDState + "\n";
            if(TempState!=null)
                returnval+= TempState.ToString();
            return returnval;
        }
    }
}
