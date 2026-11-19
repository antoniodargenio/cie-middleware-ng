using System;
using System.IO;
using System.IO.Compression;

namespace CIEID.Core
{
    /// <summary>Raccolta ed eliminazione dei file di log in %PROGRAMDATA%\CIEPKI-NG.</summary>
    public static class LogFileService
    {
        public static string LogDirectory
        {
            get { return Path.Combine(Environment.GetEnvironmentVariable("PROGRAMDATA") ?? @"C:\ProgramData", "CIEPKI-NG"); }
        }

        /// <summary>Crea uno zip con i file .log; restituisce false (e non lascia il file) se non ce ne sono.</summary>
        public static bool CollectToZip(string zipPath)
        {
            bool logFound = false;
            using (FileStream fileStream = new FileStream(zipPath, FileMode.Create))
            using (ZipArchive archive = new ZipArchive(fileStream, ZipArchiveMode.Create, true))
            {
                foreach (string fileName in Directory.GetFiles(LogDirectory))
                {
                    if (Path.GetExtension(fileName).ToLower() == ".log")
                    {
                        logFound = true;
                        ZipArchiveEntry entry = archive.CreateEntry(Path.GetFileName(fileName), CompressionLevel.Optimal);
                        using (Stream zipStream = entry.Open())
                        {
                            byte[] logData = File.ReadAllBytes(fileName);
                            zipStream.Write(logData, 0, logData.Length);
                        }
                    }
                }
            }
            if (!logFound && File.Exists(zipPath))
                File.Delete(zipPath);
            return logFound;
        }

        /// <summary>Elimina i file .log; false se qualcuno non è stato cancellabile (es. in uso).</summary>
        public static bool ClearAll(Action<string> onError = null)
        {
            bool ok = true;
            foreach (string fileName in Directory.GetFiles(LogDirectory))
            {
                try
                {
                    if (Path.GetExtension(fileName).ToLower() == ".log")
                        File.Delete(fileName);
                }
                catch (Exception ex)
                {
                    if (onError != null)
                        onError(ex.Message);
                    ok = false;
                }
            }
            return ok;
        }
    }
}
