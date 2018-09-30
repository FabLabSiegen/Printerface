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
            OctoprintFullPrinterState result = new OctoprintFullPrinterState()
            {
                TempState = new OctoprintTemperatureState()
                {
                    Bed = new OctoprintTemperature()
                    {
                        Actual = (double)data["temperature"]["bed"]["actual"],
                        Target = (double)data["temperature"]["bed"]["target"],
                        Offset = (double)data["temperature"]["bed"]["offset"]

                    }
                },
                SDState = (bool)data["sd"]["ready"],
                PrinterState = new OctoprintPrinterState()
                {
                    Text = (string)data["state"]["text"],
                    Flags = new OctoprintPrinterFlags()
                    {
                        Operational = (bool)data["state"]["flags"]["operational"],
                        Paused = (bool)data["state"]["flags"]["paused"],
                        Printing = (bool)data["state"]["flags"]["printing"],
                        Cancelling = (bool)data["state"]["flags"]["cancelling"],
                        SDReady = (bool)data["state"]["flags"]["sdReady"],
                        Error = (bool)data["state"]["flags"]["error"],
                        Ready = (bool)data["state"]["flags"]["ready"],
                        ClosedOrError = (bool)data["state"]["flags"]["closedOrError"]
                    }
                }
            };
            for (int i = 0; i < 256; i++)
            {
                if (data["temperature"]["tool" + i] != null)
                {
                    result.TempState.Tools.Add(new OctoprintTemperature()
                    {
                        Actual = (double)data["temperature"]["tool" + i]["actual"],
                        Target = (double)data["temperature"]["tool" + i]["target"],
                        Offset = (double)data["temperature"]["tool" + i]["offset"]
                    });
                }
                else
                {
                    break;
                }
            }
            foreach (JObject historydata in data["temperature"]["history"])
            {

                OctoprintHistoricTemperatureState historicTempState = new OctoprintHistoricTemperatureState()
                {
                    Time = (int)historydata["time"],
                    Bed = new OctoprintTemperature()
                    {
                        Actual = (double)historydata["bed"]["actual"],
                        Target = (double)historydata["bed"]["target"],
                        Offset = (double)historydata["bed"]["offset"]
                    }
                };

                for (int i = 0; i < 256; i++)
                {
                    if (data["temperature"]["tool" + i] != null)
                    {
                        historicTempState.Tools.Add(new OctoprintTemperature()
                        {
                            Actual = (double)historydata["tool" + i]["actual"],
                            Target = (double)historydata["tool" + i]["target"],
                            Offset = (double)historydata["tool" + i]["offset"]
                        });
                    }
                    else
                    {
                        break;
                    }
                }
                result.TempState.History.Add(historicTempState);
            }
            return result;
        }
        public OctoprintPrinterState GetPrinterState()
        {
            string jobInfo = connection.MakeRequest("api/printer?exclude=temperature,sd");
            JObject data = JsonConvert.DeserializeObject<JObject>(jobInfo);
            OctoprintPrinterState result = new OctoprintPrinterState()
            {
                Text = (string)data["state"]["text"],
                Flags = new OctoprintPrinterFlags()
                {
                    Operational = (bool)data["state"]["flags"]["operational"],
                    Paused = (bool)data["state"]["flags"]["paused"],
                    Printing = (bool)data["state"]["flags"]["printing"],
                    Cancelling = (bool)data["state"]["flags"]["cancelling"],
                    SDReady = (bool)data["state"]["flags"]["sdReady"],
                    Error = (bool)data["state"]["flags"]["error"],
                    Ready = (bool)data["state"]["flags"]["ready"],
                    ClosedOrError = (bool)data["state"]["flags"]["closedOrError"]
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
