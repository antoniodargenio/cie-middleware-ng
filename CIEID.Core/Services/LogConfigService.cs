using System;
using System.IO;

namespace CIEID.Core
{
    /// <summary>Livelli di log salvati in %PROGRAMDATA%\CIEPKI-NG\config (LIB_LOG_LEVEL / APP_LOG_LEVEL).</summary>
    public static class LogConfigService
    {
        private static string ConfigPath
        {
            get { return Path.Combine(LogFileService.LogDirectory, "config"); }
        }

        public static void Read(out LogLevel app, out LogLevel lib)
        {
            app = LogLevel.NONE;
            lib = LogLevel.NONE;
            try
            {
                foreach (string line in File.ReadAllLines(ConfigPath))
                {
                    string[] kv = line.Split(new char[] { '=' }, 2);
                    int v;
                    if (kv.Length != 2 || !int.TryParse(kv[1].Trim(), out v) || v < 0 || v > 3) continue;
                    if (kv[0].Trim() == "APP_LOG_LEVEL") app = (LogLevel)v;
                    else if (kv[0].Trim() == "LIB_LOG_LEVEL") lib = (LogLevel)v;
                }
            }
            catch (Exception) { }
        }

        public static bool Write(LogLevel app, LogLevel lib)
        {
            try
            {
                Directory.CreateDirectory(LogFileService.LogDirectory);
                File.WriteAllLines(ConfigPath, new string[]
                {
                    "LIB_LOG_LEVEL=" + (int)lib,
                    "APP_LOG_LEVEL=" + (int)app
                });
                return true;
            }
            catch (Exception) { return false; }
        }
    }
}
