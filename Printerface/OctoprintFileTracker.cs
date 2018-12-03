using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace OctoprintClient
{
    /// <summary>
    /// Tracks Files, can delete, uplad and slice.
    /// </summary>
    public class OctoprintFileTracker:OctoprintTracker
    {
        /// <summary>
        /// Initializes a Filetracker, this shouldn't be done directly and is part of the Connection it needs anyway
        /// </summary>
        /// <param name="con">The Octoprint connection it connects to.</param>
        public OctoprintFileTracker(OctoprintConnection con):base(con)
        {
        }

        /// <summary>
        /// Gets all the files on the Server
        /// </summary>
        public OctoprintFolder GetFiles()
        {
            string jobInfo = Connection.Get("api/files");
            JObject data = JsonConvert.DeserializeObject<JObject>(jobInfo);
            OctoprintFolder rootfolder = new OctoprintFolder(data, this) { Name = "root", Path = "/", Type = "root" };
            return rootfolder;

        }

        /// <summary>
        /// Gets certain file informations
        /// </summary>
        /// <returns>The file Informations.</returns>
        /// <param name="path">The path to the Folder or File.</param>
        public OctoprintFolder GetFiles(string path)
        {
            string jobInfo="";
            try
            {
                jobInfo = Connection.Get("api/files" + path);
            }
            catch (WebException e)
            {
                switch (((HttpWebResponse)e.Response).StatusCode)
                {
                    case HttpStatusCode.NotFound:
                        Debug.WriteLine("searched for a file that wasn't there at " + path);
                        return null;
                }
            }
            JObject data = JsonConvert.DeserializeObject<JObject>(jobInfo);
            OctoprintFolder folder = new OctoprintFolder(data,this) { Name = data.Value<String>("name")??"", Path = data.Value<String>("path")??"", Type = "folder" };
            return folder;
        }

        /// <summary>
        /// Selects the File for printing
        /// </summary>
        /// <returns>The Http Result</returns>
        /// <param name="path">The path of the file that should be selected.</param>
        /// <param name="location">The location (local or sdcard) where this File should be. Normally local</param>
        /// <param name="print">If set, defines if the GCode should be printed directly after being selected. null means false</param>
        public string Select( string path, string location="local", bool print=false)
        {
            JObject data = new JObject
            {
                { "command", "select" },
                { "print", print}
            };


            try
            {
                return Connection.PostJson("api/files/" + location + "/" + path, data);
            }
            catch (WebException e)
            {
                switch (((HttpWebResponse)e.Response).StatusCode)
                {
                    case HttpStatusCode.Conflict:
                        return "409 The Printer is propably not operational";
                    default:
                        return "unknown webexception occured";
                }

            }
        }

        /// <summary>
        /// Slices a certain 3D object.
        /// </summary>
        /// <returns>The Http Result</returns>
        /// <param name="location">The Location of the stl file, sdcard or local.</param>
        /// <param name="path">The Path to the File.</param>
        /// <param name="select">If set to <c>true</c> selects the file for printing</param>
        /// <param name="gcode">Gcode filename to slice to.</param>
        /// <param name="posx">Position of the Object on the Printbed in x direction.</param>
        /// <param name="posy">Position of the Object on the Printbed in y direction.</param>
        /// <param name="slicer">The name of the Slicer, if none is given it defaults to the internal Cura.</param>
        /// <param name="profile">The Profile of the Slicer.</param>
        /// <param name="profileparam">Parameter of the slicer that need to be overwriten from the Profile.</param>
        /// <param name="print">If set to <c>true</c> prints the GCode after slicing.</param>
        public string Slice(string location, string path, bool select=false, string gcode="", int posx=100, int posy=100, string slicer="", string profile="", Dictionary<string,string> profileparam=null, bool print=false)
        {
            JObject data = new JObject
            {
                { "command", "slice" },
                { "slicer", slicer},
                { "position", new JObject{ {"x",posx },{"y",posy } } },
                { "select", select},
                { "print", print}


            };
            if (profileparam!=null && profileparam.Count>0)
            {
                data.Add(JObject.FromObject(profileparam));
            }
            if (profile != "")
            {
                data.Add("profile", profile);
            }
            if (gcode != "")
            {
                data.Add("gcode", gcode);
            }
            try {
                return Connection.PostJson("api/files/" + location + "/" + path, data);
            }
            catch (WebException e)
            {
                switch (((HttpWebResponse)e.Response).StatusCode)
                {
                    case (HttpStatusCode.UnsupportedMediaType):
                        return "415 that file you are trying to slice seems to be no stl";
                    case HttpStatusCode.NotFound:
                        return "404 did not find the file";
                    case HttpStatusCode.BadRequest:
                        return "400 command is not supported in this way";
                    case HttpStatusCode.Conflict:
                        return "409 conflict occured, cannot slice while printing or printer is not operational or something else";
                    default:
                        return "unknown webexception occured";
                }
                   
            }


        }

        /// <summary>
        /// Copies file inside a location
        /// </summary>
        /// <returns>The Http Result</returns>
        /// <param name="location">Location, either sdcard or local.</param>
        /// <param name="path">The Path of the file that should be copied.</param>
        /// <param name="destination">The destination to copy to.</param>
        public string Copy(string location, string path, string destination)
        {
            JObject data = new JObject
            {
                { "command", "copy" },
                { "destination", destination}
            };
            try
            {
                return Connection.PostJson("api/files/" + location + "/" + path, data);
            }
            catch (WebException e)
            {
                switch (((HttpWebResponse)e.Response).StatusCode)
                {
                    case HttpStatusCode.Conflict:
                        return "409 cannot overwrite that destination, there is allready a file named similarly there";
                    case HttpStatusCode.NotFound:
                        return "404 did not find destination folder or file to copy";
                    default:
                        return "unknown webexception occured";
                }

            }
        }

        /// <summary>
        /// Deletes a File
        /// </summary>
        /// <returns>The Http Result</returns>
        /// <param name="location">Location of the File to delete, sdcard or local</param>
        /// <param name="path">The path of the File to delete.</param>
        public string Delete(string location, string path)
        {
            try {
                return Connection.Delete("api/files/" + location + "/" + path);
            }
            catch (WebException e)
            {
                switch (((HttpWebResponse)e.Response).StatusCode)
                {
                    case HttpStatusCode.Conflict:
                        return "409 The file is currently in use by a Slicer or a Printer";
                    case HttpStatusCode.NotFound:
                        return "404 did not find the file";
                    default:
                        return "unknown webexception occured";
                }

            }


        }

        /// <summary>
        /// Creates a folder, if a subfolder should be created, create it with slashes and the path before it.
        /// </summary>
        /// <returns>The Http Result</returns>
        /// <param name="path">The Path of the Folder.</param>
        public string CreateFolder(string path)
        {
            string foldername = path.Split('/')[path.Split('/').Length - 1];
            path = path.Substring(0, path.Length - foldername.Length);
            string packagestring="" +
                "--{0}\r\n" +
                "Content-Disposition: form-data; name=\"foldername\";\r\n" +
                "\r\n" +
                foldername + "\r\n" +
                "--{0}--\r\n" +
                "Content-Disposition: form-data; name=\"path\"\r\n" +
                "\r\n" +
                path + "\r\n" +
                "--{0}--\r\n";
            return Connection.PostMultipart(packagestring, "/api/files/local");
        }

        /// <summary>
        /// Uploads a file from local to the Server
        /// </summary>
        /// <returns>The Http Result</returns>
        /// <param name="filename">Filename of the local file.</param>
        /// <param name="onlinepath">Path to upload the file to.</param>
        /// <param name="location">Location to upload to, local or sdcard, not sure if sdcard works, but takes ages anyway.</param>
        /// <param name="select">If set to <c>true</c> selects the File to print next.</param>
        /// <param name="print">If set to <c>true</c> prints the File.</param>
        public string UploadFile(string filename,  string onlinepath="", string location="local", bool select=false, bool print=false)
        {
            string fileData =string.Empty;
            fileData= System.IO.File.ReadAllText(filename);
            filename=(filename.Split('/')[filename.Split('/').Length-1]).Split('\\')[filename.Split('\\')[filename.Split('\\').Length - 1].Length - 1];
            string packagestring="" +
                "--{0}\r\n" +
                "Content-Disposition: form-data; name=\"file\"; filename=\""+filename+"\"\r\n" +
                "Content-Type: application/octet-stream\r\n" +
                "\r\n" +
                fileData + "\r\n" +
                
                "--{0}\r\n" +
                "Content-Disposition: form-data; name=\"path\";\r\n" +
                "\r\n" +
                onlinepath + "\r\n" +
                "--{0}--\r\n" +
                "Content-Disposition: form-data; name=\"select\";\r\n" +
                "\r\n" +
                select + "\r\n" +
                "--{0}--\r\n" +
                "Content-Disposition: form-data; name=\"print\"\r\n" +
                "\r\n" +
                print + "\r\n" +
                "--{0}--\r\n";
            return Connection.PostMultipart(packagestring, "api/files/"+location);
        }
    }

    public class OctoprintFile
    {
        public OctoprintFile(JObject filedata)
        {

            Name = filedata.Value<String>("name") ?? "";
            Path = filedata.Value<String>("path") ?? "";
            Type = filedata.Value<String>("type") ?? "file";
            Hash = filedata.Value<String>("hash") ?? "";
            Size = filedata.Value<int?>("size") ?? -1;
            Date = filedata.Value<int?>("date") ?? -1;
            Origin = filedata.Value<String>("origin") ?? "";
        }

        public string Name { get; set; }
        public string Path { get; set; }
        public string Type { get; set; }
        //public string[] TypePath { get; set; }
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
        public override string ToString()
        {
            string returnvalue = "";
            returnvalue += Name + ", path: " +Origin+"/"+ Path + " ("+ Type + ") :\n";
            return returnvalue;
        }
    }
    public class OctoprintFolder
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public string Type { get; set; }
        //public string[] TypePath { get; set; }
        public List<OctoprintFile> octoprintFiles;
        public List<OctoprintFolder> octoprintFolders;

        public OctoprintFolder(JObject data, OctoprintFileTracker t)
        {
            octoprintFolders = new List<OctoprintFolder>();
            octoprintFiles = new List<OctoprintFile>();
            foreach (JObject filedata in data["files"])
            {
                if ((string)filedata["type"] == "folder")
                {
                    OctoprintFolder folder = t.GetFiles((string)filedata["path"]);
                    octoprintFolders.Add(folder);
                }
                else
                {
                    OctoprintFile file = new OctoprintFile(filedata);
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
                    octoprintFiles.Add(file);
                }
            }
        }

        public override string ToString()
        {
            string returnvalue = "";
            returnvalue += Name + ": " + Path + " ("+ Type + ") :\n";
            foreach (OctoprintFile file in octoprintFiles){
                returnvalue+="  " + file.ToString()+"\n";
            }
            foreach (OctoprintFolder folder in octoprintFolders)
            {
                if(folder!=null)
                    returnvalue += "    " + folder.ToString().Replace("\n", "\n ");
            }
            return returnvalue;
        }
    }
}
