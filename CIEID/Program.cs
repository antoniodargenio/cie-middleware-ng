using System;
using System.IO;
using System.Windows.Forms;
using System.Threading;
using System.Runtime.InteropServices;

namespace CIEID
{
    /// <summary>Punto di ingresso dell'applicazione: istanza unica, configurazione dei log e avvio della prima finestra.</summary>
    static class Program
    {
        // Mutex che garantisce una sola istanza dell'applicazione
        private static Mutex mutex = null;
        // Chiavi del file di configurazione %PROGRAMDATA%/CIEPKI-NG/config (valori: 0=disattivato, 1=debug, 2=info, 3=errore)
        private static string LogConfigPrefixLib ="LIB_LOG_LEVEL";
        private static string LogConfigPrefixApp ="APP_LOG_LEVEL";
        // Valori di default per il logger
        public static LogLevel LogLevelApp = Logger.DefaultLogLevel;
        public static LogLevel LogLevelLib = Logger.DefaultLogLevel;


        [DllImport("user32.dll", SetLastError = true)]
        // Dichiara l'applicazione sensibile al DPI per evitare sfocature sugli schermi ad alta risoluzione
        static extern bool SetProcessDPIAware();

        // Settings.Upgrade() ritrova solo le impostazioni della stessa cartella (stesso percorso dell'exe).
        // Se l'elenco delle CIE abbinate è vuoto, lo recupera dall'installazione precedente più recente
        // (altro percorso, es. Program Files (x86)) cercando i user.config in %LOCALAPPDATA%\CIE-Middleware-NG.
        private static void MigraElencoCie()
        {
            try
            {
                if (!string.IsNullOrEmpty(Properties.Settings.Default.cieList) && Properties.Settings.Default.cieList != "0")
                    return;

                string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CIE-Middleware-NG");
                if (!Directory.Exists(root))
                    return;

                string current = System.Configuration.ConfigurationManager
                    .OpenExeConfiguration(System.Configuration.ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath;
                string best = null;
                DateTime bestTime = DateTime.MinValue;
                foreach (string file in Directory.GetFiles(root, "user.config", SearchOption.AllDirectories))
                {
                    if (string.Equals(file, current, StringComparison.OrdinalIgnoreCase))
                        continue;
                    string value = LeggiElencoCie(file);
                    DateTime time = File.GetLastWriteTimeUtc(file);
                    if (value != null && time > bestTime)
                    {
                        best = value;
                        bestTime = time;
                    }
                }

                if (best != null)
                {
                    Properties.Settings.Default.cieList = best;
                    Logger.Debug("Elenco CIE migrato da un'installazione precedente: " + best);
                }
            }
            catch (Exception ex)
            {
                Logger.Debug("Migrazione elenco CIE non riuscita: " + ex.Message);
            }
        }

        // Legge l'impostazione 'cieList' da un file user.config; restituisce null se assente o vuota
        private static string LeggiElencoCie(string file)
        {
            try
            {
                var doc = System.Xml.Linq.XDocument.Load(file);
                foreach (var setting in doc.Descendants("setting"))
                {
                    if ((string)setting.Attribute("name") == "cieList")
                    {
                        string v = (string)setting.Element("value");
                        return string.IsNullOrWhiteSpace(v) || v == "0" ? null : v;
                    }
                }
            }
            catch { }
            return null;
        }
        // Logger globale dell'applicazione
        static public Logger Logger;

        /// <summary>
        /// Punto di ingresso principale dell'applicazione.
        /// </summary>
        [STAThread]
        // Inizializza il logger, applica i livelli di log del file di configurazione, impedisce
        // una seconda istanza e avvia la schermata iniziale (primo avvio) o la finestra principale.
        static void Main(string[] args)
        {
            const string appName = "CIEID";
            bool createdNew;

            Logger = new Logger(LogLevelApp);
            LogLevel[] LogConfigFile = LoadLogConfigFromFile();
            SetLogConfig(LogConfigFile[0], LogConfigFile[1]);
            Logger.Info("App CIE - logger inizializzato");
            Logger.Error($"Livello di log applicazione: {LogLevelApp}");
            Logger.Error($"Livello di log libreria: {LogLevelLib}");

            mutex = new Mutex(true, appName, out createdNew);

            if (!createdNew)
            {
                //app is already running! Exiting the application
                return;
            }

            if (Environment.OSVersion.Version.Major >= 6)
                SetProcessDPIAware();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            //Properties.Settings.Default.firstTime = true;
            //Properties.Settings.Default.serialNumber = "";
            //Properties.Settings.Default.cardHolder = "";
            //Properties.Settings.Default.Save();

            Properties.Settings.Default.Upgrade();
            MigraElencoCie();
            Properties.Settings.Default.Save();

            if (Properties.Settings.Default.firstTime)
            {
                Application.Run(new Intro());
            }
            else
            {
                Application.Run(new mainForm(args.Length > 0 ? args[0] : null));
            }

        }

        // Legge il file di configurazione dei log; se manca o contiene valori non validi usa i
        // valori predefiniti e lo (ri)scrive. Restituisce {livello app, livello libreria}.
        static LogLevel[] LoadLogConfigFromFile()
        {
            LogLevel logLevelApp = Logger.DefaultLogLevel;
            LogLevel logLevelLib = Logger.DefaultLogLevel;
            bool _writeLogConfigFile = false;

            string programDataDir = System.Environment.GetEnvironmentVariable("PROGRAMDATA");
            string configFilePath = $"{programDataDir}\\CIEPKI-NG\\config";
            try {
                Console.WriteLine("Leggo file configurazione log");
                string[] configFileLines = System.IO.File.ReadAllLines(configFilePath);
                foreach (var line in configFileLines) {
                    if (line.Contains(LogConfigPrefixApp))
                    {
                        Console.WriteLine($"Configurazione log applicazione: {line}");
                        string value = line.Split('=')[1];
                        try
                        {
                            int intValue = int.Parse(value);
                            if (Enum.IsDefined(typeof(LogLevel), intValue))
                            {
                                logLevelApp = (LogLevel)intValue;
                            }
                            else
                            {
                                Console.WriteLine($"valore '{intValue}' del livello di log applicazione fuori intervallo - uso default");
                                _writeLogConfigFile = true;
                            }
                        }
                        catch (FormatException e)
                        {
                            Console.WriteLine($"valore '{value}' del livello di log applicazione non valido - uso default");
                            _writeLogConfigFile = true;
                        }
                    }
                    else if (line.Contains(LogConfigPrefixLib))
                    {
                        Console.WriteLine($"Configurazione log libreria: {line}");
                        string value = line.Split('=')[1];
                        try
                        {
                            int intValue = int.Parse(value);
                            if (Enum.IsDefined(typeof(LogLevel), intValue))
                            {
                                logLevelLib = (LogLevel)intValue;
                            }
                            else
                            {
                                Console.WriteLine($"valore '{intValue}' del livello di log libreria fuori intervallo - uso default");
                                _writeLogConfigFile = true;
                            }
                        }
                        catch (FormatException)
                        {
                            Console.WriteLine($"valore '{value}' del livello di log libreria non valido - uso default");
                            _writeLogConfigFile = true;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Riga di configurazione log non valida: {line}");
                    }
              }
            }

            catch (FileNotFoundException)
            {
                Console.WriteLine($"File configurazione log non trovato, lo creo con valori di default");
                _writeLogConfigFile = true;
            }
            catch (DirectoryNotFoundException)
            {
                Console.WriteLine($"Directory di configurazione log non trovata, la creo");
                try
                {
                    System.IO.Directory.CreateDirectory($"{programDataDir}\\CIEPKI-NG");
                    _writeLogConfigFile = true;
                }
                catch (Exception)
                {
                    Console.WriteLine($"Errore nel creare directory per il file di configurazione dei log");
                    _writeLogConfigFile = false;
                }
            }

            if (_writeLogConfigFile)
            {
                SaveLogConfigToFile(logLevelApp, logLevelLib);
            }

            return new LogLevel[] {logLevelApp, logLevelLib};
        }

        // Imposta i livelli di log correnti (applicazione e libreria)
        public static void SetLogConfig(LogLevel logLevelApp, LogLevel logLevelLib)
        {
            Logger.Info("SetLogConfig() - Inizia funzione");
            Logger.Debug($"SetLogConfig() - logLevelApp: {logLevelApp}");
            Logger.Debug($"SetLogConfig() - logLevelLib: {logLevelLib}");
            LogLevelApp = logLevelApp;
            LogLevelLib = logLevelLib;
            Logger.Level = LogLevelApp;
        }

        // Salva su file i livelli di log correnti
        public static void SaveLogConfigToFile()
        {
            Logger.Info("SaveLogConfigToFile() - Inizia funzione");
            SaveLogConfigToFile(LogLevelApp, LogLevelLib);
        }

        // Scrive le due righe LIB_LOG_LEVEL e APP_LOG_LEVEL nel file di configurazione
        static void SaveLogConfigToFile(LogLevel logLevelApp, LogLevel logLevelLib)
        {
            Logger.Info("SaveLogConfigToFile(LogLevel, LogLevel) - Inizia funzione");
            Logger.Debug($"SaveLogConfigToFile(LogLevel, LogLevel) - logLevelApp: {logLevelApp}");
            Logger.Debug($"SaveLogConfigToFile(LogLevel, LogLevel) - logLevelLib: {logLevelLib}");
            string programDataDir = System.Environment.GetEnvironmentVariable("PROGRAMDATA");
            string configFilePath = $"{programDataDir}\\CIEPKI-NG\\config";
            string[] lines =
            {
                $"LIB_LOG_LEVEL={(int)logLevelLib}", $"APP_LOG_LEVEL={(int)logLevelApp}",
            };
            try
            {
                File.WriteAllLines(configFilePath, lines);
            }
            catch (Exception)
            {
                string message = $"Errore nel salvare il file di configurazione dei log";
                Logger.Error(message);
                Console.WriteLine(message);
            }
        }

    }
}
