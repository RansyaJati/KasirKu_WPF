using KasirKu.Shared.Data;
using KasirKu.Shared.Models;
using MySqlConnector;

namespace KasirKu.Shared.Repositories.MySql
{
    public class MySqlStockMovementRepository : IStockMovementRepository
    {
        private readonly string _connectionString;

        public MySqlStockMovementRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        private MySqlConnection CreateConnection() => new MySqlConnection(_connectionString);

        public async Task<List<StockMovement>> GetAllAsync()
        {
            var list = new List<StockMovement>();
            using var conn = CreateConnection();
            await conn.OpenAsync();

            string query = @"
                SELECT 
                    m.id, m.tanggal, m.product_id, COALESCE(p.kode, '') AS kode, COALESCE(p.nama, '') AS nama,
                    COALESCE(mt.nama, 'Masuk') AS jenis, m.qty,
                    m.stok_sebelum, m.stok_sesudah, COALESCE(m.keterangan, '') AS keterangan
                FROM stock_movements m
                LEFT JOIN products p ON p.id = m.product_id
                LEFT JOIN movement_types mt ON mt.id = m.movement_type_id
                ORDER BY m.tanggal DESC, m.id DESC
                LIMIT 5000;";

            using var cmd = new MySqlCommand(query, conn);
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                list.Add(new StockMovement
                {
                    Id = rdr.GetInt32(0),
                    Tanggal = rdr.IsDBNull(1) ? DateTime.Now : rdr.GetDateTime(1),
                    ProductId = rdr.GetInt32(2),
                    Kode = rdr.GetString(3),
                    Nama = rdr.GetString(4),
                    Jenis = rdr.GetString(5),
                    Qty = rdr.GetInt32(6),
                    StokSebelum = rdr.GetInt32(7),
                    StokSesudah = rdr.GetInt32(8),
                    Keterangan = rdr.GetString(9)
                });
            }

            return list;
        }

        public async Task<List<StockMovement>> GetByProductIdAsync(int productId)
        {
            var list = new List<StockMovement>();
            using var conn = CreateConnection();
            await conn.OpenAsync();

            string query = @"
                SELECT 
                    m.id, m.tanggal, m.product_id, COALESCE(p.kode, '') AS kode, COALESCE(p.nama, '') AS nama,
                    COALESCE(mt.nama, 'Masuk') AS jenis, m.qty,
                    m.stok_sebelum, m.stok_sesudah, COALESCE(m.keterangan, '') AS keterangan
                FROM stock_movements m
                LEFT JOIN products p ON p.id = m.product_id
                LEFT JOIN movement_types mt ON mt.id = m.movement_type_id
                WHERE m.product_id = @pid
                ORDER BY m.tanggal DESC, m.id DESC;";

            using var cmd = new MySqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@pid", productId);
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                list.Add(new StockMovement
                {
                    Id = rdr.GetInt32(0),
                    Tanggal = rdr.IsDBNull(1) ? DateTime.Now : rdr.GetDateTime(1),
                    ProductId = rdr.GetInt32(2),
                    Kode = rdr.GetString(3),
                    Nama = rdr.GetString(4),
                    Jenis = rdr.GetString(5),
                    Qty = rdr.GetInt32(6),
                    StokSebelum = rdr.GetInt32(7),
                    StokSesudah = rdr.GetInt32(8),
                    Keterangan = rdr.GetString(9)
                });
            }

            return list;
        }

        public async Task<StockMovement> AddAsync(StockMovement movement)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            int typeId = movement.Jenis.Equals("Masuk", StringComparison.OrdinalIgnoreCase) ? 1 : 2;

            string query = @"
                INSERT INTO stock_movements (tanggal, product_id, movement_type_id, qty, stok_sebelum, stok_sesudah, keterangan)
                VALUES (@tanggal, @pid, @typeId, @qty, @seb, @ses, @ket);
                SELECT LAST_INSERT_ID();";

            using var cmd = new MySqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@tanggal", movement.Tanggal);
            cmd.Parameters.AddWithValue("@pid", movement.ProductId);
            cmd.Parameters.AddWithValue("@typeId", typeId);
            cmd.Parameters.AddWithValue("@qty", movement.Qty);
            cmd.Parameters.AddWithValue("@seb", movement.StokSebelum);
            cmd.Parameters.AddWithValue("@ses", movement.StokSesudah);
            cmd.Parameters.AddWithValue("@ket", movement.Keterangan);

            var newId = await cmd.ExecuteScalarAsync();
            movement.Id = Convert.ToInt32(newId);
            return movement;
        }
    }
}
