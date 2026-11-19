using System.Windows;
using System.Windows.Controls;
using CIEID.Wpf.ViewModels;

namespace CIEID.Wpf.Views
{
    public partial class CardsView : UserControl
    {
        public CardsView() { InitializeComponent(); }

        private CardsViewModel VM { get { return DataContext as CardsViewModel; } }

        // Doppio clic sul numero di carta: lo seleziona tutto, pronto per essere copiato
        private void Serial_DoubleClick(object s, RoutedEventArgs e) { ((TextBox)s).SelectAll(); }

        private void PinBox_Changed(object s, RoutedEventArgs e) { if (VM != null) VM.Pin = PinBox.Password; }
        private void NewPin_Changed(object s, RoutedEventArgs e) { if (VM != null) VM.NewPin = NewPinBox.Password; }
        private void NewPin2_Changed(object s, RoutedEventArgs e) { if (VM != null) VM.NewPin2 = NewPin2Box.Password; }
        private void Puk_Changed(object s, RoutedEventArgs e) { if (VM != null) VM.Puk = PukBox.Password; }
    }
}
