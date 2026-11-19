using System.Windows;
using System.Windows.Controls;
using CIEID.Core;

namespace CIEID.Wpf.Services
{
    /// <summary>Finestra mostrata quando la marca temporale non è ottenibile.</summary>
    public class TimestampFallbackDialog : Window
    {
        private readonly RadioButton none, digicert, custom;
        private readonly TextBox url, user;
        private readonly PasswordBox pass;
        public TimestampChoice Choice;

        public TimestampFallbackDialog(string failedUrl)
        {
            Title = "Marca temporale non disponibile";
            SizeToContent = SizeToContent.WidthAndHeight;
            MinWidth = 440;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            Owner = Application.Current.MainWindow;

            StackPanel sp = new StackPanel { Margin = new Thickness(20) };
            sp.Children.Add(new TextBlock
            {
                Text = "Il server di marca temporale" + (string.IsNullOrEmpty(failedUrl) ? "" : " (" + failedUrl + ")") + " non ha risposto. Come vuoi procedere?",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            });
            none = new RadioButton { Content = "Firma senza marca temporale", IsChecked = true, Margin = new Thickness(0, 4, 0, 4) };
            digicert = new RadioButton { Content = "Usa il server DigiCert (timestamp.digicert.com)", Margin = new Thickness(0, 4, 0, 4) };
            custom = new RadioButton { Content = "Usa un altro server", Margin = new Thickness(0, 4, 0, 4) };
            sp.Children.Add(none); sp.Children.Add(digicert); sp.Children.Add(custom);
            url = new TextBox { Margin = new Thickness(20, 4, 0, 4), IsEnabled = false };
            user = new TextBox { Margin = new Thickness(20, 4, 0, 4), IsEnabled = false, ToolTip = "Utente (facoltativo)" };
            pass = new PasswordBox { Margin = new Thickness(20, 4, 0, 4), IsEnabled = false, ToolTip = "Password (facoltativa)" };
            sp.Children.Add(url); sp.Children.Add(user); sp.Children.Add(pass);
            custom.Checked += delegate { url.IsEnabled = user.IsEnabled = pass.IsEnabled = true; };
            custom.Unchecked += delegate { url.IsEnabled = user.IsEnabled = pass.IsEnabled = false; };

            StackPanel bar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
            Button ok = new Button { Content = "Continua", IsDefault = true, Style = (Style)Application.Current.FindResource("PrimaryButton"), Margin = new Thickness(0, 0, 8, 0) };
            Button cancel = new Button { Content = "Annulla firma", IsCancel = true };
            ok.Click += delegate
            {
                if (custom.IsChecked == true)
                {
                    string u = url.Text.Trim();
                    System.Uri p;
                    if (!System.Uri.TryCreate(u, System.UriKind.Absolute, out p) || (p.Scheme != System.Uri.UriSchemeHttp && p.Scheme != System.Uri.UriSchemeHttps))
                    {
                        MessageBox.Show("Inserisci un URL che inizi con http:// o https://", "URL non valido", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                        return;
                    }
                    Choice = new TimestampChoice { Url = u, User = user.Text, Password = pass.Password };
                }
                else if (digicert.IsChecked == true) Choice = new TimestampChoice { Url = "http://timestamp.digicert.com" };
                else Choice = new TimestampChoice { Url = null };
                DialogResult = true;
            };
            bar.Children.Add(ok); bar.Children.Add(cancel);
            sp.Children.Add(bar);
            Content = sp;
        }
    }
}
