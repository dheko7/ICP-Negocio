// Recepciones.xaml.cs
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;

namespace ICP.Negocio
{
    public partial class Recepciones : Window
    {
        private ObservableCollection<RecepcionLinea> lineas = new ObservableCollection<RecepcionLinea>();
        private bool reciboConfirmado = false;
        private string cs = "Server=localhost\\SQLEXPRESS01;Database=bdsaid;Integrated Security=True;MultipleActiveResultSets=True;";

        public Recepciones()
        {
            InitializeComponent();
            RecepcionesGrid.ItemsSource = lineas;
            CargarUbicaciones();
        }

        private void CargarUbicaciones()
        {
            try
            {
                using (var conn = new SqlConnection(cs))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand("SELECT UBICACION FROM UBICACIONES", conn))
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                            cmbUbicaciones.Items.Add(rdr["UBICACION"].ToString());
                    }
                }
                if (cmbUbicaciones.Items.Count > 0)
                    cmbUbicaciones.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar ubicaciones: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnNuevaRecepcion_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtAlbaran.Text))
            {
                MessageBox.Show("Ingresa número de albarán.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            using (var conn = new SqlConnection(cs))
            {
                conn.Open();
                var exists = new SqlCommand("SELECT COUNT(*) FROM RECEPCIONES_CAB WHERE ALBARAN=@a", conn);
                exists.Parameters.AddWithValue("@a", txtAlbaran.Text.Trim());
                if ((int)exists.ExecuteScalar() > 0)
                {
                    MessageBox.Show("El albarán ya existe.", "Información", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                var ins = new SqlCommand(
                    "INSERT INTO RECEPCIONES_CAB(ALBARAN,PROVEEDOR,F_CREACION,ESTATUS_RECEPCION,CODIGO_CLIENTE) " +
                    "VALUES(@a,'PROV002',GETDATE(),0,'CLI002')", conn);
                ins.Parameters.AddWithValue("@a", txtAlbaran.Text.Trim());
                ins.ExecuteNonQuery();
            }
            MessageBox.Show("Recepción creada.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            CargarRecepciones();
        }

        private void BtnCargar_Click(object sender, RoutedEventArgs e) => CargarRecepciones();

        private void CargarRecepciones()
        {
            lineas.Clear();
            reciboConfirmado = false;

            if (string.IsNullOrWhiteSpace(txtAlbaran.Text))
            {
                MessageBox.Show("Ingresa número de albarán.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            using (var conn = new SqlConnection(cs))
            {
                conn.Open();
                var cnt = new SqlCommand("SELECT COUNT(*) FROM RECEPCIONES_CAB WHERE ALBARAN=@a", conn);
                cnt.Parameters.AddWithValue("@a", txtAlbaran.Text.Trim());
                if ((int)cnt.ExecuteScalar() == 0)
                {
                    MessageBox.Show("Albarán no encontrado.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Cargo líneas
                var cmd = new SqlCommand(@"
                    SELECT rl.LINEA,rl.REFERENCIA,rl.CANTIDAD_BUENA,rl.CANTIDAD_MALA,
                           rl.NUMERO_SERIE,rl.UBICACION,p.ESTATUS_PALET,rl.PALETID
                    FROM RECEPCIONES_LIN rl
                    LEFT JOIN PALETS p ON rl.PALETID=p.PALET
                    WHERE rl.ALBARAN=@a", conn);
                cmd.Parameters.AddWithValue("@a", txtAlbaran.Text.Trim());
                using (var rdr = cmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        lineas.Add(new RecepcionLinea
                        {
                            Albaran = txtAlbaran.Text.Trim(),
                            Linea = (int)rdr["LINEA"],
                            Referencia = rdr["REFERENCIA"].ToString(),
                            CantidadBuena = rdr["CANTIDAD_BUENA"] as int? ?? 0,
                            CantidadMala = rdr["CANTIDAD_MALA"] as int? ?? 0,
                            NumeroSerie = rdr["NUMERO_SERIE"].ToString(),
                            Ubicacion = rdr["UBICACION"].ToString(),
                            PaletId = rdr["PALETID"] as int? ?? 0,
                            EstatusPalet = rdr["ESTATUS_PALET"] as int? ?? 0
                        });
                    }
                }

                // Estado de la recepción
                var st = new SqlCommand("SELECT ESTATUS_RECEPCION FROM RECEPCIONES_CAB WHERE ALBARAN=@a", conn);
                st.Parameters.AddWithValue("@a", txtAlbaran.Text.Trim());
                reciboConfirmado = (int)st.ExecuteScalar() == 3;
            }
        }

        private bool ValidarEntradas()
        {
            if (string.IsNullOrWhiteSpace(txtReferencia.Text))
            {
                MessageBox.Show("Ingresa referencia.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (!int.TryParse(txtCantidadBuena.Text, out int b) || b < 0)
            {
                MessageBox.Show("Cantidad buena inválida.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (!int.TryParse(txtCantidadMala.Text, out int m) || m < 0)
            {
                MessageBox.Show("Cantidad mala inválida.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (cmbUbicaciones.SelectedItem == null)
            {
                MessageBox.Show("Selecciona ubicación.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            return true;
        }

        private void BtnAgregar_Click(object sender, RoutedEventArgs e)
        {
            if (reciboConfirmado)
            {
                MessageBox.Show("La recepción ya fue confirmada.", "Información", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!ValidarEntradas()) return;

            using (var conn = new SqlConnection(cs))
            {
                conn.Open();
                // 1) Insertar palet
                int total = int.Parse(txtCantidadBuena.Text) + int.Parse(txtCantidadMala.Text);
                var cmdP = new SqlCommand(@"
                    INSERT INTO PALETS(REFERENCIA,CANTIDAD,ALBARAN_RECEPCION,UBICACION,ESTATUS_PALET,F_INSERT)
                    OUTPUT INSERTED.PALET
                    VALUES(@r,@c,@a,@u,1,GETDATE())", conn);
                cmdP.Parameters.AddWithValue("@r", txtReferencia.Text.Trim());
                cmdP.Parameters.AddWithValue("@c", total);
                cmdP.Parameters.AddWithValue("@a", txtAlbaran.Text.Trim());
                cmdP.Parameters.AddWithValue("@u", cmbUbicaciones.SelectedItem.ToString());
                int pid = (int)cmdP.ExecuteScalar();

                // 2) NSERIE
                if (!string.IsNullOrWhiteSpace(txtNumeroSerie.Text))
                {
                    var cmdN = new SqlCommand(@"
                        INSERT INTO NSERIES_RECEPCIONES(NUMERO_SERIE,PALET,ALBARAN,F_REGISTRO)
                        VALUES(@n,@p,@a,GETDATE())", conn);
                    cmdN.Parameters.AddWithValue("@n", txtNumeroSerie.Text.Trim());
                    cmdN.Parameters.AddWithValue("@p", pid);
                    cmdN.Parameters.AddWithValue("@a", txtAlbaran.Text.Trim());
                    cmdN.ExecuteNonQuery();
                }

                // 3) Línea
                var cmdL = new SqlCommand(@"
                    INSERT INTO RECEPCIONES_LIN(ALBARAN,LINEA,REFERENCIA,CANTIDAD,
                                                CANTIDAD_BUENA,CANTIDAD_MALA,UBICACION,NUMERO_SERIE,PALETID)
                    VALUES(@a,@l,@r,@tot,@b,@m,@u,@n,@p)", conn);
                cmdL.Parameters.AddWithValue("@a", txtAlbaran.Text.Trim());
                cmdL.Parameters.AddWithValue("@l", RecepcionesGrid.Items.Count + 1);
                cmdL.Parameters.AddWithValue("@r", txtReferencia.Text.Trim());
                cmdL.Parameters.AddWithValue("@tot", total);
                cmdL.Parameters.AddWithValue("@b", int.Parse(txtCantidadBuena.Text));
                cmdL.Parameters.AddWithValue("@m", int.Parse(txtCantidadMala.Text));
                cmdL.Parameters.AddWithValue("@u", cmbUbicaciones.SelectedItem.ToString());
                cmdL.Parameters.AddWithValue("@n", (object)txtNumeroSerie.Text.Trim() ?? DBNull.Value);
                cmdL.Parameters.AddWithValue("@p", pid);
                cmdL.ExecuteNonQuery();
            }

            CargarRecepciones();
        }

        private void BtnModificar_Click(object sender, RoutedEventArgs e)
        {
            // 1) No permitimos cambios si ya se confirmó la recepción
            if (reciboConfirmado)
            {
                MessageBox.Show("No se puede modificar después de confirmar la recepción.", "Recepción Confirmada", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 2) Debe haber una línea seleccionada
            if (!(RecepcionesGrid.SelectedItem is RecepcionLinea lin))
            {
                MessageBox.Show("Selecciona una línea para modificar o eliminar.", "Selección Requerida", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 3) Preguntamos al usuario si quiere editar (OK) o eliminar (Cancel)
            var result = MessageBox.Show(
                "¿Qué deseas hacer con esta línea?\n\n" +
                "- Aceptar: Modificar la línea\n" +
                "- Cancelar: Eliminar la línea",
                "Modificar o Eliminar",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question);

            using (var conn = new SqlConnection(cs))
            {
                conn.Open();

                if (result == MessageBoxResult.OK)
                {
                    // **** ACTUALIZAR LÍNEA ****
                    using (var cmd = new SqlCommand(
                        @"UPDATE RECEPCIONES_LIN 
                  SET CANTIDAD_BUENA = @b, 
                      CANTIDAD_MALA  = @m, 
                      NUMERO_SERIE   = @n 
                  WHERE ALBARAN = @a AND LINEA = @l", conn))
                    {
                        cmd.Parameters.AddWithValue("@b", lin.CantidadBuena);
                        cmd.Parameters.AddWithValue("@m", lin.CantidadMala);
                        cmd.Parameters.AddWithValue("@n", (object)lin.NumeroSerie ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@a", lin.Albaran);
                        cmd.Parameters.AddWithValue("@l", lin.Linea);
                        cmd.ExecuteNonQuery();
                    }

                    MessageBox.Show("Línea modificada exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // **** ELIMINAR LÍNEA ****
                    using (var cmd = new SqlCommand(
                        "DELETE FROM RECEPCIONES_LIN WHERE ALBARAN = @a AND LINEA = @l", conn))
                    {
                        cmd.Parameters.AddWithValue("@a", lin.Albaran);
                        cmd.Parameters.AddWithValue("@l", lin.Linea);
                        cmd.ExecuteNonQuery();
                    }

                    // **** ELIMINAR NSERIES ****
                    if (lin.PaletId > 0)
                    {
                        using (var cmd = new SqlCommand(
                            "DELETE FROM NSERIES_RECEPCIONES WHERE PALET = @p", conn))
                        {
                            cmd.Parameters.AddWithValue("@p", lin.PaletId);
                            cmd.ExecuteNonQuery();
                        }

                        // **** ELIMINAR PALETS ****
                        using (var cmd = new SqlCommand(
                            "DELETE FROM PALETS WHERE PALET = @p", conn))
                        {
                            cmd.Parameters.AddWithValue("@p", lin.PaletId);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    MessageBox.Show("Línea eliminada exitosamente.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }

            // 4) Refrescamos la grilla con lo que quede en la BD
            CargarRecepciones();
        }



        private void BtnNuevaRef_Click(object sender, RoutedEventArgs e)
        {
            if (reciboConfirmado)
            {
                MessageBox.Show("La recepción ya fue confirmada.", "Información", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            string nuevaRef = "REF-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            using (var conn = new SqlConnection(cs))
            {
                conn.Open();
                var ins = new SqlCommand(@"
                    INSERT INTO REFERENCIAS(REFERENCIA,DES_REFERENCIA,PRECIO,
                                            LLEVA_N_SERIES,ESTA_HABILITADA)
                    VALUES(@r,@d,0,0,1)", conn);
                ins.Parameters.AddWithValue("@r", nuevaRef);
                ins.Parameters.AddWithValue("@d", "Desconocida");
                ins.ExecuteNonQuery();
            }
            MessageBox.Show("Referencia creada: " + nuevaRef, "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnConfirmar_Click(object sender, RoutedEventArgs e)
        {
            if (reciboConfirmado)
            {
                MessageBox.Show("La recepción ya fue confirmada.", "Información", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (lineas.Count == 0)
            {
                MessageBox.Show("No hay líneas para confirmar.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (MessageBox.Show("¿Confirmar recepción completa?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            using (var conn = new SqlConnection(cs))
            {
                conn.Open();
                var cmd1 = new SqlCommand("PA_CONFIRMAR_RECEPCION", conn) { CommandType = CommandType.StoredProcedure };
                cmd1.Parameters.AddWithValue("@Albaran", txtAlbaran.Text.Trim());
                cmd1.Parameters.AddWithValue("@FechaConfirmacion", DateTime.Now);
                cmd1.ExecuteNonQuery();

                var cmd2 = new SqlCommand("PA_ENVIAR_DBMAIL", conn) { CommandType = CommandType.StoredProcedure };
                cmd2.Parameters.AddWithValue("@Albaran", txtAlbaran.Text.Trim());
                cmd2.Parameters.AddWithValue("@Resumen", "Recepción completada: " + txtAlbaran.Text.Trim());
                cmd2.Parameters.AddWithValue("@Destinatario", "pruebas@icp.com");
                cmd2.ExecuteNonQuery();
            }

            reciboConfirmado = true;
            MessageBox.Show("Recepción confirmada y mail enviado.", "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnVolver_Click(object sender, RoutedEventArgs e)
        {
            new MenuPrincipal().Show();
            this.Close();
        }

        private void RecepcionesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Si no hay nada seleccionado, salimos
            if (RecepcionesGrid.SelectedItem == null) return;

            // Obtenemos la línea seleccionada
            var linea = RecepcionesGrid.SelectedItem as RecepcionLinea;
            if (linea == null) return;

            // Volcamos sus datos en los TextBox/ComboBox
            txtReferencia.Text = linea.Referencia;
            txtCantidadBuena.Text = linea.CantidadBuena.ToString();
            txtCantidadMala.Text = linea.CantidadMala.ToString();
            txtNumeroSerie.Text = linea.NumeroSerie ?? string.Empty;

            // Seleccionamos en el ComboBox la ubicación que ya existe
            cmbUbicaciones.SelectedItem = linea.Ubicacion;
        }

    }

    public class RecepcionLinea : INotifyPropertyChanged
    {
        public string Albaran { get; set; }
        public int Linea { get; set; }
        public string Referencia { get; set; }
        public int CantidadBuena { get; set; }
        public int CantidadMala { get; set; }
        public string NumeroSerie { get; set; }
        public string Ubicacion { get; set; }
        public int PaletId { get; set; }
        public int EstatusPalet { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string n) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
