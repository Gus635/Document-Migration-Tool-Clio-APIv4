using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ClioDataMigrator.Models.Interfaces;
using ClioDataMigrator.Utils;
using Microsoft.Extensions.Configuration;

namespace ClioDataMigrator.Models
{
    public class ConfigManager : IConfigManager
    {
        private readonly IConfiguration _configuration;
        private readonly ISecureStorage _secureStorage;
        private const string AUTH_STATE_KEY = "ClioAuthState";

        public ConfigManager(IConfiguration configuration, ISecureStorage secureStorage)
        {
            _configuration =
                configuration ?? throw new ArgumentNullException(nameof(configuration));
            _secureStorage =
                secureStorage ?? throw new ArgumentNullException(nameof(secureStorage));
        }

        public string GetClioClientId()
        {
            return _configuration["Clio:ClientId"];
        }

        public string GetClioClientSecret()
        {
            return _configuration["Clio:ClientSecret"];
        }

        public string GetClioRedirectUri()
        {
            return _configuration["Clio:RedirectUri"];
        }

        public void StoreAuthState(string state)
        {
            if (string.IsNullOrEmpty(state))
            {
                throw new ArgumentException("Auth state cannot be null or empty", nameof(state));
            }

            _secureStorage.StoreSecret(AUTH_STATE_KEY, state);
        }

        public string GetStoredAuthState()
        {
            return _secureStorage.RetrieveSecret(AUTH_STATE_KEY);
        }

        public bool SaveClioCredentials(
            string clientId,
            string clientSecret,
            string redirectUri = null
        )
        {
            try
            {
                // Path to appsettings.json
                string appSettingsPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "appsettings.json"
                );

                // If we're running from the project directory during development
                if (!File.Exists(appSettingsPath))
                {
                    appSettingsPath = "appsettings.json";
                }

                if (!File.Exists(appSettingsPath))
                {
                    throw new FileNotFoundException("Could not find appsettings.json file");
                }

                // Read the existing JSON file
                string jsonContent = File.ReadAllText(appSettingsPath);
                using JsonDocument document = JsonDocument.Parse(jsonContent);

                // Create a mutable in-memory representation of the JSON
                var settingsObj = new Dictionary<string, JsonElement>();
                foreach (JsonProperty prop in document.RootElement.EnumerateObject())
                {
                    settingsObj[prop.Name] = prop.Value.Clone();
                }

                // Get the Clio section as a dictionary
                Dictionary<string, object> clioSection;
                if (settingsObj.TryGetValue("Clio", out JsonElement clioElement))
                {
                    clioSection = new Dictionary<string, object>();
                    foreach (JsonProperty prop in clioElement.EnumerateObject())
                    {
                        clioSection[prop.Name] = prop.Value.GetString();
                    }
                }
                else
                {
                    clioSection = new Dictionary<string, object>();
                }

                // Update the values
                clioSection["ClientId"] = clientId;
                clioSection["ClientSecret"] = clientSecret;
                if (!string.IsNullOrEmpty(redirectUri))
                {
                    clioSection["RedirectUri"] = redirectUri;
                }

                // Create a new JSON document with the updated values
                using MemoryStream ms = new MemoryStream();
                using Utf8JsonWriter writer = new Utf8JsonWriter(
                    ms,
                    new JsonWriterOptions { Indented = true }
                );

                writer.WriteStartObject();

                // Write each property, using updated Clio section when appropriate
                foreach (var kvp in settingsObj)
                {
                    if (kvp.Key == "Clio")
                    {
                        writer.WritePropertyName("Clio");
                        writer.WriteStartObject();

                        foreach (var clioProp in clioSection)
                        {
                            writer.WritePropertyName(clioProp.Key);
                            writer.WriteStringValue(clioProp.Value?.ToString() ?? "");
                        }

                        writer.WriteEndObject();
                    }
                    else
                    {
                        writer.WritePropertyName(kvp.Key);
                        JsonSerializer.Serialize(writer, kvp.Value);
                    }
                }

                writer.WriteEndObject();
                writer.Flush();

                // Write the updated JSON back to the file
                string updatedJson = System.Text.Encoding.UTF8.GetString(ms.ToArray());
                File.WriteAllText(appSettingsPath, updatedJson);

                return true;
            }
            catch (Exception ex)
            {
                // Log the exception in a real application
                Console.WriteLine($"Error saving Clio credentials: {ex.Message}");
                return false;
            }
        }
    }
}
