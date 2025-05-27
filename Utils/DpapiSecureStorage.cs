using System;
using System.IO;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ClioDataMigrator.Utils
{
    [SupportedOSPlatform("windows")]
    public class DpapiSecureStorage : ISecureStorage
    {
        private const string REFRESH_TOKEN_KEY = "ClioRefreshToken";

        // Synchronous methods used by ConfigManager
        public void StoreSecret(string key, string value)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            if (string.IsNullOrEmpty(value))
                throw new ArgumentException("Value cannot be null or empty", nameof(value));

            // Use DPAPI to encrypt the value
            byte[] encryptedData = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(value),
                null,
                DataProtectionScope.CurrentUser
            );

            // Convert to Base64 for storage
            string encryptedValue = Convert.ToBase64String(encryptedData);

            // Store in the user's profile folder
            string filePath = GetFilePathForKey(key);
            File.WriteAllText(filePath, encryptedValue);
        }

        public string RetrieveSecret(string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            string filePath = GetFilePathForKey(key);

            if (!File.Exists(filePath))
                return null;

            try
            {
                // Read encrypted data from file
                string encryptedValue = File.ReadAllText(filePath);

                // Decrypt using DPAPI
                byte[] encryptedData = Convert.FromBase64String(encryptedValue);
                byte[] decryptedData = ProtectedData.Unprotect(
                    encryptedData,
                    null,
                    DataProtectionScope.CurrentUser
                );

                return Encoding.UTF8.GetString(decryptedData);
            }
            catch
            {
                // If reading or decryption fails, delete the corrupted file
                try
                {
                    File.Delete(filePath);
                }
                catch { }
                return null;
            }
        }

        public bool DeleteSecret(string key)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("Key cannot be null or empty", nameof(key));

            string filePath = GetFilePathForKey(key);

            if (!File.Exists(filePath))
                return false;

            try
            {
                File.Delete(filePath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Async methods for token operations
        public Task StoreRefreshTokenAsync(string refreshToken)
        {
            return Task.Run(() => StoreSecret(REFRESH_TOKEN_KEY, refreshToken));
        }

        public Task<string> RetrieveRefreshTokenAsync()
        {
            return Task.Run(() => RetrieveSecret(REFRESH_TOKEN_KEY));
        }

        public Task<bool> DeleteRefreshTokenAsync()
        {
            return Task.Run(() => DeleteSecret(REFRESH_TOKEN_KEY));
        }

        private string GetFilePathForKey(string key)
        {
            // Create a hash of the key to use as a filename
            using (var sha = SHA256.Create())
            {
                byte[] keyBytes = Encoding.UTF8.GetBytes(key);
                byte[] hashBytes = sha.ComputeHash(keyBytes);
                string filename =
                    BitConverter.ToString(hashBytes).Replace("-", "").ToLower() + ".dat";

                // Store in the application data folder
                string appDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ClioDataMigrator"
                );

                // Create directory if it doesn't exist
                if (!Directory.Exists(appDataFolder))
                    Directory.CreateDirectory(appDataFolder);

                return Path.Combine(appDataFolder, filename);
            }
        }
    }
}
