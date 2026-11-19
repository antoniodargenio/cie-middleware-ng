using System.Windows;
using CIEID.Wpf.ViewModels;

namespace CIEID.Wpf
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
            Title = "CIE Middleware NG " + App.VersionText + " – build non ufficiale";
            VersionLabel.Text = "Versione " + App.VersionText;
            Nav.SelectedIndex = 0;
        }

        private void HelpToggle_Click(object sender, RoutedEventArgs e)
        {
            HelpPanel.Visibility = HelpPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}
