using System.Windows;

namespace KasirKu.Manager
{
    public partial class App : Application
    {
        private void App_Startup(object sender, StartupEventArgs e)
        {
            var login = new LoginWindow();
            if (login.ShowDialog() == true && login.LoggedInUser != null)
            {
                var main = new MainWindow();
                main.SetLoggedInUser(login.LoggedInUser);
                main.Show();
            }
            else
            {
                Shutdown();
            }
        }
    }
}
