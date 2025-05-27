// MainWindowViewModel.cs
// This is the ViewModel for the MainWindow. It handles UI logic,
// data binding, commands, and interacts with the Model layer.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel; // For LogMessages
using System.ComponentModel; // Required for INotifyPropertyChanged
using System.ComponentModel.DataAnnotations;
using System.Diagnostics; // Required for Process.Start
using System.Net;
using System.Runtime.CompilerServices; // Required for CallerMemberName
using System.Runtime.InteropServices; // For Marshal class
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input; // Required for ICommand
using ClioDataMigrator.Models.Interfaces; // To use ClioApiClient
using ClioDataMigrator.Utils; // To use HttpRedirectHandler
using Microsoft.Win32; // For OpenFileDialog

namespace ClioDataMigrator.ViewModels
{
    public class MainWindowViewModel : BaseViewModel
    {
        private readonly IClioApiClient _clioApiClient;
        private readonly IConfigManager _configManager;
        private readonly ILogger _logger;

        // TODO: Add dependencies for FileParser, DataTransformer, MigrationService, ConfigurationManager
        // private readonly FileParser _fileParser;
        // private readonly DataTransformer _dataTransformer;
        // private readonly MigrationService _migrationService;

        // --- Properties for UI Binding ---
        private string _clioClientId;

