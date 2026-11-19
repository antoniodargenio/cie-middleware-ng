using System.Diagnostics;
using System.IO;
using System.Windows;
using CIEID.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace CIEID.Wpf.ViewModels
{
    public partial class LogViewModel : PageViewModel
    {
        public override string Title { get { return "Log"; } }
        public override string Glyph { get { return ""; } }
        public override string Help
        {
            get
            {
                return "I file di log servono a diagnosticare problemi. Per attivarli scegli un livello in Impostazioni, riproduci il problema ed esporta il pacchetto.\n\n" +
                       "I log possono contenere dettagli tecnici sulla tua sessione: controllali prima di condividerli.";
            }
        }

        [ObservableProperty] private string status;
        public string Directory { get { return LogFileService.LogDirectory; } }

        [RelayCommand]
        private void Open()
        {
            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + Directory + "\"") { UseShellExecute = true });
            }
            catch (System.Exception ex) { Status = ex.Message; }
        }

        [RelayCommand]
        private void Export()
        {
            SaveFileDialog d = new SaveFileDialog { Filter = "Archivio zip (*.zip)|*.zip", FileName = "cie-middleware-ng-log.zip" };
            if (d.ShowDialog() != true) return;
            try { Status = LogFileService.CollectToZip(d.FileName) ? "Log esportati." : "Non ci sono file di log da esportare."; }
            catch (System.Exception ex) { Status = "Esportazione non riuscita: " + ex.Message; }
        }

        [RelayCommand]
        private void Clear()
        {
            if (MessageBox.Show("Eliminare tutti i file di log?", "Elimina log", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
            try { Status = LogFileService.ClearAll() ? "Log eliminati." : "Alcuni file sono in uso e non sono stati eliminati."; }
            catch (System.Exception ex) { Status = ex.Message; }
        }
    }
}
