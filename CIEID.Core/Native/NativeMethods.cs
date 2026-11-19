using System;
using System.Runtime.InteropServices;
using System.Text;

namespace CIEID.Core
{
    // Callback invocate dalla libreria nativa (da thread nativi): avanzamento, completamento abbinamento e firma
    public delegate long ProgressCallback(int progress, string message);
    public delegate long CompletedCallback(string pan, string name, string ef_seriale);
    public delegate long SignCompletedCallback(int retValue);

    // Stato di revoca del certificato (valori restituiti dalla libreria nativa)
    public enum RevocationStatus
    {
        Good = 0,
        Revoked = 1,
        Suspended = 2,
        Unknown = 3
    }

    /// <summary>Esito della verifica di un singolo firmatario; corrisponde alla struct nativa di CIEPKI-NG.dll.</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct verifyInfo_t
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)]
        public string name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)]
        public string surname;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)]
        public string cn;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)]
        public string signingTime;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)]
        public string cadn;
        public int CertRevocStatus;
        [MarshalAs(UnmanagedType.U1)]
        public bool isSignValid;
        [MarshalAs(UnmanagedType.U1)]
        public bool isCertValid;
    }

    /// <summary>
    /// Funzioni esportate da CIEPKI-NG.dll (installata in System32).
    /// ATTENZIONE: il nome della DLL deve coincidere con quello del file installato.
    /// </summary>
    public static class NativeMethods
    {
        [DllImport("CIEPKI-NG.dll")]
        public static extern int VerificaCIEAbilitata(string pan);

        [DllImport("CIEPKI-NG.dll")]
        public static extern int DisabilitaCIE(string pan);

        [DllImport("CIEPKI-NG.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern int isCIEEnrolled(StringBuilder pan);

        [DllImport("CIEPKI-NG.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern int AbbinaCIE(string szPAN, string szPIN, int[] attempts, ProgressCallback progressCallBack, CompletedCallback completedCallBack);

        [DllImport("CIEPKI-NG.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern int ChangePIN(string szPIN, string szNewPIN, int[] attempts, ProgressCallback progressCallBack);

        [DllImport("CIEPKI-NG.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern int UnlockPIN(string szPUK, string szNewPIN, int[] attempts, ProgressCallback progressCallBack);

        [DllImport("CIEPKI-NG.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern int firmaConCIE(string inFilePath, string type, string pin, string pan, int page, float x, float y, float w, float h, string imagePathFile, string outFilePath, ProgressCallback progressCallBack, SignCompletedCallback signCompletedCallBack, string tsaUrl, string tsaUsername, string tsaPassword);

        [DllImport("CIEPKI-NG.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern int estraiP7m(string inFilePath, string outFilePath);

        // Verifica del file (restituisce il numero di firme) e lettura dell'esito del firmatario di indice 'index'
        [DllImport("CIEPKI-NG.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern long verificaConCIE(string filePath, string proxyAddress, int proxyPort, string usrPass);

        [DllImport("CIEPKI-NG.dll", CallingConvention = CallingConvention.StdCall)]
        public static extern long getVerifyInfo(int index, ref verifyInfo_t vInfo);
    }
}
