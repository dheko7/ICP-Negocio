using System;
using System.Globalization;
using System.Windows.Data;

namespace ICP.Negocio
{
    public class PaletStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int status)
            {
                switch (status)
                {
                    case 0: return "Libre";
                    case 1: return "Sin Liberar";
                    case 2: return "Bloqueado";
                    case 3: return "Asignado";
                    case 4: return "Ejecutado";
                    case 5: return "Revisado";
                    case 6: return "Enviado";
                    default: return "Desconocido";
                }
            }
            return "Desconocido";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
