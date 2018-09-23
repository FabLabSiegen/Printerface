using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace OctoprintClient
{
    public class OctoprintFileTracker:OctoprintTracker
    {
        public OctoprintFileTracker(OctoprintConnection con):base(con)
        {
        }
        public OctoprintFolder GetFiles()
        {
            OctoprintFolder rootfolder = new OctoprintFolder() { Name = "root", Path = "/", Type = "root" };

            string jobInfo = connection.MakeRequest("api/files");
            JObject data = JsonConvert.DeserializeObject<JObject>(jobInfo);
            foreach (JObject filedata in data["files"])
            {
                if ((string)filedata["type"] == "folder")
                {
                    OctoprintFolder folder = GetFiles((string)filedata["path"]);
                    rootfolder.octoprintFolders.Add(folder);
                }
                if ((string)filedata["type"] == "machinecode")
                {
                    OctoprintFile file = new OctoprintFile()
                    {
                        Name = (string)filedata["name"],
                        Path = (string)filedata["path"],
                        Type = "machinecode",
                        Hash = (string)filedata["hash"],
                        Size = (int)filedata["size"],
                        Date = (int)filedata["date"],
                        Origin = (string)filedata["string"],
                        Refs_resource = (string)filedata["refs"]["resource"],
                        Refs_download = (string)filedata["refs"]["download"],
                        GcodeAnalysis_estimatedPrintTime = (int)filedata["gcodeAnalysis"]["estimatedPrintTime"],
                        GcodeAnalysis_filament_length = (int)filedata["gcodeAnalysis"]["filament"]["length"],
                        GcodeAnalysis_filament_volume = (int)filedata["gcodeAnalysis"]["filament"]["volume"],
                        Print_failure = (int)filedata["print"]["failure"],
                        Print_last_date = (int)filedata["print"]["last"]["date"],
                        Print_last_success = (bool)filedata["print"]["last"]["success"]

                    };
                    rootfolder.octoprintFiles.Add(file);
                }
            }
            return rootfolder;

        }
        public OctoprintFolder GetFiles(string path)
        {
            string jobInfo = connection.MakeRequest("api/files" + path);
            JObject data = JsonConvert.DeserializeObject<JObject>(jobInfo);
            OctoprintFolder folder = new OctoprintFolder() { Name = (string)data["name"], Path = (string)data["path"], Type = "folder" };

            foreach (JObject filedata in data["files"])
            {
                if ((string)filedata["type"] == "folder")
                {
                    OctoprintFolder subfolder = GetFiles((string)filedata["path"]);
                    folder.octoprintFolders.Add(subfolder);
                }

                if ((string)filedata["type"] == "machinecode")
                {
                    OctoprintFile file = new OctoprintFile()
                    {
                        Name = (string)filedata["name"],
                        Path = (string)filedata["path"],
                        Type = "machinecode",
                        Hash = (string)filedata["hash"],
                        Size = (int)filedata["size"],
                        Date = (int)filedata["date"],
                        Origin = (string)filedata["string"],
                        Refs_resource = (string)filedata["refs"]["resource"],
                        Refs_download = (string)filedata["refs"]["download"],
                        GcodeAnalysis_estimatedPrintTime = (int)filedata["gcodeAnalysis"]["estimatedPrintTime"],
                        GcodeAnalysis_filament_length = (int)filedata["gcodeAnalysis"]["filament"]["length"],
                        GcodeAnalysis_filament_volume = (int)filedata["gcodeAnalysis"]["filament"]["volume"],
                        Print_failure = (int)filedata["print"]["failure"],
                        Print_last_date = (int)filedata["print"]["last"]["date"],
                        Print_last_success = (bool)filedata["print"]["last"]["success"]

                    };
                    folder.octoprintFiles.Add(file);
                }
            }
            return folder;
        }
    }

    public class OctoprintFile
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string Type { get; set; }
        public string[] TypePath { get; set; }
        public string Hash { get; set; }
        public int Size { get; set; }
        public int Date { get; set; }
        public string Origin { get; set; }
        public string Refs_resource { get; set; }
        public string Refs_download { get; set; }
        public int GcodeAnalysis_estimatedPrintTime { get; set; }
        public int GcodeAnalysis_filament_length { get; set; }
        public int GcodeAnalysis_filament_volume { get; set; }
        public int Print_failure { get; set; }
        public int Print_success { get; set; }
        public int Print_last_date { get; set; }
        public bool Print_last_success { get; set; }
    }
    public class OctoprintFolder
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string Type { get; set; }
        public string[] TypePath { get; set; }
        public List<OctoprintFile> octoprintFiles;
        public List<OctoprintFolder> octoprintFolders;
    }
}
