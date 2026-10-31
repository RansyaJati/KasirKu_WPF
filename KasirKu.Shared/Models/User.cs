namespace KasirKu.Shared.Models
{
    /// <summary>
    /// Akun pengguna aplikasi KasirKu.
    /// Role menentukan aplikasi mana yang boleh diakses:
    ///   "Kasir"   → hanya bisa login di POS
    ///   "Manager" → hanya bisa login di Manager
    /// </summary>
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string NamaLengkap { get; set; } = string.Empty;
        public string Role { get; set; } = "Kasir";          // "Kasir" | "Manager"
        public string PasswordHash { get; set; } = string.Empty;
        public string Salt { get; set; } = string.Empty;
        public bool Aktif { get; set; } = true;
    }
}
