using System;
using System.Windows;
using CIEID.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CIEID.Wpf.ViewModels
{
    public partial class SettingsViewModel : PageViewModel
    {
        public override string Title { get { return "Impostazioni"; } }
        public override string Glyph { get { return ""; } }
        public override string Help
        {
            get
            {
                return "Marca temporale: certifica la data e l'ora della firma. Il server predefinito è quello di Actalis; se usi un server tuo, inserisci l'URL e, se serve, le credenziali.\n\n" +
                       "Proxy: da compilare solo se la tua rete richiede un proxy per uscire su Internet.\n\n" +
                       "Livelli di log: lascia «Disattivato» salvo diagnosi di un problema.";
            }
        }

        public LogLevel[] Levels { get { return (LogLevel[])Enum.GetValues(typeof(LogLevel)); } }

        [ObservableProperty] private bool timestampEnabled;
        [ObservableProperty] private string timestampUrl;
        [ObservableProperty] private string timestampCustomUrl;
        [ObservableProperty] private string tsaUser;
        [ObservableProperty] private string proxyUrl;
        [ObservableProperty] private int proxyPort;
        [ObservableProperty] private string proxyUser;
        [ObservableProperty] private LogLevel appLevel;
        [ObservableProperty] private LogLevel libLevel;
        [ObservableProperty] private string status;

        public string TsaPassword = "", ProxyPassword = "";

        public SettingsViewModel()
        {
            ISettingsStore s = App.Settings;
            TimestampEnabled = s.TimestampEnabled;
            TimestampUrl = s.TimestampUrl;
            TimestampCustomUrl = s.TimestampCustomUrl;
            ProxyUrl = s.ProxyUrl;
            ProxyPort = s.ProxyPort;
            string u, p;
            if (CredentialHelper.TryDecrypt(s.TimestampCredentials, out u, out p)) { TsaUser = u; TsaPassword = p; }
            if (CredentialHelper.TryDecrypt(s.ProxyCredentials, out u, out p)) { ProxyUser = u; ProxyPassword = p; }
            LogLevel a, l;
            LogConfigService.Read(out a, out l);
            AppLevel = a; LibLevel = l;
        }

        private static bool ValidUrl(string u)
        {
            Uri parsed;
            return u.Length < 256 && Uri.TryCreate(u, UriKind.Absolute, out parsed) &&
                   (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps);
        }

        [RelayCommand]
        private void Save()
        {
            string custom = (TimestampCustomUrl ?? "").Trim();
            if (custom.Length > 0 && !ValidUrl(custom))
            {
                Status = "L'URL del server di marca temporale deve iniziare con http:// o https:// (massimo 255 caratteri).";
                return;
            }
            ISettingsStore s = App.Settings;
            s.TimestampEnabled = TimestampEnabled;
            s.TimestampCustomUrl = custom;
            s.TimestampCredentials = (custom.Length > 0 && !string.IsNullOrEmpty(TsaUser)) ? CredentialHelper.Encrypt(TsaUser, TsaPassword ?? "") : "";
            s.ProxyUrl = (ProxyUrl ?? "").Trim();
            s.ProxyPort = ProxyPort;
            s.ProxyCredentials = (s.ProxyUrl.Length > 0 && !string.IsNullOrEmpty(ProxyUser)) ? CredentialHelper.Encrypt(ProxyUser, ProxyPassword ?? "") : "";
            s.Save();
            App.Log.Level = AppLevel;
            Status = LogConfigService.Write(AppLevel, LibLevel)
                ? "Impostazioni salvate."
                : "Impostazioni salvate, ma non è stato possibile scrivere la configurazione dei log (servono permessi di amministratore).";
        }
    }
}
