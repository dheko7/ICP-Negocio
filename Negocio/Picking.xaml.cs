using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;

namespace ICP.Negocio
{
    public partial class Picking : Window
    {
        private ObservableCollection<Pedido> pedidos = new ObservableCollection<Pedido>();
        private Pedido pedidoSeleccionado;
        private Picada picadaActual;
        private int lineasPicadas;
        private int totalLineas;

        private const string ConnectionString =
            "Server=localhost\\SQLEXPRESS01;Database=bdsaid;Integrated Security=True;MultipleActiveResultSets=True;";
        private const string Usuario = "USER01";

        public Picking()
        {
            InitializeComponent();
            dgPedidos.ItemsSource = pedidos;
            CargarPedidos();
        }

        private void CargarPedidos()
        {
            pedidos.Clear();
            using (var conn = new SqlConnection(ConnectionString))
            using (var cmd = new SqlCommand(@"
                SELECT PETICION,
                       CODIGO_CLIENTE,
                       ESTATUS_PETICION,
                       (SELECT COUNT(*) 
                          FROM ORDEN_SALIDA_LIN 
                         WHERE PETICION = OSC.PETICION) AS TOTAL_LINEAS
                  FROM ORDEN_SALIDA_CAB OSC
                 WHERE ESTATUS_PETICION = 1
                 ORDER BY PETICION", conn))
            {
                conn.Open();
                using (var rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        pedidos.Add(new Pedido
                        {
                            Peticion = (int)rdr["PETICION"],
                            NombreCliente = rdr["CODIGO_CLIENTE"].ToString(),
                            EstatusPeticion = (int)rdr["ESTATUS_PETICION"],
                            TotalLineas = (int)rdr["TOTAL_LINEAS"]
                        });
                    }
                }
            }
        }

        private void DgPedidos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgPedidos.SelectedItem is Pedido p)
            {
                pedidoSeleccionado = p;
                txtPeticion.Text = p.Peticion.ToString();
                txtCliente.Text = p.NombreCliente;
                txtEstado.Text = p.EstatusPeticion.ToString();
                txtTotalLineas.Text = p.TotalLineas.ToString();
                btnIniciarPicking.IsEnabled = true;

                lineasPicadas = 0;
                totalLineas = p.TotalLineas;
                pbProgreso.Value = 0;
                txtProgreso.Text = "0% completado";

                DeshabilitarControlesPicada();
                btnConfirmarPedido.IsEnabled = false;
            }
        }

        private void BtnIniciarPicking_Click(object sender, RoutedEventArgs e)
        {
            if (pedidoSeleccionado == null || pedidoSeleccionado.EstatusPeticion != 1)
            {
                MessageBox.Show("El pedido no está en estado adecuado para picking.", "Error");
                return;
            }

            if (!VerificarStockParaPedido(pedidoSeleccionado.Peticion))
                return;

            // Iniciamos flujo de picada
            CargarPicadaActual();
        }

