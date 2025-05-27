using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Controls;

namespace ICP.Negocio
{
    public partial class Revision : Window
    {
        private readonly string _connectionString =
            "Server=localhost\\SQLEXPRESS01;Database=bdsaid;Integrated Security=True;MultipleActiveResultSets=True;";

        private class RecepcionCab
        {
            public string ALBARAN { get; set; }
            public string PROVEEDOR { get; set; }
            public DateTime F_CREACION { get; set; }
        }

        private class RecepcionLin
        {
            public int LINEA { get; set; }
            public string REFERENCIA { get; set; }
            public int CANTIDAD_BUENA { get; set; }
            public string NUMERO_SERIE { get; set; }
            public string UBICACION { get; set; }
        }

        public Revision()
        {
            InitializeComponent();
            CargarRecepciones();
        }

        private void CargarRecepciones()
        {
            var lista = new List<RecepcionCab>();
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                using (var cmd = new SqlCommand(
                    "SELECT ALBARAN, PROVEEDOR, F_CREACION " +
                    "FROM RECEPCIONES_CAB WHERE ESTATUS_RECEPCION = 2", conn))
                {
                    conn.Open();
                    using (var rdr = cmd.ExecuteReader())
                        while (rdr.Read())
                            lista.Add(new RecepcionCab
                            {
                                ALBARAN = rdr.GetString(0),
                                PROVEEDOR = rdr.IsDBNull(1) ? "" : rdr.GetString(1),
                                F_CREACION = rdr.GetDateTime(2)
                            });
                }

                dgRecepciones.ItemsSource = lista;
                dgLineas.ItemsSource = null;
                btnConfirmar.IsEnabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar recepciones:\n{ex.Message}", "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DgRecepciones_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var sel = dgRecepciones.SelectedItem as RecepcionCab;
            if (sel == null)
            {
                dgLineas.ItemsSource = null;
                btnConfirmar.IsEnabled = false;
                return;
            }

            CargarLineas(sel.ALBARAN);
            btnConfirmar.IsEnabled = true;
        }

        private void CargarLineas(string albaran)
        {
            var lista = new List<RecepcionLin>();
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                using (var cmd = new SqlCommand(
                    "SELECT LINEA, REFERENCIA, CANTIDAD_BUENA, NUMERO_SERIE, UBICACION " +
                    "FROM RECEPCIONES_LIN WHERE ALBARAN = @alb", conn))
                {
                    cmd.Parameters.AddWithValue("@alb", albaran);
                    conn.Open();
                    using (var rdr = cmd.ExecuteReader())
                        while (rdr.Read())
                            lista.Add(new RecepcionLin
                            {
                                LINEA = rdr.GetInt32(0),
                                REFERENCIA = rdr.GetString(1),
                                CANTIDAD_BUENA = rdr.IsDBNull(2) ? 0 : rdr.GetInt32(2),
                                NUMERO_SERIE = rdr.IsDBNull(3) ? "" : rdr.GetString(3),
                                UBICACION = rdr.IsDBNull(4) ? "" : rdr.GetString(4)
                            });
                }

                dgLineas.ItemsSource = lista;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar líneas:\n{ex.Message}", "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnConfirmar_Click(object sender, RoutedEventArgs e)
        {
            var sel = dgRecepciones.SelectedItem as RecepcionCab;
            if (sel == null) return;

            var resp = MessageBox.Show(
                $"¿Confirmar recepción '{sel.ALBARAN}' como Enviada a ICP?",
                "Confirmar envío", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (resp != MessageBoxResult.Yes) return;

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                using (var cmd = new SqlCommand(
                    "UPDATE RECEPCIONES_CAB " +
                    "SET ESTATUS_RECEPCION = 3, F_CONFIRMACION = GETDATE() " +
                    "WHERE ALBARAN = @alb", conn))
                {
                    cmd.Parameters.AddWithValue("@alb", sel.ALBARAN);
                    conn.Open();
                    if (cmd.ExecuteNonQuery() == 0)
                        throw new Exception("No se actualizó ninguna fila. ¿Existe la recepción?");
                }

                MessageBox.Show("Recepción confirmada y enviada a ICP.", "Éxito",
                                MessageBoxButton.OK, MessageBoxImage.Information);

                CargarRecepciones();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al confirmar envío:\n{ex.Message}", "Error",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnVolver_Click(object sender, RoutedEventArgs e)
        {
            var menu = new MenuPrincipal();
            menu.Show();
            this.Close();
        }
    }
}
