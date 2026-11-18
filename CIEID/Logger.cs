using System;
using System.IO;

namespace CIEID
{
    // Livelli di log: NONE = disattivato, poi in ordine di gravita' crescente
    public enum LogLevel
    {
        NONE = 0,
        DEBUG = 1,
        INFO = 2,
        ERROR = 3,
    };

    public class Logger
    {
        // Livello predefinito (impostato in avvio da APP_LOG_LEVEL del file di configurazione)
        public static LogLevel DefaultLogLevel = LogLevel.NONE;
        private LogLevel _level;
        public LogLevel Level
        {
            get => _level;
            set => _level = value;
        }

        // Nota: il valore predefinito dell'argomento dovrebbe essere Logger.DefaultLogLevel
        // invece di un valore esplicito dell'enum LogLevel.
        public Logger(LogLevel logLevel = LogLevel.NONE)
        {
            Info($"Init della classe Logger, livello di log {logLevel}");
            Level = logLevel;
        }

        // Funzioni pubbliche per i vari livelli di log
        // Public functions
        public void Debug(string message)
        {
            Log($"[D] {message}", LogLevel.DEBUG);
        }

        public void Info(string message)
        {
            Log($"[I] {message}", LogLevel.INFO);
        }

        public void Error(string message)
        {
            Log($"[E] {message}", LogLevel.ERROR);
        }

        // Scrive il messaggio solo se il log e' attivo e il livello del messaggio e' sufficiente
        public void Log(string message, LogLevel messageLevel)
        {
            if (Level > 0 && Level <= messageLevel)
            {
                Write(message);
            }
        }

        // Accoda il messaggio a %PROGRAMDATA%/CIEPKI-NG/CIE-Middleware-NG_<data>.log;
        // se la scrittura fallisce ripiega sulla console
        private void Write(string message)
        {
            string programDataDir = System.Environment.GetEnvironmentVariable("PROGRAMDATA");
            string currentDate = DateTime.Now.ToString("yyyy-MM-dd");
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string logFilePath = $"{programDataDir}\\CIEPKI-NG\\CIE-Middleware-NG_{currentDate}.log";
            try
            {
                StreamWriter streamWriter = new StreamWriter(logFilePath, append: true);
                streamWriter.WriteLineAsync($"{timestamp}  {message}");
                streamWriter.Close();
            }
            catch(Exception)
            {
                Console.WriteLine($"{timestamp}  {message}");
            }
        }
    }
}
