using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KasirKu.Shared.Models;

namespace KasirKu.Shared.Services
{
    /// <summary>
    /// Data pengguna yang disimpan ke users.json.
    /// </summary>
    public class UsersData
    {
        public int NextUserId { get; set; } = 1;
        public List<User> Users { get; set; } = new();
    }

    /// <summary>
    /// Service autentikasi tanpa NuGet.
    /// Password di-hash dengan PBKDF2 (RFC-2898) bawaan .NET,
    /// salt acak 16 byte, iterasi 100.000, SHA-256.
    /// </summary>
    public static class AuthService
    {
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true
        };

        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const int Iterations = 100_000;

        public static string UsersPath =>
            Path.Combine(DataStorageService.DataDir, "users.json");

        // ── Hash & Verify ──────────────────────────────────────

        /// <summary>Buat salt acak (Base64).</summary>
        public static string GenerateSalt()
        {
            byte[] salt = new byte[SaltSize];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(salt);
            return Convert.ToBase64String(salt);
        }

        /// <summary>Hash password + salt → Base64 string.</summary>
        public static string HashPassword(string password, string saltBase64)
        {
            byte[] salt = Convert.FromBase64String(saltBase64);
            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlgorithmName.SHA256, HashSize);
            return Convert.ToBase64String(hash);
        }

        /// <summary>Verifikasi password terhadap hash yang tersimpan.</summary>
        public static bool VerifyPassword(string password, string storedHash, string storedSalt)
        {
            string computed = HashPassword(password, storedSalt);
            return CryptographicEquals(storedHash, computed);
        }

        /// <summary>Constant-time comparison untuk mencegah timing attack.</summary>
        private static bool CryptographicEquals(string a, string b)
        {
            byte[] ba = Convert.FromBase64String(a);
            byte[] bb = Convert.FromBase64String(b);
            if (ba.Length != bb.Length) return false;
            int diff = 0;
            for (int i = 0; i < ba.Length; i++)
                diff |= ba[i] ^ bb[i];
            return diff == 0;
        }

        // ── Load / Save ────────────────────────────────────────

        /// <summary>
        /// Muat data user. Jika belum ada → seed akun default.
        /// Default:
        ///   admin  / admin123   (Manager)
        ///   kasir  / kasir123   (Kasir)
        /// </summary>
        public static UsersData Load()
        {
            if (!File.Exists(UsersPath))
            {
                var data = SeedDefault();
                Save(data);
                return data;
            }
            try
            {
                string json = File.ReadAllText(UsersPath);
                var data = JsonSerializer.Deserialize<UsersData>(json, JsonOpts);
                return data ?? SeedDefault();
            }
            catch
            {
                try
                {
                    string backup = UsersPath + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                    File.Move(UsersPath, backup);
                }
                catch { }
                var data = SeedDefault();
                Save(data);
                return data;
            }
        }

        public static void Save(UsersData data)
        {
            Directory.CreateDirectory(DataStorageService.DataDir);
            string json = JsonSerializer.Serialize(data, JsonOpts);
            string tmp = UsersPath + ".tmp";
            File.WriteAllText(tmp, json, new UTF8Encoding(false));
            File.Move(tmp, UsersPath, overwrite: true);
        }

        // ── Seed default ───────────────────────────────────────

        private static UsersData SeedDefault()
        {
            var data = new UsersData();

            // Akun Manager default
            string saltAdmin = GenerateSalt();
            data.Users.Add(new User
            {
                Id = data.NextUserId++,
                Username = "admin",
                NamaLengkap = "Administrator",
                Role = "Manager",
                PasswordHash = HashPassword("admin123", saltAdmin),
                Salt = saltAdmin,
                Aktif = true
            });

            // Akun Kasir default
            string saltKasir = GenerateSalt();
            data.Users.Add(new User
            {
                Id = data.NextUserId++,
                Username = "kasir",
                NamaLengkap = "Kasir 1",
                Role = "Kasir",
                PasswordHash = HashPassword("kasir123", saltKasir),
                Salt = saltKasir,
                Aktif = true
            });

            return data;
        }

        // ── Autentikasi ────────────────────────────────────────

        /// <summary>
        /// Login: cari user aktif berdasarkan username, verifikasi password,
        /// lalu pastikan role sesuai dengan aplikasi yang memanggil.
        /// </summary>
        /// <param name="username">Username (case-insensitive)</param>
        /// <param name="password">Password plain-text</param>
        /// <param name="requiredRole">Role yang dibutuhkan: "Kasir" atau "Manager"</param>
        /// <param name="errorMessage">Pesan error jika gagal</param>
        /// <returns>User jika berhasil, null jika gagal</returns>
        public static User? Login(string username, string password, string requiredRole, out string errorMessage)
        {
            var data = Load();
            var user = data.Users.Find(u =>
                u.Username.Equals(username.Trim(), StringComparison.OrdinalIgnoreCase) && u.Aktif);

            if (user == null)
            {
                errorMessage = "Username tidak ditemukan atau akun tidak aktif.";
                return null;
            }

            if (!VerifyPassword(password, user.PasswordHash, user.Salt))
            {
                errorMessage = "Password salah.";
                return null;
            }

            if (!user.Role.Equals(requiredRole, StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = $"Akun ini bukan akun {requiredRole}. Silakan login di aplikasi {user.Role}.";
                return null;
            }

            errorMessage = string.Empty;
            return user;
        }

        // ── CRUD helpers (untuk manajemen user di Manager) ─────

        /// <summary>Tambah user baru. Kembalikan pesan error jika username sudah ada.</summary>
        public static string? AddUser(UsersData data, string username, string namaLengkap, string role, string password)
        {
            if (string.IsNullOrWhiteSpace(username)) return "Username tidak boleh kosong.";
            if (string.IsNullOrWhiteSpace(password)) return "Password tidak boleh kosong.";
            if (password.Length < 4) return "Password minimal 4 karakter.";
            if (data.Users.Exists(u => u.Username.Equals(username.Trim(), StringComparison.OrdinalIgnoreCase)))
                return $"Username \"{username}\" sudah digunakan.";

            string salt = GenerateSalt();
            data.Users.Add(new User
            {
                Id = data.NextUserId++,
                Username = username.Trim().ToLowerInvariant(),
                NamaLengkap = namaLengkap.Trim(),
                Role = role,
                PasswordHash = HashPassword(password, salt),
                Salt = salt,
                Aktif = true
            });
            Save(data);
            return null; // sukses
        }

        /// <summary>Reset password user.</summary>
        public static void ResetPassword(UsersData data, User user, string newPassword)
        {
            string salt = GenerateSalt();
            user.Salt = salt;
            user.PasswordHash = HashPassword(newPassword, salt);
            Save(data);
        }

        /// <summary>Toggle aktif/nonaktif user.</summary>
        public static void ToggleAktif(UsersData data, User user)
        {
            user.Aktif = !user.Aktif;
            Save(data);
        }

        /// <summary>Hapus user (hard delete).</summary>
        public static void DeleteUser(UsersData data, User user)
        {
            data.Users.Remove(user);
            Save(data);
        }
    }
}
