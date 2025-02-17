﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
using System.Diagnostics;
using System.Security.Permissions;
using System.Configuration;
using System.IO;
using System.Data.SQLite;
//using System.Data.SQLite.Linq;
using System.Data.Linq;
using System.Linq;
using System.Data.Linq.Mapping;
using System.Collections;
//using System.DirectoryServices;
//using System.Management;
using Newtonsoft.Json;
using Microsoft.Win32;
using System.Net;
/***
 * 
 * TODO: Reflect changes made in config.html into this.persistant!!!
 * 
 * 
 * 
 * 
 * Consider optimising by storing all values from html in c# array?
 * this.folders: takes 0.0002ms - 0.0005ms
 * this.browser.Document.GetElementById(id).InnerHtml takes 55.1357ms first request and 0.2303ms - 0.3389ms consequtive requests
 * 
 ***/

namespace RPS {
    [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
    [System.Runtime.InteropServices.ComVisibleAttribute(true)]

    public partial class Config : Form {

        public enum Order { Random = 1, Sequential = 0 };

        private bool configInitialised = false;

        private Dictionary<string, object> persistant;
        DBConnector dbConnector;
        //SQLiteConnection connection;

        private Screensaver screensaver;

        private Dictionary<string, object> trackChanges = new Dictionary<string, object>() {
           {"folders", null},
           {"excludedSubfolders", null},
           {"excludeAllSubfolders", null},
           {"ignoreHiddenFiles", null},
           {"ignoreHiddenFolders", null},
           {"imageExtensions", null},
           {"videoExtensions", null},
           {"rawExtensions", null},
        };

        public long maxMonitorDimension = 0;

        public jsonFolder effects;

        private bool checkUpdates = false;
        private bool downloadUpdates = false;
        private bool newVersionAvailable = false;

        private Stopwatch downloadProgress = new Stopwatch();
        //private bool installUpdates = false;

        //public WebBrowser browser;

        //private delegate void AddBrowser();

        public Config(Screensaver screensaver) {
            this.screensaver = screensaver;
            this.InitializeComponent();
            this.browser.ObjectForScripting = this;
            this.browser.AllowWebBrowserDrop = false;
            foreach (Screen screen in Screen.AllScreens) {
                this.maxMonitorDimension = Math.Max(Math.Max(this.maxMonitorDimension, screen.Bounds.Width), screen.Bounds.Height);
            }
        }

        public SQLiteConnection connectToDB() {
            this.dbConnector = new DBConnector(Constants.selectProgramAppDataFolder(Constants.PersistantConfigFileName), Constants.SettingsDefinition, false);
            //return new SQLiteConnection("Data Source=" + path + ";Version=3;");
            return this.dbConnector.connection;
        }

        public void saveDebug() {
            string path = this.browser.Url.LocalPath.Replace(Constants.ConfigHtmlFile, "_C_" + DateTime.Now.ToString("yyyyMMddhhmmss") + ".html");
            File.WriteAllText(path, this.browser.Document.GetElementsByTagName("HTML")[0].OuterHtml);
        }

        public string jsFileBrowserDialog(string filename, string filter) {
            OpenFileDialog dialog = new System.Windows.Forms.OpenFileDialog();
            if (filename == null) filename = "";
            else dialog.InitialDirectory = Path.GetFullPath(filename);
            dialog.FileName = Path.GetFileName(filename);
            if (filter != null && filter.Length > 0) {
                dialog.Filter = filter;
            }
            if (dialog.ShowDialog() == DialogResult.OK) {
                return dialog.FileName;
            } else {
                return filename;
            }
        }


        public string jsFolderBrowserDialog(string path) {
            FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.SelectedPath = path;
            if (dialog.ShowDialog() == DialogResult.OK) {
                return dialog.SelectedPath;
            } else {
                return path;
            }
        }

        public string jsRawConverterAvailable(string path) {
            if (File.Exists(path)) return "true";
            else return "false";
        }

        public string jsGetUFRawLocation() {
            string path = (string) Registry.GetValue("HKEY_CURRENT_USER\\Software\\Microsoft\\IntelliPoint\\AppSpecific\\ufraw.exe", "Path", null);
            path = path.Replace("ufraw.exe", "ufraw-batch.exe");
            if (path != null && path.Length > 0) {
                if (File.Exists(path)) {
                    return path;
                }
            }
            return null;
        }

        public void jsInputChanged(string id, string value) {
            this.setPersistant(id, value, false);
        }

        public void jsSetSelectedEffects(string jsonEffects) {
            this.effects = JsonConvert.DeserializeObject<jsonFolder>(jsonEffects);
            this.persistant["effects"] = jsonEffects;
        }

        public void jsOpenExternalLink(string href) {
            Utils.RunTaskScheduler("OpenURL", "explorer.exe", "\"" + href + "\"");
        }

        public string jsGetSelectedEffects() {
            return JsonConvert.SerializeObject(this.effects);
        }

        public string jsGetFilters() {
            try {
                return this.getPersistantString("filters");
            } catch(System.Collections.Generic.KeyNotFoundException e) {
                return null;
            }
        }

        public string jsGetFilterColumns() {
            Dictionary<string, ColumnInfo> columns;
            columns = Constants.FileNodesDefinition.columns.Union(Constants.MetadataDefinition.columns)
                .ToLookup(pair => pair.Key, pair => pair.Value)
                .ToDictionary(group => group.Key, group => group.First());
            return JsonConvert.SerializeObject(columns/*, Newtonsoft.Json.Formatting.Indented*/);
        }

        public void jsOpenProgramAppDataFolder() {
            if (Utils.RunTaskScheduler(@"OpenInExplorer", "explorer.exe", "\"" + Constants.selectProgramAppDataFolder("") + "\"")) {
                //this.monitors[i].showInfoOnMonitor("Opened in Explorer Window", false, true);
            }
        }

        public void setBrowserBodyClasses(WebBrowser browser, Screensaver.Actions action) {
            setBrowserBodyClasses(browser, action, null);
        }

        public static void setBrowserBodyClasses(WebBrowser browser, Screensaver.Actions action, string classes) {
            HtmlElementCollection elems = browser.Document.GetElementsByTagName("body");
            foreach (HtmlElement elem in elems) {
                switch (action) {
                    case Screensaver.Actions.Preview: classes += " preview"; break;
                    case Screensaver.Actions.Config: classes += " config"; break;
                    case Screensaver.Actions.Screensaver: classes += " screensaver"; break;
                    case Screensaver.Actions.Test: classes += " test"; break;
                    case Screensaver.Actions.Slideshow: classes += " slideshow"; break;
                }
                classes += " IE" + browser.Version.Major;
                if (browser.Version.Major < 8) classes += " lowIE";
                elem.SetAttribute("className", elem.GetAttribute("className") + classes);
            }
        }

        public void loadPersistantConfig() {
            if (this.screensaver.monitors != null) this.loadPersistantConfig(this.screensaver.monitors.Length);
            else this.loadPersistantConfig(Screen.AllScreens.Length);
        }

        public void loadPersistantConfig(int nrMonitors) {
            #if (DEBUG)
                this.screensaver.debugLog.Add("loadPersistantConfig(" + nrMonitors + ")");
            #endif

            //SQLiteConnection connection = 
            this.connectToDB();
            this.persistant = new Dictionary<string, object>();

            DataContext context = new DataContext(this.dbConnector.connection);
            var items = context.GetTable<Setting>();
            foreach(Setting item in items) {
                this.persistant.Add(item.Key, item.Value);
            }
            if (!this.persistant.ContainsKey("filterNrLines")) this.persistant["filterNrLines"] = 0;

            object regvalue = Registry.GetValue("HKEY_CURRENT_USER\\" + Constants.regkeyGPURendering, Constants.regkeyExecutable, 0);
            bool gpuRendering = (regvalue != null && (int)regvalue == 1);
            if (this.persistant.ContainsKey("gpuRendering")) this.persistant["gpuRendering"] = gpuRendering;
            else this.persistant.Add("gpuRendering", gpuRendering);
            
            //this.browser.Document.InvokeScript("initMonitorsAndFilterCount", new string[] { Convert.ToString(Screen.AllScreens.Length), Convert.ToString(this.persistant["filterNrLines"]) });
            this.browser.Document.InvokeScript("initMonitors", new string[] { Convert.ToString(Screen.AllScreens.Length) });
            
            if (!this.persistant.ContainsKey("folders") || this.persistant["folders"] == null || this.getPersistantString("folders").Trim().Length == 0) { 
                string path = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) + Environment.NewLine + 
                                                        Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
                if (!this.persistant.ContainsKey("folders")) {
                    this.persistant.Add("folders", path);
                } else {
                    this.persistant["folders"] = path;
                }
            }

            if (!this.persistant.ContainsKey("rawFolder") || this.persistant["rawFolder"] == null || Convert.ToString(this.persistant["rawFolder"]).Trim().Length == 0) {
                string path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                    Constants.AppName,
                    Constants.RawCacheFolder
                );
                    
                if (!this.persistant.ContainsKey("rawFolder")) {
                    this.persistant.Add("rawFolder", path);
                } else {
                    this.persistant["rawFolder"] = path;
                }
            }

            if (this.persistant.ContainsKey("effects") && this.persistant["effects"] != null && Convert.ToString(this.persistant["effects"]) != "null" && Convert.ToString(this.persistant["effects"]).Trim().Length > 0) {
                this.effects = JsonConvert.DeserializeObject<jsonFolder>(Convert.ToString(this.persistant["effects"]));
            } else {
                this.effects = new jsonFolder();
            }
            string jsonPath = Constants.getDataFolder(Constants.EffectsJsonFile);
            if (File.Exists(jsonPath)) {
                //this.effects = new jsonFolder();
                JsonSerializer serializer = new JsonSerializer();
                serializer.NullValueHandling = NullValueHandling.Ignore;
                using (StreamReader sr = new StreamReader(jsonPath))
                using (JsonReader reader = new JsonTextReader(sr)) {
                    jsonFolder newEffects = serializer.Deserialize<jsonFolder>(reader);
                    this.effects.mergeChildren(newEffects);
                }
            }

            HtmlElementCollection hec = this.GetElementsByTagName("input");
            foreach (HtmlElement e in hec) {
                if (this.persistant.ContainsKey(e.GetAttribute("id")) || (e.GetAttribute("type") == "radio" && this.persistant.ContainsKey(e.GetAttribute("name")))) {
                    switch (e.GetAttribute("type")) {
                        case "checkbox":
                            if (this.getPersistantBool(e.GetAttribute("id")) == true) {
                                e.SetAttribute("checked", "true");
                            } else {
                                e.SetAttribute("checked", "");
                            }
                            break;
                        case "radio":
                            if (this.getPersistantString(e.GetAttribute("name")) == e.GetAttribute("value")) {
                                e.SetAttribute("checked", "true");
                            }
                            break;
                        default:
                            e.SetAttribute("value", this.getPersistantString(e.GetAttribute("id")));
                            break;
                    }
                } else {
                    switch (e.GetAttribute("type")) {
                        case "checkbox":
                            this.persistant[e.GetAttribute("id")] =  this.getDomCheckboxValue(e.GetAttribute("id"));
                        break;
                        case "radio":
                            this.persistant[e.GetAttribute("name")] =  this.getDomRadioValue(e.GetAttribute("name"));
                        break;
                        default:
                            this.persistant[e.GetAttribute("id")] =  this.getDomValue(e.GetAttribute("id"));
                        break;
                    }
                    
                    // Set persistant value with default
                }
            }

            hec = this.browser.Document.GetElementsByTagName("textarea");
            foreach (HtmlElement e in hec) {
                if (this.persistant.ContainsKey(e.GetAttribute("id"))) {
                    e.SetAttribute("value", Utils.HtmlDecode(Convert.ToString(this.persistant[e.GetAttribute("id")])));
                } else {
                    this.persistant[e.GetAttribute("id")] = Utils.HtmlDecode(this.getDomValue(e.GetAttribute("id")));
                }
            }

            hec = this.browser.Document.GetElementsByTagName("select");
            foreach (HtmlElement e in hec) {
                if (this.persistant.ContainsKey(e.GetAttribute("id"))) {
                    e.SetAttribute("value", Convert.ToString(this.persistant[e.GetAttribute("id")]));
                } else {
                    this.persistant[e.GetAttribute("id")] = this.getDomValue(e.GetAttribute("id"));
                }
            }

            string classes = null;
            if (nrMonitors > 1) classes += " multi ";
            Config.setBrowserBodyClasses(this.browser, this.screensaver.action, classes);

            this.browser.Document.InvokeScript("persistantConfigLoaded", new string[] { Convert.ToString(Screen.AllScreens.Length) });

            if (this.screensaver.action == Screensaver.Actions.Preview && this.screensaver.monitors != null) {
                this.screensaver.monitors[0].defaultShowHide();
            }

        }

