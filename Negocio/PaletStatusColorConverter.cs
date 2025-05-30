using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ICP.Negocio
{
    public class PaletStatusColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int status)
            {
                switch (status)
                {
                    case 0: return Brushes.LightGray;
                    case 1: return Brushes.Gold;
                    case 2: return Brushes.LightCoral;
                    case 3: return Brushes.LightSkyBlue;
                    case 4: return Brushes.LightGreen;
                    case 5: return Brushes.LightBlue;
                    case 6: return Brushes.LightSalmon;
                    default: return Brushes.Transparent;
                }
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
