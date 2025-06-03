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

        /// <summary>
        ///  Carga la lista de pedidos (cabecera) en el DataGrid dgPedidos.
        /// </summary>
        private void CargarPedidos()
        {
            pedidos.Clear();
            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    string sql = @"
                        SELECT 
                            PETICION,
                            CODIGO_CLIENTE,
                            ESTATUS_PETICION,
                            (SELECT COUNT(*) 
                               FROM ORDEN_SALIDA_LIN 
                              WHERE PETICION = OSC.PETICION) AS TOTAL_LINEAS
                          FROM ORDEN_SALIDA_CAB OSC
                         ORDER BY PETICION";

                    SqlCommand cmd = new SqlCommand(sql, conn);
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

        /// <summary>
        ///  Cuando el usuario selecciona un pedido en el DataGrid,
        ///  cargamos la cabecera y habilitamos/deshabilitamos botones.
        /// </summary>
        private void DgPedidos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(dgPedidos.SelectedItem is Pedido p)) return;
            pedidoSeleccionado = p;

            // Rellenamos los TextBox de la cabecera con Petición, Cliente, Estado y Total Líneas
            txtPeticion.Text = p.Peticion.ToString();
            txtCliente.Text = p.NombreCliente;
            txtEstado.Text = p.EstatusNombre;
            txtTotalLineas.Text = p.TotalLineas.ToString();

            // Cargar Fecha Creación y Dirección Entrega de la tabla ORDEN_SALIDA_CAB
            CargarDatosCabecera(p.Peticion);

            // Sólo se habilita “Iniciar Picking” si el estado es Pendiente (1)
            btnIniciarPicking.IsEnabled = (p.EstatusPeticion == 1);
            btnConfirmarPedido.IsEnabled = false;

            lineasPicadas = 0;
            totalLineas = p.TotalLineas;
            pbProgreso.Value = 0;
            txtProgreso.Text = "0% completado";

            DeshabilitarControlesPicada();
        }

        /// <summary>
        ///  Lee la fecha de creación y la dirección de entrega de ORDEN_SALIDA_CAB
        ///  para el pedido dado. Quitar cualquier columna que no exista (por ejemplo, TELEFONO_CONTACTO).
        /// </summary>
        private void CargarDatosCabecera(int peticion)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    conn.Open();

                    // Nota: ya NO pedimos TELEFONO_CONTACTO, solo F_CREACION y DIRECCION_ENTREGA
                    string sqlCab = @"
                        SELECT 
                            F_CREACION,
                            DIRECCION_ENTREGA
                          FROM ORDEN_SALIDA_CAB
                         WHERE PETICION = @PETICION";

                    using (SqlCommand cmdCab = new SqlCommand(sqlCab, conn))
                    {
                        cmdCab.Parameters.AddWithValue("@PETICION", peticion);

                        using (SqlDataReader rdr = cmdCab.ExecuteReader())
                        {
                            if (rdr.Read())
                            {
                                // Fecha de creación
                                if (!rdr.IsDBNull(rdr.GetOrdinal("F_CREACION")))
                                {
                                    DateTime fecha = (DateTime)rdr["F_CREACION"];
                                    txtFechaCreacion.Text = fecha.ToString("dd/MM/yyyy");
                                }
                                else
                                {
                                    txtFechaCreacion.Text = "";
                                }

                                // Dirección de entrega
                                if (!rdr.IsDBNull(rdr.GetOrdinal("DIRECCION_ENTREGA")))
                                {
                                    txtDireccionEntrega.Text = rdr["DIRECCION_ENTREGA"].ToString();
                                }
                                else
                                {
                                    txtDireccionEntrega.Text = "";
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error cargando datos de cabecera:\n" + ex.Message,
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        ///  Botón “Iniciar Picking”: verifica stock y carga la primera línea pendiente.
        /// </summary>
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

        /// <summary>
        ///  Verifica que haya stock suficiente (comparando ORDEN_SALIDA_LIN y VW_STOCK_DISPONIBLE).
        ///  Si falta stock en alguna referencia, muestra un mensaje y devuelve false.
        /// </summary>
        private bool VerificarStockParaPedido(int peticion)
        {
            const string sql = @"
                SELECT 
                    osl.REFERENCIA,
                    osl.CANTIDAD - ISNULL(osl.CANTIDAD_PICADA, 0) AS CANT_NEC,
                    ISNULL(vw.STOCK_DISPONIBLE, 0) AS STOCK_DISPONIBLE
                  FROM ORDEN_SALIDA_LIN osl
             LEFT JOIN VW_STOCK_DISPONIBLE vw
                    ON vw.REFERENCIA = osl.REFERENCIA
                 WHERE osl.PETICION = @PETICION";

            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
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
            }
            return true;
        }

        /// <summary>
        ///  Carga la “picada actual”: la siguiente línea pendiente de ORDEN_SALIDA_LIN para este pedido.
        ///  Rellena txtReferencia, txtCantidadRequerida, txtUbicacion, txtStockDisponible, txtCantidadPicar.
        ///  También muestra el panel de número de serie si la referencia lo requiere.
        /// </summary>
        private void CargarPicadaActual()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(ConnectionString))
                {
                    conn.Open();

                    // 1) Obtener la siguiente línea a picar
                    string sql = @"
                        SELECT TOP 1
                            osl.PETICION,
                            osl.LINEA,
                            osl.REFERENCIA,
                            (osl.CANTIDAD - ISNULL(osl.CANTIDAD_PICADA, 0)) AS CANTIDAD,
                            r.LLEVA_N_SERIES AS REQUIERE_NUMERO_SERIE
                          FROM ORDEN_SALIDA_LIN osl
                          JOIN REFERENCIAS r 
                            ON r.REFERENCIA = osl.REFERENCIA
                         WHERE osl.PETICION = @PETICION
                           AND (osl.CANTIDAD - ISNULL(osl.CANTIDAD_PICADA,0)) > 0
                         ORDER BY osl.LINEA";

                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@PETICION", pedidoSeleccionado.Peticion);
                        using (SqlDataReader rdr = cmd.ExecuteReader())
                        {
                            if (!rdr.Read())
                            {
                                // Ya no quedan líneas por picar
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
                    }

                    // 2) Ahora traemos ubicación + stock disponible desde la vista VW_STOCK_DISPONIBLE
                    string stockSql = @"
                        SELECT TOP 1 
                            UBICACION, STOCK_DISPONIBLE
                          FROM VW_STOCK_DISPONIBLE
                         WHERE REFERENCIA = @REF
                           AND STOCK_DISPONIBLE > 0
                         ORDER BY STOCK_DISPONIBLE DESC";

                    using (SqlCommand stockCmd = new SqlCommand(stockSql, conn))
                    {
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
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error en CargarPicadaActual:\n" + ex.Message,
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 3) Refrescar la interfaz con los datos de picadaActual
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

        /// <summary>
        ///  Botón “Confirmar Picada”: invoca SP_MARCAR_PICADA con los datos de la línea actual.
        ///  Luego avanza a la siguiente línea, y finalmente habilita “Confirmar Pedido” si todas las líneas están hechas.
        /// </summary>
        private void BtnConfirmarPicada_Click(object sender, RoutedEventArgs e)
        {
            if (picadaActual == null) return;

            if (!int.TryParse(txtCantidadPicar.Text, out int qty) ||
                qty <= 0 || qty > picadaActual.Cantidad)
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

            // Llamada al SP que marca la picada
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            using (SqlCommand cmd = new SqlCommand("SP_MARCAR_PICADA", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@PETICION", picadaActual.Peticion);
                cmd.Parameters.AddWithValue("@LINEA_ID", picadaActual.LineaId);
                cmd.Parameters.AddWithValue("@CANTIDAD_PICADA", qty);
                cmd.Parameters.AddWithValue("@UBICACION", picadaActual.Ubicacion);
                cmd.Parameters.AddWithValue("@NUMERO_SERIE",
                    string.IsNullOrWhiteSpace(txtNumeroSerie.Text)
                        ? (object)DBNull.Value
                        : txtNumeroSerie.Text.Trim());
                cmd.Parameters.AddWithValue("@USUARIO", Usuario);

                conn.Open();
                cmd.ExecuteNonQuery();
            }

            lineasPicadas++;
            VerificarEstadoPedido();
            CargarPicadaActual();
        }

        /// <summary>
        ///  Actualiza la barra de progreso y habilita “Confirmar Pedido” 
        ///  cuando todas las líneas hayan sido picadas.
        /// </summary>
        private void VerificarEstadoPedido()
        {
            double porcentaje;
            if (totalLineas == 0)
                porcentaje = 100;
            else
                porcentaje = (double)lineasPicadas / totalLineas * 100;

            pbProgreso.Value = porcentaje;
            txtProgreso.Text = porcentaje.ToString("F1") + "% completado";

            btnConfirmarPedido.IsEnabled = (lineasPicadas >= totalLineas);
        }

        /// <summary>
        ///  Botón “Confirmar Pedido”: recarga la lista de pedidos (para refrescar los estatus)
        ///  y deshabilita controles de picking.
        /// </summary>
        private void BtnConfirmarPedido_Click(object sender, RoutedEventArgs e)
        {
            CargarPedidos();
            DeshabilitarControlesPicada();
            btnIniciarPicking.IsEnabled = false;
            btnConfirmarPedido.IsEnabled = false;
        }

        /// <summary>
        ///  Habilita los controles de picada (cantidad a picar y, si toca, el nº de serie).
        /// </summary>
        private void HabilitarControlesPicada()
        {
            txtCantidadPicar.IsEnabled = true;
            btnConfirmarPicada.IsEnabled = true;
            txtNumeroSerie.IsEnabled = picadaActual.RequiereNumeroSerie;
        }

        /// <summary>
        ///  Limpia y deshabilita todos los controles de “línea de picking”
        /// </summary>
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

        /// <summary>
        ///  Botón “Volver”: cierra esta ventana y vuelve al menú principal.
        /// </summary>
        private void BtnVolver_Click(object sender, RoutedEventArgs e)
        {
            new MenuPrincipal().Show();
            this.Close();
        }
    }

    /// <summary>
    ///  Clase que representa cada fila de la cabecera de pedido (ORDEN_SALIDA_CAB).
    /// </summary>
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

    /// <summary>
    ///  Clase auxiliar para mantener los datos de la línea de picking actual.
    /// </summary>
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
