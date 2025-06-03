using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Net;
using System.Net.Mail;
using System.Windows;
using System.Windows.Controls;

namespace ICP.Negocio
{
    public partial class Recepciones : Window
    {
        // Colección enlazada al DataGrid para mostrar las líneas de RECEPCIONES_LIN
        private ObservableCollection<RecepcionLinea> lineas = new ObservableCollection<RecepcionLinea>();

        // Bandera para saber si la cabecera ya fue confirmada (ESTATUS_RECEPCION = 3)
        private bool reciboConfirmado = false;

        // Cadena de conexión a la base de datos (ajusta si fuera necesario)
        private readonly string cs =
            "Server=localhost\\SQLEXPRESS01;Database=bdsaid;Integrated Security=True;MultipleActiveResultSets=True;";

        public Recepciones()
        {
            InitializeComponent();
            // Enlaza el DataGrid al ObservableCollection para refrescar automáticamente
            RecepcionesGrid.ItemsSource = lineas;
        }

        // **********************
        // 1) Se ejecuta cuando la ventana termina de cargarse
        // **********************
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CargarAlbaranes();
            CargarUbicaciones();
        }

        // **********************
        // 2) Carga todos los albaranes existentes y los agrega al ComboBox
        // **********************
        private void CargarAlbaranes()
        {
            cmbAlbaranes.Items.Clear();
            try
            {
                using (var conn = new SqlConnection(cs))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(
                        "SELECT ALBARAN FROM RECEPCIONES_CAB ORDER BY F_CREACION DESC", conn))
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            cmbAlbaranes.Items.Add(rdr.GetString(0));
                        }
                    }
                }

                if (cmbAlbaranes.Items.Count > 0)
                    cmbAlbaranes.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar lista de albaranes:\n" + ex.Message,
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // **********************
        // 3) Carga todas las ubicaciones de la tabla UBICACIONES en el ComboBox cmbUbicaciones
        // **********************
        private void CargarUbicaciones()
        {
            cmbUbicaciones.Items.Clear();
            try
            {
                using (var conn = new SqlConnection(cs))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand("SELECT UBICACION FROM UBICACIONES", conn))
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                            cmbUbicaciones.Items.Add(rdr.GetString(0));
                    }
                }
                if (cmbUbicaciones.Items.Count > 0)
                    cmbUbicaciones.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar ubicaciones:\n" + ex.Message,
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // **********************
        // 4) “Nueva Recepción” – Inserta la cabecera en RECEPCIONES_CAB si el albarán no existe.
        // **********************
        private void BtnNuevaRecepcion_Click(object sender, RoutedEventArgs e)
        {
            string albaran = (cmbAlbaranes.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(albaran))
            {
                MessageBox.Show("Ingresa número de albarán.", "Error",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var conn = new SqlConnection(cs))
                {
                    conn.Open();
                    // Verifico si YA existe esa cabecera de albarán
                    var exists = new SqlCommand(
                        "SELECT COUNT(*) FROM RECEPCIONES_CAB WHERE ALBARAN = @a", conn);
                    exists.Parameters.AddWithValue("@a", albaran);
                    if ((int)exists.ExecuteScalar() > 0)
                    {
                        MessageBox.Show("El albarán ya existe.", "Información",
                                        MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    // Insertar la nueva cabecera con ESTATUS_RECEPCION = 0 (por defecto)
                    var ins = new SqlCommand(
                        "INSERT INTO RECEPCIONES_CAB(ALBARAN, PROVEEDOR, F_CREACION, ESTATUS_RECEPCION, CODIGO_CLIENTE) " +
                        "VALUES(@a, 'PROV002', GETDATE(), 0, 'CLI002')", conn);
                    ins.Parameters.AddWithValue("@a", albaran);
                    ins.ExecuteNonQuery();
                }

                MessageBox.Show("Recepción creada.", "Éxito",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                CargarAlbaranes();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al crear recepción:\n" + ex.Message, "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // **********************
        // 5) “Cargar” – Obtiene todas las líneas de RECEPCIONES_LIN para el albarán seleccionado
        // **********************
        private void BtnCargar_Click(object sender, RoutedEventArgs e)
        {
            CargarRecepciones();
        }

        // **********************
        // 6) Cargar las líneas de RECEPCIONES_LIN y ver si la cabecera ya está confirmada
        // **********************
        private void CargarRecepciones()
        {
            lineas.Clear();
            reciboConfirmado = false;

            string albaran = (cmbAlbaranes.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(albaran))
            {
                MessageBox.Show("Ingresa número de albarán.", "Error",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var conn = new SqlConnection(cs))
                {
                    conn.Open();

                    // Verificar que exista la cabecera de RECEPCIONES_CAB
                    var cnt = new SqlCommand(
                        "SELECT COUNT(*) FROM RECEPCIONES_CAB WHERE ALBARAN = @a", conn);
                    cnt.Parameters.AddWithValue("@a", albaran);
                    if ((int)cnt.ExecuteScalar() == 0)
                    {
                        MessageBox.Show("Albarán no encontrado.", "Error",
                                        MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Recupero todas las líneas de RECEPCIONES_LIN, junto con DESCRIPCIÓN y ESTATUS_PALET y PALETID
                    var cmd = new SqlCommand(@"
                        SELECT 
                            rl.LINEA,
                            rl.REFERENCIA,
                            r.DES_REFERENCIA    AS DESCRIPCION,
                            rl.CANTIDAD_BUENA,
                            rl.CANTIDAD_MALA,
                            rl.NUMERO_SERIE,
                            rl.UBICACION,
                            p.ESTATUS_PALET,
                            rl.PALETID
                        FROM RECEPCIONES_LIN rl
                        LEFT JOIN REFERENCIAS r ON rl.REFERENCIA = r.REFERENCIA
                        LEFT JOIN PALETS       p ON rl.PALETID = p.PALET
                        WHERE rl.ALBARAN = @a", conn);
                    cmd.Parameters.AddWithValue("@a", albaran);

                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            lineas.Add(new RecepcionLinea
                            {
                                Albaran = albaran,
                                Linea = rdr.GetInt32(rdr.GetOrdinal("LINEA")),
                                Referencia = rdr.GetString(rdr.GetOrdinal("REFERENCIA")),
                                Descripcion = rdr.IsDBNull(rdr.GetOrdinal("DESCRIPCION"))
                                                  ? "(Sin descripción)"
                                                  : rdr.GetString(rdr.GetOrdinal("DESCRIPCION")),
                                CantidadBuena = rdr.IsDBNull(rdr.GetOrdinal("CANTIDAD_BUENA"))
                                                  ? 0
                                                  : rdr.GetInt32(rdr.GetOrdinal("CANTIDAD_BUENA")),
                                CantidadMala = rdr.IsDBNull(rdr.GetOrdinal("CANTIDAD_MALA"))
                                                  ? 0
                                                  : rdr.GetInt32(rdr.GetOrdinal("CANTIDAD_MALA")),
                                NumeroSerie = rdr.IsDBNull(rdr.GetOrdinal("NUMERO_SERIE"))
                                                  ? ""
                                                  : rdr.GetString(rdr.GetOrdinal("NUMERO_SERIE")),
                                Ubicacion = rdr.IsDBNull(rdr.GetOrdinal("UBICACION"))
                                                  ? ""
                                                  : rdr.GetString(rdr.GetOrdinal("UBICACION")),
                                EstatusPalet = rdr.IsDBNull(rdr.GetOrdinal("ESTATUS_PALET"))
                                                  ? 0
                                                  : rdr.GetInt32(rdr.GetOrdinal("ESTATUS_PALET")),
                                PaletId = rdr.IsDBNull(rdr.GetOrdinal("PALETID"))
                                                  ? 0
                                                  : rdr.GetInt32(rdr.GetOrdinal("PALETID"))
                            });
                        }
                    }

                    // Compruebo si la cabecera ya fue confirmada (ESTATUS_RECEPCION = 3)
                    var st = new SqlCommand(
                        "SELECT ESTATUS_RECEPCION FROM RECEPCIONES_CAB WHERE ALBARAN = @a", conn);
                    st.Parameters.AddWithValue("@a", albaran);
                    reciboConfirmado = ((int)st.ExecuteScalar() == 3);
                }

                // Si se ha cargado alguna línea, seleccionamos la primera
                if (lineas.Count > 0)
                {
                    RecepcionesGrid.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al cargar recepción:\n" + ex.Message, "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // **********************
        // 7) Validaciones antes de agregar o modificar una línea
        // **********************
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

        // **********************
        // 8) “Agregar Línea” – Inserta la línea nueva en RECEPCIONES_LIN, crea Palet y NSERIES si hace falta.
        // **********************
        private void BtnAgregar_Click(object sender, RoutedEventArgs e)
        {
            if (reciboConfirmado)
            {
                MessageBox.Show("La recepción ya fue confirmada.", "Información",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!ValidarEntradas()) return;

            string albaran = (cmbAlbaranes.Text ?? "").Trim();

            try
            {
                using (var conn = new SqlConnection(cs))
                {
                    conn.Open();

                    // 1) Insertar Palet y capturar el ID (INSERTED.PALET)
                    int total = int.Parse(txtCantidadBuena.Text) + int.Parse(txtCantidadMala.Text);
                    var cmdP = new SqlCommand(@"
                        INSERT INTO PALETS(REFERENCIA, CANTIDAD, ALBARAN_RECEPCION, UBICACION, ESTATUS_PALET, F_INSERT)
                        OUTPUT INSERTED.PALET
                        VALUES(@r, @c, @a, @u, 1, GETDATE())", conn);
                    cmdP.Parameters.AddWithValue("@r", txtReferencia.Text.Trim());
                    cmdP.Parameters.AddWithValue("@c", total);
                    cmdP.Parameters.AddWithValue("@a", albaran);
                    cmdP.Parameters.AddWithValue("@u", cmbUbicaciones.SelectedItem.ToString());
                    int pid = (int)cmdP.ExecuteScalar();

                    // 2) Si el usuario escribió un número de serie, guardarlo en NSERIES_RECEPCIONES
                    if (!string.IsNullOrWhiteSpace(txtNumeroSerie.Text))
                    {
                        var cmdN = new SqlCommand(@"
                            INSERT INTO NSERIES_RECEPCIONES(NUMERO_SERIE, PALET, ALBARAN, F_REGISTRO)
                            VALUES(@n, @p, @a, GETDATE())", conn);
                        cmdN.Parameters.AddWithValue("@n", txtNumeroSerie.Text.Trim());
                        cmdN.Parameters.AddWithValue("@p", pid);
                        cmdN.Parameters.AddWithValue("@a", albaran);
                        cmdN.ExecuteNonQuery();
                    }

                    // 3) Insertar la propia línea en RECEPCIONES_LIN
                    var cmdL = new SqlCommand(@"
                        INSERT INTO RECEPCIONES_LIN(ALBARAN, LINEA, REFERENCIA, CANTIDAD,
                                                    CANTIDAD_BUENA, CANTIDAD_MALA, UBICACION, NUMERO_SERIE, PALETID)
                        VALUES(@a, @l, @r, @tot, @b, @m, @u, @n, @p)", conn);
                    cmdL.Parameters.AddWithValue("@a", albaran);
                    cmdL.Parameters.AddWithValue("@l", RecepcionesGrid.Items.Count + 1);
                    cmdL.Parameters.AddWithValue("@r", txtReferencia.Text.Trim());
                    cmdL.Parameters.AddWithValue("@tot", total);
                    cmdL.Parameters.AddWithValue("@b", int.Parse(txtCantidadBuena.Text));
                    cmdL.Parameters.AddWithValue("@m", int.Parse(txtCantidadMala.Text));
                    cmdL.Parameters.AddWithValue("@u", cmbUbicaciones.SelectedItem.ToString());
                    cmdL.Parameters.AddWithValue("@n",
                        string.IsNullOrWhiteSpace(txtNumeroSerie.Text) ? (object)DBNull.Value : txtNumeroSerie.Text.Trim());
                    cmdL.Parameters.AddWithValue("@p", pid);
                    cmdL.ExecuteNonQuery();
                }

                // Después de insertar la línea, recargo el DataGrid
                CargarRecepciones();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al agregar línea:\n" + ex.Message, "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // **********************
        // 9) “Modificar Línea” – Toma los valores directamente de los TextBoxes y ComboBox
        // **********************
        private void BtnModificar_Click(object sender, RoutedEventArgs e)
        {
            if (reciboConfirmado)
            {
                MessageBox.Show("No se puede modificar después de confirmar la recepción.",
                                "Recepción Confirmada", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!(RecepcionesGrid.SelectedItem is RecepcionLinea sel))
            {
                MessageBox.Show("Selecciona una línea para modificar o eliminar.",
                                "Selección Requerida", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Validamos que el formulario superior esté bien llenado
            if (!ValidarEntradas()) return;

            var result = MessageBox.Show(
                "¿Deseas actualizar esta línea con los nuevos valores?\n\n" +
                "- Sí: Guardar cambios\n" +
                "- No: Cancelar modificación",
                "Modificar Línea",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            // Lectura de valores modificados desde los controles
            string albaran = sel.Albaran;
            int linea = sel.Linea;
            int cantidadBuenaMod;
            int cantidadMalaMod;
            string numeroSerieMod = txtNumeroSerie.Text.Trim();
            string ubicacionMod = cmbUbicaciones.SelectedItem?.ToString() ?? "";

            if (!int.TryParse(txtCantidadBuena.Text, out cantidadBuenaMod) || cantidadBuenaMod < 0)
            {
                MessageBox.Show("Cantidad buena inválida.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!int.TryParse(txtCantidadMala.Text, out cantidadMalaMod) || cantidadMalaMod < 0)
            {
                MessageBox.Show("Cantidad mala inválida.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var conn = new SqlConnection(cs))
                {
                    conn.Open();

                    // Actualizar registro en RECEPCIONES_LIN
                    using (var cmd = new SqlCommand(@"
                        UPDATE RECEPCIONES_LIN 
                           SET CANTIDAD_BUENA = @b, 
                               CANTIDAD_MALA  = @m, 
                               NUMERO_SERIE   = @n,
                               UBICACION      = @u
                         WHERE ALBARAN     = @a 
                           AND LINEA       = @l", conn))
                    {
                        cmd.Parameters.AddWithValue("@b", cantidadBuenaMod);
                        cmd.Parameters.AddWithValue("@m", cantidadMalaMod);
                        cmd.Parameters.AddWithValue("@n",
                            string.IsNullOrWhiteSpace(numeroSerieMod) ? (object)DBNull.Value : numeroSerieMod);
                        cmd.Parameters.AddWithValue("@u", ubicacionMod);
                        cmd.Parameters.AddWithValue("@a", albaran);
                        cmd.Parameters.AddWithValue("@l", linea);
                        cmd.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Línea modificada exitosamente.", "Éxito",
                                MessageBoxButton.OK, MessageBoxImage.Information);

                // Después de guardar, recargo las líneas para reflejar cambios en pantalla
                CargarRecepciones();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al modificar línea:\n" + ex.Message, "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // **********************
        // 10) “Referencia Desconocida” – Crea una referencia nueva genérica en la tabla REFERENCIAS
        // **********************
        private void BtnNuevaRef_Click(object sender, RoutedEventArgs e)
        {
            if (reciboConfirmado)
            {
                MessageBox.Show("La recepción ya fue confirmada.", "Información",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string nuevaRef = "REF-" + Guid.NewGuid().ToString("N").Substring(0, 8);

            try
            {
                using (var conn = new SqlConnection(cs))
                {
                    conn.Open();
                    var ins = new SqlCommand(@"
                        INSERT INTO REFERENCIAS(REFERENCIA, DES_REFERENCIA, PRECIO,
                                                LLEVA_N_SERIES, ESTA_HABILITADA)
                        VALUES(@r, @d, 0, 0, 1)", conn);
                    ins.Parameters.AddWithValue("@r", nuevaRef);
                    ins.Parameters.AddWithValue("@d", "Desconocida");
                    ins.ExecuteNonQuery();
                }

                MessageBox.Show("Referencia creada: " + nuevaRef, "Éxito",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al crear referencia:\n" + ex.Message, "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // **********************
        // 11) “Confirmar Recepción” – Llama al SP para fijar ESTATUS_RECEPCION = 3, actualiza pallets y envía un correo SMTP
        // **********************
        private void BtnConfirmar_Click(object sender, RoutedEventArgs e)
        {
            if (reciboConfirmado)
            {
                MessageBox.Show("La recepción ya fue confirmada.", "Información",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (lineas.Count == 0)
            {
                MessageBox.Show("No hay líneas para confirmar.", "Error",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (MessageBox.Show("¿Confirmar recepción completa?", "Confirmar",
                                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            string albaran = (cmbAlbaranes.Text ?? "").Trim();

            try
            {
                using (var conn = new SqlConnection(cs))
                {
                    conn.Open();

                    // SP que marca ESTATUS_RECEPCION = 3 en RECEPCIONES_CAB
                    var cmd1 = new SqlCommand("PA_CONFIRMAR_RECEPCION", conn)
                    {
                        CommandType = System.Data.CommandType.StoredProcedure
                    };
                    cmd1.Parameters.AddWithValue("@Albaran", albaran);
                    cmd1.Parameters.AddWithValue("@FechaConfirmacion", DateTime.Now);
                    cmd1.ExecuteNonQuery();

                    // Actualizar pallets de ese albarán (por ejemplo, ESTATUS_PALET = 2)
                    var cmdPalets = new SqlCommand(@"
                        UPDATE PALETS
                           SET ESTATUS_PALET = 2
                         WHERE ALBARAN_RECEPCION = @a", conn);
                    cmdPalets.Parameters.AddWithValue("@a", albaran);
                    cmdPalets.ExecuteNonQuery();
                }

                reciboConfirmado = true;
                MessageBox.Show("Recepción confirmada y pallets actualizados.", "Éxito",
                                MessageBoxButton.OK, MessageBoxImage.Information);

                // ——————————————————————————
                // Enviar correo SMTP notificando la confirmación
                // ——————————————————————————
                string asunto = $"Recepción Confirmada: {albaran}";
                string cuerpo = $"La recepción con albarán <b>{albaran}</b> " +
                                $"fue confirmada correctamente el {DateTime.Now:dd/MM/yyyy HH:mm:ss}.";
                EnviarCorreoSMTP("saidjniah@gmail.com", asunto, cuerpo);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al confirmar recepción:\n" + ex.Message, "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // **********************
        // 12) “Volver” – Regresa al menú principal sin cerrar la app
        // **********************
        private void BtnVolver_Click(object sender, RoutedEventArgs e)
        {
            new MenuPrincipal().Show();
            this.Close();
        }

        // **********************
        // 13) on SelectionChanged en el DataGrid de líneas: 
        //     Rellena los TextBox y ComboBox con los valores de la línea seleccionada
        // **********************
        private void RecepcionesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RecepcionesGrid.SelectedItem is RecepcionLinea sel)
            {
                txtReferencia.Text = sel.Referencia;
                txtCantidadBuena.Text = sel.CantidadBuena.ToString();
                txtCantidadMala.Text = sel.CantidadMala.ToString();
                txtNumeroSerie.Text = sel.NumeroSerie;

                if (!string.IsNullOrWhiteSpace(sel.Ubicacion)
                    && cmbUbicaciones.Items.Contains(sel.Ubicacion))
                {
                    cmbUbicaciones.SelectedItem = sel.Ubicacion;
                }
                else if (cmbUbicaciones.Items.Count > 0)
                {
                    cmbUbicaciones.SelectedIndex = 0;
                }
            }
        }


        // ****************************************
        // MÉTODO AUXILIAR: ENVÍO DE CORREO SMTP
        // ****************************************
        private void EnviarCorreoSMTP(string toEmail, string subject, string body)
        {
            try
            {
                // ——————————————————————————————————————
                // 1) Configuración del cliente SMTP (Gmail)
                // ——————————————————————————————————————
                var smtpHost = "smtp.gmail.com";
                var smtpPort = 587;
                var smtpUser = "dheko22@gmail.com";           // <-- TU cuenta real de Gmail
                var smtpPass = "htdgeqewwcxcpwci";                // <-- TU App Password DE 16 caracteres

                using (var client = new SmtpClient(smtpHost, smtpPort))
                {
                    client.EnableSsl = true;
                    client.Credentials = new NetworkCredential(smtpUser, smtpPass);

                    // ——————————————————————————
                    // 2) Construimos el mensaje
                    // ——————————————————————————
                    var mail = new MailMessage();
                    mail.From = new MailAddress(smtpUser, "Notificaciones ICP");
                    mail.To.Add(toEmail);
                    mail.Subject = subject;
                    mail.Body = body;
                    mail.IsBodyHtml = true;  // Permitir HTML en el cuerpo

                    // ——————————————————————————
                    // 3) Envío
                    // ——————————————————————————
                    client.Send(mail);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error enviando correo SMTP:\n{ex.Message}",
                    "Error al enviar correo",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    // **********************
    // Clase que representa cada fila de RECEPCIONES_LIN en el DataGrid.
    // Incluye la propiedad Descripcion para mostrar DES_REFERENCIA y 
    // notifica cambios si editas CantidadBuena/CantidadMala (INotifyPropertyChanged).
    // **********************
    public class RecepcionLinea : INotifyPropertyChanged
    {
        public string Albaran { get; set; }
        public int Linea { get; set; }
        public string Referencia { get; set; }
        public string Descripcion { get; set; }    // Muestra DES_REFERENCIA
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
