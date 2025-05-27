namespace ClioDataMigrator.ViewModels
{
    /// <summary>
    /// Represents types of authentication errors that can occur
    /// </summary>
    public enum AuthenticationErrorType
    {
        /// <summary>
        /// No authentication error
        /// </summary>
        None,

        /// <summary>
        /// Error with client configuration (client ID, redirect URI, etc.)
        /// </summary>
        ConfigurationError,

        /// <summary>
        /// Error opening the browser or user cancelled the authentication
        /// </summary>
        UserCancelledOrBrowserError,

        /// <summary>
        /// Error with the HTTP listener (port already in use, etc.)
        /// </summary>
        ListenerError,

        /// <summary>
        /// Timeout waiting for the authentication flow to complete
        /// </summary>
        Timeout,

        /// <summary>
        /// Error exchanging the authorization code for tokens
        /// </summary>
        TokenExchangeError,

        /// <summary>
        /// Error validating the state parameter (CSRF protection)
        /// </summary>
        SecurityError,

        /// <summary>
        /// Network or connection error communicating with the API
        /// </summary>
        NetworkError,

        /// <summary>
        /// API returned an error response
        /// </summary>
        ApiError,

        /// <summary>
        /// Other or unknown error
        /// </summary>
        OtherError,
    }
}
