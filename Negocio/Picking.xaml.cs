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
            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    SqlCommand cmd = new SqlCommand(@"
                        SELECT PETICION,
                               CODIGO_CLIENTE,
                               ESTATUS_PETICION,
                               (SELECT COUNT(*) FROM ORDEN_SALIDA_LIN WHERE PETICION = OSC.PETICION) AS TOTAL_LINEAS
                          FROM ORDEN_SALIDA_CAB OSC
                         ORDER BY PETICION", conn);
                    conn.Open();
                    using (SqlDataReader rdr = cmd.ExecuteReader())
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
            catch (Exception ex)
            {
                MessageBox.Show("Error cargando pedidos:\n" + ex.Message,
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DgPedidos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(dgPedidos.SelectedItem is Pedido p)) return;
            pedidoSeleccionado = p;

            txtPeticion.Text = p.Peticion.ToString();
            txtCliente.Text = p.NombreCliente;
            txtEstado.Text = p.EstatusNombre;
            txtTotalLineas.Text = p.TotalLineas.ToString();

            // Solo activo si está Pendiente (1)
            btnIniciarPicking.IsEnabled = (p.EstatusPeticion == 1);
            btnConfirmarPedido.IsEnabled = false;

            lineasPicadas = 0;
            totalLineas = p.TotalLineas;
            pbProgreso.Value = 0;
            txtProgreso.Text = "0% completado";

            DeshabilitarControlesPicada();
        }

        private void BtnIniciarPicking_Click(object sender, RoutedEventArgs e)
        {
            if (pedidoSeleccionado == null) return;
            if (pedidoSeleccionado.EstatusPeticion != 1)
            {
                MessageBox.Show("Sólo pedidos pendientes (1).",
                                "Estado inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!VerificarStockParaPedido(pedidoSeleccionado.Peticion)) return;
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

            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@PETICION", peticion);
                conn.Open();
                using (SqlDataReader rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        int nec = (int)rdr["CANT_NEC"];
                        int disp = (int)rdr["STOCK_DISPONIBLE"];
                        if (disp < nec)
                        {
                            MessageBox.Show(
                                $"Stock insuficiente para {rdr["REFERENCIA"]}.\nDisp: {disp}, Nec: {nec}",
                                "Stock insuficiente",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        private void CargarPicadaActual()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    conn.Open();

                    // 1) Siguiente línea pendiente
                    string sql = @"
                        SELECT TOP 1
                            osl.PETICION,
                            osl.LINEA,
                            osl.REFERENCIA,
                            osl.CANTIDAD - ISNULL(osl.CANTIDAD_PICADA,0) AS CANTIDAD,
                            r.LLEVA_N_SERIES AS REQUIERE_NUMERO_SERIE
                        FROM ORDEN_SALIDA_LIN osl
                        JOIN REFERENCIAS r ON r.REFERENCIA = osl.REFERENCIA
                        WHERE osl.PETICION = @PETICION
                          AND osl.CANTIDAD > ISNULL(osl.CANTIDAD_PICADA,0)
                        ORDER BY osl.LINEA";

                    SqlCommand cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@PETICION", pedidoSeleccionado.Peticion);

                    using (SqlDataReader rdr = cmd.ExecuteReader())
                    {
                        if (!rdr.Read())
                        {
                            MessageBox.Show("No quedan líneas por picar.",
                                            "Picking completado",
                                            MessageBoxButton.OK, MessageBoxImage.Information);
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
                            RequiereNumeroSerie = (bool)rdr["REQUIERE_NUMERO_SERIE"]
                        };
                    }

                    // 2) Ubicación + stock desde la vista
                    string stockSql = @"
                        SELECT TOP 1 UBICACION, STOCK_DISPONIBLE
                          FROM VW_STOCK_DISPONIBLE
                         WHERE REFERENCIA = @REF
                           AND STOCK_DISPONIBLE > 0
                         ORDER BY STOCK_DISPONIBLE DESC";

                    SqlCommand stockCmd = new SqlCommand(stockSql, conn);
                    stockCmd.Parameters.AddWithValue("@REF", picadaActual.Referencia);

                    using (SqlDataReader sr = stockCmd.ExecuteReader())
                    {
                        if (sr.Read())
                        {
                            picadaActual.Ubicacion = sr["UBICACION"].ToString();
                            picadaActual.StockDisponible = (int)sr["STOCK_DISPONIBLE"];
                        }
                        else
                        {
                            picadaActual.Ubicacion = "";
                            picadaActual.StockDisponible = 0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error en CargarPicadaActual:\n" + ex.Message,
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Refrescar UI
            txtReferencia.Text = picadaActual.Referencia;
            txtCantidadRequerida.Text = picadaActual.Cantidad.ToString();
            txtUbicacion.Text = picadaActual.Ubicacion;
            txtStockDisponible.Text = picadaActual.StockDisponible.ToString();
            txtCantidadPicar.Text = picadaActual.Cantidad.ToString();
            panelNumeroSerie.Visibility =
                picadaActual.RequiereNumeroSerie
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            txtNumeroSerie.Clear();
            HabilitarControlesPicada();
        }

        private void BtnConfirmarPicada_Click(object sender, RoutedEventArgs e)
        {
            if (picadaActual == null) return;

            int qty;
            if (!int.TryParse(txtCantidadPicar.Text, out qty) ||
                qty <= 0 ||
                qty > picadaActual.Cantidad)
            {
                MessageBox.Show("Cantidad a picar inválida.",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (picadaActual.RequiereNumeroSerie &&
                string.IsNullOrWhiteSpace(txtNumeroSerie.Text))
            {
                MessageBox.Show("Se requiere número de serie.",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Llamada al SP que controla línea, log y cierre
            using (var conn = new SqlConnection(ConnectionString))
            using (var cmd = new SqlCommand("SP_MARCAR_PICADA", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@PETICION", picadaActual.Peticion);
                cmd.Parameters.AddWithValue("@LINEA_ID", picadaActual.LineaId);
                cmd.Parameters.AddWithValue("@CANTIDAD_PICADA", qty);
                cmd.Parameters.AddWithValue("@UBICACION", picadaActual.Ubicacion);

                // ← Aquí añades el parámetro del n-serie
                cmd.Parameters.AddWithValue("@NUMERO_SERIE", (object)txtNumeroSerie.Text ?? DBNull.Value);

                cmd.Parameters.AddWithValue("@USUARIO", Usuario);

                conn.Open();
                cmd.ExecuteNonQuery();
            }

            lineasPicadas++;
            VerificarEstadoPedido();    // <— habilita ConfirmarPedido cuando toque
            CargarPicadaActual();
        }

        private void VerificarEstadoPedido()
        {
            double porcentaje;
            if (totalLineas == 0)
                porcentaje = 100;
            else
                porcentaje = (double)lineasPicadas / totalLineas * 100;

            pbProgreso.Value = porcentaje;
            txtProgreso.Text = porcentaje.ToString("F1") + "% completado";

            // Habilitamos ConfirmarPedido sólo al acabar
            btnConfirmarPedido.IsEnabled = (lineasPicadas >= totalLineas);
        }

        private void BtnConfirmarPedido_Click(object sender, RoutedEventArgs e)
        {
            // Recargamos la lista (ahora ya con ESTATUS actualizado)
            CargarPedidos();
            DeshabilitarControlesPicada();
            btnIniciarPicking.IsEnabled = false;
            btnConfirmarPedido.IsEnabled = false;
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
        public string EstatusNombre
        {
            get
            {
                switch (EstatusPeticion)
                {
                    case 1: return "Pendiente";
                    case 2: return "En Proceso";
                    case 3: return "Ejecutado";
                    case 4: return "Comprobado";
                    case 5: return "Enviado";
                    default: return "Desconocido";
                }
            }
        }
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
