using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace CIEID.Core
{
    /// <summary>Elenco dei lettori smart card (PC/SC) installati.</summary>
    public static class ReaderService
    {
        [DllImport("winscard.dll")]
        private static extern int SCardEstablishContext(int scope, IntPtr r1, IntPtr r2, out IntPtr ctx);
        [DllImport("winscard.dll")]
        private static extern int SCardReleaseContext(IntPtr ctx);
        [DllImport("winscard.dll", CharSet = CharSet.Unicode)]
        private static extern int SCardListReadersW(IntPtr ctx, string groups, char[] readers, ref int len);

        public static string[] ListReaders()
        {
            List<string> result = new List<string>();
            IntPtr ctx = IntPtr.Zero;
            try
            {
                if (SCardEstablishContext(2, IntPtr.Zero, IntPtr.Zero, out ctx) != 0)
                    return result.ToArray();
                int len = 0;
                if (SCardListReadersW(ctx, null, null, ref len) != 0 || len <= 1)
                    return result.ToArray();
                char[] buf = new char[len];
                if (SCardListReadersW(ctx, null, buf, ref len) != 0)
                    return result.ToArray();
                foreach (string s in new string(buf, 0, len).Split('\0'))
                    if (s.Length > 0) result.Add(s);
            }
            catch (Exception) { }
            finally
            {
                if (ctx != IntPtr.Zero) SCardReleaseContext(ctx);
            }
            return result.ToArray();
        }
    }
}
