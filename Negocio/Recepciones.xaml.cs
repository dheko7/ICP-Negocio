// ******************************
//  Recepciones.xaml.cs  –  versión compatible con C# 7.3
// ******************************
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Linq;                    // ← necesario para Any()
using System.Net;
using System.Net.Mail;
using System.Windows;
using System.Windows.Controls;

namespace ICP.Negocio
{
    public partial class Recepciones : Window
    {
        // ----------------  CAMPOS ----------------
        private readonly ObservableCollection<RecepcionLinea> lineas = new ObservableCollection<RecepcionLinea>();
        private bool reciboConfirmado = false;
        private const string connectionString =
            "Server=localhost\\SQLEXPRESS01;Database=bdsaid;Integrated Security=True;MultipleActiveResultSets=True;";
        private const int MAX_ALBARAN = 12;   // longitud real de la columna ALBARAN

        // --------------  CTOR  --------------------
        public Recepciones()
        {
            InitializeComponent();
            RecepcionesGrid.ItemsSource = lineas;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CargarAlbaranes();
            CargarUbicaciones();
        }

        // --------------  Cargar combos  --------------------
        private void CargarAlbaranes()
        {
            cmbAlbaranes.Items.Clear();
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(
                        "SELECT ALBARAN FROM RECEPCIONES_CAB ORDER BY F_CREACION DESC", conn))
                    using (SqlDataReader rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                            cmbAlbaranes.Items.Add(rdr.GetString(0));
                    }
                }
                if (cmbAlbaranes.Items.Count > 0)
                    cmbAlbaranes.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar albaranes:\n" + ex.Message);
            }
        }

        private void CargarUbicaciones()
        {
            cmbUbicaciones.Items.Clear();
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("SELECT UBICACION FROM UBICACIONES", conn))
                    using (SqlDataReader rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                            cmbUbicaciones.Items.Add(rdr.GetString(0));
                    }
                }
                if (cmbUbicaciones.Items.Count > 0 && cmbUbicaciones.SelectedIndex == -1)
                    cmbUbicaciones.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar ubicaciones:\n" + ex.Message);
            }
        }

        // --------------  NUEVA RECEPCION  --------------------
        private void BtnNuevaRecepcion_Click(object sender, RoutedEventArgs e)
        {
            string albaranRaw = (cmbAlbaranes.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(albaranRaw))
            {
                MessageBox.Show("Ingresa número de albarán.");
                return;
            }
            if (albaranRaw.Length > MAX_ALBARAN)
            {
                MessageBox.Show("El albarán excede la longitud máxima de " + MAX_ALBARAN + " caracteres. Se recortará.");
                albaranRaw = albaranRaw.Substring(0, MAX_ALBARAN);
            }
            string albaran = albaranRaw;

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    // Comprueba existencia
                    using (SqlCommand exists = new SqlCommand(
                        "SELECT COUNT(*) FROM RECEPCIONES_CAB WHERE ALBARAN=@a", conn))
                    {
                        exists.Parameters.Add("@a", SqlDbType.VarChar, MAX_ALBARAN).Value = albaran;
                        if ((int)exists.ExecuteScalar() > 0)
                        {
                            MessageBox.Show("El albarán ya existe.");
                            return;
                        }
                    }
                    // Inserta cabecera
                    using (SqlCommand ins = new SqlCommand(@"
                        INSERT INTO RECEPCIONES_CAB
                          (ALBARAN,PROVEEDOR,F_CREACION,ESTATUS_RECEPCION,CODIGO_CLIENTE)
                        VALUES(@a,'PROV002',GETDATE(),0,'CLI002')", conn))
                    {
                        ins.Parameters.Add("@a", SqlDbType.VarChar, MAX_ALBARAN).Value = albaran;
                        ins.ExecuteNonQuery();
                    }
                }
                MessageBox.Show("Recepción creada.");
                CargarAlbaranes();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al crear recepción:\n" + ex.Message);
            }
        }

        // --------------  CARGAR RECEPCION  --------------------
        private void BtnCargar_Click(object sender, RoutedEventArgs e) => CargarRecepciones();

        private void CargarRecepciones()
        {
            lineas.Clear();
            reciboConfirmado = false;

            string albaran = (cmbAlbaranes.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(albaran))
            {
                MessageBox.Show("Ingresa número de albarán.");
                return;
            }

            try
            {
                // Lee líneas existentes
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(@"
                        SELECT rl.LINEA, rl.REFERENCIA, r.DES_REFERENCIA,
                               rl.CANTIDAD_BUENA, rl.CANTIDAD_MALA, rl.NUMERO_SERIE, rl.UBICACION,
                               p.ESTATUS_PALET, rl.PALETID
                          FROM RECEPCIONES_LIN rl
                          LEFT JOIN REFERENCIAS r ON rl.REFERENCIA = r.REFERENCIA
                          LEFT JOIN PALETS      p ON rl.PALETID    = p.PALET
                         WHERE rl.ALBARAN = @a
                         ORDER BY rl.LINEA", conn))
                    {
                        cmd.Parameters.AddWithValue("@a", albaran);
                        using (SqlDataReader rdr = cmd.ExecuteReader())
                        {
                            while (rdr.Read())
                            {
                                lineas.Add(new RecepcionLinea
                                {
                                    Albaran = albaran,
                                    Linea = rdr.GetInt32(0),
                                    Referencia = rdr.GetString(1),
                                    Descripcion = rdr.IsDBNull(2) ? "(Sin descripción)" : rdr.GetString(2),
                                    CantidadBuena = rdr.IsDBNull(3) ? 0 : rdr.GetInt32(3),
                                    CantidadMala = rdr.IsDBNull(4) ? 0 : rdr.GetInt32(4),
                                    NumeroSerie = rdr.IsDBNull(5) ? "" : rdr.GetString(5),
                                    Ubicacion = rdr.IsDBNull(6) ? "" : rdr.GetString(6),
                                    EstatusPalet = rdr.IsDBNull(7) ? 0 : rdr.GetInt32(7),
                                    PaletId = rdr.IsDBNull(8) ? 0 : rdr.GetInt32(8)
                                });
                            }
                        }
                    }
                    // Comprueba si ya confirmada
                    using (SqlCommand st = new SqlCommand(
                        "SELECT ESTATUS_RECEPCION FROM RECEPCIONES_CAB WHERE ALBARAN=@a", conn))
                    {
                        st.Parameters.AddWithValue("@a", albaran);
                        reciboConfirmado = ((int)st.ExecuteScalar() == 3);
                    }
                }

                if (lineas.Count > 0)
                    RecepcionesGrid.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar recepción:\n" + ex.Message);
            }
        }

        // --------------  AGREGAR LÍNEA  --------------------
        private bool ValidarEntradas()
        {
            int dummy;
            if (string.IsNullOrWhiteSpace(txtReferencia.Text)) return false;
            if (!int.TryParse(txtCantidadBuena.Text, out dummy)) return false;
            if (!int.TryParse(txtCantidadMala.Text, out dummy)) return false;
            return cmbUbicaciones.SelectedItem != null;
        }

        private void BtnAgregar_Click(object sender, RoutedEventArgs e)
        {
            if (reciboConfirmado)
            {
                MessageBox.Show("Recepción confirmada: no se puede agregar.");
                return;
            }
            if (!ValidarEntradas())
            {
                MessageBox.Show("Campos incompletos.");
                return;
            }

            string albaran = (cmbAlbaranes.Text ?? string.Empty).Trim();
            int buenas = int.Parse(txtCantidadBuena.Text);
            int malas = int.Parse(txtCantidadMala.Text);
            int total = buenas + malas;

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    // Inserta nuevo palet
                    int nuevoPaletId;
                    using (SqlCommand cmdP = new SqlCommand(@"
                        INSERT INTO PALETS
                          (REFERENCIA,CANTIDAD,ALBARAN_RECEPCION,UBICACION,ESTATUS_PALET,F_INSERT)
                        OUTPUT INSERTED.PALET
                        VALUES(@r,@c,@a,@u,0,GETDATE())", conn))
                    {
                        cmdP.Parameters.AddWithValue("@r", txtReferencia.Text.Trim());
                        cmdP.Parameters.AddWithValue("@c", total);
                        cmdP.Parameters.AddWithValue("@a", albaran);
                        cmdP.Parameters.AddWithValue("@u", cmbUbicaciones.SelectedItem.ToString());
                        nuevoPaletId = (int)cmdP.ExecuteScalar();
                    }
                    // Inserta línea
                    using (SqlCommand cmdL = new SqlCommand(@"
                        INSERT INTO RECEPCIONES_LIN
                          (ALBARAN,LINEA,REFERENCIA,CANTIDAD,CANTIDAD_BUENA,CANTIDAD_MALA,
                           UBICACION,NUMERO_SERIE,PALETID)
                        VALUES(@a,@l,@r,@t,@b,@m,@u,@n,@p)", conn))
                    {
                        cmdL.Parameters.AddWithValue("@a", albaran);
                        cmdL.Parameters.AddWithValue("@l", lineas.Count + 1);
                        cmdL.Parameters.AddWithValue("@r", txtReferencia.Text.Trim());
                        cmdL.Parameters.AddWithValue("@t", total);
                        cmdL.Parameters.AddWithValue("@b", buenas);
                        cmdL.Parameters.AddWithValue("@m", malas);
                        cmdL.Parameters.AddWithValue("@u", cmbUbicaciones.SelectedItem.ToString());
                        cmdL.Parameters.AddWithValue("@n", string.IsNullOrWhiteSpace(txtNumeroSerie.Text)
                                                          ? (object)DBNull.Value
                                                          : txtNumeroSerie.Text.Trim());
                        cmdL.Parameters.AddWithValue("@p", nuevoPaletId);
                        cmdL.ExecuteNonQuery();
                    }
                }
                CargarRecepciones();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al agregar línea:\n" + ex.Message);
            }
        }

        // --------------  CONFIRMAR RECEPCION  --------------------
        private void BtnConfirmar_Click(object sender, RoutedEventArgs e)
        {
            if (!lineas.Any())
            {
                MessageBox.Show("No hay líneas para confirmar.");
                return;
            }

            string albaran = (cmbAlbaranes.Text ?? string.Empty).Trim();
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var tx = conn.BeginTransaction())
                    {
                        // ÚNICA llamada al SP que ajusta INVENTARIO internamente
                        using (SqlCommand cmd = new SqlCommand("PA_CONFIRMAR_RECEPCION", conn, tx))
                        {
                            cmd.CommandType = CommandType.StoredProcedure;
                            cmd.Parameters.AddWithValue("@Albaran", albaran);
                            cmd.Parameters.AddWithValue("@FechaConfirmacion", DateTime.Now);
                            int filas = cmd.ExecuteNonQuery();
                            if (filas == 0)
                                throw new Exception("No se confirmó la recepción: verifica el albarán.");
                        }
                        tx.Commit();
                    }
                }

                // Envío de correo
                EnviarCorreoSMTP(
                    "destino@example.com",
                    "Recepción confirmada",
                    "La recepción " + albaran + " ha sido confirmada correctamente.");

                MessageBox.Show("Recepción confirmada.");
                reciboConfirmado = true;
                // Refresca Palets si está abierta
                foreach (Window w in Application.Current.Windows)
                    if (w is Palets p) { p.CargarPalets(); p.Activate(); }
            }
            catch (Exception ex)
            {
                MessageBox.Show("ERROR al confirmar recepción:\n" + ex.Message);
            }
        }

        // --------------  SMTP --------------------
        private void EnviarCorreoSMTP(string toEmail, string subject, string body)
        {
            try
            {
                using (SmtpClient client = new SmtpClient("smtp.gmail.com", 587))
                {
                    client.EnableSsl = true;
                    client.Credentials =
                        new NetworkCredential("dheko22@gmail.com", "fwvtauvwjmucnthd");
                    using (MailMessage mail = new MailMessage())
                    {
                        mail.From = new MailAddress("dheko22@gmail.com", "Notificaciones ICP");
                        mail.To.Add(toEmail);
                        mail.Subject = subject;
                        mail.Body = body;
                        mail.IsBodyHtml = false;
                        client.Send(mail);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo enviar correo:\n" + ex.Message);
            }
        }

        private void BtnVolver_Click(object sender, RoutedEventArgs e)
        {
            new MenuPrincipal().Show();
            Close();
        }

        // ----------------  SELECCIÓN EN GRID ----------------
        private void RecepcionesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RecepcionesGrid.SelectedItem is RecepcionLinea sel)
            {
                txtReferencia.Text = sel.Referencia;
                txtCantidadBuena.Text = sel.CantidadBuena.ToString();
                txtCantidadMala.Text = sel.CantidadMala.ToString();
                txtNumeroSerie.Text = sel.NumeroSerie;
                // Ubicación
                if (!string.IsNullOrWhiteSpace(sel.Ubicacion) &&
                    cmbUbicaciones.Items.Contains(sel.Ubicacion))
                {
                    cmbUbicaciones.SelectedItem = sel.Ubicacion;
                }
                else if (cmbUbicaciones.Items.Count > 0)
                {
                    cmbUbicaciones.SelectedIndex = 0;
                }
            }
        }

        // ----------------  MODIFICAR LÍNEA ----------------
        private void BtnModificar_Click(object sender, RoutedEventArgs e)
{
    // 1) Comprobaciones iniciales
    if (reciboConfirmado)
    {
        MessageBox.Show("La recepción ya fue confirmada; no se puede modificar.",
                        "Recepción confirmada", MessageBoxButton.OK, MessageBoxImage.Warning);
        return;
    }
    if (!(RecepcionesGrid.SelectedItem is RecepcionLinea sel))
    {
        MessageBox.Show("Selecciona una línea para modificar.",
                        "Selección requerida", MessageBoxButton.OK, MessageBoxImage.Information);
        return;
    }
    if (!ValidarEntradas())
    {
        MessageBox.Show("Campos incompletos.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        return;
    }

    // 2) Leer valores de los controles
    int cantBuena = int.Parse(txtCantidadBuena.Text);
    int cantMala  = int.Parse(txtCantidadMala.Text);
    int total     = cantBuena + cantMala;
    string numSerie = string.IsNullOrWhiteSpace(txtNumeroSerie.Text)
                       ? null
                       : txtNumeroSerie.Text.Trim();
    string ubi = cmbUbicaciones.SelectedItem?.ToString() ?? string.Empty;
    string albaran = sel.Albaran;
    int linea = sel.Linea;

    // 3) Confirmación del usuario
    if (MessageBox.Show("¿Guardar los cambios y asignar nuevo palet?",
                        "Confirmar modificación",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question) != MessageBoxResult.Yes)
    {
        return;
    }

    try
    {
        using (var conn = new SqlConnection(connectionString))
        {
            conn.Open();
            using (var tx = conn.BeginTransaction())
            {
                // 4) Insertar nuevo palet
                int nuevoPaletId;
                using (var cmdP = new SqlCommand(@"
                    INSERT INTO PALETS
                      (REFERENCIA, CANTIDAD, ALBARAN_RECEPCION, UBICACION, ESTATUS_PALET, F_INSERT)
                    OUTPUT INSERTED.PALET
                    VALUES(@r,@c,@a,@u,0,GETDATE())", conn, tx))
                {
                    cmdP.Parameters.AddWithValue("@r", sel.Referencia);
                    cmdP.Parameters.AddWithValue("@c", total);
                    cmdP.Parameters.AddWithValue("@a", albaran);
                    cmdP.Parameters.AddWithValue("@u", ubi);
                    nuevoPaletId = (int)cmdP.ExecuteScalar();
                }

                // 5) Actualizar línea para apuntar al nuevo palet
                using (var cmdL = new SqlCommand(@"
                    UPDATE RECEPCIONES_LIN
                       SET CANTIDAD_BUENA = @b,
                           CANTIDAD_MALA  = @m,
                           CANTIDAD       = @t,
                           NUMERO_SERIE   = @n,
                           UBICACION      = @u,
                           PALETID        = @p
                     WHERE ALBARAN = @a
                       AND LINEA   = @l", conn, tx))
                {
                    cmdL.Parameters.AddWithValue("@b", cantBuena);
                    cmdL.Parameters.AddWithValue("@m", cantMala);
                    cmdL.Parameters.AddWithValue("@t", total);
                    cmdL.Parameters.AddWithValue("@n", (object)numSerie ?? DBNull.Value);
                    cmdL.Parameters.AddWithValue("@u", ubi);
                    cmdL.Parameters.AddWithValue("@p", nuevoPaletId);
                    cmdL.Parameters.AddWithValue("@a", albaran);
                    cmdL.Parameters.AddWithValue("@l", linea);

                    int filas = cmdL.ExecuteNonQuery();
                    if (filas == 0)
                        throw new Exception("No se encontró la línea en la base de datos.");
                }

                tx.Commit();
            }
        }

        // 6) Notificar y refrescar
        MessageBox.Show("Línea modificada y asignada a nuevo palet correctamente.",
                        "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
        CargarRecepciones();
    }
    catch (Exception ex)
    {
        MessageBox.Show("Error al modificar y asignar palet:\n" + ex.Message,
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}


        // --------------  DTO --------------------
        public class RecepcionLinea : INotifyPropertyChanged
        {
            public string Albaran { get; set; }
            public int Linea { get; set; }
            public string Referencia { get; set; }
            public string Descripcion { get; set; }
            public int CantidadBuena { get; set; }
            public int CantidadMala { get; set; }
            public string NumeroSerie { get; set; }
            public string Ubicacion { get; set; }
            public int PaletId { get; set; }
            public int EstatusPalet { get; set; }
            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged(string p) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
        }
    }
}
