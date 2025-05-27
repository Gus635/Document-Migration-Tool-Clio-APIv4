using System;

namespace ClioDataMigrator.Utils
{
    public interface ILogger
    {
        void Log(string message);
        void LogError(string message, Exception ex);
        void LogWarning(string message);
        void LogDebug(string message);
    }
}
