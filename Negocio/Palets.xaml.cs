// Palets.xaml.cs
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Windows;

namespace ICP.Negocio
{
    public partial class Palets : Window
    {
        private ObservableCollection<Palet> palets = new ObservableCollection<Palet>();
        private ObservableCollection<string> ubicaciones = new ObservableCollection<string>();
        private string connectionString =
            "Server=localhost\\SQLEXPRESS01;Database=bdsaid;Integrated Security=True;MultipleActiveResultSets=True;";

        public Palets()
        {
            InitializeComponent();
            PaletsGrid.ItemsSource = palets;
            cmbUbicaciones.ItemsSource = ubicaciones;
            CargarUbicaciones();
            CargarPalets();  // Al iniciar, carga todos los palets existentes
        }

        // **********************
        // Se dispara cada vez que la ventana recibe el foco (por ejemplo, al volver desde “Recepciones”)
        // **********************
        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            CargarUbicaciones(); // Asegura que la lista de ubicaciones esté al día
            CargarPalets();      // Recarga todos los palets en pantalla
        }

        private void CargarUbicaciones()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("SELECT UBICACION FROM UBICACIONES", conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        ubicaciones.Clear();
                        while (reader.Read())
                        {
                            ubicaciones.Add(reader.GetString(0));
                        }
                    }
                }

                if (ubicaciones.Count > 0 && cmbUbicaciones.SelectedIndex == -1)
                    cmbUbicaciones.SelectedIndex = 0;
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

        // **********************
        // Este método lee **todos** los pallets de la tabla PALETS y los muestra.
        // **********************
        private void CargarPalets(string filtroAlbaran = null, string filtroReferencia = null)
        {
            palets.Clear();
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string query = @"
                        SELECT 
                            PALET, 
                            REFERENCIA, 
                            CANTIDAD, 
                            ALBARAN_RECEPCION, 
                            UBICACION, 
                            ESTATUS_PALET
                        FROM PALETS";

                    bool hasFilter = false;
                    if (!string.IsNullOrEmpty(filtroAlbaran))
                    {
                        query += " WHERE ALBARAN_RECEPCION LIKE @FiltroAlbaran";
                        hasFilter = true;
                    }
                    if (!string.IsNullOrEmpty(filtroReferencia))
                    {
                        query += hasFilter
                            ? " AND REFERENCIA LIKE @FiltroReferencia"
                            : " WHERE REFERENCIA LIKE @FiltroReferencia";
                    }

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        if (!string.IsNullOrEmpty(filtroAlbaran))
                            cmd.Parameters.AddWithValue("@FiltroAlbaran", "%" + filtroAlbaran + "%");
                        if (!string.IsNullOrEmpty(filtroReferencia))
                            cmd.Parameters.AddWithValue("@FiltroReferencia", "%" + filtroReferencia + "%");

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                palets.Add(new Palet
                                {
                                    PaletId = reader.GetInt32(reader.GetOrdinal("PALET")),
                                    Referencia = reader.GetString(reader.GetOrdinal("REFERENCIA")),
                                    Cantidad = reader.GetInt32(reader.GetOrdinal("CANTIDAD")),
                                    Albaran = reader.GetString(reader.GetOrdinal("ALBARAN_RECEPCION")),
                                    Ubicacion = reader.GetString(reader.GetOrdinal("UBICACION")),
                                    EstatusPalet = reader.GetInt32(reader.GetOrdinal("ESTATUS_PALET"))
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
            txtFiltroAlbaran.Text = "";
            txtFiltroReferencia.Text = "";
            CargarPalets();
        }

        private void BtnMover_Click(object sender, RoutedEventArgs e)
        {
            if (!(PaletsGrid.SelectedItem is Palet palet))
            {
                MessageBox.Show("Por favor, seleccione un palet para mover.", "Selección Requerida");
                return;
            }

            // Validar que el pallet no esté en estados que no permiten movimiento (4, 5, 6)
            if (palet.EstatusPalet == 4 || palet.EstatusPalet == 5 || palet.EstatusPalet == 6)
            {
                string statusDesc;
                switch (palet.EstatusPalet)
                {
                    case 4: statusDesc = "Ejecutado"; break;
                    case 5: statusDesc = "Revisado"; break;
                    case 6: statusDesc = "Enviado"; break;
                    default: statusDesc = "Desconocido"; break;
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
                MessageBox.Show("La nueva ubicación es la misma que la actual. Seleccione otra.", "Validación");
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
                MessageBox.Show($"Palet {palet.PaletId} movido a '{nuevaUbicacion}' correctamente.", "Éxito");
            }
            catch (SqlException ex)
            {
                MessageBox.Show($"Error al mover palet: {ex.Message}", "Error de Base de Datos");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error inesperado: {ex.Message}", "Error General");
            }
        }

        private void BtnVolver_Click(object sender, RoutedEventArgs e)
        {
            new MenuPrincipal().Show();
            this.Close();
        }
    }

    // **********************
    // POCO que representa cada fila de PALETS en el DataGrid.
    // Implementa INotifyPropertyChanged para refrescar cambios.
    // **********************
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
            get { return paletId; }
            set { paletId = value; OnPropertyChanged(nameof(PaletId)); }
        }

        public string Referencia
        {
            get { return referencia; }
            set { referencia = value; OnPropertyChanged(nameof(Referencia)); }
        }

        public int Cantidad
        {
            get { return cantidad; }
            set { cantidad = value; OnPropertyChanged(nameof(Cantidad)); }
        }

        public string Albaran
        {
            get { return albaran; }
            set { albaran = value; OnPropertyChanged(nameof(Albaran)); }
        }

        public string Ubicacion
        {
            get { return ubicacion; }
            set { ubicacion = value; OnPropertyChanged(nameof(Ubicacion)); }
        }

        public int EstatusPalet
        {
            get { return estatusPalet; }
            set { estatusPalet = value; OnPropertyChanged(nameof(EstatusPalet)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propiedad)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propiedad));
        }
    }
}