        [Required(ErrorMessage = "Client ID is required")]
        public string ClioClientId
        {
            get => _clioClientId;
            set
            {
                if (SetProperty(ref _clioClientId, value))
                {
                    ValidateProperty(value, nameof(ClioClientId));
                    (DesktopAuthenticateCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (SaveCredentialsCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        // Note: For security, you typically would NOT bind a PasswordBox directly
        // to a string property in the ViewModel. You would access the SecurePassword
        // from the PasswordBox in the code-behind and pass it securely to the ViewModel/Model.
        // This property is a placeholder.
        private string _clioClientSecret;
        public string ClioClientSecret
        {
            get => _clioClientSecret;
            set
            {
                if (SetProperty(ref _clioClientSecret, value))
                {
                    // Explicitly raise CanExecuteChanged for commands that depend on this property
                    (DesktopAuthenticateCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (SaveCredentialsCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        private string _redirectUri = "http://127.0.0.1:8888/callback"; // Default redirect URI (update as needed)
        public string RedirectUri
        {
            get => _redirectUri;
            set => SetProperty(ref _redirectUri, value);
        }

        private string _sourceFilePath;
        public string SourceFilePath
        {
            get => _sourceFilePath;
            set => SetProperty(ref _sourceFilePath, value);
        }

        private string _currentStatus = "Ready";
        public string CurrentStatus
        {
            get => _currentStatus;
            set => SetProperty(ref _currentStatus, value);
        }

        private int _migrationProgress; // 0-100
        public int MigrationProgress
        {
            get => _migrationProgress;
            set => SetProperty(ref _migrationProgress, value);
        }

        private ObservableCollection<string> _logMessages;
        public ObservableCollection<string> LogMessages
        {
            get => _logMessages;
            set => SetProperty(ref _logMessages, value);
        }

        private bool _isAuthenticated;
        public bool IsAuthenticated
        {
            get => _isAuthenticated;
            set
            {
                SetProperty(ref _isAuthenticated, value);
                // Update command states when authentication status changes
                (DesktopAuthenticateCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (StartMigrationCommand as RelayCommand)?.RaiseCanExecuteChanged();
                // TODO: Update other command states if needed
            }
        }

        // Add missing properties for UI binding
        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        // Property for manual authorization code entry in desktop authentication
        private string _authorizationCode;
        public string AuthorizationCode
        {
            get => _authorizationCode;
            set
            {
                if (SetProperty(ref _authorizationCode, value))
                {
                    (ProcessAuthCodeCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        } // --- Commands for UI Actions ---
        public ICommand DesktopAuthenticateCommand { get; }
        public ICommand ProcessAuthCodeCommand { get; }
        public ICommand SelectFileCommand { get; }
        public ICommand StartMigrationCommand { get; }
        public ICommand SaveCredentialsCommand { get; }

        // TODO: Add commands for Cancel, Pause, etc.

        // --- Constructor ---
        /// <summary>
        /// Initializes a new instance of the MainWindowViewModel.
        /// </summary>
        public MainWindowViewModel(
            IClioApiClient clioApiClient,
            IConfigManager configManager,
            ILogger logger
        )
        {
            _clioApiClient =
                clioApiClient ?? throw new ArgumentNullException(nameof(clioApiClient));
            _configManager =
                configManager ?? throw new ArgumentNullException(nameof(configManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            ClioClientId = _configManager.GetClioClientId();
            ClioClientSecret = _configManager.GetClioClientSecret();
            RedirectUri = _configManager.GetClioRedirectUri();

            // TODO: Initialize other Model dependencies
            // _fileParser = new FileParser();
            // _dataTransformer = new DataTransformer();
            // _migrationService = new MigrationService(_fileParser, _dataTransformer, _clioApiClient);            // Initialize LogMessages collection
            LogMessages = new ObservableCollection<string>();

            // Initialize Commands
            DesktopAuthenticateCommand = new RelayCommand(
                ExecuteDesktopAuthenticate,
                CanExecuteDesktopAuthenticate
            );
            ProcessAuthCodeCommand = new RelayCommand(
                ExecuteProcessAuthCode,
                CanExecuteProcessAuthCode
            );
            SelectFileCommand = new RelayCommand(ExecuteSelectFile);
            StartMigrationCommand = new RelayCommand(
                ExecuteStartMigration,
                CanExecuteStartMigration
            );
            SaveCredentialsCommand = new RelayCommand(
                ExecuteSaveCredentials,
                CanExecuteSaveCredentials
            );

            // Initial status update
            Log("Application started.");
        } // --- Command Implementations ---

        private bool CanExecuteDesktopAuthenticate(object parameter)
        {
            // Can authenticate if not already authenticated and Client ID/Secret/Redirect URI are provided
            return !IsAuthenticated
                && !string.IsNullOrEmpty(ClioClientId)
                && !string.IsNullOrEmpty(ClioClientSecret)
                && !string.IsNullOrEmpty(RedirectUri);
        }

        /// <summary>
        /// Desktop authentication flow using Clio's special approval redirect URI.
        /// The authorization code will appear in both the URL and page title of the approval page.
        /// </summary>
        private async void ExecuteDesktopAuthenticate(object parameter)
        {
            await ExecuteSafelyAsync(
                async () =>
                {
                    IsBusy = true;
                    CurrentStatus = "Starting desktop authentication...";
                    Log("Initiating Clio API desktop authentication flow."); // Generate and store a state parameter for CSRF protection
                    string state = Guid.NewGuid().ToString();
                    _configManager.StoreAuthState(state);

                    // Generate the desktop authorization URL
                    // Using desktop authorization URL which redirects to Clio's special approval page
                    // Scopes are not included as they have been configured via Clio already
                    string authUrl = _clioApiClient.GenerateDesktopAuthorizationUrl(state);

                    Log($"Opening browser for desktop authentication: {authUrl}");
                    Log(
                        "After granting permission, you will be redirected to Clio's approval page."
                    );
                    Log("The authorization code will appear in both the URL and page title.");
                    Log("Format: 'Success code=<authorization_code>' in the page title.");
                    Log(
                        "Copy the authorization code and enter it below to complete authentication."
                    );

                    // Open the URL in the user's default browser
                    Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });

                    CurrentStatus =
                        "Waiting for manual code entry - see browser for authorization code";
                    Log(
                        "Please complete the authorization in your browser and copy the authorization code."
                    );

                    // For desktop apps, we can't automatically capture the code
                    // The user will need to manually enter it
                    IsBusy = false;

                    // Use Task.CompletedTask to satisfy the async requirement
                    await Task.CompletedTask;
                },
                "Desktop authentication failed: "
            );
        }

        private bool CanExecuteProcessAuthCode(object parameter)
        {
            return !string.IsNullOrWhiteSpace(AuthorizationCode) && !IsBusy;
        }

        private async void ExecuteProcessAuthCode(object parameter)
        {
            await ProcessDesktopAuthorizationCode(AuthorizationCode);
        }

        /// <summary>
        /// Processes the manually entered authorization code from desktop authentication.
        /// </summary>
        /// <param name="authorizationCode">The authorization code from Clio's approval page</param>
        public async Task ProcessDesktopAuthorizationCode(string authorizationCode)
        {
            if (string.IsNullOrWhiteSpace(authorizationCode))
            {
                SetError("Authorization code cannot be empty.");
                return;
            }

            await ExecuteSafelyAsync(
                async () =>
                {
                    IsBusy = true;
                    CurrentStatus = "Processing authorization code...";
                    Log($"Processing authorization code: {authorizationCode}");

                    try
                    {
                        Log("Exchanging authorization code for access and refresh tokens.");

                        try
                        {
                            var accessToken = await _clioApiClient.GetAccessToken(
                                authorizationCode
                            );

                            if (!string.IsNullOrEmpty(accessToken))
                            {
                                IsAuthenticated = true;
                                CurrentStatus = "Authenticated successfully!";
                                Log("Successfully authenticated with Clio API using desktop flow.");
                                ClearError();

                                // Clear the authorization code field
                                AuthorizationCode = string.Empty;
                            }
                            else
                            {
                                IsAuthenticated = false;
                                CurrentStatus = "Authentication failed.";
                                SetError(
                                    "Failed to exchange authorization code for tokens: No access token received"
                                );
                                Log(
                                    "Failed to exchange authorization code for tokens: No access token received"
                                );
                            }
                        }
                        catch (Exception ex)
                        {
                            IsAuthenticated = false;
                            CurrentStatus = "Authentication failed.";
                            SetError(
                                $"Failed to exchange authorization code for tokens: {ex.Message}"
                            );
                            Log($"Failed to exchange authorization code for tokens: {ex.Message}");
                        }
                    }
                    finally
                    {
                        IsBusy = false;
                    }
                },
                "Desktop authentication failed: "
            );
        }

        /// <summary>
        /// Process authorization code using simplified token exchange for debugging
        /// </summary>
        private async void ProcessDesktopAuthorizationCodeDebug(object parameter)
        {
            await ExecuteSafelyAsync(
                async () =>
                {
                    if (string.IsNullOrWhiteSpace(AuthorizationCode))
                    {
                        Log("Please enter the authorization code from the browser.");
                        return;
                    }

                    IsBusy = true;
                    CurrentStatus = "Processing authorization code...";
                    Log($"Processing authorization code: {AuthorizationCode}");

                    // Use the proper ClioApiClient token exchange method
                    try
                    {
                        var accessToken = await _clioApiClient.GetAccessToken(
                            AuthorizationCode.Trim()
                        );

                        if (!string.IsNullOrEmpty(accessToken))
                        {
                            Log("✅ SUCCESS: Token exchange completed!");
                            Log(
                                $"Access token obtained: {accessToken.Substring(0, Math.Min(20, accessToken.Length))}..."
                            );
                            CurrentStatus = "Authentication successful!";
                            IsAuthenticated = true;
                            ClearError();

                            // Clear the authorization code field
                            AuthorizationCode = string.Empty;
                        }
                        else
                        {
                            Log("❌ ERROR: Token exchange failed - No access token received");
                            CurrentStatus = "Authentication failed";
                            SetError("Token exchange failed: No access token received");
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"❌ ERROR: Token exchange failed - {ex.Message}");
                        CurrentStatus = "Authentication failed";
                        SetError($"Token exchange failed: {ex.Message}");
                    }

                    IsBusy = false;
                },
                "Failed to process authorization code"
            );
        }

        private void ExecuteSelectFile(object parameter)
        {
            var openFileDialog = new OpenFileDialog()
            {
                Title = "Select source data file",
                Filter = "CSV files (*.csv)|*.csv|Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*",
                FilterIndex = 1,
            };

            if (openFileDialog.ShowDialog() == true)
            {
                SourceFilePath = openFileDialog.FileName;
                Log($"Source file selected: {SourceFilePath}");
                (StartMigrationCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        private bool CanExecuteStartMigration(object parameter)
        {
            return IsAuthenticated && !string.IsNullOrEmpty(SourceFilePath);
        }

        private async void ExecuteStartMigration(object parameter)
        {
            CurrentStatus = "Starting migration...";
            Log($"Starting migration from {SourceFilePath} to Clio.");

            await ExecuteSafelyAsync(
                async () =>
                {
                    IsBusy = true;

                    try
                    {
                        // TODO: Implement the actual migration logic:
                        // 1. Parse the source file using FileParser.
                        // 2. Transform the data using DataTransformer.
                        // 3. Call MigrationService to start the migration.
                        // 4. Update MigrationProgress to show percentage.
                        // 5. Log each significant step.
                        // 6. Running this logic on a background thread to keep the UI responsive.

                        // Example: Simulate migration process
                        for (int i = 0; i <= 100; i += 5)
                        {
                            // Check for cancellation (you'd need to implement this)
                            // if (cancellationToken.IsCancellationRequested) { break; }

                            MigrationProgress = i;
                            CurrentStatus = $"Migrating... {i}%";
                            Log($"Processed batch {i / 5}");
                            await Task.Delay(200); // Simulate work
                        }

                        CurrentStatus = "Migration complete.";
                        Log("Migration process finished successfully.");
                        // TODO: Report success/failure count
                    }
                    catch (Exception ex)
                    {
                        string errorMessage = $"Migration failed: {ex.Message}";
                        CurrentStatus = "Migration failed.";
                        Log($"An error occurred during migration: {ex.Message}");
                        _logger.LogError(errorMessage, ex);
                        throw; // Re-throw to be caught by ExecuteSafelyAsync
                    }
                    finally
                    {
                        IsBusy = false;
                    }
                },
                "Migration failed: "
            );
        }

        private bool CanExecuteSaveCredentials(object parameter)
        {
            return !string.IsNullOrEmpty(ClioClientId) && !string.IsNullOrEmpty(ClioClientSecret);
        }

        private void ExecuteSaveCredentials(object parameter)
        {
            try
            {
                bool success = _configManager.SaveClioCredentials(
                    ClioClientId,
                    ClioClientSecret,
                    RedirectUri
                );

                if (success)
                {
                    CurrentStatus = "Credentials saved successfully.";
                    Log("Credentials saved to configuration file.");
                    ClearError();
                }
                else
                {
                    CurrentStatus = "Failed to save credentials.";
                    SetError("Unable to save credentials to configuration file.");
                } // Update command states
                (DesktopAuthenticateCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                CurrentStatus = "Error saving credentials.";
                SetError($"Error saving credentials: {ex.Message}");
                _logger.LogError("Failed to save credentials", ex);
            }
        }

        private void Log(string message)
        {
            if (LogMessages != null)
            {
                string timestampedMessage = $"[{DateTime.Now:HH:mm:ss}] {message}";
                LogMessages.Add(timestampedMessage);
                if (LogMessages.Count > 1000)
                    LogMessages.RemoveAt(0);
                _logger.Log(message);
            }
        }

        public void SetClientSecret(System.Security.SecureString securePassword)
        {
            if (securePassword == null || securePassword.Length == 0)
            {
                ClioClientSecret = string.Empty;
                return;
            }

            IntPtr valuePtr = IntPtr.Zero;
            try
            {
                valuePtr = Marshal.SecureStringToGlobalAllocUnicode(securePassword);
                ClioClientSecret = Marshal.PtrToStringUni(valuePtr);
            }
            finally
            {
                if (valuePtr != IntPtr.Zero)
                {
                    Marshal.ZeroFreeGlobalAllocUnicode(valuePtr);
                }
            }
        }

        // Implementation for property validation
        private void ValidateProperty(object value, string propertyName)
        {
            var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            var context = new ValidationContext(this) { MemberName = propertyName };

            Validator.TryValidateProperty(value, context, results);

            // Clear previous errors for this property
            ClearError();

            if (results.Count > 0)
            {
                // Set the first error message
                SetError(results[0].ErrorMessage);
            }
        }

        /// <summary>
        /// Diagnostic method to test token exchange with detailed logging
        /// </summary>
        public async Task TestTokenExchange(string authorizationCode)
        {
            try
            {
                var accessToken = await _clioApiClient.GetAccessToken(authorizationCode);
                if (string.IsNullOrEmpty(accessToken))
                {
                    Log("Token exchange failed: No access token received");
                }
                else
                {
                    Log("Token exchange succeeded");
                }
            }
            catch (Exception ex)
            {
                Log($"Token exchange failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Test the GetAccessToken method for token exchange
        /// </summary>
        public async Task TestBothTokenExchangeMethods(string authorizationCode)
        {
            try
            {
                var accessToken = await _clioApiClient.GetAccessToken(authorizationCode);
                Log(
                    $"GetAccessToken method result: Success={!string.IsNullOrEmpty(accessToken)}, Token={(!string.IsNullOrEmpty(accessToken) ? "Received" : "None")}"
                );
            }
            catch (Exception ex)
            {
                Log($"GetAccessToken method failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Simple method to get an access token directly using the new GetAccessToken method.
        /// This is the simplified OAuth 2.0 approach.
        /// </summary>
        public async Task GetAccessTokenSimple(string authorizationCode)
        {
            try
            {
                Log("Testing simplified GetAccessToken method...");

                var accessToken = await _clioApiClient.GetAccessToken(authorizationCode);

                if (!string.IsNullOrEmpty(accessToken))
                {
                    Log(
                        $"Success! Access token obtained: {accessToken.Substring(0, Math.Min(20, accessToken.Length))}..."
                    );
                    // You can now use this access token for API calls
                }
                else
                {
                    Log("Failed to obtain access token. Check logs for details.");
                }
            }
            catch (Exception ex)
            {
                Log($"Exception in GetAccessTokenSimple: {ex.Message}");
            }
        }
    }
}
