using System;
using System.IO;
using System.Xml.Linq;
using Newtonsoft.Json;

namespace CIEID.Core
{
    /// <summary>
    /// Archivio impostazioni in JSON (%LOCALAPPDATA%\CIE-Middleware-NG\settings.json), usato dalla nuova interfaccia.
    /// Al primo avvio importa, se presente, l'elenco delle CIE e le impostazioni dal user.config della vecchia GUI.
    /// </summary>
    public class JsonSettingsStore : ISettingsStore
    {
        private class Data
        {
            public string CieList = "";
            public string ProxyUrl = "";
            public int ProxyPort;
            public string ProxyCredentials = "";
            public bool TimestampEnabled = true;
            public string TimestampUrl = "http://timestamp.actalis.it";
            public string TimestampCustomUrl = "";
            public string TimestampCredentials = "";
            public bool FirstTime = true;
        }

        private readonly string path;
        private Data d = new Data();

        public static string DataDirectory
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CIE-Middleware-NG"); }
        }

        public JsonSettingsStore() : this(Path.Combine(DataDirectory, "settings.json")) { }

        public JsonSettingsStore(string filePath)
        {
            path = filePath;
            if (File.Exists(path))
            {
                try { d = JsonConvert.DeserializeObject<Data>(File.ReadAllText(path)) ?? new Data(); }
                catch (Exception) { d = new Data(); }
            }
            else
            {
                ImportLegacy();
                Save();
            }
        }

        public string CieList { get { return d.CieList ?? ""; } set { d.CieList = value; } }
        public string ProxyUrl { get { return d.ProxyUrl ?? ""; } set { d.ProxyUrl = value; } }
        public int ProxyPort { get { return d.ProxyPort; } set { d.ProxyPort = value; } }
        public string ProxyCredentials { get { return d.ProxyCredentials ?? ""; } set { d.ProxyCredentials = value; } }
        public bool TimestampEnabled { get { return d.TimestampEnabled; } set { d.TimestampEnabled = value; } }
        public string TimestampUrl { get { return d.TimestampUrl ?? ""; } set { d.TimestampUrl = value; } }
        public string TimestampCustomUrl { get { return d.TimestampCustomUrl ?? ""; } set { d.TimestampCustomUrl = value; } }
        public string TimestampCredentials { get { return d.TimestampCredentials ?? ""; } set { d.TimestampCredentials = value; } }
        public bool FirstTime { get { return d.FirstTime; } set { d.FirstTime = value; } }

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonConvert.SerializeObject(d, Formatting.Indented));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        // Importa dal user.config più recente della vecchia GUI (solo lettura)
        private void ImportLegacy()
        {
            try
            {
                string root = DataDirectory;
                if (!Directory.Exists(root)) return;
                string best = null;
                DateTime bestTime = DateTime.MinValue;
                foreach (string f in Directory.GetFiles(root, "user.config", SearchOption.AllDirectories))
                {
                    DateTime t = File.GetLastWriteTimeUtc(f);
                    if (t > bestTime) { best = f; bestTime = t; }
                }
                if (best == null) return;

                foreach (XElement s in XDocument.Load(best).Descendants("setting"))
                {
                    string name = (string)s.Attribute("name");
                    string val = (string)s.Element("value") ?? "";
                    switch (name)
                    {
                        case "cieList": d.CieList = val == "0" ? "" : val; break;
                        case "proxyURL": d.ProxyUrl = val; break;
                        case "proxyPort": int.TryParse(val, out d.ProxyPort); break;
                        case "credentials": d.ProxyCredentials = val; break;
                        case "timestampEnabled": bool.TryParse(val, out d.TimestampEnabled); break;
                        case "timestampUrl": if (val.Length > 0) d.TimestampUrl = val; break;
                        case "timestampCustomUrl": d.TimestampCustomUrl = val; break;
                        case "timestampCredentials": d.TimestampCredentials = val; break;
                    }
                }
                d.FirstTime = false;
            }
            catch (Exception)
            {
                // import best-effort
            }
        }
    }
}
