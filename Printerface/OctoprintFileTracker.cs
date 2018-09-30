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
            rootfolder.octoprintFolders = new List<OctoprintFolder>();
            rootfolder.octoprintFiles = new List<OctoprintFile>();
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
                    OctoprintFile file = new OctoprintFile
                    {
                        Name = filedata.Value<String>("name") ?? "",
                        Path = filedata.Value<String>("path") ?? "",
                        Type = "machinecode",
                        Hash = filedata.Value<String>("hash") ?? "",
                        Size = filedata.Value<int?>("size") ?? -1,
                        Date = filedata.Value<int?>("date") ?? -1,
                        Origin = filedata.Value<String>("string") ?? ""
                    };
                    JToken refs = filedata.Value<JToken>("refs");
                    if (refs != null)
                    {
                        file.Refs_resource = refs.Value<String>("resource") ?? "";
                        file.Refs_download = refs.Value<String>("download") ?? "";
                    }
                    JToken gcodeanalysis = filedata.Value<JToken>("gcodeAnalysis");
                    if (gcodeanalysis != null)
                    {
                        file.GcodeAnalysis_estimatedPrintTime = gcodeanalysis.Value<int?>("estimatedPrintTime") ?? 0;
                        JToken filament = gcodeanalysis.Value<JToken>("filament");
                        if(filament!= null)
                        {
                            file.GcodeAnalysis_filament_length = filament.Value<int?>("length") ?? -1;
                            file.GcodeAnalysis_filament_volume = filament.Value<int?>("volume") ?? -1;
                        }
                    }
                    JToken print = filedata.Value<JToken>("print");
                    if (print != null)
                    {
                        file.Print_failure = print.Value<int?>("failure")??-1;
                        JToken last = print.Value<JToken>("last");
                        if (last != null)
                        {
                            file.Print_last_date = last.Value<int>("date");
                            file.Print_last_success = last.Value<bool>("success");
                        }
                    }
                    rootfolder.octoprintFiles.Add(file);
                }
            }
            return rootfolder;

        }
        public OctoprintFolder GetFiles(string path)
        {
            string jobInfo = connection.MakeRequest("api/files" + path);
            JObject data = JsonConvert.DeserializeObject<JObject>(jobInfo);
            OctoprintFolder folder = new OctoprintFolder() { Name = data.Value<String>("name")??"", Path = data.Value<String>("path")??"", Type = "folder" };
            folder.octoprintFolders = new List<OctoprintFolder>();
            folder.octoprintFiles = new List<OctoprintFile>();

            foreach (JObject filedata in data["files"])
            {
                if ((string)filedata["type"] == "folder")
                {
                    OctoprintFolder subfolder = GetFiles(filedata.Value<String>("path")??"");
                    folder.octoprintFolders.Add(subfolder);
                }

                if ((string)filedata["type"] == "machinecode")
                {
                    OctoprintFile file = new OctoprintFile
                    {
                        Name = filedata.Value<String>("name") ?? "",
                        Path = filedata.Value<String>("path") ?? "",
                        Type = "machinecode",
                        Hash = filedata.Value<String>("hash") ?? "",
                        Size = filedata.Value<int?>("size") ?? -1,
                        Date = filedata.Value<int?>("date") ?? -1,
                        Origin = filedata.Value<String>("string") ?? ""
                    };
                    JToken refs = filedata.Value<JToken>("refs");
                    if (refs != null)
                    {
                        file.Refs_resource = refs.Value<String>("resource") ?? "";
                        file.Refs_download = refs.Value<String>("download") ?? "";
                    }
                    JToken gcodeanalysis = filedata.Value<JToken>("gcodeAnalysis");
                    if (gcodeanalysis != null)
                    {
                        file.GcodeAnalysis_estimatedPrintTime = gcodeanalysis.Value<int?>("estimatedPrintTime") ?? 0;
                        JToken filament = gcodeanalysis.Value<JToken>("filament");
                        if (filament != null)
                        {
                            file.GcodeAnalysis_filament_length = filament.Value<int?>("length") ?? -1;
                            file.GcodeAnalysis_filament_volume = filament.Value<int?>("volume") ?? -1;
                        }
                    }
                    JToken print = filedata.Value<JToken>("print");
                    if (print != null)
                    {
                        file.Print_failure = print.Value<int?>("failure") ?? -1;
                        JToken last = print.Value<JToken>("last");
                        if (last != null)
                        {
                            file.Print_last_date = last.Value<int>("date");
                            file.Print_last_success = last.Value<bool>("success");
                        }
                    }
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