        public string jsonAllPersistant() {
            if (this.persistant != null) {
                return JsonConvert.SerializeObject(this.persistant, Newtonsoft.Json.Formatting.Indented);
            }
            return null;
        }

        public bool savePersistantConfig() {
            if (this.persistant != null) {
                this.persistant["effects"] = JsonConvert.SerializeObject(this.effects);
                this.persistant["filters"] = Convert.ToString(this.browser.Document.InvokeScript("getJsonFilters"));
                if (this.screensaver.action != Screensaver.Actions.Config) {
                    for (int i = 0; i < this.screensaver.monitors.Length; i++) {
                        this.persistant["historyM" + Convert.ToString(i)] = JsonConvert.SerializeObject(this.screensaver.monitors[i].historyLastEntries(Convert.ToInt32(this.getPersistant("rememberLast"))));
                        this.persistant["historyOffsetM" + Convert.ToString(i)] = Convert.ToString(this.screensaver.monitors[i].offset);
                    }
                }

                this.connectToDB();
                SQLiteTransaction transaction = null;
                try {
                    transaction = this.dbConnector.connection.BeginTransaction(true);
                } catch (System.Data.SQLite.SQLiteException se) {
                    this.screensaver.showInfoOnMonitors("Error: " + se.Message, true);
                    return false;
                }
                foreach (var p in this.persistant) {
                    SQLiteCommand insertSQL = new SQLiteCommand("INSERT OR REPLACE INTO Setting (key, value) VALUES (@key, @value);", this.dbConnector.connection);
                    insertSQL.Parameters.AddWithValue("@key", p.Key);
                    insertSQL.Parameters.AddWithValue("@value", p.Value);
                    insertSQL.ExecuteNonQuery();
                }
                transaction.Commit();
            }
            return true;
        }
 
