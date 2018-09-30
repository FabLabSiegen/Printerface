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
            string jobInfo = connection.MakeRequest("api/printer");
            JObject data = JsonConvert.DeserializeObject<JObject>(jobInfo);
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
            string jobInfo = connection.MakeRequest("api/printer?exclude=temperature,sd");
            JObject data = JsonConvert.DeserializeObject<JObject>(jobInfo);
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
    }
    public class OctoprintTemperature
    {
        public double Actual { get; set; }
        public double Target { get; set; }
        public double Offset { get; set; }

    }
    public class OctoprintTemperatureState
    {
        public List<OctoprintTemperature> Tools { get; set; }
        public OctoprintTemperature Bed { get; set; }
        public List<OctoprintHistoricTemperatureState> History { get; set; }

    }
    public class OctoprintHistoricTemperatureState
    {
        public int Time { get; set; }
        public List<OctoprintTemperature> Tools { get; set; }
        public OctoprintTemperature Bed { get; set; }
    }
    public class OctoprintPrinterState
    {
        public string Text { get; set; }
        public OctoprintPrinterFlags Flags { get; set; }
    }
    public class OctoprintFullPrinterState
    {
        public OctoprintTemperatureState TempState { get; set; }
        public bool SDState { get; set; }
        public OctoprintPrinterState PrinterState { get; set; }
    }
}
