using System;
using System.Threading;
using System.Windows;
using CIEID.Core;

namespace CIEID.Wpf
{
    public partial class App : Application
    {
        private Mutex instanceMutex;

        public static ISettingsStore Settings { get; private set; }
        public static Logger Log { get; private set; }
        public static CardService Cards { get; private set; }
        public static SigningService Signing { get; private set; }
        public static VerificationService Verification { get; private set; }

        public static string VersionText
        {
            get
            {
                Version v = typeof(App).Assembly.GetName().Version;
                return v == null ? "" : v.ToString();
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            bool created;
            instanceMutex = new Mutex(true, "CIE-Middleware-NG-Wpf-Instance", out created);
            if (!created)
            {
                MessageBox.Show("CIE Middleware NG è già in esecuzione.", "CIE Middleware NG", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            LogLevel app, lib;
            LogConfigService.Read(out app, out lib);
            Logger.DefaultLogLevel = app;
            Log = new Logger(app);

            DispatcherUnhandledException += delegate (object s, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs a)
            {
                Log.Error("Eccezione non gestita: " + a.Exception);
                MessageBox.Show("Si è verificato un errore imprevisto:\n" + a.Exception.Message, "CIE Middleware NG", MessageBoxButton.OK, MessageBoxImage.Error);
                a.Handled = true;
            };

            Settings = new JsonSettingsStore();
            Cards = new CardService(Settings, Log);
            Signing = new SigningService(Settings, Log);
            Verification = new VerificationService(Settings, Log);

            base.OnStartup(e);
            new MainWindow().Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (instanceMutex != null) { try { instanceMutex.ReleaseMutex(); } catch (Exception) { } instanceMutex.Dispose(); }
            base.OnExit(e);
        }

        /// <summary>Esegue l'azione nel thread UI (le callback native arrivano da altri thread).</summary>
        public static void UI(Action a)
        {
            Application.Current.Dispatcher.BeginInvoke(a);
        }
    }
}
