using System.Windows;
using System.Windows.Input;
using KasirKu.Shared.Models;
using KasirKu.Shared.Services;

namespace KasirKu.POS
{
    public partial class LoginWindow : Window
    {
        /// <summary>User yang berhasil login (null jika belum/gagal).</summary>
        public User? LoggedInUser { get; private set; }

        public LoginWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => TxtUsername.Focus();
        }

        // ── Event handlers ─────────────────────────────────────

        private void BtnLogin_Click(object sender, RoutedEventArgs e) => DoLogin();

        private void TxtUsername_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (string.IsNullOrEmpty(TxtPassword.Password))
                    TxtPassword.Focus();
                else
                    DoLogin();
            }
        }

        private void TxtPassword_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) DoLogin();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void DragBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        // ── Login logic ────────────────────────────────────────

        private void DoLogin()
        {
            string username = TxtUsername.Text.Trim();
            string password = TxtPassword.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                ShowError("Username dan password harus diisi.");
                return;
            }

            var user = AuthService.Login(username, password, "Kasir", out string error);
            if (user == null)
            {
                ShowError(error);
                TxtPassword.Clear();
                TxtPassword.Focus();
                return;
            }

            LoggedInUser = user;
            DialogResult = true;
            Close();
        }

        private void ShowError(string msg)
        {
            TxtError.Text = msg;
            TxtError.Visibility = Visibility.Visible;
        }
    }
}
