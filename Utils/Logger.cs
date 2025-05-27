using System;
using System.IO;
using System.Text;

namespace ClioDataMigrator.Utils
{
    public class Logger : ILogger
    {
        // Initialize with a default value or in constructor
        private readonly string _logFilePath;

        public Logger()
        {
            // Initialize in constructor - use a path in AppData or similar location
            _logFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ClioDataMigrator",
                "logs",
                $"log_{DateTime.Now:yyyyMMdd}.txt"
            );

            // Ensure directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(_logFilePath));
        }

        // Or accept it as a constructor parameter
        public Logger(string logFilePath)
        {
            _logFilePath = logFilePath ?? throw new ArgumentNullException(nameof(logFilePath));

            // Ensure directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(_logFilePath));
        }

        public void LogInformation(string message)
        {
            Log("INFO", message);
        }

        public void LogWarning(string message)
        {
            Log("WARNING", message);
        }

        public void LogError(string message)
        {
            Log("ERROR", message);
        }

        public void LogError(string message, Exception ex)
        {
            Log("ERROR", $"{message} - Exception: {ex.Message}");
        }

        public void LogDebug(string message)
        {
            Log("DEBUG", message);
        }

        public void Log(string message)
        {
            Log("LOG", message);
        }

        private void Log(string level, string message)
        {
            try
            {
                string logMessage = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";

                // Log to console
                Console.WriteLine(logMessage);

                // Log to file
                if (!string.IsNullOrEmpty(_logFilePath))
                {
                    // Append to log file
                    File.AppendAllText(
                        _logFilePath,
                        logMessage + Environment.NewLine,
                        Encoding.UTF8
                    );
                }
            }
            catch (Exception ex)
            {
                // Just write to console if logging to file fails
                Console.WriteLine($"Failed to write to log file: {ex.Message}");
                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}");
            }
        }
    }
}
