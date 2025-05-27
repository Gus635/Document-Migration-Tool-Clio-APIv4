using System;
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Windows;
using ClioDataMigrator.Models; // Add model namespace
using ClioDataMigrator.Models.Interfaces; // Add interfaces namespace
using ClioDataMigrator.Utils; // Add utils namespace
using ClioDataMigrator.View; // Add this line to reference MainWindow
using ClioDataMigrator.ViewModels; // Add viewmodels namespace
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog; // Add Serilog namespace

namespace ClioDataMigrator // Ensure this matches your project's namespace
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    [SupportedOSPlatform("windows")]
    public partial class App : Application
    {
        private IServiceProvider _serviceProvider;
        private Microsoft.Extensions.Logging.ILogger _appLogger; // Use Microsoft.Extensions.Logging

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ConfigureServices();

            _appLogger = _serviceProvider.GetRequiredService<ILogger<App>>();
            _appLogger.LogInformation("Application starting up.");

            // Set up global exception handling
            SetupExceptionHandling();

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>(); // Or your main window class
            mainWindow.Show();
        }

        private void ConfigureServices()
        {
            var services = new ServiceCollection();

            // 1. Configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();
            services.AddSingleton<IConfiguration>(configuration);

            // 2. Logging (using Serilog configured via appsettings.json)
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();

            services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.ClearProviders(); // Remove other providers like Console, Debug
                loggingBuilder.AddSerilog(dispose: true); // Use Serilog
            });

            // 3. HttpClientFactory and ClioApiClient HttpClient
            services.AddHttpClient<ClioApiClient>(
                (serviceProvider, client) => {
                    // Base address and default headers are set within ClioApiClient constructor using IConfiguration
                    // You could configure timeouts or other default settings here if needed
                    // client.Timeout = TimeSpan.FromSeconds(60);
                }
            );
            // If you need HttpClient for other purposes, register IHttpClientFactory directly
            // services.AddHttpClient();

            // 4. Secure Storage Implementation
            services.AddSingleton<ISecureStorage, DpapiSecureStorage>();

            // 5. Register interfaces
            services.AddSingleton<IClioApiClient, ClioApiClient>();
            services.AddSingleton<IConfigManager, ConfigManager>();
            services.AddSingleton<Utils.ILogger, Utils.Logger>();

            // 6. Data Transformer
            services.AddTransient<DataTransformer>();

            // 7. Register Windows/ViewModels
            services.AddTransient<MainWindowViewModel>();
            services.AddTransient<MainWindow>(provider =>
            {
                var viewModel = provider.GetRequiredService<MainWindowViewModel>();
                return new MainWindow(viewModel);
            });

            _serviceProvider = services.BuildServiceProvider();

            Log.Information("Services configured successfully.");
        }

        // Optional: Add global exception handling
        private void SetupExceptionHandling()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                LogUnhandledException(
                    (Exception)e.ExceptionObject,
                    "AppDomain.CurrentDomain.UnhandledException"
                );
            };

            DispatcherUnhandledException += (s, e) =>
            {
                LogUnhandledException(
                    e.Exception,
                    "Application.Current.DispatcherUnhandledException"
                );
                e.Handled = true; // Prevent application termination
                MessageBox.Show(
                    $"An unexpected error occurred: {e.Exception.Message}\n\nPlease check the logs for details.",
                    "Unhandled Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                // Optionally, try to gracefully shut down or offer to continue
                // Current.Shutdown();
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                LogUnhandledException(e.Exception, "TaskScheduler.UnobservedTaskException");
                e.SetObserved(); // Mark exception as observed
            };
        }

        private void LogUnhandledException(Exception exception, string source)
        {
            string message = $"Unhandled exception ({source})";
            try
            {
                System.Reflection.AssemblyName assemblyName = System
                    .Reflection.Assembly.GetExecutingAssembly()
                    .GetName();
                message = string.Format(
                    "Unhandled exception in {0} v{1}",
                    assemblyName.Name,
                    assemblyName.Version
                );
            }
            catch (Exception ex)
            {
                _appLogger?.LogError(ex, "Exception in LogUnhandledException");
            }
            finally
            {
                _appLogger?.LogError(exception, message);
                Log.CloseAndFlush(); // Ensure logs are written before potential shutdown
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Log.CloseAndFlush(); // Ensure all logs are written on exit
            base.OnExit(e);
        }
    }
}
