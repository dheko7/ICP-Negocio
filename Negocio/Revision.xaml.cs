using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ICP.Negocio
{
    public partial class Revision : Window
    {
        private const string cs =
            "Server=localhost\\SQLEXPRESS01;Database=bdsaid;Integrated Security=True;";

        private readonly ObservableCollection<LineaPedido> _lineas = new ObservableCollection<LineaPedido>();

        public Revision()
        {
            InitializeComponent();
            dgLineas.ItemsSource = _lineas;
            RefreshPedidos();
        }

        private void RefreshPedidos(int selPet = 0)
        {
            dgPedidos.ItemsSource = LoadPedidos();
            if (selPet != 0)
                dgPedidos.SelectedItem = dgPedidos.Items
                    .Cast<PedidoCab>()
                    .FirstOrDefault(p => p.Peticion == selPet);
        }

        private ObservableCollection<PedidoCab> LoadPedidos()
        {
            var lista = new ObservableCollection<PedidoCab>();
            using (var conn = new SqlConnection(cs))
            using (var cmd = new SqlCommand(@"
                SELECT PETICION, F_CREACION, CODIGO_CLIENTE, ESTATUS_PETICION,
                       (SELECT COUNT(*) FROM ORDEN_SALIDA_LIN WHERE PETICION=cab.PETICION)
                  FROM ORDEN_SALIDA_CAB cab
                 WHERE ESTATUS_PETICION IN (2,3)
                 ORDER BY F_CREACION DESC", conn))
            {
                conn.Open();
                using (var rdr = cmd.ExecuteReader())
                    while (rdr.Read())
                        lista.Add(new PedidoCab
                        {
                            Peticion = rdr.GetInt32(0),
                            Fecha = rdr.GetDateTime(1),
                            CodigoCliente = rdr.GetString(2),
                            Estatus = rdr.GetInt32(3),
                            TotalLineas = rdr.GetInt32(4)
                        });
            }
            return lista;
        }

        private void DgPedidos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _lineas.Clear();
            btnEnviar.IsEnabled = false;
            txtEstado.Text = "";

            if (!(dgPedidos.SelectedItem is PedidoCab ped)) return;

            // Cargo líneas
            using (var conn = new SqlConnection(cs))
            using (var cmd = new SqlCommand(@"
                SELECT LINEA, REFERENCIA, CANTIDAD, ISNULL(CANTIDAD_PICADA,0)
                  FROM ORDEN_SALIDA_LIN
                 WHERE PETICION=@p
                 ORDER BY LINEA", conn))
            {
                cmd.Parameters.AddWithValue("@p", ped.Peticion);
                conn.Open();
                using (var rdr = cmd.ExecuteReader())
                    while (rdr.Read())
                        _lineas.Add(new LineaPedido
                        {
                            Linea = rdr.GetInt32(0),
                            Referencia = rdr.GetString(1),
                            Cantidad = rdr.GetInt32(2),
                            Picada = rdr.GetInt32(3),
                            Confirmado = (ped.Estatus == 3)
                        });
            }

            // Muestro estado textual
            txtEstado.Text = ped.Estatus == 2 ? "En Proceso" : "Ejecutado";
            // Sólo habilito Enviar si ya está Ejecutado (3)
            btnEnviar.IsEnabled = (ped.Estatus == 3);
        }

        private void BtnConfirmarLinea_Click(object sender, RoutedEventArgs e)
        {
            var lin = (LineaPedido)((Button)sender).DataContext;
            lin.Confirmado = true;
            dgLineas.Items.Refresh();

            // Si todas confirmadas y estaba en 2 → lo marco 3 “Ejecutado”
            if (_lineas.All(x => x.Confirmado)
                && dgPedidos.SelectedItem is PedidoCab ped
                && ped.Estatus == 2)
            {
                using (var conn = new SqlConnection(cs))
                using (var cmd = new SqlCommand(
                    "UPDATE ORDEN_SALIDA_CAB SET ESTATUS_PETICION=3 WHERE PETICION=@p", conn))
                {
                    cmd.Parameters.AddWithValue("@p", ped.Peticion);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
                RefreshPedidos(ped.Peticion);
            }
        }

        private void BtnEnviar_Click(object sender, RoutedEventArgs e)
        {
            if (!(dgPedidos.SelectedItem is PedidoCab ped) || ped.Estatus != 3)
                return;

            if (MessageBox.Show(
                    $"¿Marcar pedido {ped.Peticion} como ENVIADO?",
                    "Enviar al transporte",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            using (var conn = new SqlConnection(cs))
            using (var cmd = new SqlCommand(@"
                UPDATE ORDEN_SALIDA_CAB
                   SET ESTATUS_PETICION=4,
                       F_CONFIRMACION=GETDATE()
                 WHERE PETICION=@p", conn))
            {
                cmd.Parameters.AddWithValue("@p", ped.Peticion);
                conn.Open();
                cmd.ExecuteNonQuery();
            }

            MessageBox.Show("Pedido marcado como ENVIADO.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            RefreshPedidos(ped.Peticion);
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
                    case 2: return "En Proceso";
                    case 3: return "Ejecutado";
                    case 4: return "Enviado";
                    default: return "Desconocido";
                }
            }
        }
    }

    public class LineaPedido : INotifyPropertyChanged
    {
        public int Linea { get; set; }
        public string Referencia { get; set; }
        public int Cantidad { get; set; }
        public int Picada { get; set; }

        private bool _confirmado;
        public bool Confirmado
        {
            get { return _confirmado; }
            set { _confirmado = value; OnPropertyChanged("Confirmado"); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string prop)
          => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
