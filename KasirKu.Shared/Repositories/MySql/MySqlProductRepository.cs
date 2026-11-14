using System.Data.Common;
using KasirKu.Shared.Data;
using KasirKu.Shared.Models;
using MySqlConnector;

namespace KasirKu.Shared.Repositories.MySql
{
    public class MySqlProductRepository : IProductRepository
    {
        private readonly string _connectionString;

        public MySqlProductRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        private MySqlConnection CreateConnection() => new MySqlConnection(_connectionString);

        public async Task<List<Product>> GetAllAsync()
        {
            var list = new List<Product>();
            using var conn = CreateConnection();
            await conn.OpenAsync();

            string query = @"
                SELECT 
                    p.id, p.kode, COALESCE(p.barcode, ''), p.nama, 
                    COALESCE(u.nama, 'pcs') AS satuan, 
                    COALESCE(c.nama, 'Umum') AS kategori, 
                    COALESCE(s.nama, '') AS supplier,
                    p.harga_beli, p.harga_jual, p.stok
                FROM products p
                LEFT JOIN categories c ON c.id = p.category_id
                LEFT JOIN units u ON u.id = p.unit_id
                LEFT JOIN suppliers s ON s.id = p.supplier_id
                ORDER BY p.id;";

            using var cmd = new MySqlCommand(query, conn);
            using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                list.Add(new Product
                {
                    Id = rdr.GetInt32(0),
                    Kode = rdr.GetString(1),
                    Barcode = rdr.GetString(2),
                    Nama = rdr.GetString(3),
                    Satuan = rdr.GetString(4),
                    Kategori = rdr.GetString(5),
                    Supplier = rdr.GetString(6),
                    HargaBeli = rdr.GetDecimal(7),
                    Harga = rdr.GetDecimal(8),
                    Stok = rdr.GetInt32(9)
                });
            }
            return list;
        }

        public async Task<Product?> GetByIdAsync(int id)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            string query = @"
                SELECT 
                    p.id, p.kode, COALESCE(p.barcode, ''), p.nama, 
                    COALESCE(u.nama, 'pcs') AS satuan, 
                    COALESCE(c.nama, 'Umum') AS kategori, 
                    COALESCE(s.nama, '') AS supplier,
                    p.harga_beli, p.harga_jual, p.stok
                FROM products p
                LEFT JOIN categories c ON c.id = p.category_id
                LEFT JOIN units u ON u.id = p.unit_id
                LEFT JOIN suppliers s ON s.id = p.supplier_id
                WHERE p.id = @id LIMIT 1;";

