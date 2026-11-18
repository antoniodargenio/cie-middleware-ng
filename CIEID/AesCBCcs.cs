using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace CIEID
{
    /// <summary>
    /// Cifratura/decifratura simmetrica AES-256 in modalita' CBC con padding PKCS7.
    /// Usata per proteggere localmente le credenziali del proxy (vedi ProxyInfoManager).
    /// Input/output cifrati sono stringhe Base64.
    /// </summary>
    class AesCBCcs
    {
        // Chiave simmetrica (32 byte)
        private byte[] key;
        // Vettore di inizializzazione (16 byte)
        private byte[] IV;


        public AesCBCcs(byte[] key, byte[] IV)
        {
            this.key = key;
            this.IV = IV;
        }

        /// <summary>Decifra una stringa Base64 e restituisce il testo in chiaro.</summary>
        public string DecryptStringFromBytes_Aes(string Text)
        {
            // Controllo dei parametri obbligatori
            if (Text == null || Text.Length <= 0)
                throw new ArgumentNullException("cipherText");
            if (this.key == null || this.key.Length <= 0)
                throw new ArgumentNullException("Key");
            if (this.IV == null || this.IV.Length <= 0)
                throw new ArgumentNullException("IV");

            string plaintext = null;
            // Alcuni trasporti sostituiscono '+' con spazio: lo ripristiniamo prima della decodifica Base64
            byte[] cipherText = Convert.FromBase64String(Text.Replace(' ', '+'));

            using (AesCryptoServiceProvider aesAlg = new AesCryptoServiceProvider())
            {
                aesAlg.Key = this.key;
                aesAlg.IV = IV;
                aesAlg.Mode = CipherMode.CBC;
                aesAlg.Padding = PaddingMode.PKCS7;
                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream msDecrypt = new MemoryStream(cipherText))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                        {
                            plaintext = srDecrypt.ReadToEnd();
                        }
                    }
                }
            }

            return plaintext;
        }

        /// <summary>Cifra il testo in chiaro e restituisce il risultato codificato in Base64.</summary>
        public string EncryptStringToBytes_Aes(string plainText)
        {
            if (plainText == null || plainText.Length <= 0)
                throw new ArgumentNullException("plainText");
            if (this.key == null || this.key.Length <= 0)
                throw new ArgumentNullException("Key");
            if (this.IV == null || this.IV.Length <= 0)
                throw new ArgumentNullException("IV");

            byte[] encrypted;


            using (AesCryptoServiceProvider aesAlg = new AesCryptoServiceProvider())
            {
                aesAlg.Key = this.key;
                aesAlg.IV = this.IV;
                aesAlg.Mode = CipherMode.CBC;
                aesAlg.Padding = PaddingMode.PKCS7;

                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            swEncrypt.Write(plainText);
                        }
                        encrypted = msEncrypt.ToArray();
                    }
                }
            }

            return Convert.ToBase64String(encrypted);
        }

    }
}
