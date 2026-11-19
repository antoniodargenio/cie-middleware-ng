using System;

namespace CIEID.Core
{
    /// <summary>Lettura/scrittura delle credenziali (proxy e TSA) cifrate nel formato "cred=user:pass".</summary>
    public static class CredentialHelper
    {
        public static string Encrypt(string user, string pass)
        {
            return new ProxyInfoManager().getEncryptedCredentials(string.Format("cred={0}:{1}", user, pass));
        }

        /// <summary>Decifra le credenziali salvate; false se vuote, non leggibili o prive di username.</summary>
        public static bool TryDecrypt(string encrypted, out string user, out string pass)
        {
            user = null;
            pass = null;
            if (string.IsNullOrEmpty(encrypted))
                return false;
            try
            {
                string cred = new ProxyInfoManager().getDecryptedCredentials(encrypted);
                if (cred != null && cred.StartsWith("cred=", StringComparison.Ordinal))
                {
                    string[] infos = cred.Substring(5).Split(new char[] { ':' }, 2);
                    if (infos.Length == 2 && infos[0].Length > 0)
                    {
                        user = infos[0];
                        pass = infos[1];
                        return true;
                    }
                }
            }
            catch (Exception)
            {
                // credenziali non decifrabili (es. salvate su un altro PC)
            }
            return false;
        }
    }
}
