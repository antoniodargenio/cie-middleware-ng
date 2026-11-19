using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using CIEID.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CIEID.Wpf.ViewModels
{
    public partial class CardsViewModel : PageViewModel
    {
        public override string Title { get { return "Carte"; } }
        public override string Glyph { get { return ""; } }
        public override string Help
        {
            get
            {
                return "Per usare la firma devi prima abbinare la tua CIE: inserisci le 8 cifre del PIN e appoggia la carta sul lettore. " +
                       "L'abbinamento si fa una volta sola per carta.\n\n" +
                       "Il PIN a 8 cifre serve solo per abbinare e per cambiare PIN; per firmare bastano le ultime 4 cifre.\n\n" +
                       "Se il PIN è bloccato dopo tre errori, usa il PUK (si trova nella lettera ricevuta con la carta) per sbloccarlo.";
            }
        }

        public ObservableCollection<CieModel> Cards { get; private set; }

        [ObservableProperty] private string readerInfo;
        [ObservableProperty] private bool busy;
        [ObservableProperty] private int progress;
        [ObservableProperty] private string message;

        // PIN/PUK impostati dalla vista (PasswordBox non è collegabile)
        public string Pin = "", NewPin = "", NewPin2 = "", Puk = "";

        public CardsViewModel()
        {
            Cards = new ObservableCollection<CieModel>();
            Refresh();
        }

        public void Refresh()
        {
            App.Cards.Reload();
            Cards.Clear();
            foreach (CieModel c in App.Cards.Cards) Cards.Add(c);
            string[] readers = ReaderService.ListReaders();
            ReaderInfo = readers.Length == 0
                ? "Nessun lettore di smart card rilevato. Collega il lettore e premi Aggiorna."
                : (readers.Length == 1 ? "Lettore: " : readers.Length + " lettori: ") + string.Join(", ", readers);
        }

        private static bool IsDigits(string s, int len)
        {
            return s != null && s.Length == len && Regex.IsMatch(s, "^[0-9]+$");
        }

        private void OnProgress(int p, string m)
        {
            App.UI(delegate { Progress = p; Message = m; });
        }

        private async Task Run(System.Func<Task<CardResult>> op, string okText)
        {
            Busy = true; Progress = 0; Message = "";
            try
            {
                CardResult r = await op();
                Message = r.Ok ? okText : r.Describe();
                if (!r.Ok) MessageBox.Show(r.Describe(), "CIE Middleware NG", MessageBoxButton.OK, MessageBoxImage.Warning);
                else MessageBox.Show(okText, "CIE Middleware NG", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                App.Log.Error("Operazione carta: " + ex);
                MessageBox.Show("Libreria CIEPKI-NG non disponibile o errore imprevisto:\n" + ex.Message, "CIE Middleware NG", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { Busy = false; Refresh(); }
        }

        [RelayCommand]
        private async Task Pair()
        {
            if (!IsDigits(Pin, 8)) { MessageBox.Show("Il PIN è composto da 8 cifre.", "PIN non valido", MessageBoxButton.OK, MessageBoxImage.Exclamation); return; }
            string pin = Pin;
            await Run(() => App.Cards.PairAsync(pin, OnProgress), "Carta abbinata correttamente.");
        }

        [RelayCommand]
        private async Task Remove(CieModel card)
        {
            if (card == null) return;
            if (MessageBox.Show("Rimuovere l'abbinamento della carta di " + card.Owner + "?\nAppoggia la carta sul lettore.", "Rimuovi carta",
                MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
            string pan = card.Pan;
            await Run(() => App.Cards.RemoveAsync(pan), "Abbinamento rimosso.");
        }

        [RelayCommand]
        private async Task ChangePin()
        {
            if (!IsDigits(Pin, 8) || !IsDigits(NewPin, 8)) { MessageBox.Show("Il PIN attuale e quello nuovo sono di 8 cifre.", "PIN non valido", MessageBoxButton.OK, MessageBoxImage.Exclamation); return; }
            if (NewPin != NewPin2) { MessageBox.Show("I due PIN nuovi non coincidono.", "PIN non valido", MessageBoxButton.OK, MessageBoxImage.Exclamation); return; }
            string a = Pin, b = NewPin;
            await Run(() => App.Cards.ChangePinAsync(a, b, OnProgress), "PIN cambiato.");
        }

        [RelayCommand]
        private async Task UnlockPin()
        {
            if (!IsDigits(Puk, 8) || !IsDigits(NewPin, 8)) { MessageBox.Show("PUK e nuovo PIN sono di 8 cifre.", "Dati non validi", MessageBoxButton.OK, MessageBoxImage.Exclamation); return; }
            if (NewPin != NewPin2) { MessageBox.Show("I due PIN nuovi non coincidono.", "PIN non valido", MessageBoxButton.OK, MessageBoxImage.Exclamation); return; }
            string a = Puk, b = NewPin;
            await Run(() => App.Cards.UnlockPinAsync(a, b, OnProgress), "Carta sbloccata con il nuovo PIN.");
        }

        [RelayCommand]
        private void RefreshReaders() { Refresh(); }
    }
}
