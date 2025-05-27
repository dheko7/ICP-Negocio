using System.Windows;

namespace ICP.Negocio
{
    public partial class MenuPrincipal : Window
    {
        public MenuPrincipal()
        {
            InitializeComponent();
        }

        private void BtnRecepciones_Click(object sender, RoutedEventArgs e)
        {
            new Recepciones().Show();
            this.Close();
        }

        private void BtnPalets_Click(object sender, RoutedEventArgs e)
        {
            new Palets().Show();
            this.Close();
        }

        private void BtnPicking_Click(object sender, RoutedEventArgs e)
        {
            new Picking().Show();
            this.Close();
        }

        private void BtnRevision_Click(object sender, RoutedEventArgs e)
        {
            new Revision().Show();
            this.Close();
        }

        private void BtnSalir_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}