            using var cmd = new MySqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@id", id);
            using var rdr = await cmd.ExecuteReaderAsync();
            if (await rdr.ReadAsync())
            {
                return new Product
                {
                    Id = rdr.GetInt32(0),
                    Kode = rdr.GetString(1),
                    Barcode = rdr.GetString(2),
                    Nama = rdr.GetString(3),
                    Satuan = rdr.GetString(4),
                    Kategori = rdr.GetString(5),
                    Supplier = rdr.GetString(6),
                    HargaBeli = rdr.GetDecimal(7),
                    Harga = rdr.GetDecimal(8),
                    Stok = rdr.GetInt32(9)
                };
            }
            return null;
        }

        public async Task<Product?> GetByKodeOrBarcodeAsync(string codeOrBarcode)
        {
            string key = codeOrBarcode.Trim();
            using var conn = CreateConnection();
            await conn.OpenAsync();

            string query = @"
                SELECT 
                    p.id, p.kode, COALESCE(p.barcode, ''), p.nama, 
                    COALESCE(u.nama, 'pcs') AS satuan, 
                    COALESCE(c.nama, 'Umum') AS kategori, 
                    COALESCE(s.nama, '') AS supplier,
                    p.harga_beli, p.harga_jual, p.stok
                FROM products p
                LEFT JOIN categories c ON c.id = p.category_id
                LEFT JOIN units u ON u.id = p.unit_id
                LEFT JOIN suppliers s ON s.id = p.supplier_id
                WHERE p.kode = @key OR p.barcode = @key LIMIT 1;";

            using var cmd = new MySqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@key", key);
            using var rdr = await cmd.ExecuteReaderAsync();
            if (await rdr.ReadAsync())
            {
                return new Product
                {
                    Id = rdr.GetInt32(0),
                    Kode = rdr.GetString(1),
                    Barcode = rdr.GetString(2),
                    Nama = rdr.GetString(3),
                    Satuan = rdr.GetString(4),
                    Kategori = rdr.GetString(5),
                    Supplier = rdr.GetString(6),
                    HargaBeli = rdr.GetDecimal(7),
                    Harga = rdr.GetDecimal(8),
                    Stok = rdr.GetInt32(9)
                };
            }
            return null;
        }

        public async Task<Product> AddAsync(Product product)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            // Cek atau buat category_id
            int catId = await EnsureCategoryAsync(conn, product.Kategori);
            int unitId = await EnsureUnitAsync(conn, product.Satuan);
            int? suppId = await EnsureSupplierAsync(conn, product.Supplier);

            string query = @"
                INSERT INTO products (kode, barcode, nama, category_id, unit_id, supplier_id, harga_beli, harga_jual, stok, aktif)
                VALUES (@kode, @barcode, @nama, @catId, @unitId, @suppId, @beli, @jual, @stok, 1);
                SELECT LAST_INSERT_ID();";

            using var cmd = new MySqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@kode", product.Kode);
            cmd.Parameters.AddWithValue("@barcode", string.IsNullOrWhiteSpace(product.Barcode) ? DBNull.Value : product.Barcode);
            cmd.Parameters.AddWithValue("@nama", product.Nama);
            cmd.Parameters.AddWithValue("@catId", catId);
            cmd.Parameters.AddWithValue("@unitId", unitId);
            cmd.Parameters.AddWithValue("@suppId", suppId.HasValue ? (object)suppId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@beli", product.HargaBeli);
            cmd.Parameters.AddWithValue("@jual", product.HargaJual);
            cmd.Parameters.AddWithValue("@stok", product.Stok);

            var newId = await cmd.ExecuteScalarAsync();
            product.Id = Convert.ToInt32(newId);
            return product;
        }

        public async Task<bool> UpdateAsync(Product product)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            int catId = await EnsureCategoryAsync(conn, product.Kategori);
            int unitId = await EnsureUnitAsync(conn, product.Satuan);
            int? suppId = await EnsureSupplierAsync(conn, product.Supplier);

            string query = @"
                UPDATE products SET
                    kode = @kode,
                    barcode = @barcode,
                    nama = @nama,
                    category_id = @catId,
                    unit_id = @unitId,
                    supplier_id = @suppId,
                    harga_beli = @beli,
                    harga_jual = @jual,
                    stok = @stok
                WHERE id = @id;";

            using var cmd = new MySqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@id", product.Id);
            cmd.Parameters.AddWithValue("@kode", product.Kode);
            cmd.Parameters.AddWithValue("@barcode", string.IsNullOrWhiteSpace(product.Barcode) ? DBNull.Value : product.Barcode);
            cmd.Parameters.AddWithValue("@nama", product.Nama);
            cmd.Parameters.AddWithValue("@catId", catId);
            cmd.Parameters.AddWithValue("@unitId", unitId);
            cmd.Parameters.AddWithValue("@suppId", suppId.HasValue ? (object)suppId.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@beli", product.HargaBeli);
            cmd.Parameters.AddWithValue("@jual", product.HargaJual);
            cmd.Parameters.AddWithValue("@stok", product.Stok);

            int rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            string query = "DELETE FROM products WHERE id = @id;";
            using var cmd = new MySqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@id", id);
            int rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }

        private async Task<int> EnsureCategoryAsync(MySqlConnection conn, string? categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName)) categoryName = "Umum";
            using var cmdFind = new MySqlCommand("SELECT id FROM categories WHERE nama = @nama LIMIT 1;", conn);
            cmdFind.Parameters.AddWithValue("@nama", categoryName.Trim());
            var res = await cmdFind.ExecuteScalarAsync();
            if (res != null) return Convert.ToInt32(res);

            using var cmdIns = new MySqlCommand("INSERT INTO categories (nama) VALUES (@nama); SELECT LAST_INSERT_ID();", conn);
            cmdIns.Parameters.AddWithValue("@nama", categoryName.Trim());
            return Convert.ToInt32(await cmdIns.ExecuteScalarAsync());
        }

        private async Task<int> EnsureUnitAsync(MySqlConnection conn, string? unitName)
        {
            if (string.IsNullOrWhiteSpace(unitName)) unitName = "pcs";
            using var cmdFind = new MySqlCommand("SELECT id FROM units WHERE nama = @nama LIMIT 1;", conn);
            cmdFind.Parameters.AddWithValue("@nama", unitName.Trim().ToLowerInvariant());
            var res = await cmdFind.ExecuteScalarAsync();
            if (res != null) return Convert.ToInt32(res);

            using var cmdIns = new MySqlCommand("INSERT INTO units (nama) VALUES (@nama); SELECT LAST_INSERT_ID();", conn);
            cmdIns.Parameters.AddWithValue("@nama", unitName.Trim().ToLowerInvariant());
            return Convert.ToInt32(await cmdIns.ExecuteScalarAsync());
        }

        private async Task<int?> EnsureSupplierAsync(MySqlConnection conn, string? supplierName)
        {
            if (string.IsNullOrWhiteSpace(supplierName)) return null;
            using var cmdFind = new MySqlCommand("SELECT id FROM suppliers WHERE nama = @nama LIMIT 1;", conn);
            cmdFind.Parameters.AddWithValue("@nama", supplierName.Trim());
            var res = await cmdFind.ExecuteScalarAsync();
            if (res != null) return Convert.ToInt32(res);

            using var cmdIns = new MySqlCommand("INSERT INTO suppliers (nama) VALUES (@nama); SELECT LAST_INSERT_ID();", conn);
            cmdIns.Parameters.AddWithValue("@nama", supplierName.Trim());
            return Convert.ToInt32(await cmdIns.ExecuteScalarAsync());
        }
    }
}
