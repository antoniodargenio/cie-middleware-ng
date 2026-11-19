using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using CIEID.Core;
using CIEID.Wpf.Services;
using CIEID.Wpf.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace CIEID.Wpf.ViewModels
{
    public partial class SignViewModel : PageViewModel
    {
        public override string Title { get { return "Firma"; } }
        public override string Glyph { get { return ""; } }
        public override string Help
        {
            get
            {
                return "1. Scegli il file da firmare.\n2. Seleziona la carta abbinata.\n3. Scegli il formato: PAdES per i PDF (firma visibile nel documento), CAdES (.p7m) per qualsiasi file.\n" +
                       "4. Per la firma grafica trascina il riquadro nel punto desiderato e ridimensionalo dall'angolo; nel riquadro «La tua firma grafica» puoi creare una firma (testo o disegno a mano), caricare un'immagine oppure ripristinare quella predefinita.\n5. Inserisci le ultime 4 cifre del PIN e appoggia la carta sul lettore.\n\n" +
                       "Il file firmato viene salvato con un nuovo nome: l'originale non viene modificato.";
            }
        }

        public ObservableCollection<CieModel> Cards { get; private set; }

        [ObservableProperty] private string filePath;
        [ObservableProperty] private CieModel selectedCard;
        [ObservableProperty] private bool pades = true;
        [ObservableProperty] private bool isPdf;
        [ObservableProperty] private bool graphic;
        [ObservableProperty] private int pageCount;
        [ObservableProperty] private int page = 1;
        [ObservableProperty] private BitmapSource pageImage;
        [ObservableProperty] private BitmapSource signatureImage;
        [ObservableProperty] private bool busy;
        [ObservableProperty] private int progress;
        [ObservableProperty] private string message;

        // posizione del riquadro, frazioni dall'angolo in alto a sinistra (aggiornate dalla vista)
        public double BoxX = 0.55, BoxY = 0.85, BoxW = 0.35, BoxH = 0.1;
        public string Pin = "";

        public SignViewModel()
        {
            Cards = new ObservableCollection<CieModel>();
            ReloadCards();
        }

        public void ReloadCards()
        {
            App.Cards.Reload();
            Cards.Clear();
            foreach (CieModel c in App.Cards.Cards) Cards.Add(c);
            if (SelectedCard == null && Cards.Count > 0) SelectedCard = Cards[0];
        }

        partial void OnSelectedCardChanged(CieModel value) { RefreshSignature(); }

        private void RefreshSignature()
        {
            if (SelectedCard == null) { SignatureImage = null; return; }
            try { SignatureImage = SignatureImageRenderer.Load(SignatureImageRenderer.Ensure(SelectedCard.Owner, SelectedCard.SerialNumber)); }
            catch (Exception ex) { App.Log.Error("Immagine firma: " + ex); SignatureImage = null; }
        }

        [RelayCommand]
        private void ImportSignature()
        {
            if (SelectedCard == null) return;
            OpenFileDialog d = new OpenFileDialog { Filter = "Immagini (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp" };
            if (d.ShowDialog() != true) return;
            try { SignatureImageRenderer.Import(d.FileName, SelectedCard.SerialNumber); RefreshSignature(); }
            catch (Exception ex) { MessageBox.Show("Immagine non valida: " + ex.Message, "Firma grafica", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        [RelayCommand]
        private void CreateSignature()
        {
            if (SelectedCard == null) return;
            SignatureEditorDialog dlg = new SignatureEditorDialog(SelectedCard.Owner) { Owner = Application.Current.MainWindow };
            if (dlg.ShowDialog() != true || dlg.Result == null) return;
            try { SignatureImageRenderer.Save(dlg.Result, SignatureImageRenderer.PathFor(SelectedCard.SerialNumber)); RefreshSignature(); }
            catch (Exception ex) { MessageBox.Show("Impossibile salvare la firma: " + ex.Message, "Firma grafica", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        [RelayCommand]
        private void ResetSignature()
        {
            if (SelectedCard == null) return;
            SignatureImageRenderer.Reset(SelectedCard.Owner, SelectedCard.SerialNumber);
            RefreshSignature();
        }

        partial void OnPageChanged(int value) { LoadPreview(); }
        partial void OnGraphicChanged(bool value) { LoadPreview(); }
        partial void OnPadesChanged(bool value) { if (!value) Graphic = false; }

        private async void LoadPreview()
        {
            if (!Graphic || !IsPdf || string.IsNullOrEmpty(FilePath)) { PageImage = null; return; }
            try
            {
                if (PageCount == 0) PageCount = await PdfRenderer.CountPagesAsync(FilePath);
                if (PageCount == 0) { Message = "Impossibile leggere le pagine del PDF."; return; }
                if (Page < 1) Page = 1;
                if (Page > PageCount) Page = PageCount;
                PageImage = await PdfRenderer.RenderPageAsync(FilePath, Page);
            }
            catch (Exception ex)
            {
                App.Log.Error("Anteprima PDF: " + ex);
                Message = "Anteprima non disponibile: " + ex.Message;
            }
        }

        public void SetFile(string path)
        {
            FilePath = path;
            IsPdf = path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
            Pades = IsPdf;
            Graphic = false;
            PageCount = 0; Page = 1; PageImage = null;
            Message = "";
        }

        [RelayCommand]
        private void Browse()
        {
            OpenFileDialog d = new OpenFileDialog { Filter = "Tutti i file (*.*)|*.*|PDF (*.pdf)|*.pdf" };
            if (d.ShowDialog() == true) SetFile(d.FileName);
        }

        [RelayCommand]
        private async Task Sign()
        {
            if (string.IsNullOrEmpty(FilePath) || !File.Exists(FilePath)) { MessageBox.Show("Scegli prima il file da firmare.", "Firma", MessageBoxButton.OK, MessageBoxImage.Exclamation); return; }
            if (SelectedCard == null) { MessageBox.Show("Nessuna carta abbinata: vai alla pagina Carte.", "Firma", MessageBoxButton.OK, MessageBoxImage.Exclamation); return; }
            if (Pin == null || !Regex.IsMatch(Pin, "^[0-9]{4}$|^[0-9]{8}$")) { MessageBox.Show("Inserisci le ultime 4 cifre del PIN.", "PIN non valido", MessageBoxButton.OK, MessageBoxImage.Exclamation); return; }

            string ext = Pades ? "-signed.pdf" : ".p7m";
            string suggested = Pades ? Path.GetFileNameWithoutExtension(FilePath) + ext : Path.GetFileName(FilePath) + ext;
            SaveFileDialog sd = new SaveFileDialog
            {
                FileName = suggested,
                InitialDirectory = Path.GetDirectoryName(FilePath),
                Filter = Pades ? "PDF firmato (*.pdf)|*.pdf" : "File firmato CAdES (*.p7m)|*.p7m"
            };
            if (sd.ShowDialog() != true) return;

            GraphicSignature g = null;
            if (Pades && Graphic && PageImage != null)
            {
                string img = SignatureImageRenderer.Ensure(SelectedCard.Owner, SelectedCard.SerialNumber);
                g = new GraphicSignature
                {
                    Page = Page - 1,
                    X = (float)BoxX,
                    // la DLL nativa vuole la frazione dall'alto del bordo INFERIORE del riquadro
                    Y = (float)(BoxY + BoxH),
                    W = (float)BoxW,
                    H = (float)BoxH,
                    ImagePath = img
                };
            }

            Busy = true; Progress = 0; Message = "Appoggia la carta sul lettore…";
            string pin = Pin;
            try
            {
                CardResult r = await App.Signing.SignAsync(FilePath, sd.FileName, Pades, pin, SelectedCard.Pan, g,
                    delegate (int p, string m) { App.UI(delegate { Progress = p; Message = m; }); },
                    delegate (string failed)
                    {
                        TimestampChoice c = null;
                        Application.Current.Dispatcher.Invoke(delegate
                        {
                            TimestampFallbackDialog dlg = new TimestampFallbackDialog(failed);
                            if (dlg.ShowDialog() == true) c = dlg.Choice;
                        });
                        return c;
                    });
                if (r.Ok)
                {
                    Message = "Documento firmato: " + sd.FileName;
                    MessageBox.Show("Documento firmato correttamente.", "Firma", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    Message = r.Describe();
                    MessageBox.Show(r.Describe(), "Firma non riuscita", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                App.Log.Error("Firma: " + ex);
                MessageBox.Show("Libreria CIEPKI-NG non disponibile o errore imprevisto:\n" + ex.Message, "Firma", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { Busy = false; }
        }
    }
}
