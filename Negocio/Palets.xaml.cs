using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Data.SqlClient;
using System.ComponentModel;

namespace ICP.Negocio
{
    public partial class Palets : Window
    {
        private ObservableCollection<Palet> palets = new ObservableCollection<Palet>();
        private ObservableCollection<string> ubicaciones = new ObservableCollection<string>();
        private string connectionString = "Server=localhost\\SQLEXPRESS01;Database=bdsaid;Integrated Security=True;MultipleActiveResultSets=True;";

        public Palets()
        {
            InitializeComponent();
            PaletsGrid.ItemsSource = palets;
            cmbUbicaciones.ItemsSource = ubicaciones;
            CargarUbicaciones();
            CargarPalets();
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
                            ubicaciones.Clear();
                            while (reader.Read())
                            {
                                ubicaciones.Add(reader["UBICACION"].ToString());
                            }
                            if (ubicaciones.Count == 0)
                            {
                                MessageBox.Show("No se encontraron ubicaciones en la tabla UBICACIONES.", "Información");
                            }
                            else if (ubicaciones.Count > 0 && cmbUbicaciones.SelectedIndex == -1)
                            {
                                cmbUbicaciones.SelectedIndex = 0;
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show($"Error al cargar ubicaciones: {ex.Message}", "Error de Base de Datos");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error inesperado al cargar ubicaciones: {ex.Message}", "Error General");
            }
        }

        private void CargarPalets(string filtroAlbaran = null, string filtroReferencia = null)
        {
            palets.Clear();
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT PALET, REFERENCIA, CANTIDAD, ALBARAN_RECEPCION, UBICACION, ESTATUS_PALET FROM PALETS";
                    // Eliminamos el filtro fijo de ESTATUS_PALET = 2 y lo hacemos opcional
                    bool hasFilter = false;
                    if (!string.IsNullOrEmpty(filtroAlbaran))
                    {
                        query += " WHERE ALBARAN_RECEPCION LIKE @FiltroAlbaran";
                        hasFilter = true;
                    }
                    if (!string.IsNullOrEmpty(filtroReferencia))
                    {
                        query += hasFilter ? " AND REFERENCIA LIKE @FiltroReferencia" : " WHERE REFERENCIA LIKE @FiltroReferencia";
                        hasFilter = true;
                    }
                    // Opcional: Agregar filtro por estado si se implementa un ComboBox
                    // Ejemplo: if (filtroEstatus != null) query += " AND ESTATUS_PALET = @FiltroEstatus";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        if (!string.IsNullOrEmpty(filtroAlbaran))
                            cmd.Parameters.AddWithValue("@FiltroAlbaran", $"%{filtroAlbaran}%");
                        if (!string.IsNullOrEmpty(filtroReferencia))
                            cmd.Parameters.AddWithValue("@FiltroReferencia", $"%{filtroReferencia}%");

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                palets.Add(new Palet
                                {
                                    PaletId = (int)reader["PALET"],
                                    Referencia = reader["REFERENCIA"].ToString(),
                                    Cantidad = (int)reader["CANTIDAD"],
                                    Albaran = reader["ALBARAN_RECEPCION"].ToString(),
                                    Ubicacion = reader["UBICACION"].ToString(),
                                    EstatusPalet = (int)reader["ESTATUS_PALET"]
                                });
                            }
                            if (palets.Count == 0)
                            {
                                MessageBox.Show("No se encontraron palets.", "Información");
                            }
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show($"Error al cargar palets: {ex.Message}", "Error de Base de Datos");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error inesperado al cargar palets: {ex.Message}", "Error General");
            }
        }

        private void BtnFiltrar_Click(object sender, RoutedEventArgs e)
        {
            string filtroAlbaran = txtFiltroAlbaran.Text.Trim();
            string filtroReferencia = txtFiltroReferencia.Text.Trim();
            CargarPalets(filtroAlbaran, filtroReferencia);
        }

        private void BtnLimpiarFiltro_Click(object sender, RoutedEventArgs e)
        {
            txtFiltroAlbaran.Text = string.Empty;
            txtFiltroReferencia.Text = string.Empty;
            CargarPalets();
        }

        private void BtnMover_Click(object sender, RoutedEventArgs e)
        {
            if (PaletsGrid.SelectedItem is Palet palet)
            {
                // Validate palet status for movement
                string statusDesc = "Desconocido";
                if (palet.EstatusPalet == 4 || palet.EstatusPalet == 5 || palet.EstatusPalet == 6)
                {
                    switch (palet.EstatusPalet)
                    {
                        case 4:
                            statusDesc = "Ejecutado";
                            break;
                        case 5:
                            statusDesc = "Revisado";
                            break;
                        case 6:
                            statusDesc = "Enviado";
                            break;
                    }
                    MessageBox.Show($"No se puede mover un palet en estado '{statusDesc}'.", "Estado No Permitido");
                    return;
                }

                if (cmbUbicaciones.SelectedItem == null)
                {
                    MessageBox.Show("Por favor, seleccione una nueva ubicación.", "Selección Requerida");
                    return;
                }

                string nuevaUbicacion = cmbUbicaciones.SelectedItem.ToString();
                if (nuevaUbicacion == palet.Ubicacion)
                {
                    MessageBox.Show("La nueva ubicación es la misma que la actual. Seleccione una ubicación diferente.", "Validación");
                    return;
                }

                try
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        using (SqlCommand cmd = new SqlCommand("PA_MOVER_PALET", conn))
                        {
                            cmd.CommandType = System.Data.CommandType.StoredProcedure;
                            cmd.Parameters.AddWithValue("@PaletId", palet.PaletId);
                            cmd.Parameters.AddWithValue("@NuevaUbicacion", nuevaUbicacion);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    palet.Ubicacion = nuevaUbicacion;
                    PaletsGrid.Items.Refresh();
                    MessageBox.Show($"El palet con id {palet.PaletId} movido a la ubicación {nuevaUbicacion} exitosamente.", "Éxito");
                }
                catch (SqlException ex)
                {
                    MessageBox.Show($"Error al mover palet: {ex.Message}", "Error de Base de Datos");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error inesperado al mover palet: {ex.Message}", "Error General");
                }
            }
            else
            {
                MessageBox.Show("Por favor, seleccione un palet para mover.", "Selección Requerida");
            }
        }

        private void BtnVolver_Click(object sender, RoutedEventArgs e)
        {
            MenuPrincipal menuPrincipal = new MenuPrincipal();
            menuPrincipal.Show();
            this.Close();
        }
    }

    public class Palet : INotifyPropertyChanged
    {
        private int paletId;
        private string referencia;
        private int cantidad;
        private string albaran;
        private string ubicacion;
        private int estatusPalet;

        public int PaletId
        {
            get => paletId;
            set { paletId = value; OnPropertyChanged(nameof(PaletId)); }
        }

        public string Referencia
        {
            get => referencia;
            set { referencia = value; OnPropertyChanged(nameof(Referencia)); }
        }

        public int Cantidad
        {
            get => cantidad;
            set { cantidad = value; OnPropertyChanged(nameof(Cantidad)); }
        }

        public string Albaran
        {
            get => albaran;
            set { albaran = value; OnPropertyChanged(nameof(Albaran)); }
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