        public void Message(string Text) {
            MessageBox.Show(Text);
        }

        public HtmlElementCollection GetElementsByTagName(string name) {
            return (HtmlElementCollection)this.browser.Invoke(new Func<HtmlElementCollection>(() => this.browser.Document.GetElementsByTagName(name)));
        }

        /****
         * Called from config.js
         ****/
        public void jsApplyFilter(string filter) {
            if (this.screensaver.fileNodes != null) {
                try { 
                    this.screensaver.fileNodes.setFilterSQL(filter);
                } catch (System.Data.SQLite.SQLiteException e) {
                    this.clearFilter();
                    this.setDomValue("useFilter", "false");
                    MessageBox.Show(e.Message + Environment.NewLine + "Correct fault and re-apply filter", "Filter fault", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        public void jsClearFilter(string jsDummy) {
            this.clearFilter();
            this.setDomValue("useFilter", "false");
        }

        public void clearFilter() {
            if (this.screensaver.fileNodes != null) this.screensaver.fileNodes.clearFilter();
        }

        public bool resetWallpaper() {
            Wallpaper wallpaper = new Wallpaper(this.screensaver);
            wallpaper.resetDefaultWallpaper();
            return true;
        }

        public bool jsSetGPURendering() {
            if (this.getPersistantBool("gpuRendering")) {
                Registry.SetValue("HKEY_CURRENT_USER\\" + Constants.regkeyGPURendering, Constants.regkeyExecutable, 1, RegistryValueKind.DWord);
                Registry.SetValue("HKEY_CURRENT_USER\\" + Constants.regkeyGPURendering, Constants.regkeyLauncher, 1, RegistryValueKind.DWord);
            } else {
                RegistryKey regKey = Registry.CurrentUser.OpenSubKey(Constants.regkeyGPURendering, true);
                if (regKey != null) {
                    regKey.DeleteValue(Constants.regkeyExecutable, false);
                    regKey.DeleteValue(Constants.regkeyLauncher, false);

                }
            }
            return false;
        }

        /****
         * Called from config.js
         * dumdum variable is used to avoid calling the function when testing with
         * if (typeof(window.external.getInitialFoldersJSON) !== "undefined") in JavaScript
         ****/
        public string getInitialFoldersJSON(bool dumdum) {
            List<jsonFolder> folders = new List<jsonFolder>();
            //MessageBox.Show("getInitialFoldersJSON()");
            //            folders.Add(new jsonFolder());
            //          folders.Add(new jsonFolder(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)));
            /*
                        jsonFolder network = new jsonFolder("Network");
                        network.key = "\\";
                        folders.Add(network);
                        DirectoryEntry root = new DirectoryEntry("WinNT:");
                        foreach (DirectoryEntry networkComputers in root.Children) {
                            foreach (DirectoryEntry networkComputer in networkComputers.Children) {
                                if (networkComputer.Name != "Schema") {
                                    jsonFolder networked = new jsonFolder(networkComputer.Name);
                                    networked.lazy = true;
                                    network.children.Add(networked);

                                    try {
                                        ManagementObjectSearcher searcher =
                                            new ManagementObjectSearcher("root\\CIMV2",
                                            "SELECT * FROM Win32_Share");

                                        foreach (ManagementObject queryObj in searcher.Get()) {
                                            Debug.WriteLine("-----------------------------------");
                                            Debug.WriteLine("Win32_Share instance");
                                            Debug.WriteLine("-----------------------------------");
                                            Debug.WriteLine("Name: {0}", queryObj["Name"]);
                                        }
                                    } catch (ManagementException e) {
                                        MessageBox.Show("An error occurred while querying for WMI data: " + e.Message);
                                    }
                                    //textBox1.Text += computer.Name + "\r\n";
                                }
                            }
                        }
                        */
            jsonFolder computer = new jsonFolder("Computer");
            computer.expanded = true;
            folders.Add(computer);
            DriveInfo[] info = DriveInfo.GetDrives();

            foreach (DriveInfo di in info) {
                string drive = "(" + di.Name.Replace("\\", "") + ")";
                jsonFolder f = new jsonFolder(drive);
                f.lazy = true;
                f.key = di.Name;

                string extraInfo = "";
                if (di.IsReady) {
                    extraInfo += di.VolumeLabel + " ";
                } else {
                    f.unselectable = true;
                    f.extraClasses = "dim";
                }
                switch (di.DriveType) {
                    case DriveType.Removable:
                        if (extraInfo == "") extraInfo += "Removable Disk ";
                        break;
                }
                if (extraInfo != "") f.title = extraInfo + f.title;
                computer.children.Add(f);
            }

            if (this.persistant == null || this.getPersistant("folders") == null) {
                this.loadPersistantConfig();
            }

            foreach (string folder in Convert.ToString(this.getPersistant("folders")).Split(new string[] { Environment.NewLine, "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)) {
                if (folder.Substring(0, 2) == "\\\\") {
                    string[] parts = folder.Split(new string[] { "\\" }, StringSplitOptions.RemoveEmptyEntries);
                    int i = 0;
                    jsonFolder network = new jsonFolder("Network");
                    folders.Add(network);
                    jsonFolder node = network;
                    while (i < parts.Length) {
                        jsonFolder newNode = new jsonFolder(parts[i]);
                        node.children.Add(newNode);
                        i++;
                        if (i == parts.Length) {
                            newNode.selected = true;
                            jsonFolder dummy = new jsonFolder("dummy");
                            dummy.selected = false;
                            //dummy.unselectable = false;
                            dummy.extraClasses = "hidden";
                            node.children.Add(dummy);
                        }
                        node = newNode;
                    }
                } else {
                    jsonFolder node = computer;
                    string basePath = "";
                    string[] parts = folder.Split(new string[] { "\\" }, StringSplitOptions.RemoveEmptyEntries);

                    int i = 0;
                    while (i < parts.Length) {
                        string key = parts[i].ToLower();
                        if (key.IndexOf(':') > -1) key = key.ToUpper() + "\\";
                        jsonFolder newNode = node.hasChild(key);

                        if (newNode == null) {
                            // Add children if not found
                            node.children.AddRange(this.getFolder(basePath));
                            node = node.hasChild(key);
                            if (node == null) break; // Escape while loop if still not found
                        } else {
                            node = newNode;
                        }
                        //node.expanded = true;
                        node.selected = true;
                        basePath += parts[i] + "\\";
                        i++;
                    }
                    if (node != null) node.selected = true;
                }
            }
            return JsonConvert.SerializeObject(folders);

        }

        public List<jsonFolder> getFolder(string folder) {
            List<jsonFolder> children = new List<jsonFolder>();
            if (!Directory.Exists(folder)) return children;
            string[] dirs = Directory.GetDirectories(folder);
            foreach (string dir in dirs) {
                try {
                    FileInfo fi = new FileInfo(dir);
                    if ((fi.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden) {
                        jsonFolder d = new jsonFolder(fi.Name.ToLower());
                        d.lazy = (Directory.GetDirectories(dir).Length > 0);
                        children.Add(d);
                    }
                } catch (Exception e) {
                    Debug.WriteLine("getFolder " + e.Message);
                    // No access
                }
            }
            return children;
        }

        public string getFolderJSON(string folder) {
            return JsonConvert.SerializeObject(this.getFolder(folder));
        }

        public string getEffectsJSON() {
            return JsonConvert.SerializeObject(this.effects);

        }

        public string getRandomEffect() {
            string json = null;
            if (this.getPersistantBool("useTransitions") && this.effects != null) {
                jsonFolder effect = jsonFolder.getRandomSelected(this.effects.children);
                jsonFolder direction = null;
                if (effect == null) {
                    json = "{\"effect\":\"fade\", \"duration\":100}";
                } else { 
                    json += "{\"effect\":\"" + effect.key + "\"";
                    if (effect.children.Count > 0) {
                        direction = jsonFolder.getRandomSelected(effect.children);
                        if (direction != null) json += ", \"direction\":\"" + direction.title.ToLower() + "\"";
                    }
                    json += ", \"duration\":1000}";
                }
            }
            return json;
        }

        public string InvokeScriptOnMonitor(int monitor, string script, string parameters) {
            string s = null;
            if (this.screensaver.monitors != null) for (int i = 0; i < this.screensaver.monitors.Length; i++) {
                if (monitor == Screensaver.CM_ALL || monitor == i) {
                    s += Convert.ToString(this.screensaver.monitors[i].InvokeScript(script, parameters.Split(';')));
                }
            }
            return s;
        }

        private HtmlElement getElementById(string id) {
            // Make thread safe 
            try {
                return (HtmlElement)this.browser.Invoke(new Func<HtmlElement>(() => this.browser.Document.GetElementById(id)));
            } catch (Exception e) {
                Debug.WriteLine("getElementById" + e.Message);
                return null;
            }
        }

        /***
         * This function assumes that <div id="#name#"> encapsulates
         *  <input type="radio" name="#name# value=...> lines
         ***/
        private string getDomRadioValue(string id) {
            HtmlElement he = (HtmlElement)this.browser.Invoke(new Func<HtmlElement>(() => this.browser.Document.GetElementById(id)));
            if (he != null) {
                HtmlElementCollection hec = he.GetElementsByTagName("input");
                foreach (HtmlElement e in hec) {
                    if (e.GetAttribute("type").ToLower() == "radio" && e.GetAttribute("checked").ToLower() == "true") {
                        return e.GetAttribute("value");
                    }
                }
            }
            return null;
        }

        private bool getDomCheckboxValue(string id) {
            HtmlElement he = this.getElementById(id);
            if (he == null) return false;
            switch (he.TagName.ToLower()) {
                case "input":
                    return (bool)he.GetAttribute("checked").ToLower().Equals("true");
                break;
                default:
                    Debug.WriteLine("getCheckboxValue called on non checkbox");
                break;
            }
            return false;
        }

        public bool persistantLoaded() {
            return this.persistant != null;
        }

        public bool hasPersistantKey(string key) {
            return this.persistant.ContainsKey(key);
        }

        public void setPersistant(string key, object value) {
            this.setPersistant(key, value, true);
        }

        public void setPersistant(string key, object value, bool updateDom) {
            if (key == null) {
                this.screensaver.showInfoOnMonitors("Invalid configuration key: null", true);
            } else { 
                this.persistant[key] = value;
                if (updateDom) this.setDomValue(key, Convert.ToString(value));
            }
        }

        public object getPersistant(string key) {
            if (this.persistant == null || !this.persistant.ContainsKey(key)) throw new KeyNotFoundException(key);
            return persistant[key];
        }

        public bool getPersistantBool(string key) {
            if (!this.persistant.ContainsKey(key)) throw new KeyNotFoundException(key);
            if (this.persistant[key].GetType() == typeof(bool)) {
                return (bool)this.persistant[key];
            }
            switch (Convert.ToString(this.persistant[key])) {
                case "True": case "true": case "1": 
                    return true; 
                break;
                case "False": case "false": case "0": 
                    return false;
                break;
            }
            throw new Exception("Can't cast keys '" + key + "' value " + this.persistant[key] + key + " to boolean");
        }

        public string getPersistantString(string key) {
            return Convert.ToString(this.getPersistant(key));
        }

        public bool syncMonitors() {
            if (this.persistant == null) return false;
            return this.getPersistantBool("syncScreens");
        }

        public Config.Order changeOrder() {
            if (this.getOrder() == Config.Order.Random) {
                //this.checkCheckbox("orderSequential");
                this.setPersistant("order", Config.Order.Sequential);
                return Config.Order.Sequential;
            } else {
                //this.checkCheckbox("orderRandom");
                this.setPersistant("order", Config.Order.Random);
                return Config.Order.Random;
            }
//            return null;
        }

        public Config.Order getOrder() {
            if (this.getPersistant("order").GetType() == typeof(string)) {
                switch (this.getPersistantString("order")) {
                    case "random": case "1":
                        return Config.Order.Random;
                    break;
                    default:
                        return Config.Order.Sequential;
                    break;
                }
            } else {
                if ((Config.Order)Enum.Parse(typeof(Config.Order), this.getPersistantString("order")) == Config.Order.Random) return Config.Order.Random;
                else return Config.Order.Sequential;
            }
        }

        public void setInnerHTML(string id, string html) {
            HtmlElement he = this.getElementById(id);
            if (he != null) {
                he.InnerHtml = html;
            }
        }

        public void setDomValue(string id, string value) {
            HtmlElement he = this.getElementById(id);
            if (he != null) {
                switch (he.TagName.ToLower()) {
                    case "textarea":
                        he.InnerHtml = Utils.HtmlDecode(value);
                    break;
                    default:
                        switch (he.GetAttribute("type").ToLower()) {
                            case "radio":
                                //he.SetAttribute("value", value);
                                if (value == "checked" || value == he.GetAttribute("value").ToLower()) {
                                    he.SetAttribute("checked", "true");
                                } else {
                                    he.SetAttribute("checked", "");
                                }
                            break;                            
                            case "checkbox":
                                if (value == "false") {
                                    he.SetAttribute("checked", "");
                                } else {
                                    he.SetAttribute("checked", "true");
                                }
                            break;
                            default:
                                he.SetAttribute("value", value);
                            break;
                        }
                    break;
                }
            }
        }

        public string getDomValue(string id) {
            HtmlElement he = this.getElementById(id);
            if (he == null) return null;
            try {
                switch (he.TagName.ToLower()) {
                    case "textarea":
                        return Utils.HtmlDecode(he.InnerHtml);
                        //return he.InnerHtml;
                    break;
                    default:
                        return he.GetAttribute("value");
                    break;
                }
            } catch (System.Runtime.InteropServices.COMException co) {
                //this.screensaver.showInfoOnMonitors("Error getPersistant(" + id + ")\n" + Convert.ToString(co.Message));
                return null;
            }
            return null;
        }

        
        public void Config_FormClosing(object sender, FormClosingEventArgs e) {
            if (screensaver.action == Screensaver.Actions.Config) {
                this.savePersistantConfig();
                Application.Exit();
            } else if (!this.screensaver.applicationClosing) {
                if (!this.syncMonitors()) {
                    // restart timer in-case sync option has changed.
                    for (int i = 1; i < this.screensaver.monitors.Length; i++) {
                        this.screensaver.monitors[i].timer.Start();
                    }
                }
                //Console.Beep();
                this.Hide();
                e.Cancel = true;
            } 
        }

        public void ConfigDocumentCompleted(object sender, System.Windows.Forms.WebBrowserDocumentCompletedEventArgs e) {
            if (this.screensaver.action == Screensaver.Actions.Wallpaper) {
                this.screensaver.initForScreensaverAndWallpaper();
                Wallpaper wallpaper = new Wallpaper(this.screensaver);
                wallpaper.generateWallpaper();
                Application.Exit();
            } else {
                this.screensaver.initializeMonitors();
                if (!this.configInitialised) {
                    this.setInnerHTML("version", Constants.getNiceVersion());
                    this.setInnerHTML("versionIE", "(IE:" + this.browser.Version.Major.ToString() + "." + this.browser.Version.Minor.ToString() + ")");
                    this.browser.Document.InvokeScript("initFancyTreeFolder");
                    this.browser.Document.InvokeScript("initFancyTreeTransitions");
                    this.configInitialised = true;
                }
            }
        }

        private void setCurrentTrackChanges() {
            List<string> keys = new List<string>(this.trackChanges.Keys);
            for (int i = 0; i < keys.Count; i++) {
                this.trackChanges[keys[i]] = this.getPersistant(keys[i]);
            }
        }

        private bool checkTrackChangesChanged() {
            List<string> keys = new List<string>(this.trackChanges.Keys);
            for (int i = 0; i < keys.Count; i++) {
                if (this.trackChanges[keys[i]] != this.getPersistant(keys[i])) return true;
            }
            return false;
        }
            
        public bool? isUpdateNewer() {
            if (this.webUpdateCheck.Document != null) {
                HtmlElement he = this.webUpdateCheck.Document.GetElementById("download");
                if (he != null) {
                    Version update = new Version(he.GetAttribute("data-version"));
                    return (this.screensaver.version.CompareTo(update) < 0);
                }
            }
            return null;
        }

        public string updateFilename() {
            if (this.webUpdateCheck.Document != null) {
                HtmlElement he = this.webUpdateCheck.Document.GetElementById("download");
                if (he != null) {
                    return Convert.ToString(Path.Combine(Constants.getUpdateFolder(), Path.GetFileName(he.GetAttribute("href"))));
                }
            }
            return null;
        }

        public string updateDownloadUrl() {
            if (this.webUpdateCheck.Document != null) {
                HtmlElement he = this.webUpdateCheck.Document.GetElementById("download");
                if (he != null) {
                    return he.GetAttribute("href");
                }
            }
            return null;
        }

        public string updateFileMD5() {
            if (this.webUpdateCheck.Document != null) {
                HtmlElement he = this.webUpdateCheck.Document.GetElementById("download");
                if (he != null) {
                    return Convert.ToString(he.GetAttribute("data-md5"));
                }
            }
            return null;
        }
        

        public void installUpdate() {
            if (this.webUpdateCheck.Document != null) {
                HtmlElement he = this.webUpdateCheck.Document.GetElementById("download");
                if (he != null) {
                    try {
                        Utils.RunTaskScheduler(@"RunRPSUpdate", Convert.ToString(Path.Combine(Constants.getUpdateFolder(), Path.GetFileName(he.GetAttribute("href")))), null);
                        this.screensaver.OnExit();
                        this.showUpdateInfo("Running installer");
                    } catch (System.ComponentModel.Win32Exception we) {
                        this.screensaver.showInfoOnMonitors("RPS update cancelled" + Environment.NewLine + we.Message, true, true);

                        string clickOrKey = "Press 'U' key to update";
                        if (this.getPersistant("mouseSensitivity") == "none" || this.screensaver.action == Screensaver.Actions.Config) clickOrKey = "Click to install now";
                        this.screensaver.showUpdateInfo("RPS " + he.GetAttribute("data-version") + " downloaded<br/><a class='exit external' target='_blank' href='file://" + Path.Combine(Constants.getUpdateFolder(), Path.GetFileName(he.GetAttribute("href"))) + "'>" + clickOrKey + "</a>.");

                        this.screensaver.resetMouseMove();
                    } 
                    return;
                }
            }
            this.showUpdateInfo("Nothing to install");
        }

        private void DownloadProgress(object sender, DownloadProgressChangedEventArgs e) {
            if (!this.downloadProgress.IsRunning) this.downloadProgress.Start();
            if (this.downloadProgress.ElapsedMilliseconds > 250) { 
                if (this.screensaver.action == Screensaver.Actions.Config) {
                    //this.downloadProgressIndicator(e.ProgressPercentage);
                } else {
                    this.screensaver.monitors[0].downloadProgressIndicator(e.ProgressPercentage);
                }
                this.downloadProgress.Reset();
                this.downloadProgress.Start();
            }
        }

        void DownloadFileCompleted(object sender, AsyncCompletedEventArgs e) {
            //WebBrowser wbCheckUpdate = sender as WebBrowser;
            HtmlElement he = this.webUpdateCheck.Document.GetElementById("download");
            string updatePath = Path.Combine(Constants.getUpdateFolder(), Path.GetFileName(he.GetAttribute("href")));
            if (!Utils.VerifyMD5(updatePath, he.GetAttribute("data-md5"))) {
                // <a href='" + he.GetAttribute("href") + "'>
                this.showUpdateInfo("Download " + he.GetAttribute("data-version") + " failed<br/>Please U key to start download manually.");
                return;
            }
            if (this.getPersistantString("checkUpdates") == "yes") this.installUpdate();
            else {
                string message = "RPS " + he.GetAttribute("data-version") + " downloaded<br/><a class='exit external' target='_blank' href='file://" + updatePath + "'>";
                if (this.getPersistant("mouseSensitivity") == "none" || this.screensaver.action == Screensaver.Actions.Config) {
                    message += "Click to install now</a>";
                } else {
                    message += "Press 'U' key to update</a><div class='small'>(Ctrl + U to ignores this update)</div>";
                }
                this.screensaver.showUpdateInfo(message);
            }
        }

        public void showUpdateInfo(string info) {
            HtmlElement he = this.browser.Document.GetElementById("update");
            he.InnerHtml = info.Replace("<br/>", " ");
            if (this.screensaver.action != Screensaver.Actions.Config) {
                this.screensaver.showUpdateInfo(info);
            }
        }

        private Uri getUpdateUri() {
            string param = "?v=" + Constants.getNiceVersion();
            if (this.screensaver.config.getPersistantBool("disableGoAn")) param = "?track=no";
            return new Uri(Constants.UpdateCheckURL + param);
        }

        public string getUpdateVersion() {
            if (this.webUpdateCheck.Url.Equals(this.getUpdateUri())) {
                HtmlElement he = this.webUpdateCheck.Document.GetElementById("download");
                return he.GetAttribute("data-version");
            }
            return null;
        }
        
        private void webUpdateCheck_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e) {
            if (this.webUpdateCheck.Url.Equals(this.getUpdateUri())) {
                HtmlElement he = this.webUpdateCheck.Document.GetElementById("download");
                if (he != null) {
                    Version ignore = null;
                    Version compareTo = this.screensaver.version;
                    //he.Get
                    Version update;
                    try {
                        update = new Version(he.GetAttribute("data-version"));
                    } catch (Exception ex) {
                        this.screensaver.showInfoOnMonitors("Error detecting latest version" + Environment.NewLine + ex.Message, true, true);
                        return;
                    }
                    if (this.getPersistantBool("ignoreUpdate")) {
                        try {
                            ignore = new Version(this.getPersistantString("ignoreVersion"));
                        } catch (Exception) { }
                        if (ignore != null && compareTo.CompareTo(ignore) < 0) {
                            compareTo = ignore;
                        }
                    }
                    if (!this.getPersistantBool("betaVersion")) {
                        // Revision number = 0 for alpha release, 
                        // Revision number != 0 indicates beta / release candidate version
                        update = new Version(update.Major, update.Minor, update.Build);
                    }
                    this.newVersionAvailable = (compareTo.CompareTo(update) < 0);

                    if (this.newVersionAvailable) {
                        if (this.downloadUpdates) {
                            string downloadedUpdateLocalPath = Path.Combine(Constants.getUpdateFolder(), Path.GetFileName(he.GetAttribute("href")));
                            if (!File.Exists(downloadedUpdateLocalPath) || !Utils.VerifyMD5(downloadedUpdateLocalPath, he.GetAttribute("data-md5"))) {
                                if (File.Exists(downloadedUpdateLocalPath)) File.Delete(downloadedUpdateLocalPath);
                                this.showUpdateInfo("<div class='downloadProgress'><div class='downloadLabel'>Downloading update: " + update.ToString() + "</div><div class='downloadProgress' style='width: 0%'></div>");
                                Directory.CreateDirectory(Path.GetDirectoryName(downloadedUpdateLocalPath));//.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Constants.DownloadFolder));
                                WebClient client = new WebClient();
                                client.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadFileCompleted);
                                client.DownloadProgressChanged += new DownloadProgressChangedEventHandler(DownloadProgress);
                                client.DownloadFileAsync(new Uri(he.GetAttribute("href")), downloadedUpdateLocalPath);
                                return;
                            } else {
                                this.DownloadFileCompleted(this, null);
                            }
                        } else {
                            this.showUpdateInfo("Update available<br/><a href='" + he.GetAttribute("href") + "'>Download RPS " + he.GetAttribute("data-version-text") + "</a>");
                        }
                    } else if (this.screensaver.showUpdateStatus) {
                        this.screensaver.showAllUpToDate();
                    }
                }
            }
        }
        
        public void timerCheckUpdates_Tick(object sender, EventArgs e) {
            this.timerCheckUpdates.Enabled = false;
            if (this.screensaver.action != Screensaver.Actions.Preview) {
                string update;
                try {
                    update = this.getPersistantString("checkUpdates");
                } catch (KeyNotFoundException knfe) {
                    // Try again in a bit
                    this.timerCheckUpdates.Interval *= 2;
                    this.timerCheckUpdates.Enabled = true;
                    return;
                }
                switch (update) {
                    case "yes":
                    case "download":
                        this.checkUpdates = true;
                        this.downloadUpdates = true;
                        break;
                    case "notify":
                        this.checkUpdates = true;
                        break;
                }
                if (this.checkUpdates) {
                    //bgwCheckUpdate.DoWork();
                    //bgwCheckUpdate.RunWorkerAsync();
                    this.webUpdateCheck.Url = this.getUpdateUri();
                }
            }
        }

        private void Config_VisibleChanged(object sender, EventArgs e) {
            if (this.Visible && this.screensaver.action != Screensaver.Actions.Config) {
                this.setCurrentTrackChanges();
            } else if (this.screensaver.action != Screensaver.Actions.Config) {
                // Hiding
                if (this.checkTrackChangesChanged()) {
                    if (this.trackChanges["ignoreHiddenFiles"] != this.getPersistant("ignoreHiddenFiles") || this.trackChanges["ignoreHiddenFolders"] != this.getPersistant("ignoreHiddenFolders")) {
                        this.screensaver.showInfoOnMonitors("Emptying Media Database", true, false);
                        this.screensaver.fileNodes.purgeMediaDatabase();
                        this.screensaver.showInfoOnMonitors("Media Database Emptied", true, true);
                    } else {
                        this.screensaver.showInfoOnMonitors("", true, false);
                    }
                    this.screensaver.fileNodes.purgeNotMatchingParentFolders(this.getPersistantString("folders"));
                    this.screensaver.fileNodes.resetFoldersQueue();
                    this.screensaver.fileNodes.restartBackgroundWorkerImageFolder();
                }
            }
            if (this.Visible) {
                Cursor.Show();
            } else {
                Cursor.Hide();
            }
        }
/*        
        private void Config_Resize(object sender, EventArgs e) {
            Console.Beep();
            if (this.WindowState == FormWindowState.Minimized) {
                this.Hide();
            }
        }
  */      
        /*
        [STAThread]
        private void bgwCheckUpdate_DoWork(object sender, DoWorkEventArgs e) {
            var wbCheckUpdate = new WebBrowser();
            wbCheckUpdate.ScriptErrorsSuppressed = true;
            wbCheckUpdate.Visible = false;
            wbCheckUpdate.DocumentCompleted += wbCheckUpdate_DocumentCompleted;
            wbCheckUpdate.Url = this.getUpdateUri();
        }

        void wbCheckUpdate_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e) {
            WebBrowser wbCheckUpdate = sender as WebBrowser;
            if (wbCheckUpdate.Url.Equals(this.getUpdateUri())) {
                HtmlElement he = wbCheckUpdate.Document.GetElementById("download");
                if (he != null) {
                    Version update = new Version(he.GetAttribute("data-version"));
                    this.newVersionAvailable = (this.screensaver.version.CompareTo(update) < 0);

                    if (this.newVersionAvailable) {
                        if (this.downloadUpdates) {
                            string updatePath = Path.Combine(Constants.getUpdateFolder(), Path.GetFileName(he.GetAttribute("href")));
                            if (!File.Exists(updatePath) || !this.VerifyMD5(updatePath, he.GetAttribute("data-md5"))) {
                                this.showUpdateInfo("<div class='downloadProgress'><div class='downloadLabel'>Downloading update: " + update.ToString() + "</div><div class='downloadProgress' style='width: 0%'></div>");
                                Directory.CreateDirectory(Path.GetDirectoryName(updatePath));//.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Constants.DownloadFolder));
                                WebClient client = new WebClient();
                                client.DownloadFileCompleted += new AsyncCompletedEventHandler(DownloadFileCompleted);
                                client.DownloadProgressChanged += new DownloadProgressChangedEventHandler(DownloadProgress);
                                client.DownloadFileAsync(new Uri(he.GetAttribute("href")), updatePath);
                                return;
                            } else {
                                this.DownloadFileCompleted(this, null);
                            }
                        } else {
                            this.showUpdateInfo("Update available<br/><a href='" + he.GetAttribute("href") + "'>Download RPS " + he.GetAttribute("data-version-text") + "</a>");
                        }
                    }
                }
            }
        }*/
    }
}
