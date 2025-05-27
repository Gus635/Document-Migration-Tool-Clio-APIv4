using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;

namespace ClioDataMigrator.View
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ViewModels.MainWindowViewModel _viewModel;

        public MainWindow(ViewModels.MainWindowViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = _viewModel;
        }

        // This method should delegate to the ViewModel
        public void SetClientSecret(System.Security.SecureString securePassword)
        {
            // Call the ViewModel's method instead of implementing logic here
            _viewModel.SetClientSecret(securePassword);
        }

        private void ClientSecretPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                SetClientSecret(passwordBox.SecurePassword.Copy());
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (DataContext is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
