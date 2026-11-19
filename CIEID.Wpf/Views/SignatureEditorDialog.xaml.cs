using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CIEID.Wpf.Services;

namespace CIEID.Wpf.Views
{
    /// <summary>Finestra per creare la firma grafica: testo (carattere e maiuscole a scelta) oppure disegno a mano.</summary>
    public partial class SignatureEditorDialog : Window
    {
        /// <summary>Immagine scelta dall'utente (valorizzata alla conferma).</summary>
        public BitmapSource Result { get; private set; }

        public SignatureEditorDialog(string defaultText)
        {
            InitializeComponent();
            Ink.DefaultDrawingAttributes.Color = Colors.Black;
            Ink.DefaultDrawingAttributes.Width = 2.5;
            Ink.DefaultDrawingAttributes.Height = 2.5;
            Ink.DefaultDrawingAttributes.FitToCurve = true;

            FontBox.ItemsSource = SignatureImageRenderer.AvailableFonts();
            FontBox.SelectedIndex = 0;
            TextBox.Text = defaultText ?? "";
            UpdatePreview();
        }

        private void OnTextOptionChanged(object sender, RoutedEventArgs e)
        {
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            if (TextPreview == null || FontBox == null || CaseBox == null || TextBox == null) return;
            string font = FontBox.SelectedItem as string ?? SignatureImageRenderer.DefaultFont;
            TextPreview.Source = SignatureImageRenderer.ComposeText(TextBox.Text, font, (CaseMode)Math.Max(0, CaseBox.SelectedIndex));
        }

        private void OnClearInk(object sender, RoutedEventArgs e)
        {
            Ink.Strokes.Clear();
        }

        private void OnOk(object sender, RoutedEventArgs e)
        {
            if (Tabs.SelectedIndex == 0)
            {
                if (string.IsNullOrWhiteSpace(TextBox.Text))
                {
                    MessageBox.Show(this, "Scrivi il testo della firma.", "Firma grafica", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return;
                }
                Result = SignatureImageRenderer.ComposeText(TextBox.Text, FontBox.SelectedItem as string, (CaseMode)Math.Max(0, CaseBox.SelectedIndex));
            }
            else
            {
                Result = SignatureImageRenderer.ComposeInk(Ink.Strokes);
                if (Result == null)
                {
                    MessageBox.Show(this, "Disegna prima la firma nel riquadro.", "Firma grafica", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                    return;
                }
            }
            DialogResult = true;
        }
    }
}
