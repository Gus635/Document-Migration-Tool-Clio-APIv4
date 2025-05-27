using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ClioDataMigrator.View.Converters
{
    /// Converts a null/non-null value to Visibility.Collapsed/Visibility.Visible
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isNull = value == null;

            // Check if we should invert the behavior
            if (
                parameter is string param
                && param.Equals("Invert", StringComparison.OrdinalIgnoreCase)
            )
            {
                isNull = !isNull;
            }

            return isNull ? Visibility.Collapsed : Visibility.Visible;
        }

        /// Not implemented as this is a one-way converter
        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture
        )
        {
            throw new NotImplementedException(
                "NullToVisibilityConverter only supports one-way conversion."
            );
        }
    }
}
