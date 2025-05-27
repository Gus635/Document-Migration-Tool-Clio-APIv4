// BaseViewModel.cs
// A base class for all ViewModels to inherit from,
// providing common functionality like INotifyPropertyChanged.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace ClioDataMigrator.ViewModels
{
    // Base class for all ViewModels in the application.
    // Implements INotifyPropertyChanged.

    public abstract class BaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(
            ref T field,
            T value,
            [CallerMemberName] string propertyName = null
        )
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false; // Value did not change
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true; // Value was changed
        }

        // Common error handling properties
        private string _errorMessage;
        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        private bool _hasError;
        public bool HasError
        {
            get => _hasError;
            set => SetProperty(ref _hasError, value);
        }

        private string _errorDetails;
        public string ErrorDetails
        {
            get => _errorDetails;
            set => SetProperty(ref _errorDetails, value);
        }

        private AuthenticationErrorType _authErrorType = AuthenticationErrorType.None;
        public AuthenticationErrorType AuthErrorType
        {
            get => _authErrorType;
            set => SetProperty(ref _authErrorType, value);
        }

        // Error handling helper methods
        protected void SetError(string errorMessage)
        {
            ErrorMessage = errorMessage;
            HasError = !string.IsNullOrEmpty(errorMessage);
        }

        protected void SetDetailedError(
            string errorMessage,
            string details,
            AuthenticationErrorType authErrorType = AuthenticationErrorType.None
        )
        {
            ErrorMessage = errorMessage;
            ErrorDetails = details;
            AuthErrorType = authErrorType;
            HasError = !string.IsNullOrEmpty(errorMessage);
        }

        protected void ClearError()
        {
            ErrorMessage = null;
            ErrorDetails = null;
            AuthErrorType = AuthenticationErrorType.None;
            HasError = false;
        } // Safe execution method for commands

        protected async Task<bool> ExecuteSafelyAsync(
            Func<Task> action,
            string errorPrefix = "Error: "
        )
        {
            try
            {
                ClearError();
                await action();
                return true;
            }
            catch (Exception ex)
            {
                SetError($"{errorPrefix}{ex.Message}");
                return false;
            }
        }

        // Safe execution method for synchronous operations
        protected bool ExecuteSafely(Action action, string errorPrefix = "Error: ")
        {
            try
            {
                ClearError();
                action();
                return true;
            }
            catch (Exception ex)
            {
                SetError($"{errorPrefix}{ex.Message}");
                return false;
            }
        }
    }
}
