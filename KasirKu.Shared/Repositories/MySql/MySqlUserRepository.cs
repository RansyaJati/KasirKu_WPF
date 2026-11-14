using KasirKu.Shared.Data;
using KasirKu.Shared.Models;
using MySqlConnector;

namespace KasirKu.Shared.Repositories.MySql
{
    public class MySqlUserRepository : IUserRepository
    {
        private readonly string _connectionString;

        public MySqlUserRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        private MySqlConnection CreateConnection() => new MySqlConnection(_connectionString);

        public async Task<List<User>> GetAllAsync()
        {
            var list = new List<User>();
            using var conn = CreateConnection();
            await conn.OpenAsync();

            string query = "SELECT id, username, nama_lengkap, role, password_hash, salt, aktif FROM users ORDER BY id;";
            using var cmd = new MySqlCommand(query, conn);
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                list.Add(new User
                {
                    Id = rdr.GetInt32(0),
                    Username = rdr.GetString(1),
                    NamaLengkap = rdr.GetString(2),
                    Role = rdr.GetString(3),
                    PasswordHash = rdr.GetString(4),
                    Salt = rdr.GetString(5),
                    Aktif = rdr.GetBoolean(6)
                });
            }
            return list;
        }

        public async Task<User?> GetByUsernameAsync(string username)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            string query = "SELECT id, username, nama_lengkap, role, password_hash, salt, aktif FROM users WHERE LOWER(username) = LOWER(@u) LIMIT 1;";
            using var cmd = new MySqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@u", username.Trim());
            using var rdr = await cmd.ExecuteReaderAsync();
            if (await rdr.ReadAsync())
            {
                return new User
                {
                    Id = rdr.GetInt32(0),
                    Username = rdr.GetString(1),
                    NamaLengkap = rdr.GetString(2),
                    Role = rdr.GetString(3),
                    PasswordHash = rdr.GetString(4),
                    Salt = rdr.GetString(5),
                    Aktif = rdr.GetBoolean(6)
                };
            }
            return null;
        }

        public async Task<User> AddAsync(User user)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            string query = @"
                INSERT INTO users (username, nama_lengkap, role, password_hash, salt, aktif)
                VALUES (@u, @nama, @role, @hash, @salt, @aktif);
                SELECT LAST_INSERT_ID();";

            using var cmd = new MySqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@u", user.Username);
            cmd.Parameters.AddWithValue("@nama", user.NamaLengkap);
            cmd.Parameters.AddWithValue("@role", user.Role);
            cmd.Parameters.AddWithValue("@hash", user.PasswordHash);
            cmd.Parameters.AddWithValue("@salt", user.Salt);
            cmd.Parameters.AddWithValue("@aktif", user.Aktif);

            var newId = await cmd.ExecuteScalarAsync();
            user.Id = Convert.ToInt32(newId);
            return user;
        }

        public async Task<bool> UpdateAsync(User user)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            string query = @"
                UPDATE users SET
                    nama_lengkap = @nama,
                    role = @role,
                    password_hash = @hash,
                    salt = @salt,
                    aktif = @aktif
                WHERE id = @id OR LOWER(username) = LOWER(@u);";

            using var cmd = new MySqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@id", user.Id);
            cmd.Parameters.AddWithValue("@u", user.Username);
            cmd.Parameters.AddWithValue("@nama", user.NamaLengkap);
            cmd.Parameters.AddWithValue("@role", user.Role);
            cmd.Parameters.AddWithValue("@hash", user.PasswordHash);
            cmd.Parameters.AddWithValue("@salt", user.Salt);
            cmd.Parameters.AddWithValue("@aktif", user.Aktif);

            int rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }

        public async Task<bool> DeleteAsync(string username)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            string query = "DELETE FROM users WHERE LOWER(username) = LOWER(@u);";
            using var cmd = new MySqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@u", username.Trim());
            int rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }
    }
}
