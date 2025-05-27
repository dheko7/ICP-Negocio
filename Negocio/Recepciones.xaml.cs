using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace ICP.Negocio
{
    public partial class Recepciones : Window
    {
        private ObservableCollection<RecepcionLinea> lineasMaterial = new ObservableCollection<RecepcionLinea>();
        private bool recepcionConfirmada = false;
        private string connectionString = "Server=localhost\\SQLEXPRESS01;Database=bdsaid;Integrated Security=True;MultipleActiveResultSets=True;";

        public Recepciones()
        {
            InitializeComponent();
            RecepcionesGrid.ItemsSource = lineasMaterial;
            CargarUbicaciones();
        }

        private void CargarUbicaciones()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("SELECT UBICACION FROM UBICACIONES", conn))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                cmbUbicaciones.Items.Add(reader["UBICACION"].ToString());
                            }
                        }
                    }
                }
                if (cmbUbicaciones.Items.Count > 0) cmbUbicaciones.SelectedIndex = 0;
            }
            catch (SqlException ex)
            {
                MessageBox.Show($"Error al cargar ubicaciones: {ex.Message}", "Error de Base de Datos");
            }
        }

        private void BtnCargar_Click(object sender, RoutedEventArgs e) => CargarRecepciones();

        private void BtnNuevaRecepcion_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtAlbaran.Text))
            {
                MessageBox.Show("Por favor, ingresa un número de albarán.", "Campo Requerido");
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM RECEPCIONES_CAB WHERE ALBARAN = @Albaran", conn))
                    {
                        cmd.Parameters.AddWithValue("@Albaran", txtAlbaran.Text.Trim());
                        if ((int)cmd.ExecuteScalar() > 0)
                        {
                            MessageBox.Show($"El albarán '{txtAlbaran.Text}' ya existe.", "Albarán Duplicado");
                            return;
                        }
                    }

                    using (SqlCommand cmd = new SqlCommand("INSERT INTO RECEPCIONES_CAB (ALBARAN, PROVEEDOR, F_CREACION, ESTATUS_RECEPCION, CODIGO_CLIENTE) VALUES (@Albaran, @Proveedor, @Fecha, @Estatus, @Cliente)", conn))
                    {
                        cmd.Parameters.AddWithValue("@Albaran", txtAlbaran.Text.Trim());
                        cmd.Parameters.AddWithValue("@Proveedor", "PROV002");
                        cmd.Parameters.AddWithValue("@Fecha", DateTime.Now);
                        cmd.Parameters.AddWithValue("@Estatus", 0);
                        cmd.Parameters.AddWithValue("@Cliente", "CLI002");
                        cmd.ExecuteNonQuery();
                    }

                    MessageBox.Show($"Nueva recepción '{txtAlbaran.Text}' creada exitosamente.", "Éxito");
                    CargarRecepciones();
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show($"Error de base de datos: {ex.Message}", "Error de Conexión");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error inesperado: {ex.Message}", "Error General");
            }
        }

        private void CargarRecepciones()
        {
            lineasMaterial.Clear();
            if (string.IsNullOrEmpty(txtAlbaran.Text))
            {
                MessageBox.Show("Por favor, ingresa un número de albarán.", "Campo Requerido");
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM RECEPCIONES_CAB WHERE ALBARAN = @Albaran", conn))
                    {
                        cmd.Parameters.AddWithValue("@Albaran", txtAlbaran.Text.Trim());
                        if ((int)cmd.ExecuteScalar() == 0)
                        {
                            MessageBox.Show($"El albarán '{txtAlbaran.Text}' no existe.", "Albarán No Encontrado");
                            return;
                        }
                    }

                    using (SqlCommand cmd = new SqlCommand(@"
                        SELECT rl.ALBARAN, rl.LINEA, rl.REFERENCIA, rl.CANTIDAD, rl.CANTIDAD_BUENA, rl.CANTIDAD_MALA, 
                               rl.UBICACION, rl.NUMERO_SERIE, rl.PALETID, p.ESTATUS_PALET
                        FROM RECEPCIONES_LIN rl
                        LEFT JOIN PALETS p ON rl.PALETID = p.PALET
                        WHERE rl.ALBARAN = @Albaran", conn))
                    {
                        cmd.Parameters.AddWithValue("@Albaran", txtAlbaran.Text.Trim());
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var linea = new RecepcionLinea
                                {
                                    Albaran = reader["ALBARAN"].ToString(),
                                    Linea = (int)reader["LINEA"],
                                    Referencia = reader["REFERENCIA"].ToString(),
                                    CantidadBuena = reader["CANTIDAD_BUENA"] != DBNull.Value ? (int)reader["CANTIDAD_BUENA"] : 0,
                                    CantidadMala = reader["CANTIDAD_MALA"] != DBNull.Value ? (int)reader["CANTIDAD_MALA"] : 0,
                                    NumeroSerie = reader["NUMERO_SERIE"] != DBNull.Value ? reader["NUMERO_SERIE"].ToString() : "",
                                    PaletId = reader["PALETID"] != DBNull.Value ? (int)reader["PALETID"] : 0,
                                    Ubicacion = reader["UBICACION"] != DBNull.Value ? reader["UBICACION"].ToString() : "UBI001",
                                    EstatusPalet = reader["ESTATUS_PALET"] != DBNull.Value ? (int)reader["ESTATUS_PALET"] : 0
                                };
                                lineasMaterial.Add(linea);
                            }
                        }
                    }

                    using (SqlCommand cmd = new SqlCommand("SELECT ESTATUS_RECEPCION FROM RECEPCIONES_CAB WHERE ALBARAN = @Albaran", conn))
                    {
                        cmd.Parameters.AddWithValue("@Albaran", txtAlbaran.Text.Trim());
                        recepcionConfirmada = (int)cmd.ExecuteScalar() == 3;
                    }
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show($"Error al cargar recepciones: {ex.Message}", "Error de Base de Datos");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error inesperado: {ex.Message}", "Error General");
            }
        }

        private void BtnAgregar_Click(object sender, RoutedEventArgs e)
        {
            if (recepcionConfirmada)
            {
                MessageBox.Show("No se puede agregar líneas después de confirmar la recepción.", "Recepción Confirmada");
                return;
            }

            if (!ValidarEntradas()) return;

            string referencia = txtReferencia.Text.Trim();
            if (!int.TryParse(txtCantidadBuena.Text, out int cantidadBuena) || cantidadBuena < 0)
            {
                MessageBox.Show("Ingresa una cantidad buena válida.", "Dato Inválido");
                return;
            }
            if (!int.TryParse(txtCantidadMala.Text, out int cantidadMala) || cantidadMala < 0)
            {
                MessageBox.Show("Ingresa una cantidad mala válida.", "Dato Inválido");
                return;
            }
            string numeroSerie = txtNumeroSerie.Text.Trim();
            string ubicacion = cmbUbicaciones.SelectedItem?.ToString() ?? "UBI001";

            AsegurarAlbaranExiste(txtAlbaran.Text.Trim());

            if (!ReferenciaExiste(referencia))
            {
                MessageBox.Show("La referencia no existe. Usa 'Referencia Desconocida' para crearla.", "Referencia Inválida");
                return;
            }

            bool llevaNSerie = VerificarLlevaNSerie(referencia);
            if (llevaNSerie && string.IsNullOrEmpty(numeroSerie))
            {
                MessageBox.Show("Esta referencia requiere un número de serie.", "Campo Requerido");
                return;
            }

            // Validar que haya al menos una cantidad
            int totalCantidad = cantidadBuena + cantidadMala;
            if (totalCantidad <= 0)
            {
                MessageBox.Show("Debes ingresar al menos una cantidad (buena o mala).", "Dato Inválido");
                return;
            }

            // Crear una sola línea con todas las cantidades
            var nuevaLinea = new RecepcionLinea
            {
                Referencia = referencia,
                CantidadBuena = cantidadBuena,
                CantidadMala = cantidadMala,
                NumeroSerie = numeroSerie,
                PaletId = 0,
                Ubicacion = ubicacion,
                Albaran = txtAlbaran.Text.Trim()
            };

            int lineaNumero = ObtenerSiguienteLinea(txtAlbaran.Text.Trim());
            nuevaLinea.Linea = lineaNumero;
            // Crear un solo palet con la cantidad total y estado inicial "Sin Liberar" (1)
            nuevaLinea.PaletId = InsertarPalet(referencia, totalCantidad, 1, txtAlbaran.Text.Trim(), ubicacion);
            InsertarRecepcionLinea(txtAlbaran.Text.Trim(), lineaNumero, nuevaLinea);
            if (llevaNSerie)
                InsertarNumeroSerie(numeroSerie, nuevaLinea.PaletId, txtAlbaran.Text.Trim());

            lineasMaterial.Add(nuevaLinea);
            CargarRecepciones();
            LimpiarCampos();
        }

        private bool ValidarEntradas()
        {
            if (string.IsNullOrEmpty(txtAlbaran.Text))
            {
                MessageBox.Show("Ingresa un número de albarán.", "Campo Requerido");
                return false;
            }
            if (string.IsNullOrEmpty(txtReferencia.Text))
            {
                MessageBox.Show("Ingresa una referencia.", "Campo Requerido");
                return false;
            }
            if (!int.TryParse(txtCantidadBuena.Text, out _) || !int.TryParse(txtCantidadMala.Text, out _))
            {
                MessageBox.Show("Ingresa cantidades válidas.", "Dato Inválido");
                return false;
            }
            if (cmbUbicaciones.SelectedItem == null)
            {
                MessageBox.Show("Selecciona una ubicación.", "Campo Requerido");
                return false;
            }
            return true;
        }

        private void LimpiarCampos()
        {
            txtReferencia.Text = "";
            txtCantidadBuena.Text = "0";
            txtCantidadMala.Text = "0";
            txtNumeroSerie.Text = "";
            if (cmbUbicaciones.Items.Count > 0) cmbUbicaciones.SelectedIndex = 0;
        }

        private void AsegurarAlbaranExiste(string albaran)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM RECEPCIONES_CAB WHERE ALBARAN = @Albaran", conn))
                    {
                        cmd.Parameters.AddWithValue("@Albaran", albaran);
                        if ((int)cmd.ExecuteScalar() == 0)
                        {
                            using (SqlCommand insertCmd = new SqlCommand("INSERT INTO RECEPCIONES_CAB (ALBARAN, PROVEEDOR, F_CREACION, ESTATUS_RECEPCION, CODIGO_CLIENTE) VALUES (@Albaran, @Proveedor, @Fecha, @Estatus, @Cliente)", conn))
                            {
                                insertCmd.Parameters.AddWithValue("@Albaran", albaran);
                                insertCmd.Parameters.AddWithValue("@Proveedor", "PROV002");
                                insertCmd.Parameters.AddWithValue("@Fecha", DateTime.Now);
                                insertCmd.Parameters.AddWithValue("@Estatus", 0);
                                insertCmd.Parameters.AddWithValue("@Cliente", "CLI002");
                                insertCmd.ExecuteNonQuery();
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show($"Error al asegurar el albarán: {ex.Message}", "Error de Base de Datos");
            }
        }

        private void BtnModificar_Click(object sender, RoutedEventArgs e)
        {
            if (recepcionConfirmada)
            {
                MessageBox.Show("No se puede modificar después de confirmar la recepción.", "Recepción Confirmada");
                return;
            }

            if (RecepcionesGrid.SelectedItem is RecepcionLinea linea)
            {
                if (linea.Linea == 0)
                {
                    MessageBox.Show("Esta línea no está guardada en la base de datos. Agrégala primero.", "Línea No Guardada");
                    return;
                }

                MessageBoxResult result = MessageBox.Show(
                    "¿Qué deseas hacer con esta línea?\n\n- Aceptar: Modificar la línea\n- Cancelar: Eliminar la línea",
                    "Modificar o Eliminar",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.OK)
                {
                    RecepcionesGrid.CommitEdit(DataGridEditingUnit.Row, true);
                    ActualizarRecepcionLinea(linea);
                    CargarRecepciones();
                    MessageBox.Show("Línea modificada exitosamente.", "Éxito");
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    EliminarRecepcionLinea(linea);
                    CargarRecepciones();
                    MessageBox.Show("Línea eliminada exitosamente.", "Éxito");
                }
            }
            else
            {
                MessageBox.Show("Selecciona una línea para modificar o eliminar.", "Selección Requerida");
            }
        }

        private void EliminarRecepcionLinea(RecepcionLinea linea)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("DELETE FROM RECEPCIONES_LIN WHERE ALBARAN = @Albaran AND LINEA = @Linea", conn))
                    {
                        cmd.Parameters.AddWithValue("@Albaran", linea.Albaran);
                        cmd.Parameters.AddWithValue("@Linea", linea.Linea);
                        int rowsAffected = cmd.ExecuteNonQuery();
                        if (rowsAffected == 0)
                        {
                            MessageBox.Show("No se pudo eliminar la línea. Verifica que exista.", "Error de Eliminación");
                        }
                    }

                    if (linea.PaletId > 0)
                    {
                        using (SqlCommand cmd = new SqlCommand("DELETE FROM NSERIES_RECEPCIONES WHERE PALET = @Palet", conn))
                        {
                            cmd.Parameters.AddWithValue("@Palet", linea.PaletId);
                            cmd.ExecuteNonQuery();
                        }

                        using (SqlCommand cmd = new SqlCommand("DELETE FROM PALETS WHERE PALET = @Palet", conn))
                        {
                            cmd.Parameters.AddWithValue("@Palet", linea.PaletId);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show($"Error al eliminar línea: {ex.Message}", "Error de Base de Datos");
            }
        }

        private void BtnNuevaRef_Click(object sender, RoutedEventArgs e)
        {
            if (recepcionConfirmada)
            {
                MessageBox.Show("No se puede agregar referencias después de confirmar.", "Recepción Confirmada");
                return;
            }

            if (!ValidarEntradas()) return;

            AsegurarAlbaranExiste(txtAlbaran.Text.Trim());
            string nuevaReferencia = "REF-NUEVA-" + Guid.NewGuid().ToString().Substring(0, 8);
            InsertarNuevaReferencia(nuevaReferencia, "Referencia Desconocida", 0, true, 10);

            int cantidadBuena = int.Parse(txtCantidadBuena.Text);
            int cantidadMala = int.Parse(txtCantidadMala.Text);
            int totalCantidad = cantidadBuena + cantidadMala;

            var nuevaLinea = new RecepcionLinea
            {
                Referencia = nuevaReferencia,
                CantidadBuena = cantidadBuena,
                CantidadMala = cantidadMala,
                NumeroSerie = txtNumeroSerie.Text.Trim(),
                PaletId = 0,
                Ubicacion = cmbUbicaciones.SelectedItem?.ToString() ?? "UBI001",
                Albaran = txtAlbaran.Text.Trim()
            };

            int lineaNumero = ObtenerSiguienteLinea(txtAlbaran.Text.Trim());
            nuevaLinea.Linea = lineaNumero;
            nuevaLinea.PaletId = InsertarPalet(nuevaReferencia, totalCantidad, 1, txtAlbaran.Text.Trim(), nuevaLinea.Ubicacion);
            InsertarRecepcionLinea(txtAlbaran.Text.Trim(), lineaNumero, nuevaLinea);
            if (!string.IsNullOrEmpty(nuevaLinea.NumeroSerie))
                InsertarNumeroSerie(nuevaLinea.NumeroSerie, nuevaLinea.PaletId, txtAlbaran.Text.Trim());

            lineasMaterial.Add(nuevaLinea);
            CargarRecepciones();
            MessageBox.Show($"Nueva referencia '{nuevaReferencia}' creada y agregada como línea.", "Éxito");
            LimpiarCampos();
        }

        private void BtnConfirmar_Click(object sender, RoutedEventArgs e)
        {
            // 1) ¿Ya está confirmada?
            if (recepcionConfirmada)
            {
                MessageBox.Show("Esta recepción ya está confirmada.",
                                "Información", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 2) ¿Tiene líneas?
            if (lineasMaterial.Count == 0)
            {
                MessageBox.Show("No hay líneas cargadas para confirmar.",
                                "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 3) Confirmación del usuario
            if (MessageBox.Show("¿Confirmar recepción y generar palets?", "Confirmar",
                                MessageBoxButton.YesNo, MessageBoxImage.Question)
                != MessageBoxResult.Yes)
            {
                return;
            }

            try
            {
                // A) Marcar cabecera como Confirmada (estado = 3)
                ConfirmarRecepcion(txtAlbaran.Text.Trim());

                // B) Enviar resumen por mail
                string resumen = GenerarResumenRecepcion();
                EnviarCorreo(txtAlbaran.Text.Trim(), resumen);

                // C) Liberar los palets (marcarlos como “asignado/liberado”)
                var etiquetas = new StringBuilder();
                foreach (var linea in lineasMaterial)
                {
                    ActualizarEstadoPalet(linea.PaletId, 3);  // ← aquí cambiamos de 2 a 3
                    int total = linea.CantidadBuena + linea.CantidadMala;
                    etiquetas.AppendLine($"Palet: {linea.PaletId} • Ref: {linea.Referencia} • Cant: {total}");
                }

                // D) Mostrar etiquetas de una vez
                MessageBox.Show("Etiquetas generadas:\n\n" + etiquetas,
                                "Impresión de Etiquetas", MessageBoxButton.OK, MessageBoxImage.Information);

                // E) Marcamos flag para no volver a confirmar
                recepcionConfirmada = true;
                MessageBox.Show("Recepción confirmada correctamente.",
                                "Éxito", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al confirmar la recepción: " + ex.Message,
                                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }




        private bool VerificarStockInsuficiente(string referencia, int cantidad)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("SELECT STOCK_DISPONIBLE FROM REFERENCIAS WHERE REFERENCIA = @Referencia", conn))
                    {
                        cmd.Parameters.AddWithValue("@Referencia", referencia);
                        object result = cmd.ExecuteScalar();
                        int stockDisponible = result != DBNull.Value ? (int)result : 0;
                        return stockDisponible < cantidad;
                    }
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show($"Error al verificar stock para la referencia {referencia}: {ex.Message}", "Error de Base de Datos");
                return true; // Asumimos que hay un problema y marcamos como bloqueado
            }
        }

        private void BtnVolver_Click(object sender, RoutedEventArgs e)
        {
            new MenuPrincipal().Show();
            this.Close();
        }

        private int ObtenerSiguienteLinea(string albaran)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("SELECT ISNULL(MAX(LINEA), 0) + 1 FROM RECEPCIONES_LIN WHERE ALBARAN = @Albaran", conn))
                    {
                        cmd.Parameters.AddWithValue("@Albaran", albaran);
                        return (int)cmd.ExecuteScalar();
                    }
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show($"Error al obtener siguiente línea: {ex.Message}", "Error de Base de Datos");
                return 1;
            }
        }

        private void InsertarRecepcionLinea(string albaran, int linea, RecepcionLinea recepcionLinea)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("INSERT INTO RECEPCIONES_LIN (ALBARAN, LINEA, REFERENCIA, CANTIDAD, CANTIDAD_BUENA, CANTIDAD_MALA, UBICACION, NUMERO_SERIE, PALETID) VALUES (@Albaran, @Linea, @Referencia, @Cantidad, @CantidadBuena, @CantidadMala, @Ubicacion, @NumeroSerie, @PaletId)", conn))
                    {
                        cmd.Parameters.AddWithValue("@Albaran", albaran);
                        cmd.Parameters.AddWithValue("@Linea", linea);
                        cmd.Parameters.AddWithValue("@Referencia", recepcionLinea.Referencia);
                        cmd.Parameters.AddWithValue("@Cantidad", recepcionLinea.CantidadBuena + recepcionLinea.CantidadMala);
                        cmd.Parameters.AddWithValue("@CantidadBuena", recepcionLinea.CantidadBuena);
                        cmd.Parameters.AddWithValue("@CantidadMala", recepcionLinea.CantidadMala);
                        cmd.Parameters.AddWithValue("@Ubicacion", recepcionLinea.Ubicacion);
                        cmd.Parameters.AddWithValue("@NumeroSerie", (object)recepcionLinea.NumeroSerie ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@PaletId", recepcionLinea.PaletId > 0 ? (object)recepcionLinea.PaletId : DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show($"Error al insertar línea: {ex.Message}", "Error de Base de Datos");
                throw;
            }
        }

        private void ActualizarRecepcionLinea(RecepcionLinea linea)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("UPDATE RECEPCIONES_LIN SET CANTIDAD = @Cantidad, CANTIDAD_BUENA = @CantidadBuena, CANTIDAD_MALA = @CantidadMala, UBICACION = @Ubicacion, NUMERO_SERIE = @NumeroSerie, PALETID = @PaletId WHERE ALBARAN = @Albaran AND LINEA = @Linea", conn))
                    {
                        cmd.Parameters.AddWithValue("@Albaran", linea.Albaran);
                        cmd.Parameters.AddWithValue("@Linea", linea.Linea);
                        cmd.Parameters.AddWithValue("@Cantidad", linea.CantidadBuena + linea.CantidadMala);
                        cmd.Parameters.AddWithValue("@CantidadBuena", linea.CantidadBuena);
                        cmd.Parameters.AddWithValue("@CantidadMala", linea.CantidadMala);
                        cmd.Parameters.AddWithValue("@Ubicacion", linea.Ubicacion);
                        cmd.Parameters.AddWithValue("@NumeroSerie", (object)linea.NumeroSerie ?? DBNull.Value);
                        cmd.Parameters.AddWithValue("@PaletId", linea.PaletId > 0 ? (object)linea.PaletId : DBNull.Value);
                        int rowsAffected = cmd.ExecuteNonQuery();
                        if (rowsAffected == 0)
                        {
                            MessageBox.Show($"No se pudo actualizar la línea. Verifica que exista (Albaran: {linea.Albaran}, Línea: {linea.Linea}).", "Error de Actualización");
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show($"Error al actualizar línea: {ex.Message}", "Error de Base de Datos");
            }
        }

        private bool ReferenciaExiste(string referencia)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM REFERENCIAS WHERE REFERENCIA = @Referencia", conn))
                    {
                        cmd.Parameters.AddWithValue("@Referencia", referencia);
                        return (int)cmd.ExecuteScalar() > 0;
                    }
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show($"Error al verificar referencia: {ex.Message}", "Error de Base de Datos");
                return false;
            }
        }

        private bool VerificarLlevaNSerie(string referencia)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("SELECT LLEVA_N_SERIES FROM REFERENCIAS WHERE REFERENCIA = @Referencia", conn))
                    {
                        cmd.Parameters.AddWithValue("@Referencia", referencia);
                        object result = cmd.ExecuteScalar();
                        return result != null && (bool)result;
                    }
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show($"Error al verificar número de serie: {ex.Message}", "Error de Base de Datos");
                return false;
            }
        }

        private void InsertarNuevaReferencia(string referencia, string descripcion, decimal precio, bool llevaNSerie, int longitudNSerie)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("INSERT INTO REFERENCIAS (REFERENCIA, DES_REFERENCIA, PRECIO, F_CREACION, LLEVA_N_SERIES, LONGITUD_N_SERIE, ESTA_HABILITADA) VALUES (@Referencia, @DesReferencia, @Precio, @Fecha, @LlevaNSerie, @LongitudNSerie, @Habilitada)", conn))
                    {
                        cmd.Parameters.AddWithValue("@Referencia", referencia);
                        cmd.Parameters.AddWithValue("@DesReferencia", descripcion);
                        cmd.Parameters.AddWithValue("@Precio", precio);
                        cmd.Parameters.AddWithValue("@Fecha", DateTime.Now);
                        cmd.Parameters.AddWithValue("@LlevaNSerie", llevaNSerie);
                        cmd.Parameters.AddWithValue("@LongitudNSerie", longitudNSerie);
                        cmd.Parameters.AddWithValue("@Habilitada", true);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show($"Error al insertar nueva referencia: {ex.Message}", "Error de Base de Datos");
            }
        }

        private int InsertarPalet(string referencia, int cantidad, int estatusPalet, string albaran, string ubicacion)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("INSERT INTO PALETS (REFERENCIA, CANTIDAD, ALBARAN_RECEPCION, UBICACION, ESTATUS_PALET, F_INSERT) OUTPUT INSERTED.PALET VALUES (@Referencia, @Cantidad, @Albaran, @Ubicacion, @EstatusPalet, @Fecha)", conn))
                    {
                        cmd.Parameters.AddWithValue("@Referencia", referencia);
                        cmd.Parameters.AddWithValue("@Cantidad", cantidad);
                        cmd.Parameters.AddWithValue("@Albaran", albaran);
                        cmd.Parameters.AddWithValue("@Ubicacion", ubicacion);
                        cmd.Parameters.AddWithValue("@EstatusPalet", estatusPalet); // Estado inicial "Sin Liberar" (1)
                        cmd.Parameters.AddWithValue("@Fecha", DateTime.Now);
                        return (int)cmd.ExecuteScalar();
                    }
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show($"Error al insertar palet: {ex.Message}", "Error de Base de Datos");
                return 0;
            }
        }

        private void InsertarNumeroSerie(string numeroSerie, int paletId, string albaran)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("INSERT INTO NSERIES_RECEPCIONES (NUMERO_SERIE, PALET, ALBARAN, F_REGISTRO) VALUES (@NumeroSerie, @Palet, @Albaran, @Fecha)", conn))
                    {
                        cmd.Parameters.AddWithValue("@NumeroSerie", numeroSerie);
                        cmd.Parameters.AddWithValue("@Palet", paletId);
                        cmd.Parameters.AddWithValue("@Albaran", albaran);
                        cmd.Parameters.AddWithValue("@Fecha", DateTime.Now);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show($"Error al insertar número de serie: {ex.Message}", "Error de Base de Datos");
            }
        }

        private void ConfirmarRecepcion(string albaran)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("PA_CONFIRMAR_RECEPCION", conn))
                    {
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Albaran", albaran);
                        cmd.Parameters.AddWithValue("@FechaConfirmacion", DateTime.Now);
                        int rowsAffected = cmd.ExecuteNonQuery();
                        if (rowsAffected == 0)
                        {
                            MessageBox.Show($"No se pudo confirmar la recepción para el albarán {albaran}. Verifica que exista.", "Advertencia");
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show($"Error al confirmar recepción: {ex.Message}", "Error de Base de Datos");
                throw;
            }
        }

        private bool ActualizarEstadoPalet(int paletId, int estatus)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand checkCmd = new SqlCommand("SELECT COUNT(*) FROM PALETS WHERE PALET = @Palet", conn))
                    {
                        checkCmd.Parameters.AddWithValue("@Palet", paletId);
                        int count = (int)checkCmd.ExecuteScalar();
                        if (count == 0)
                        {
                            return false;
                        }
                    }

                    using (SqlCommand cmd = new SqlCommand("UPDATE PALETS SET ESTATUS_PALET = @Estatus WHERE PALET = @Palet", conn))
                    {
                        cmd.Parameters.AddWithValue("@Estatus", estatus);
                        cmd.Parameters.AddWithValue("@Palet", paletId);
                        int rowsAffected = cmd.ExecuteNonQuery();
                        return rowsAffected > 0;
                    }
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show($"Error al actualizar estado del palet {paletId}: {ex.Message}", "Error de Base de Datos");
                return false;
            }
        }

        private void ImprimirEtiqueta(int paletId, string referencia, int cantidad)
        {
            string etiqueta = $"Palet: {paletId}\nCódigo de barras: [Simulado-{paletId}]\nReferencia: {referencia}\nCantidad: {cantidad}";
            MessageBox.Show($"Imprimiendo etiqueta:\n{etiqueta}", "Impresión de Etiqueta");
        }

        private string GenerarResumenRecepcion()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Resumen de recepción para albarán {txtAlbaran.Text.Trim()} - {DateTime.Now:yyyy-MM-dd HH:mm:ss} CET:");
            foreach (var linea in lineasMaterial)
            {
                sb.AppendLine($"- Palet {linea.PaletId}: Ref {linea.Referencia}, Cantidad Buena: {linea.CantidadBuena}, Cantidad Mala: {linea.CantidadMala}, Ubicación: {linea.Ubicacion}, Estatus: {linea.EstatusPalet}");
            }
            return sb.ToString();
        }

        private void EnviarCorreo(string albaran, string resumen)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("PA_ENVIAR_DBMAIL", conn))
                    {
                        cmd.CommandType = System.Data.CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@Albaran", albaran);
                        cmd.Parameters.AddWithValue("@Resumen", resumen);
                        cmd.Parameters.AddWithValue("@Destinatario", "pruebas@icp.com");
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show($"Error al enviar correo: {ex.Message}. El correo no se envió, pero la recepción se confirmó.", "Error de Correo");
            }
        }
    }

    public class RecepcionLinea : INotifyPropertyChanged
    {
        private string albaran;
        private int linea;
        private string referencia;
        private int cantidadBuena;
        private int cantidadMala;
        private string numeroSerie;
        private int paletId;
        private string ubicacion;
        private int estatusPalet;

        public string Albaran
        {
            get => albaran;
            set { albaran = value; OnPropertyChanged(nameof(Albaran)); }
        }

        public int Linea
        {
            get => linea;
            set { linea = value; OnPropertyChanged(nameof(Linea)); }
        }

        public string Referencia
        {
            get => referencia;
            set { referencia = value; OnPropertyChanged(nameof(Referencia)); }
        }

        public int CantidadBuena
        {
            get => cantidadBuena;
            set { cantidadBuena = value; OnPropertyChanged(nameof(CantidadBuena)); }
        }

        public int CantidadMala
        {
            get => cantidadMala;
            set { cantidadMala = value; OnPropertyChanged(nameof(CantidadMala)); }
        }

        public string NumeroSerie
        {
            get => numeroSerie;
            set { numeroSerie = value; OnPropertyChanged(nameof(NumeroSerie)); }
        }

        public int PaletId
        {
            get => paletId;
            set { paletId = value; OnPropertyChanged(nameof(PaletId)); }
        }

        public string Ubicacion
        {
            get => ubicacion;
            set { ubicacion = value; OnPropertyChanged(nameof(Ubicacion)); }
        }

        public int EstatusPalet
        {
            get => estatusPalet;
            set { estatusPalet = value; OnPropertyChanged(nameof(EstatusPalet)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}