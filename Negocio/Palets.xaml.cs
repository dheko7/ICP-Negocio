using System;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Windows;

namespace ICP.Negocio
{
    public partial class Palets : Window
    {
        private const string cs =
            "Server=localhost\\SQLEXPRESS01;Database=bdsaid;Integrated Security=True;MultipleActiveResultSets=True;";

        private readonly ObservableCollection<Palet> palets = new ObservableCollection<Palet>();
        private readonly ObservableCollection<string> ubicaciones = new ObservableCollection<string>();

        public Palets()
        {
            InitializeComponent();
            PaletsGrid.ItemsSource = palets;
            cmbUbicaciones.ItemsSource = ubicaciones;
            CargarUbicaciones();
            CargarPalets();
        }

        public void CargarUbicaciones()
        {
            ubicaciones.Clear();
            using (var conn = new SqlConnection(cs))
            using (var cmd = new SqlCommand("SELECT UBICACION FROM UBICACIONES ORDER BY UBICACION", conn))
            {
                conn.Open();
                using (var rdr = cmd.ExecuteReader())
                    while (rdr.Read())
                        ubicaciones.Add(rdr.GetString(0));
            }
            if (ubicaciones.Count > 0 && cmbUbicaciones.SelectedIndex == -1)
                cmbUbicaciones.SelectedIndex = 0;
        }

        // **AHORA PÚBLICO** para que Recepciones pueda llamar p.CargarPalets()
        public void CargarPalets(string fA = null, string fR = null)
        {
            palets.Clear();
            using (var conn = new SqlConnection(cs))
            {
                conn.Open();
                var sql = "SELECT PALET,REFERENCIA,CANTIDAD,ALBARAN_RECEPCION,UBICACION,ESTATUS_PALET FROM PALETS";
                bool filA = !string.IsNullOrWhiteSpace(fA),
                     filR = !string.IsNullOrWhiteSpace(fR);
                if (filA) sql += " WHERE ALBARAN_RECEPCION LIKE @fA";
                if (filR) sql += filA
                    ? " AND REFERENCIA LIKE @fR"
                    : " WHERE REFERENCIA LIKE @fR";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    if (filA) cmd.Parameters.AddWithValue("@fA", "%" + fA + "%");
                    if (filR) cmd.Parameters.AddWithValue("@fR", "%" + fR + "%");
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                            palets.Add(new Palet
                            {
                                PaletId = rdr.GetInt32(0),
                                Referencia = rdr.GetString(1),
                                Cantidad = rdr.GetInt32(2),
                                Albaran = rdr.IsDBNull(3) ? "" : rdr.GetString(3),
                                Ubicacion = rdr.IsDBNull(4) ? "" : rdr.GetString(4),
                                EstatusPalet = rdr.GetInt32(5)
                            });
                    }
                }
            }
        }

        private void BtnFiltrar_Click(object sender, RoutedEventArgs e)
            => CargarPalets(txtFiltroAlbaran.Text.Trim(), txtFiltroReferencia.Text.Trim());

        private void BtnLimpiarFiltro_Click(object sender, RoutedEventArgs e)
        {
            txtFiltroAlbaran.Clear();
            txtFiltroReferencia.Clear();
            CargarPalets();
        }

        private void BtnMover_Click(object sender, RoutedEventArgs e)
        {
            if (!(PaletsGrid.SelectedItem is Palet p)) return;
            if (p.EstatusPalet >= 3)
            {
                MessageBox.Show("No se puede mover un palet enviado.", "Error");
                return;
            }
            var nueva = cmbUbicaciones.SelectedItem as string;
            if (nueva == p.Ubicacion) return;

            using (var conn = new SqlConnection(cs))
            using (var cmd = new SqlCommand("PA_MOVER_PALET", conn))
            {
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@PaletId", p.PaletId);
                cmd.Parameters.AddWithValue("@NuevaUbicacion", nueva);
                conn.Open();
                cmd.ExecuteNonQuery();
            }

            p.Ubicacion = nueva;
            PaletsGrid.Items.Refresh();
            MessageBox.Show("Palet movido correctamente.", "Éxito");
        }

        private void BtnVolver_Click(object sender, RoutedEventArgs e)
        {
            new MenuPrincipal().Show();
            Close();
        }
    }

    public class Palet
    {
        public int PaletId { get; set; }
        public string Referencia { get; set; }
        public int Cantidad { get; set; }
        public string Albaran { get; set; }
        public string Ubicacion { get; set; }
        public int EstatusPalet { get; set; }
    }
}
