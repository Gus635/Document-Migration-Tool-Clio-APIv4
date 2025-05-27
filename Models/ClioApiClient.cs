using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using ClioDataMigrator.Models.Interfaces;
using ClioDataMigrator.Utils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RestSharp;

namespace ClioDataMigrator.Models
{
    public class ClioApiClient : IClioApiClient, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly IConfigManager _configManager;
        private readonly ILogger<ClioApiClient> _logger;
        private readonly RestClient _apiClient;
        private string _accessToken;
        private DateTime _accessTokenExpiration;

        public ClioApiClient(
            HttpClient httpClient,
            IConfigManager configManager,
            ILogger<ClioApiClient> logger
        )
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _configManager =
                configManager ?? throw new ArgumentNullException(nameof(configManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _apiClient = new RestClient("https://app.clio.com/api/v4");
        }

        public bool IsAuthenticated =>
            !string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _accessTokenExpiration;

        public string GenerateDesktopAuthorizationUrl(string state = null)
        {
            var clientId = _configManager.GetClioClientId();
            var baseUrl = "https://app.clio.com/oauth/authorize";
            var redirectUri = "https://app.clio.com/oauth/approval";

            var url =
                $"{baseUrl}?response_type=code&client_id={clientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&scope=read write";

            if (!string.IsNullOrEmpty(state))
            {
                url += $"&state={Uri.EscapeDataString(state)}";
            }

            return url;
        }

        // public async Task<bool> RefreshAccessTokenAsync()
        // {
        //     // TODO: Implement token refresh logic
        //     await Task.CompletedTask;
        //     return false;
        // }

        public async Task<string> GetAccessToken(string code)
        {
            try
            {
                var clientId = _configManager.GetClioClientId();
                var clientSecret = _configManager.GetClioClientSecret();
                var request = new RestRequest("oauth/token", Method.Post);

                var parameters = new Dictionary<string, string>
                {
                    { "grant_type", "authorization_code" },
                    { "code", code },
                    { "client_id", clientId },
                    { "client_secret", clientSecret },
                    { "redirect_uri", "https://app.clio.com/oauth/approval" },
                };
                _logger.LogInformation("Authorization Code: {AuthCode}", code);
                var content = new FormUrlEncodedContent(parameters);
                var client = new HttpClient();
                var response = await client.PostAsync("https://app.clio.com/oauth/token", content);

                string responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation(
                    "Token exchange response: Status={StatusCode}, Content={Content}",
                    response.StatusCode,
                    responseContent
                );
                Console.WriteLine(responseContent);

                // _logger.LogInformation("Making token exchange request to Clio API");
                // _logger.LogInformation("Using client_id: {ClientId}", clientId);
                // _logger.LogInformation(
                //     "Using code: {CodePrefix}...",
                //     code.Substring(0, Math.Min(10, code.Length))
                // );

                // var client = new RestClient("https://app.clio.com");
                // var response = await client.ExecuteAsync(request);

                // _logger.LogInformation(
                //     "Token exchange response: Status={StatusCode}, Content={Content}",
                //     response.StatusCode,
                //     response.Content
                // );

                if (response.IsSuccessStatusCode)
                {
                    var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(
                        responseContent
                    );

                    _accessToken = tokenResponse.access_token;
                    _accessTokenExpiration = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in);

                    _logger.LogInformation(
                        "Token exchange successful. Token expires at: {Expiration}",
                        _accessTokenExpiration
                    );
                    return tokenResponse.access_token;
                }
                else
                {
                    var errorMsg =
                        $"Token exchange failed with status {response.StatusCode}: {responseContent}";

                    // Try to parse error details if available
                    try
                    {
                        var errorObj = JsonConvert.DeserializeObject<Dictionary<string, string>>(
                            responseContent
                        );

                        if (errorObj != null)
                        {
                            string error = null;
                            string description = null;

                            errorObj.TryGetValue("error", out error);
                            errorObj.TryGetValue("error_description", out description);

                            _logger.LogError(
                                "Token exchange error details: Error={Error}, Description={Description}",
                                error ?? "Unknown",
                                description ?? "No description"
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Could not parse error response: {Message}", ex.Message);
                    }

                    _logger.LogError(errorMsg);
                    throw new HttpRequestException(errorMsg);
                }
            }
            catch (Exception ex)
            {
                var errorMsg = $"Exception during token exchange: {ex.Message}";
                _logger.LogError(ex, errorMsg);
                throw;
            }
        }

        // Generic API request methods
        /// <summary>
        /// Makes a GET request to the specified endpoint and deserializes the response.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the response to</typeparam>
        /// <param name="endpoint">The API endpoint (e.g., "contacts", "matters")</param>
        /// <returns>The deserialized response object</returns>
        public async Task<T> GetAsync<T>(string endpoint)
        {
            if (!IsAuthenticated)
            {
                throw new InvalidOperationException(
                    "Client is not authenticated. Call GetAccessToken first."
                );
            }

            try
            {
                _logger.LogInformation("Making GET request to endpoint: {Endpoint}", endpoint);

                var request = new RestRequest(endpoint, Method.Get);
                request.AddHeader("Authorization", $"Bearer {_accessToken}");

                var response = await _apiClient.ExecuteAsync(request);

                if (response.IsSuccessful)
                {
                    _logger.LogInformation(
                        "GET request successful for endpoint: {Endpoint}",
                        endpoint
                    );
                    return JsonConvert.DeserializeObject<T>(response.Content);
                }
                else
                {
                    _logger.LogError(
                        "GET request failed for endpoint {Endpoint}. "
                            + "Status: {StatusCode}, Content: {Content}",
                        endpoint,
                        response.StatusCode,
                        response.Content
                    );
                    throw new HttpRequestException(
                        $"API request failed: {response.StatusCode} - {response.Content}"
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Exception occurred during GET request to {Endpoint}",
                    endpoint
                );
                throw;
            }
        }

        /// <summary>
        /// Makes a POST request to the specified endpoint with the provided data.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the response to</typeparam>
        /// <param name="endpoint">The API endpoint</param>
        /// <param name="data">The data to send in the request body</param>
        /// <returns>The deserialized response object</returns>
        public async Task<T> PostAsync<T>(string endpoint, object data)
        {
            if (!IsAuthenticated)
            {
                throw new InvalidOperationException(
                    "Client is not authenticated. Call GetAccessToken first."
                );
            }

            try
            {
                _logger.LogInformation("Making POST request to endpoint: {Endpoint}", endpoint);

                var request = new RestRequest(endpoint, Method.Post);
                request.AddHeader("Authorization", $"Bearer {_accessToken}");
                request.AddHeader("Content-Type", "application/json");

                if (data != null)
                {
                    request.AddJsonBody(data);
                }

                var response = await _apiClient.ExecuteAsync(request);

                if (response.IsSuccessful)
                {
                    _logger.LogInformation(
                        "POST request successful for endpoint: {Endpoint}",
                        endpoint
                    );
                    return JsonConvert.DeserializeObject<T>(response.Content);
                }
                else
                {
                    _logger.LogError(
                        "POST request failed for endpoint {Endpoint}. "
                            + "Status: {StatusCode}, Content: {Content}",
                        endpoint,
                        response.StatusCode,
                        response.Content
                    );
                    throw new HttpRequestException(
                        $"API request failed: {response.StatusCode} - {response.Content}"
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Exception occurred during POST request to {Endpoint}",
                    endpoint
                );
                throw;
            }
        }

        /// <summary>
        /// Makes a PUT request to the specified endpoint with the provided data.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the response to</typeparam>
        /// <param name="endpoint">The API endpoint</param>
        /// <param name="data">The data to send in the request body</param>
        /// <returns>The deserialized response object</returns>
        public async Task<T> PutAsync<T>(string endpoint, object data)
        {
            if (!IsAuthenticated)
            {
                throw new InvalidOperationException(
                    "Client is not authenticated. Call GetAccessToken first."
                );
            }

            try
            {
                _logger.LogInformation("Making PUT request to endpoint: {Endpoint}", endpoint);

                var request = new RestRequest(endpoint, Method.Put);
                request.AddHeader("Authorization", $"Bearer {_accessToken}");
                request.AddHeader("Content-Type", "application/json");

                if (data != null)
                {
                    request.AddJsonBody(data);
                }

                var response = await _apiClient.ExecuteAsync(request);

                if (response.IsSuccessful)
                {
                    _logger.LogInformation(
                        "PUT request successful for endpoint: {Endpoint}",
                        endpoint
                    );
                    return JsonConvert.DeserializeObject<T>(response.Content);
                }
                else
                {
                    _logger.LogError(
                        "PUT request failed for endpoint {Endpoint}. "
                            + "Status: {StatusCode}, Content: {Content}",
                        endpoint,
                        response.StatusCode,
                        response.Content
                    );
                    throw new HttpRequestException(
                        $"API request failed: {response.StatusCode} - {response.Content}"
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Exception occurred during PUT request to {Endpoint}",
                    endpoint
                );
                throw;
            }
        }

        /// <summary>
        /// Makes a DELETE request to the specified endpoint.
        /// </summary>
        /// <param name="endpoint">The API endpoint</param>
        /// <returns>True if the delete was successful</returns>
        public async Task<bool> DeleteAsync(string endpoint)
        {
            if (!IsAuthenticated)
            {
                throw new InvalidOperationException(
                    "Client is not authenticated. Call GetAccessToken first."
                );
            }

            try
            {
                _logger.LogInformation("Making DELETE request to endpoint: {Endpoint}", endpoint);

                var request = new RestRequest(endpoint, Method.Delete);
                request.AddHeader("Authorization", $"Bearer {_accessToken}");

                var response = await _apiClient.ExecuteAsync(request);

                if (response.IsSuccessful)
                {
                    _logger.LogInformation(
                        "DELETE request successful for endpoint: {Endpoint}",
                        endpoint
                    );
                    return true;
                }
                else
                {
                    _logger.LogError(
                        "DELETE request failed for endpoint {Endpoint}. "
                            + "Status: {StatusCode}, Content: {Content}",
                        endpoint,
                        response.StatusCode,
                        response.Content
                    );
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Exception occurred during DELETE request to {Endpoint}",
                    endpoint
                );
                throw;
            }
        }

        /// <summary>
        /// Private helper method to execute API requests with proper error handling and logging
        /// </summary>
        private async Task<T> ExecuteApiRequestAsync<T>(ApiRequestOptions<T> options)
        {
            if (!IsAuthenticated)
            {
                throw new InvalidOperationException(
                    "Client is not authenticated. Call GetAccessToken first."
                );
            }

            string methodName = options.HttpMethod.ToString().ToUpper();

            try
            {
                _logger.LogInformation(
                    "Making {Method} request to endpoint: {Endpoint}",
                    methodName,
                    options.Endpoint
                );

                var request = new RestRequest(options.Endpoint, options.HttpMethod);
                request.AddHeader("Authorization", $"Bearer {_accessToken}");

                if (options.RequiresJsonContent)
                {
                    request.AddHeader("Content-Type", "application/json");
                }

                if (options.Data != null)
                {
                    request.AddJsonBody(options.Data);
                }

                var response = await _apiClient.ExecuteAsync(request);

                if (response.IsSuccessful)
                {
                    _logger.LogInformation(
                        "{Method} request successful for endpoint: {Endpoint}",
                        methodName,
                        options.Endpoint
                    );

                    // For Delete operations that return void/bool
                    if (typeof(T) == typeof(bool))
                    {
                        return (T)(object)true;
                    }

                    return JsonConvert.DeserializeObject<T>(response.Content);
                }
                else
                {
                    _logger.LogError(
                        "{Method} request failed for endpoint {Endpoint}. "
                            + "Status: {StatusCode}, Content: {Content}",
                        methodName,
                        options.Endpoint,
                        response.StatusCode,
                        response.Content
                    );
                    throw new HttpRequestException(
                        $"API request failed: {response.StatusCode} - {response.Content}"
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Exception occurred during {Method} request to {Endpoint}",
                    methodName,
                    options.Endpoint
                );
                throw;
            }
        }

        public void Dispose()
        {
            // HttpClient is injected, so we don't dispose it here
        }
    }

    public class TokenResponse
    {
        public string access_token { get; set; }
        public string token_type { get; set; }
        public int expires_in { get; set; }
        public string refresh_token { get; set; }
        public string scope { get; set; }

        // Pascal case properties for compatibility
        [JsonProperty("access_token")]
        public string AccessToken
        {
            get => access_token;
            set => access_token = value;
        }

        [JsonProperty("token_type")]
        public string TokenType
        {
            get => token_type;
            set => token_type = value;
        }

        [JsonProperty("expires_in")]
        public int ExpiresIn
        {
            get => expires_in;
            set => expires_in = value;
        }

        [JsonProperty("refresh_token")]
        public string RefreshToken
        {
            get => refresh_token;
            set => refresh_token = value;
        }
    }

    // Private helper class for internal use only
    internal class ApiRequestOptions<T>
    {
        public string Endpoint { get; set; }
        public Method HttpMethod { get; set; }
        public object Data { get; set; }
        public bool RequiresJsonContent { get; set; } = true;
    }
}
