using System;
using System.Threading.Tasks;

namespace CIEID.Core
{
    /// <summary>Posizione della firma grafica: coordinate frazionarie rispetto alla pagina (pagina da 0).</summary>
    public class GraphicSignature
    {
        public int Page;
        public float X, Y, W, H;
        public string ImagePath;
    }

    /// <summary>Scelta dell'utente quando la marca temporale non è ottenibile.</summary>
    public class TimestampChoice
    {
        public string Url;        // null = firma senza marca
        public string User;
        public string Password;
    }

    public class SigningService
    {
        private readonly ISettingsStore settings;
        private readonly Logger log;

        public SigningService(ISettingsStore settings, Logger log)
        {
            this.settings = settings;
            this.log = log;
        }

        /// <summary>
        /// Firma il file. Se la marca temporale fallisce chiama askFallback (max 3 volte); restituisce null per annullare.
        /// </summary>
        public Task<CardResult> SignAsync(string input, string output, bool pades, string pin, string pan,
            GraphicSignature graphic, Action<int, string> progress, Func<string, TimestampChoice> askFallback)
        {
            string tsaUrl = null, tsaUser = null, tsaPass = null;
            if (settings.TimestampEnabled)
            {
                if (!string.IsNullOrEmpty(settings.TimestampCustomUrl))
                {
                    tsaUrl = settings.TimestampCustomUrl;
                    CredentialHelper.TryDecrypt(settings.TimestampCredentials, out tsaUser, out tsaPass);
                }
                else tsaUrl = settings.TimestampUrl;
            }

            return Task.Run(delegate
            {
                ProgressCallback pcb = delegate (int p, string m)
                {
                    try { if (progress != null) progress(p, m); } catch (Exception) { }
                    return 0;
                };
                SignCompletedCallback scb = delegate (int r) { return 0; };

                Func<string, int> doSign = delegate (string url)
                {
                    if (pades && graphic != null)
                        return NativeMethods.firmaConCIE(input, "pdf", pin, pan, graphic.Page, graphic.X, graphic.Y, graphic.W, graphic.H,
                            graphic.ImagePath, output, pcb, scb, url, tsaUser, tsaPass);
                    return NativeMethods.firmaConCIE(input, pades ? "pdf" : "p7m", pin, pan, 0, 0f, 0f, 0f, 0f,
                        null, output, pcb, scb, url, tsaUser, tsaPass);
                };

                int ret = doSign(tsaUrl);
                log.Info("firmaConCIE -> 0x" + ret.ToString("X"));
                for (int i = 0; ret == NativeCodes.CARD_TSA_ERROR && i < 3 && askFallback != null; i++)
                {
                    TimestampChoice c = askFallback(tsaUrl);
                    if (c == null) break;
                    tsaUrl = c.Url; tsaUser = c.User; tsaPass = c.Password;
                    ret = doSign(tsaUrl);
                }
                tsaUser = null; tsaPass = null;
                GC.KeepAlive(pcb); GC.KeepAlive(scb);
                return new CardResult { Code = ret };
            });
        }
    }
}
