namespace ClioDataMigrator.Models.Interfaces
{
    public interface IConfigManager
    {
        string GetClioClientId();
        string GetClioClientSecret();
        string GetClioRedirectUri();
        void StoreAuthState(string state);
        string GetStoredAuthState();

        // New methods for saving credentials
        bool SaveClioCredentials(string clientId, string clientSecret, string redirectUri = null);
    }
}
