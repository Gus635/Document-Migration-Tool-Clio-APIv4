using System;
using System.Threading.Tasks;

namespace ClioDataMigrator.Utils
{
    public interface ISecureStorage
    {
        // Synchronous methods used by ConfigManager
        void StoreSecret(string key, string value);
        string RetrieveSecret(string key);
        bool DeleteSecret(string key);

        // Asynchronous methods for token operations (if needed elsewhere)
        Task StoreRefreshTokenAsync(string refreshToken);
        Task<string> RetrieveRefreshTokenAsync();
        Task<bool> DeleteRefreshTokenAsync();
    }
}
