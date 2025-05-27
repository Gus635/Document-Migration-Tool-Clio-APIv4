using System.Threading.Tasks;

namespace ClioDataMigrator.Models.Interfaces
{
    public interface IClioApiClient
    {
        bool IsAuthenticated { get; }
        string GenerateDesktopAuthorizationUrl(string state = null);

        //Task<bool> RefreshAccessTokenAsync();
        Task<string> GetAccessToken(string code);

        // Generic API methods
        Task<T> GetAsync<T>(string endpoint);
        Task<T> PostAsync<T>(string endpoint, object data);
        Task<T> PutAsync<T>(string endpoint, object data);
        Task<bool> DeleteAsync(string endpoint);
    }
}
