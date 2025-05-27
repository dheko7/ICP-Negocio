using System;
using System.Windows;
using System.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;

namespace ICP.Negocio
{
    public partial class MainWindow : Window
    {
        private string connectionString = "Server=localhost\\SQLEXPRESS01;Database=bdsaid;Integrated Security=True;MultipleActiveResultSets=True;";

        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnIntro_Click(object sender, RoutedEventArgs e)
        {
            string codigoCliente = txtCodigoCliente.Text;
            string contraseña = txtContraseña.Password;

            if (string.IsNullOrEmpty(codigoCliente) || string.IsNullOrEmpty(contraseña))
            {
                MessageBox.Show("Por favor, complete todos los campos.", "Error");
                return;
            }

            try
            {
                string hashedPassword = HashPassword(contraseña);

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("SELECT COUNT(*) FROM USUARIOS WHERE Usuario = @Usuario AND ContraseñaHash = @Contraseña", conn))
                    {
                        cmd.Parameters.AddWithValue("@Usuario", codigoCliente);
                        cmd.Parameters.AddWithValue("@Contraseña", hashedPassword);
                        int count = (int)cmd.ExecuteScalar();
                        if (count > 0)
                        {
                            var menuPrincipal = new MenuPrincipal();
                            menuPrincipal.Show();
                            this.Close();
                        }
                        else
                        {
                            MessageBox.Show("Credenciales incorrectas.", "Error");
                        }
                    }
                }
            }
            catch (SqlException ex)
            {
                MessageBox.Show($"Error de conexión o base de datos: {ex.Message}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error inesperado: {ex.Message}");
            }
        }

        private string HashPassword(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password.Trim()));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }
}