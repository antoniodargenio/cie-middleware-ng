using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using CIEID.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace CIEID.Wpf.ViewModels
{
    public class SignerRow
    {
        public string Who { get; set; }
        public string When { get; set; }
        public string Signature { get; set; }
        public string Certificate { get; set; }
        public string Revocation { get; set; }
        public string Issuer { get; set; }
        public bool AllGood { get; set; }
    }

    public partial class VerifyViewModel : PageViewModel
    {
        public override string Title { get { return "Verifica"; } }
        public override string Glyph { get { return ""; } }
        public override string Help
        {
            get
            {
                return "Scegli un file .pdf o .p7m: l'app controlla ogni firma, il certificato e il suo stato di revoca.\n\n" +
                       "La verifica della revoca richiede una connessione a Internet (eventualmente tramite il proxy impostato in Impostazioni).\n\n" +
                       "Per i file .p7m puoi anche estrarre il documento originale.";
            }
        }

        public ObservableCollection<SignerRow> Signers { get; private set; }

        [ObservableProperty] private string filePath;
        [ObservableProperty] private string summary;
        [ObservableProperty] private bool busy;
        [ObservableProperty] private bool canExtract;

        public VerifyViewModel() { Signers = new ObservableCollection<SignerRow>(); }

        private static string FormatTime(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "Firma senza marca temporale";
            DateTime t;
            if (DateTime.TryParseExact(raw, "yyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out t))
                return t.ToLocalTime().ToString("f", new CultureInfo("it-IT"));
            return raw;
        }

        [RelayCommand]
        private void Browse()
        {
            OpenFileDialog d = new OpenFileDialog { Filter = "Documenti firmati (*.pdf;*.p7m)|*.pdf;*.p7m" };
            if (d.ShowDialog() == true) { FilePath = d.FileName; Verify(); }
        }

        [RelayCommand]
        private async Task Verify()
        {
            if (string.IsNullOrEmpty(FilePath) || !File.Exists(FilePath)) { Summary = "Scegli prima un file."; return; }
            Busy = true; Signers.Clear(); Summary = "Verifica in corso…"; CanExtract = false;
            try
            {
                VerificationResult r = await App.Verification.VerifyAsync(FilePath);
                if (r.Error != null) { Summary = r.Error; return; }
                if (r.Count == 0) { Summary = "Il file non contiene firme."; return; }
                Summary = r.Count == 1 ? "1 firmatario" : r.Count + " firmatari";
                CanExtract = r.IsPkcs7;
                foreach (SignerResult s in r.Signers)
                {
                    string rev = s.Revocation == RevocationStatus.Good ? "Certificato non revocato"
                        : s.Revocation == RevocationStatus.Revoked ? "Certificato revocato"
                        : s.Revocation == RevocationStatus.Suspended ? "Certificato sospeso" : "Stato di revoca sconosciuto";
                    Signers.Add(new SignerRow
                    {
                        Who = (s.Name + " " + s.Surname).Trim() + (string.IsNullOrEmpty(s.CommonName) ? "" : "  ·  " + s.CommonName),
                        When = FormatTime(s.SigningTime),
                        Signature = s.SignValid ? "Firma valida" : "Firma non valida",
                        Certificate = s.CertValid ? "Certificato valido" : "Certificato non valido",
                        Revocation = rev,
                        Issuer = s.Issuer,
                        AllGood = s.SignValid && s.CertValid && s.Revocation == RevocationStatus.Good
                    });
                }
            }
            finally { Busy = false; }
        }

        [RelayCommand]
        private async Task Extract()
        {
            SaveFileDialog d = new SaveFileDialog { FileName = Path.GetFileNameWithoutExtension(FilePath) };
            if (d.ShowDialog() != true) return;
            int rc = await App.Verification.ExtractP7mAsync(FilePath, d.FileName);
            MessageBox.Show(rc == 0 ? "Documento estratto." : "Estrazione non riuscita (codice " + rc + ").", "CIE Middleware NG",
                MessageBoxButton.OK, rc == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
    }
}
