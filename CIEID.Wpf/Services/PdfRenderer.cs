using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace CIEID.Wpf.Services
{
    /// <summary>Anteprima delle pagine PDF tramite mutool.exe (distribuito accanto all'app).</summary>
    public static class PdfRenderer
    {
        private static string MutoolPath
        {
            get { return Path.Combine(AppContext.BaseDirectory, "Tools", "mutool.exe"); }
        }

        private static string Run(params string[] args)
        {
            ProcessStartInfo psi = new ProcessStartInfo(MutoolPath)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (string a in args) psi.ArgumentList.Add(a);
            using (Process p = Process.Start(psi))
            {
                Task<string> o = p.StandardOutput.ReadToEndAsync();
                p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(30000)) { try { p.Kill(); } catch (Exception) { } throw new TimeoutException("mutool non risponde"); }
                return o.Result;
            }
        }

        public static Task<int> CountPagesAsync(string pdf)
        {
            return Task.Run(delegate
            {
                string o = Run("info", pdf);
                Match m = Regex.Match(o, @"Pages:\s*(\d+)");
                return m.Success ? int.Parse(m.Groups[1].Value) : 0;
            });
        }

        /// <summary>Rende la pagina (da 1) in un bitmap.</summary>
        public static Task<BitmapSource> RenderPageAsync(string pdf, int page)
        {
            return Task.Run(delegate
            {
                string tmp = Path.Combine(Path.GetTempPath(), "cieng-" + Guid.NewGuid().ToString("N") + ".png");
                try
                {
                    Run("draw", "-r", "110", "-o", tmp, pdf, page.ToString());
                    BitmapImage bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.UriSource = new Uri(tmp);
                    bi.EndInit();
                    bi.Freeze();
                    return (BitmapSource)bi;
                }
                finally { try { File.Delete(tmp); } catch (Exception) { } }
            });
        }
    }
}
