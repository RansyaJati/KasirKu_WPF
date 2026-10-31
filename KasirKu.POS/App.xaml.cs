using System.Windows;

namespace KasirKu.POS
{
    public partial class App : Application
    {
        private void App_Startup(object sender, StartupEventArgs e)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show("Terjadi kesalahan aplikasi:\n" + args.Exception.Message, "Error Sistem", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            var login = new LoginWindow();
            bool? result = login.ShowDialog();
            if (result == true && login.LoggedInUser != null)
            {
                var main = new MainWindow();
                main.SetLoggedInUser(login.LoggedInUser);
                MainWindow = main;
                ShutdownMode = ShutdownMode.OnMainWindowClose;
                main.Show();
            }
            else
            {
                Shutdown();
            }
        }
    }
}
