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
        private const string CS =
            @"Server=localhost\SQLEXPRESS01;Database=bdsaid;Integrated Security=True;";
        private const string USUARIO = "USER01";

        private readonly ObservableCollection<Pedido> _pedidos = new ObservableCollection<Pedido>();
        private Pedido _pedidoSel;
        private Picada _picada;
        private int _lineasPicadas;
        private int _totalLineas;

        public Picking()
        {
            InitializeComponent();
            dgPedidos.ItemsSource = _pedidos;
            CargarPedidos();
        }

        private void CargarPedidos()
        {
            _pedidos.Clear();
            const string sql = @"
                SELECT PETICION, CODIGO_CLIENTE, ESTATUS_PETICION,
                       (SELECT COUNT(*) FROM ORDEN_SALIDA_LIN
                          WHERE PETICION=cab.PETICION) AS TOT
                  FROM ORDEN_SALIDA_CAB cab
                 ORDER BY PETICION DESC";
            using (var conn = new SqlConnection(CS))
            using (var cmd = new SqlCommand(sql, conn))
            {
                conn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        _pedidos.Add(new Pedido
                        {
                            Peticion = rd.GetInt32(0),
                            NombreCliente = rd.GetString(1),
                            EstatusPeticion = rd.GetInt32(2),
                            TotalLineas = rd.GetInt32(3)
                        });
                    }
                }
            }
        }

        private void DgPedidos_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _pedidoSel = dgPedidos.SelectedItem as Pedido;
            if (_pedidoSel == null) return;

            // Rellenar cabecera
            txtPeticion.Text = _pedidoSel.Peticion.ToString();
            txtCliente.Text = _pedidoSel.NombreCliente;
            txtEstado.Text = _pedidoSel.EstatusNombre;
            txtTotalLineas.Text = _pedidoSel.TotalLineas.ToString();
            txtFechaCreacion.Text = ObtenerCampoDate(
                                            "SELECT F_CREACION FROM ORDEN_SALIDA_CAB WHERE PETICION=@p",
                                            "@p", _pedidoSel.Peticion)
                                       .ToString("dd/MM/yyyy");
            txtDireccionEntrega.Text = ObtenerCampoString(
                                            "SELECT DIRECCION_ENTREGA FROM ORDEN_SALIDA_CAB WHERE PETICION=@p",
                                            "@p", _pedidoSel.Peticion);

            // Calcular progreso
            _totalLineas = _pedidoSel.TotalLineas;
            int pendientes = ObtenerPendientes(_pedidoSel.Peticion);
            _lineasPicadas = _totalLineas - pendientes;
            VerificarEstadoPedido();

            btnIniciarPicking.IsEnabled = pendientes > 0 && _pedidoSel.EstatusPeticion <= 2;
            btnConfirmarPedido.IsEnabled = pendientes == 0 && _pedidoSel.EstatusPeticion == 2;

            DeshabilitarControlesPicada();
        }

        private string ObtenerCampoString(string sql, string param, object val)
        {
            using (var conn = new SqlConnection(CS))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue(param, val);
                conn.Open();
                object o = cmd.ExecuteScalar();
                return (o == null || o == DBNull.Value) ? "" : o.ToString();
            }
        }

        private DateTime ObtenerCampoDate(string sql, string param, object val)
        {
            using (var conn = new SqlConnection(CS))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue(param, val);
                conn.Open();
                object o = cmd.ExecuteScalar();
                return (o == null || o == DBNull.Value)
                    ? DateTime.MinValue
                    : Convert.ToDateTime(o);
            }
        }

        private int ObtenerPendientes(int pet)
        {
            const string sql = @"
                SELECT SUM(CANTIDAD - ISNULL(CANTIDAD_PICADA,0))
                  FROM ORDEN_SALIDA_LIN
                 WHERE PETICION=@p";
            using (var conn = new SqlConnection(CS))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@p", pet);
                conn.Open();
                object o = cmd.ExecuteScalar();
                return (o == null || o == DBNull.Value) ? 0 : Convert.ToInt32(o);
            }
        }

        private void BtnIniciarPicking_Click(object sender, RoutedEventArgs e)
        {
            if (_pedidoSel == null) return;
            if (!VerificarStockParaPedido(_pedidoSel.Peticion)) return;

            // 1) Marco “En Proceso” en la cabecera de la BD
            using (var conn = new SqlConnection(CS))
            using (var cmd = new SqlCommand(@"
            UPDATE ORDEN_SALIDA_CAB
               SET ESTATUS_PETICION = 2
             WHERE PETICION = @p", conn))
            {
                cmd.Parameters.AddWithValue("@p", _pedidoSel.Peticion);
                conn.Open();
                cmd.ExecuteNonQuery();
            }

            // 2) Actualizo el POJO en memoria y la UI
            _pedidoSel.EstatusPeticion = 2;
            txtEstado.Text = _pedidoSel.EstatusNombre;

            // 3) Refresco el DataGrid para que se recompute el estilo de la fila
            dgPedidos.Items.Refresh();

            // 4) Ahora sí cargo la primera picada pendiente
            CargarPicadaActual();
        }


        private bool VerificarStockParaPedido(int pet)
        {
            const string sql = @"
                SELECT l.REFERENCIA,
                       SUM(l.CANTIDAD - ISNULL(l.CANTIDAD_PICADA,0)) AS NEC,
                       SUM(i.CANTIDAD_DISPONIBLE)            AS DISP
                  FROM ORDEN_SALIDA_LIN l
                  JOIN INVENTARIO i ON i.REFERENCIA = l.REFERENCIA
                 WHERE l.PETICION = @pet
                   AND i.CANTIDAD_DISPONIBLE > 0
                 GROUP BY l.REFERENCIA";
            using (var conn = new SqlConnection(CS))
            using (var cmd = new SqlCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@pet", pet);
                conn.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        int nec = rd.GetInt32(1);
                        int disp = rd.GetInt32(2);
                        if (disp < nec)
                        {
                            MessageBox.Show(
                                $"Stock insuficiente para {rd.GetString(0)}\nNecesitas {nec}, disponibles {disp}.",
                                "Stock insuficiente", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        private void CargarPicadaActual()
        {
            // 0) Si no hay pedido, no hacemos nada
            if (_pedidoSel == null) return;

            // 1) Siempre limpiamos la UI antes de empezar
            DeshabilitarControlesPicada();

            _picada = null;
            using (var conn = new SqlConnection(CS))
            {
                conn.Open();

                // 2) Traer la siguiente línea pendiente
                const string sql1 = @"
SELECT TOP 1
    l.LINEA,
    l.REFERENCIA,
    l.CANTIDAD - ISNULL(l.CANTIDAD_PICADA, 0) AS NEC,
    r.LLEVA_N_SERIES
  FROM ORDEN_SALIDA_LIN AS l
  JOIN REFERENCIAS        AS r ON r.REFERENCIA = l.REFERENCIA
 WHERE l.PETICION = @p
   AND (l.CANTIDAD - ISNULL(l.CANTIDAD_PICADA, 0)) > 0
 ORDER BY l.LINEA;";
                using (var cmd1 = new SqlCommand(sql1, conn))
                {
                    cmd1.Parameters.AddWithValue("@p", _pedidoSel.Peticion);
                    using (var rd1 = cmd1.ExecuteReader())
                    {
                        if (rd1.Read())
                        {
                            _picada = new Picada
                            {
                                Peticion = _pedidoSel.Peticion,
                                LineaId = rd1.GetInt32(0),
                                Referencia = rd1.GetString(1),
                                Cantidad = rd1.GetInt32(2),
                                RequiereNumeroSerie = rd1.GetBoolean(3)
                            };
                        }
                    }
                }

                // 3) Si no tenemos nada que picar, salimos ya (UI queda limpia)
                if (_picada == null)
                    return;

                // 4) Si sí tenemos línea, traemos ubicación y stock
                const string sql2 = @"
SELECT TOP 1
    UBICACION,
    CANTIDAD_DISPONIBLE
  FROM INVENTARIO
 WHERE REFERENCIA = @r
 ORDER BY CANTIDAD_DISPONIBLE DESC;";
                using (var cmd2 = new SqlCommand(sql2, conn))
                {
                    cmd2.Parameters.AddWithValue("@r", _picada.Referencia);
                    using (var rd2 = cmd2.ExecuteReader())
                    {
                        if (rd2.Read())
                        {
                            _picada.Ubicacion = rd2.GetString(0);
                            _picada.StockDisponible = rd2.GetInt32(1);
                        }
                    }
                }
            }

            // 5) Finalmente, rellenamos la UI con la info de _picada
            txtReferencia.Text = _picada.Referencia;
            txtNombreProducto.Text = ObtenerCampoString(
                                            "SELECT DES_REFERENCIA FROM REFERENCIAS WHERE REFERENCIA=@r",
                                            "@r", _picada.Referencia);
            txtCantidadRequerida.Text = _picada.Cantidad.ToString();
            txtUbicacion.Text = _picada.Ubicacion;
            txtStockDisponible.Text = _picada.StockDisponible.ToString();
            txtCantidadPicar.Text = _picada.Cantidad.ToString();

            panelNumeroSerie.Visibility =
                _picada.RequiereNumeroSerie
                ? Visibility.Visible
                : Visibility.Collapsed;
            txtNumeroSerie.Clear();
            btnConfirmarPicada.IsEnabled = true;
        }




        private void BtnConfirmarPicada_Click(object sender, RoutedEventArgs e)
        {
            if (_picada == null) return;
            if (!int.TryParse(txtCantidadPicar.Text, out int qty)
                || qty <= 0 || qty > _picada.Cantidad)
            {
                MessageBox.Show("Cantidad inválida."); return;
            }
            if (_picada.RequiereNumeroSerie
                && string.IsNullOrWhiteSpace(txtNumeroSerie.Text))
            {
                MessageBox.Show("Introduce nº de serie."); return;
            }

            using (var conn = new SqlConnection(CS))
            {
                conn.Open();
                using (var tx = conn.BeginTransaction())
                {
                    try
                    {
                        // 1) descuento todo de una vez
                        using (var upd = new SqlCommand(@"
UPDATE INVENTARIO
   SET CANTIDAD_DISPONIBLE = CANTIDAD_DISPONIBLE - @qty
 WHERE REFERENCIA=@r AND UBICACION=@u", conn, tx))
                        {
                            upd.Parameters.AddWithValue("@qty", qty);
                            upd.Parameters.AddWithValue("@r", _picada.Referencia);
                            upd.Parameters.AddWithValue("@u", _picada.Ubicacion);
                            if (upd.ExecuteNonQuery() == 0)
                                throw new Exception("No puedo descontar stock.");
                        }
                        // 2) marcación en bloque
                        using (var sp = new SqlCommand("SP_MARCAR_PICADA", conn, tx))
                        {
                            sp.CommandType = CommandType.StoredProcedure;
                            sp.Parameters.AddWithValue("@PETICION", _picada.Peticion);
                            sp.Parameters.AddWithValue("@LINEA_ID", _picada.LineaId);
                            sp.Parameters.AddWithValue("@CANTIDAD_PICADA", qty);
                            sp.Parameters.AddWithValue("@UBICACION", _picada.Ubicacion);
                            sp.Parameters.AddWithValue("@REFERENCIA", _picada.Referencia);
                            sp.Parameters.AddWithValue("@NUMERO_SERIE",
                                _picada.RequiereNumeroSerie
                                  ? (object)txtNumeroSerie.Text.Trim()
                                  : DBNull.Value);
                            sp.Parameters.AddWithValue("@USUARIO", USUARIO);
                            sp.ExecuteNonQuery();
                        }

                        tx.Commit();
                    }
                    catch (Exception ex)
                    {
                        tx.Rollback();
                        MessageBox.Show("Error al confirmar picada:\n" + ex.Message);
                        return;
                    }
                }
            }

            // actualizo progreso y continuo
            _lineasPicadas += qty;
            VerificarEstadoPedido();
            CargarPicadaActual();
        }

        private void VerificarEstadoPedido()
        {
            double pct = (_totalLineas == 0)
                       ? 100.0
                       : (_lineasPicadas * 100.0) / _totalLineas;
            pct = Math.Max(0, Math.Min(100, pct));
            pbProgreso.Value = pct;
            txtProgreso.Text = pct.ToString("0") + "% completado";
            btnConfirmarPedido.IsEnabled =
                (pct >= 100.0 && _pedidoSel.EstatusPeticion == 2);
        }

        private void BtnConfirmarPedido_Click(object sender, RoutedEventArgs e)
        {
            using (var conn = new SqlConnection(CS))
            using (var cmd = new SqlCommand(@"
                UPDATE ORDEN_SALIDA_CAB
                   SET ESTATUS_PETICION = 3,
                       F_CONFIRMACION   = GETDATE()
                 WHERE PETICION = @p", conn))
            {
                cmd.Parameters.AddWithValue("@p", _pedidoSel.Peticion);
                conn.Open();
                cmd.ExecuteNonQuery();
            }
            MessageBox.Show("Pedido marcado como Ejecutado.");
            CargarPedidos();
            DeshabilitarControlesPicada();
        }

        private void DeshabilitarControlesPicada()
        {
            txtReferencia.Clear();
            txtNombreProducto.Clear();
            txtCantidadRequerida.Clear();
            txtUbicacion.Clear();
            txtStockDisponible.Clear();
            txtCantidadPicar.Clear();
            txtNumeroSerie.Clear();
            panelNumeroSerie.Visibility = Visibility.Collapsed;
            btnConfirmarPicada.IsEnabled = false;
        }


        private void BtnVolver_Click(object sender, RoutedEventArgs e)
        {
            new MenuPrincipal().Show();
            Close();
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
        public int LineaId { get; set; }
        public int Peticion { get; set; }
        public string Referencia { get; set; }
        public int Cantidad { get; set; }
        public string Ubicacion { get; set; }
        public int StockDisponible { get; set; }
        public bool RequiereNumeroSerie { get; set; }
    }
}
