using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ICP.Negocio
{
    public partial class Revision : Window
    {
        private const string cs =
            "Server=localhost\\SQLEXPRESS01;Database=bdsaid;Integrated Security=True;MultipleActiveResultSets=True;";

        private ObservableCollection<LineaPedido> _lineas = new ObservableCollection<LineaPedido>();

        public Revision()
        {
            InitializeComponent();
            dgPedidos.ItemsSource = LoadPedidos();
            dgLineas.ItemsSource = _lineas;
        }

        private ObservableCollection<PedidoCab> LoadPedidos()
        {
            var list = new ObservableCollection<PedidoCab>();
            using (var conn = new SqlConnection(cs))
            using (var cmd = new SqlCommand(@"
                SELECT PETICION,
                       F_CREACION,
                       CODIGO_CLIENTE,
                       ESTATUS_PETICION,
                       (SELECT COUNT(*) FROM ORDEN_SALIDA_LIN WHERE PETICION = cab.PETICION) AS TotalLineas
                  FROM ORDEN_SALIDA_CAB cab
                 WHERE ESTATUS_PETICION IN (2,3)
                 ORDER BY F_CREACION DESC", conn))
            {
                conn.Open();
                using (var rdr = cmd.ExecuteReader())
                    while (rdr.Read())
                        list.Add(new PedidoCab
                        {
                            Peticion = rdr.GetInt32(0),
                            Fecha = rdr.GetDateTime(1),
                            CodigoCliente = rdr.GetString(2),
                            Estatus = rdr.GetInt32(3),
                            TotalLineas = rdr.GetInt32(4)
                        });
            }
            return list;
        }

        private void DgPedidos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _lineas.Clear();
            btnEnviar.IsEnabled = false;
            txtEstado.Text = "";

            var sel = dgPedidos.SelectedItem as PedidoCab;
            if (sel == null) return;

            // Si ya está enviado (3), deshabilitamos confirmación
            bool esEnviado = sel.Estatus == 3;

            using (var conn = new SqlConnection(cs))
            using (var cmd = new SqlCommand(@"
                SELECT LINEA, REFERENCIA, CANTIDAD, ISNULL(CANTIDAD_PICADA,0) AS Picada
                  FROM ORDEN_SALIDA_LIN
                 WHERE PETICION = @pet
                 ORDER BY LINEA", conn))
            {
                cmd.Parameters.AddWithValue("@pet", sel.Peticion);
                conn.Open();
                using (var rdr = cmd.ExecuteReader())
                    while (rdr.Read())
                        _lineas.Add(new LineaPedido
                        {
                            Linea = rdr.GetInt32(0),
                            Referencia = rdr.GetString(1),
                            Cantidad = rdr.GetInt32(2),
                            Picada = rdr.GetInt32(3),
                            Confirmado = esEnviado // si ya está enviado, marcamos todas confirmadas
                        });
            }

            // Si estaba envío (3), no permitimos volver a enviar
            if (esEnviado)
            {
                txtEstado.Text = "Este pedido ya fue enviado.";
                btnEnviar.IsEnabled = false;
            }
        }

        private void BtnConfirmarLinea_Click(object sender, RoutedEventArgs e)
        {
            var lin = (LineaPedido)((Button)sender).DataContext;
            lin.Confirmado = true;
            dgLineas.Items.Refresh();

            if (_lineas.All(x => x.Confirmado))
            {
                txtEstado.Text = "Todas las líneas comprobadas.";
                btnEnviar.IsEnabled = true;
            }
        }

        private void BtnEnviar_Click(object sender, RoutedEventArgs e)
        {
            var sel = dgPedidos.SelectedItem as PedidoCab;
            if (sel == null) return;

            if (MessageBox.Show(
                $"¿Marcar pedido {sel.Peticion} como ENVIADO?",
                "Enviar al transporte",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            using (var conn = new SqlConnection(cs))
            using (var cmd = new SqlCommand(@"
                UPDATE ORDEN_SALIDA_CAB
                   SET ESTATUS_PETICION = 3,
                       F_CONFIRMACION   = GETDATE()
                 WHERE PETICION = @pet", conn))
            {
                cmd.Parameters.AddWithValue("@pet", sel.Peticion);
                conn.Open();
                cmd.ExecuteNonQuery();
            }

            MessageBox.Show("Pedido marcado como ENVIADO.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);

            // Recargo la lista con estado 2 y 3, para que el enviado siga viéndose
            dgPedidos.ItemsSource = LoadPedidos();
            // mantengo el mismo seleccionado
            dgPedidos.SelectedItem = dgPedidos.Items.Cast<PedidoCab>()
                                        .FirstOrDefault(x => x.Peticion == sel.Peticion);
        }

        private void BtnVolver_Click(object sender, RoutedEventArgs e)
        {
            new MenuPrincipal().Show();
            Close();
        }
    }

    public class PedidoCab
    {
        public int Peticion { get; set; }
        public DateTime Fecha { get; set; }
        public string CodigoCliente { get; set; }
        public int Estatus { get; set; }
        public int TotalLineas { get; set; }

        public string EstadoNombre
        {
            get
            {
                switch (Estatus)
                {
                    case 2: return "Ejecutado";
                    case 3: return "Enviado";
                    default: return "Desconocido";
                }
            }
        }
    }

    public class LineaPedido
    {
        public int Linea { get; set; }
        public string Referencia { get; set; }
        public int Cantidad { get; set; }
        public int Picada { get; set; }
        public bool Confirmado { get; set; }
    }
}