        private bool VerificarStockParaPedido(int peticion)
        {
            const string sql = @"
                SELECT osl.REFERENCIA,
                       osl.CANTIDAD - ISNULL(osl.CANTIDAD_PICADA,0) AS CANT_NEC,
                       ISNULL(vw.STOCK_DISPONIBLE,0) AS STOCK_DISPONIBLE
                  FROM ORDEN_SALIDA_LIN osl
             LEFT JOIN VW_STOCK_DISPONIBLE vw
                    ON vw.REFERENCIA = osl.REFERENCIA
                 WHERE osl.PETICION = @PETICION";

            using (var conn = new SqlConnection(ConnectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.Add("@PETICION", SqlDbType.Int).Value = peticion;
                conn.Open();
                using (var rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        int nec = (int)rdr["CANT_NEC"];
                        int disp = (int)rdr["STOCK_DISPONIBLE"];
                        if (disp < nec)
                        {
                            MessageBox.Show(
                                $"Stock insuficiente para {rdr["REFERENCIA"]}.\nDisponible: {disp}, Necesario: {nec}",
                                "Stock insuficiente",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        private void CargarPicadaActual()
        {
            // Sacamos la siguiente línea pendiente de picar
            using (var conn = new SqlConnection(ConnectionString))
            using (var cmd = new SqlCommand("SELECT * FROM FN_OBTENER_SIGUIENTE_PICADA(@PETICION)", conn))
            {
                cmd.Parameters.Add("@PETICION", SqlDbType.Int).Value = pedidoSeleccionado.Peticion;
                conn.Open();
                using (var rdr = cmd.ExecuteReader())
                {
                    if (!rdr.Read())
                    {
                        // Ya no quedan líneas
                        picadaActual = null;
                        DeshabilitarControlesPicada();
                        VerificarEstadoPedido();
                        return;
                    }

                    picadaActual = new Picada
                    {
                        Peticion = (int)rdr["PETICION"],
                        LineaId = (int)rdr["LINEA"],
                        Referencia = rdr["REFERENCIA"].ToString(),
                        Cantidad = (int)rdr["CANTIDAD"],
                        Ubicacion = rdr["UBICACION"].ToString(),
                        RequiereNumeroSerie = (bool)rdr["REQUIERE_NUMERO_SERIE"]
                    };
                }

                // Obtenemos stock disponible para esa ubicación
                using (var stockCmd = new SqlCommand(@"
                    SELECT ISNULL(SUM(STOCK_DISPONIBLE),0)
                      FROM VW_STOCK_DISPONIBLE
                     WHERE REFERENCIA = @REF
                       AND UBICACION  = @UBI", conn))
                {
                    stockCmd.Parameters.AddWithValue("@REF", picadaActual.Referencia);
                    stockCmd.Parameters.AddWithValue("@UBI", picadaActual.Ubicacion);
                    picadaActual.StockDisponible = Convert.ToInt32(stockCmd.ExecuteScalar());
                }
            }

            // Refrescamos UI
            txtReferencia.Text = picadaActual.Referencia;
            txtCantidadRequerida.Text = picadaActual.Cantidad.ToString();
            txtUbicacion.Text = picadaActual.Ubicacion;
            txtStockDisponible.Text = picadaActual.StockDisponible.ToString();
            txtCantidadPicar.Text = picadaActual.Cantidad.ToString();
            panelNumeroSerie.Visibility = picadaActual.RequiereNumeroSerie
                                          ? Visibility.Visible
                                          : Visibility.Collapsed;
            txtNumeroSerie.Text = "";
            HabilitarControlesPicada();
        }

        private void BtnConfirmarPicada_Click(object sender, RoutedEventArgs e)
        {
            if (picadaActual == null) return;

            if (!int.TryParse(txtCantidadPicar.Text, out int qty)
                || qty <= 0
                || qty > picadaActual.Cantidad)
            {
                MessageBox.Show("Cantidad inválida.", "Error");
                return;
            }
            if (picadaActual.RequiereNumeroSerie
                && string.IsNullOrWhiteSpace(txtNumeroSerie.Text))
            {
                MessageBox.Show("Se requiere número de serie.", "Error");
                return;
            }

            // Llamamos al SP de picada
            using (var conn = new SqlConnection(ConnectionString))
            using (var cmd = new SqlCommand("SP_MARCAR_PICADA", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@PETICION", picadaActual.Peticion);
                cmd.Parameters.AddWithValue("@LINEA_ID", picadaActual.LineaId);
                cmd.Parameters.AddWithValue("@CANTIDAD_PICADA", qty);
                cmd.Parameters.AddWithValue("@UBICACION", picadaActual.Ubicacion);
                cmd.Parameters.AddWithValue("@NUMERO_SERIE", (object)txtNumeroSerie.Text ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@USUARIO", Usuario);
                conn.Open();
                cmd.ExecuteNonQuery();
            }

            lineasPicadas++;
            CargarPicadaActual();
        }

        private void VerificarEstadoPedido()
        {
            double porcentaje = totalLineas == 0
                ? 100
                : (double)lineasPicadas / totalLineas * 100;

            pbProgreso.Value = porcentaje;
            txtProgreso.Text = $"{porcentaje:F1}% completado";

            // Cuando haya picado todas las líneas...
            btnConfirmarPedido.IsEnabled = (lineasPicadas >= totalLineas);
        }

        private void BtnConfirmarPedido_Click(object sender, RoutedEventArgs e)
        {
            if (pedidoSeleccionado == null) return;

            using (var conn = new SqlConnection(ConnectionString))
            using (var cmd = new SqlCommand(
                "UPDATE ORDEN_SALIDA_CAB " +
                "   SET ESTATUS_PETICION = 2 " +
                " WHERE PETICION = @PETICION", conn))
            {
                cmd.Parameters.AddWithValue("@PETICION", pedidoSeleccionado.Peticion);
                conn.Open();
                cmd.ExecuteNonQuery();
            }

            MessageBox.Show("Pedido marcado como ‘Ejecutado’ (2).", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            CargarPedidos();
            DeshabilitarControlesPicada();
            btnConfirmarPedido.IsEnabled = false;
            btnIniciarPicking.IsEnabled = false;
        }

        private void HabilitarControlesPicada()
        {
            txtCantidadPicar.IsEnabled = true;
            btnConfirmarPicada.IsEnabled = true;
            txtNumeroSerie.IsEnabled = picadaActual.RequiereNumeroSerie;
        }

        private void DeshabilitarControlesPicada()
        {
            txtReferencia.Clear();
            txtCantidadRequerida.Clear();
            txtUbicacion.Clear();
            txtStockDisponible.Clear();
            txtCantidadPicar.Clear();
            txtNumeroSerie.Clear();
            txtNumeroSerie.IsEnabled = false;
            btnConfirmarPicada.IsEnabled = false;
        }

        private void BtnVolver_Click(object sender, RoutedEventArgs e)
        {
            new MenuPrincipal().Show();
            this.Close();
        }
    }

    public class Pedido
    {
        public int Peticion { get; set; }
        public string NombreCliente { get; set; }
        public int EstatusPeticion { get; set; }
        public int TotalLineas { get; set; }
    }

    public class Picada
    {
        public int Peticion { get; set; }
        public int LineaId { get; set; }
        public string Referencia { get; set; }
        public int Cantidad { get; set; }
        public string Ubicacion { get; set; }
        public int StockDisponible { get; set; }
        public bool RequiereNumeroSerie { get; set; }
    }
}
