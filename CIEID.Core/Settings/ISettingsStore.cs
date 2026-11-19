namespace CIEID.Core
{
    /// <summary>
    /// Persistenza delle impostazioni utente, astratta dalla UI. Il vecchio WinForms la implementa con
    /// Properties.Settings, la nuova interfaccia con un proprio archivio.
    /// </summary>
    public interface ISettingsStore
    {
        string CieList { get; set; }
        string ProxyUrl { get; set; }
        int ProxyPort { get; set; }
        /// <summary>Credenziali proxy cifrate (vedi CredentialHelper).</summary>
        string ProxyCredentials { get; set; }
        bool TimestampEnabled { get; set; }
        string TimestampUrl { get; set; }
        string TimestampCustomUrl { get; set; }
        string TimestampCredentials { get; set; }
        bool FirstTime { get; set; }
        void Save();
    }
}
