using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace OctoprintClient
{
    public class OctoprintPrinterTracker:OctoprintTracker
    {
        public OctoprintPrinterTracker(OctoprintConnection con):base(con)
        {
        }
        public OctoprintFullPrinterState GetFullPrinterState()
        {
            string jobInfo = Connection.Get("api/printer");
            JObject data=new JObject();
            try
            {
                data = JsonConvert.DeserializeObject<JObject>(jobInfo);
            }catch
            {
                if (jobInfo.Contains("409"))
                {
                    OctoprintFullPrinterState returnval = new OctoprintFullPrinterState
                    {
                        PrinterState = new OctoprintPrinterState()
                    };
                    returnval.PrinterState.Text = "Error 409 is the Printer Connected at all?\n";
                    return returnval;
                }

            }
            JToken temperaturedata = data.Value<JToken>("temperature");
            JToken bedtemperature = temperaturedata.Value<JToken>("bed");
            JToken statedata = data.Value<JToken>("state");
            JToken stateflags = statedata.Value<JToken>("flags");
            OctoprintFullPrinterState result = new OctoprintFullPrinterState()
            {
                TempState = new OctoprintTemperatureState()
                {
                    Bed = new OctoprintTemperature()
                    {
                        Actual = bedtemperature.Value<double?>("actual") ?? -1.0,
                        Target = bedtemperature.Value<double?>("target") ?? -1.0,
                        Offset = bedtemperature.Value<double?>("offset") ?? -1.0

                    }
                },
                SDState = data.Value<JToken>("sd").Value<bool?>("ready") ?? false,
                PrinterState = new OctoprintPrinterState()
                {
                    Text = statedata.Value<String>("text"),
                    Flags = new OctoprintPrinterFlags()
                    {
                        Operational = stateflags.Value<bool?>("operational")??false,
                        Paused = stateflags.Value<bool?>("paused") ?? false,
                        Printing = stateflags.Value<bool?>("printing") ?? false,
                        Cancelling = stateflags.Value<bool?>("canceling") ?? false,
                        SDReady = stateflags.Value<bool?>("sdReady") ?? false,
                        Error = stateflags.Value<bool?>("error") ?? false,
                        Ready = stateflags.Value<bool?>("ready") ?? false,
                        ClosedOrError = stateflags.Value<bool?>("closedOrError") ?? false
                    }
                }
            };
            result.TempState.Tools = new List<OctoprintTemperature>();
            for (int i = 0; i < 256; i++)
            {
                JToken tooltemp = temperaturedata.Value<JToken>("tool"+i);
                if (tooltemp != null)
                {
                    result.TempState.Tools.Add(new OctoprintTemperature()
                    {
                        Actual = tooltemp.Value<double?>("actual")??-1.0,
                        Target = tooltemp.Value<double?>("target") ?? -1.0,
                        Offset = tooltemp.Value<double?>("offset") ?? -1.0
                    });
                }
                else
                {
                    break;
                }
            }
            if (temperaturedata != null && temperaturedata.Value<JToken>("history")!=null)
            {
                result.TempState.History = new List<OctoprintHistoricTemperatureState>();
                foreach (JObject historydata in temperaturedata["history"])
                {

                    OctoprintHistoricTemperatureState historicTempState = new OctoprintHistoricTemperatureState()
                    {
                        Time = historydata.Value<int?>("time") ?? -1,
                        Bed = new OctoprintTemperature()
                        {
                            Actual = historydata.Value<JToken>("bed").Value<double?>("actual") ?? -1.0,
                            Target = historydata.Value<JToken>("bed").Value<double?>("target") ?? -1.0,
                            Offset = historydata.Value<JToken>("bed").Value<double?>("offset") ?? -1.0
                        }
                    };

                    historicTempState.Tools = new List<OctoprintTemperature>();
                    for (int i = 0; i < 256; i++)
                    {
                        JToken tooltemp = historydata.Value<JToken>("tool" + i);
                        if (tooltemp != null)
                        {
                            historicTempState.Tools.Add(new OctoprintTemperature()
                            {
                                Actual = tooltemp.Value<double?>("actual") ?? -1.0,
                                Target = tooltemp.Value<double?>("target") ?? -1.0,
                                Offset = tooltemp.Value<double?>("offset") ?? -1.0
                            });
                        }
                        else
                        {
                            break;
                        }
                    }
                    result.TempState.History.Add(historicTempState);
                }
            }

            return result;
        }
        public OctoprintPrinterState GetPrinterState()
        {
            string jobInfo = Connection.Get("api/printer?exclude=temperature,sd");
            JObject data = new JObject();
            try
            {
                data = JsonConvert.DeserializeObject<JObject>(jobInfo);
            }
            catch
            {
                if (jobInfo.Contains("409"))
                {
                    OctoprintPrinterState returnval = new OctoprintPrinterState
                    {
                        Text = "Error 409 is the Printer Connected at all?\n"
                    };
                    return returnval;
                }

            }
            JToken statedata = data.Value<JToken>("state");
            JToken stateflags = statedata.Value<JToken>("flags");
            OctoprintPrinterState result = new OctoprintPrinterState()
            {
                Text = statedata.Value<String>("text"),
                Flags = new OctoprintPrinterFlags()
                {
                    Operational = stateflags.Value<bool?>("operational") ?? false,
                    Paused = stateflags.Value<bool?>("paused") ?? false,
                    Printing = stateflags.Value<bool?>("printing") ?? false,
                    Cancelling = stateflags.Value<bool?>("canceling") ?? false,
                    SDReady = stateflags.Value<bool?>("sdReady") ?? false,
                    Error = stateflags.Value<bool?>("error") ?? false,
                    Ready = stateflags.Value<bool?>("ready") ?? false,
                    ClosedOrError = stateflags.Value<bool?>("closedOrError") ?? false
                }
            };
            return result;

        }

        public string MakePrintheadJog(float? x, float? y, float? z, bool? absolute, int? speed)
        {

            string returnValue = string.Empty;
            JObject data = new JObject
            {
                { "command", "jog" }
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
            if (absolute.HasValue)
            {
                data.Add("absolute", absolute);
            }
            if (speed.HasValue)
            {
                data.Add("speed", speed);
            }
            if (!absolute.HasValue || absolute == true)
            {
                Connection.Position.SetPos(x, y, z);
            }
            else Connection.Position.Move(x, y, z);
            returnValue = Connection.PostJson("api/printer/printhead", data);
            return returnValue;
        }

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
            returnValue =Connection.PostJson("api/printer/printhead", data);
            Connection.Position.SetPos(x, y, z);
            return returnValue;
        }
        public string SetFeedrate(int? ifeed,float? ffeed)
        {
            JObject data = new JObject
            {
                {"command", "feedrate"}
            };
            if (ffeed.HasValue)
            {
                data.Add("factor", ffeed);
            }
            else if (ifeed.HasValue)
            {
                data.Add("factor", ifeed);
            }
            else
            {
                data.Add("factor", 100);
            }
            return Connection.PostJson("api/printer/printhead", data);
        }

        public string SetTemperatureTarget(Dictionary<string, int> targets)
        {
            string returnValue = string.Empty;
            JObject data = new JObject
            {
                { "command", "target" },
                { "targets", JObject.FromObject(targets)}
            };
            returnValue =Connection.PostJson("api/printer/tool", data);
            return returnValue;
        }

        public string SetTemperatureOffset(Dictionary<string, int> offsets)
        {
            string returnValue = string.Empty;
            JObject data = new JObject
            {
                { "command", "offset" },
                { "offsets", JObject.FromObject(offsets)}
            };
            returnValue =Connection.PostJson("api/printer/tool", data);
            return returnValue;
        }

        public string SelectTool(string tool)
        {
            JObject data = new JObject
            {
                { "command", "select" },
                { "tool", tool}
            };
            return Connection.PostJson("api/printer/tool", data);
        }

        public string ExtrudeSelectedTool(int mm)
        {
            JObject data = new JObject
            {
                { "command", "extrude" },
                { "amount", mm}
            };
            return Connection.PostJson("api/printer/tool", data);
        }

        public string SetFlowrateSelectedTool(int? iflow,float? fflow)
        {
            JObject data = new JObject
            {
                {"command", "flowrate"}
            };
            if (fflow.HasValue)
            {
                data.Add("factor", fflow);
            }
            else if (iflow.HasValue)
            {
                data.Add("factor", iflow);
            }
            else
            {
                data.Add("factor", 100);
            }
            return Connection.PostJson("api/printer/tool", data);
        }
        public string SetTemperatureTargetBed(int temperature)
        {
            JObject data = new JObject
            {
                { "command", "target" },
                { "target", temperature}
            };
            return Connection.PostJson("api/printer/bed", data);
        }

        public string SetTemperatureOffsetBed(int offset)
        {
            JObject data = new JObject
            {
                { "command", "offset" },
                { "offset", offset}
            };
            return Connection.PostJson("api/printer/bed", data);
        }
    }

    public class OctoprintPrinterFlags
    {
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
