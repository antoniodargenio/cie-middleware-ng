using System.Windows;
using System.Windows.Controls;
using CIEID.Wpf.ViewModels;

namespace CIEID.Wpf.Views
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
            Loaded += delegate
            {
                SettingsViewModel vm = DataContext as SettingsViewModel;
                if (vm != null) { TsaPass.Password = vm.TsaPassword; ProxyPass.Password = vm.ProxyPassword; }
            };
        }

        private void TsaPass_Changed(object s, RoutedEventArgs e)
        {
            SettingsViewModel vm = DataContext as SettingsViewModel;
            if (vm != null) vm.TsaPassword = TsaPass.Password;
        }

        private void ProxyPass_Changed(object s, RoutedEventArgs e)
        {
            SettingsViewModel vm = DataContext as SettingsViewModel;
            if (vm != null) vm.ProxyPassword = ProxyPass.Password;
        }
    }
}
