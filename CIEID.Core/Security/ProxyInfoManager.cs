using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace CIEID.Core
{
    /// <summary>
    /// Cifra e decifra le credenziali del proxy con una chiave derivata dall'identificativo
    /// del processore della macchina: i dati cifrati non sono quindi portabili su altri PC.
    /// </summary>
    public class ProxyInfoManager
    {
        private AesCbc aesCipher;
        private byte[] key;
        private byte[] IV;

        public ProxyInfoManager()
        {
            // Il seed e' il ProcessorId del primo processore (WMI)
            ManagementClass managClass = new ManagementClass("win32_processor");
            ManagementObjectCollection managCollec = managClass.GetInstances();
            string seed = "";

            foreach (ManagementObject managObj in managCollec)
            {
                seed = managObj.Properties["processorID"].Value.ToString();
                break;
            }

            // Chiave AES-256 = SHA-256 del seed; IV fisso
            SHA256 hash = SHA256.Create();

            this.key = hash.ComputeHash(Encoding.UTF8.GetBytes(seed));
            this.IV = Encoding.ASCII.GetBytes(@"9/\~V).A,lY&=t2b");

            aesCipher =  new AesCbc(key, IV);
        }

        // Cifra le credenziali (risultato in Base64)
        public string getEncryptedCredentials(string credentials)
        {
            return aesCipher.EncryptStringToBytes_Aes(credentials);
        }

        // Decifra le credenziali precedentemente cifrate
        public string getDecryptedCredentials(string encryptedCredentials)
        {
            return aesCipher.DecryptStringFromBytes_Aes(encryptedCredentials);
        }
    }
}
