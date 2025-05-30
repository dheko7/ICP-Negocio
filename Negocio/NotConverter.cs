using System;
using System.Globalization;
using System.Windows.Data;

namespace ICP.Negocio
{
    [ValueConversion(typeof(bool), typeof(bool))]
    public class NotConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : false;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : false;
    }
}
