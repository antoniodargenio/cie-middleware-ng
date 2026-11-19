using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CIEID.Core
{
    public class SignerResult
    {
        public string Name, Surname, CommonName, SigningTime, Issuer;
        public bool SignValid, CertValid;
        public RevocationStatus Revocation;
    }

    public class VerificationResult
    {
        public bool IsPkcs7;
        public int Count;          // numero firmatari
        public string Error;       // null se ok
        public List<SignerResult> Signers = new List<SignerResult>();
    }

    public class VerificationService
    {
        private readonly ISettingsStore settings;
        private readonly Logger log;

        public VerificationService(ISettingsStore settings, Logger log)
        {
            this.settings = settings;
            this.log = log;
        }

        public Task<VerificationResult> VerifyAsync(string path)
        {
            return Task.Run(delegate
            {
                VerificationResult r = new VerificationResult();
                r.IsPkcs7 = path.EndsWith(".p7m", StringComparison.OrdinalIgnoreCase);
                try
                {
                    string cred = null;
                    string u, p;
                    if (CredentialHelper.TryDecrypt(settings.ProxyCredentials, out u, out p))
                        cred = u + ":" + p;
                    long n = NativeMethods.verificaConCIE(path, settings.ProxyUrl, settings.ProxyPort, cred);
                    if ((uint)n == NativeCodes.INVALID_FILE_TYPE) { r.Error = "Il file non è valido: si possono verificare solo file .p7m o .pdf."; return r; }
                    if (n < 0) { r.Error = "Errore durante la verifica del file."; return r; }
                    r.Count = (int)n;
                    for (int i = 0; i < n; i++)
                    {
                        verifyInfo_t v = new verifyInfo_t();
                        NativeMethods.getVerifyInfo(i, ref v);
                        string t = v.signingTime ?? "";
                        if (t.EndsWith("Z")) t = t.Substring(0, t.Length - 1);
                        r.Signers.Add(new SignerResult
                        {
                            Name = v.name, Surname = v.surname, CommonName = v.cn, SigningTime = t, Issuer = v.cadn,
                            SignValid = v.isSignValid, CertValid = v.isCertValid,
                            Revocation = (RevocationStatus)v.CertRevocStatus
                        });
                    }
                }
                catch (Exception ex)
                {
                    log.Error("Verifica: " + ex);
                    r.Error = "Libreria CIEPKI-NG non disponibile o errore imprevisto: " + ex.Message;
                }
                return r;
            });
        }

        public Task<int> ExtractP7mAsync(string input, string output)
        {
            return Task.Run(() => NativeMethods.estraiP7m(input, output));
        }
    }
}
