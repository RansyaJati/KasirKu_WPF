using KasirKu.Shared.Data;
using KasirKu.Shared.Models;
using MySqlConnector;

namespace KasirKu.Shared.Repositories.MySql
{
    public class MySqlTransactionRepository : ITransactionRepository
    {
        private readonly string _connectionString;

        public MySqlTransactionRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        private MySqlConnection CreateConnection() => new MySqlConnection(_connectionString);

        public async Task<List<Transaction>> GetAllAsync()
        {
            return await GetRecentAsync(5000);
        }

        public async Task<List<Transaction>> GetRecentAsync(int limit = 5000)
        {
            var list = new List<Transaction>();
            using var conn = CreateConnection();
            await conn.OpenAsync();

            string queryTrx = $@"
                SELECT 
                    t.id, t.tanggal, COALESCE(pm.nama, 'Tunai') AS metode, t.total, t.bayar
                FROM transactions t
                LEFT JOIN payment_methods pm ON pm.id = t.payment_method_id
                ORDER BY t.tanggal DESC, t.id DESC
                LIMIT {limit};";

            var trxMap = new Dictionary<string, Transaction>();
            using (var cmd = new MySqlCommand(queryTrx, conn))
            using (var rdr = await cmd.ExecuteReaderAsync())
            {
                while (await rdr.ReadAsync())
                {
                    string id = rdr.GetString(0);
                    var trx = new Transaction
                    {
                        Id = id,
                        Tanggal = rdr.IsDBNull(1) ? DateTime.Now : rdr.GetDateTime(1),
                        MetodeBayar = rdr.GetString(2),
                        Total = rdr.GetDecimal(3),
                        Bayar = rdr.GetDecimal(4),
                        Items = new List<TransactionItem>()
                    };
                    list.Add(trx);
                    trxMap[id] = trx;
                }
            }

            if (list.Count > 0)
            {
                string queryItems = @"
                    DROP TEMPORARY TABLE IF EXISTS tmp_top_trxs;
                    CREATE TEMPORARY TABLE tmp_top_trxs AS
                    SELECT t.id FROM transactions t ORDER BY t.tanggal DESC, t.id DESC LIMIT " + limit + @";

                    SELECT 
                        ti.transaction_id, ti.kode, ti.nama, ti.harga_beli, ti.harga_jual, ti.qty
                    FROM transaction_items ti
                    JOIN tmp_top_trxs top ON top.id = ti.transaction_id;";

                using var cmdItems = new MySqlCommand(queryItems, conn);
                using var rdrItems = await cmdItems.ExecuteReaderAsync();
                while (await rdrItems.ReadAsync())
                {
                    string trxId = rdrItems.GetString(0);
                    if (trxMap.TryGetValue(trxId, out var trx))
                    {
                        trx.Items.Add(new TransactionItem
                        {
                            Kode = rdrItems.GetString(1),
                            Nama = rdrItems.GetString(2),
                            HargaBeli = rdrItems.GetDecimal(3),
                            Harga = rdrItems.GetDecimal(4),
                            Qty = rdrItems.GetInt32(5)
                        });
                    }
                }
            }

            return list;
        }

        public async Task<Transaction?> GetByIdAsync(string id)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            string queryTrx = @"
                SELECT 
                    t.id, t.tanggal, COALESCE(pm.nama, 'Tunai') AS metode, t.total, t.bayar
                FROM transactions t
                LEFT JOIN payment_methods pm ON pm.id = t.payment_method_id
                WHERE t.id = @id LIMIT 1;";

            Transaction? trx = null;
            using (var cmd = new MySqlCommand(queryTrx, conn))
            {
                cmd.Parameters.AddWithValue("@id", id);
                using var rdr = await cmd.ExecuteReaderAsync();
                if (await rdr.ReadAsync())
                {
                    trx = new Transaction
                    {
                        Id = rdr.GetString(0),
                        Tanggal = rdr.IsDBNull(1) ? DateTime.Now : rdr.GetDateTime(1),
                        MetodeBayar = rdr.GetString(2),
                        Total = rdr.GetDecimal(3),
                        Bayar = rdr.GetDecimal(4),
                        Items = new List<TransactionItem>()
                    };
                }
            }

            if (trx != null)
            {
                string queryItems = @"
                    SELECT kode, nama, harga_beli, harga_jual, qty
                    FROM transaction_items
                    WHERE transaction_id = @id;";

                using var cmdItems = new MySqlCommand(queryItems, conn);
                cmdItems.Parameters.AddWithValue("@id", id);
                using var rdrItems = await cmdItems.ExecuteReaderAsync();
                while (await rdrItems.ReadAsync())
                {
                    trx.Items.Add(new TransactionItem
                    {
                        Kode = rdrItems.GetString(0),
                        Nama = rdrItems.GetString(1),
                        HargaBeli = rdrItems.GetDecimal(2),
                        Harga = rdrItems.GetDecimal(3),
                        Qty = rdrItems.GetInt32(4)
                    });
                }
            }

            return trx;
        }

        public async Task<List<Transaction>> GetByDateRangeAsync(DateTime from, DateTime to)
        {
            var list = new List<Transaction>();
            using var conn = CreateConnection();
            await conn.OpenAsync();

            string queryTrx = @"
                SELECT 
                    t.id, t.tanggal, COALESCE(pm.nama, 'Tunai') AS metode, t.total, t.bayar
                FROM transactions t
                LEFT JOIN payment_methods pm ON pm.id = t.payment_method_id
                WHERE t.tanggal >= @from AND t.tanggal <= @to
                ORDER BY t.tanggal DESC, t.id DESC;";

            var trxMap = new Dictionary<string, Transaction>();
            using (var cmd = new MySqlCommand(queryTrx, conn))
            {
                cmd.Parameters.AddWithValue("@from", from);
                cmd.Parameters.AddWithValue("@to", to);
                using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    string id = rdr.GetString(0);
                    var trx = new Transaction
                    {
                        Id = id,
                        Tanggal = rdr.IsDBNull(1) ? DateTime.Now : rdr.GetDateTime(1),
                        MetodeBayar = rdr.GetString(2),
                        Total = rdr.GetDecimal(3),
                        Bayar = rdr.GetDecimal(4),
                        Items = new List<TransactionItem>()
                    };
                    list.Add(trx);
                    trxMap[id] = trx;
                }
            }

            if (list.Count > 0)
            {
                string queryItems = @"
                    SELECT 
                        ti.transaction_id, ti.kode, ti.nama, ti.harga_beli, ti.harga_jual, ti.qty
                    FROM transaction_items ti
                    JOIN transactions t ON t.id = ti.transaction_id
                    WHERE t.tanggal >= @from AND t.tanggal <= @to;";

                using var cmdItems = new MySqlCommand(queryItems, conn);
                cmdItems.Parameters.AddWithValue("@from", from);
                cmdItems.Parameters.AddWithValue("@to", to);
                using var rdrItems = await cmdItems.ExecuteReaderAsync();
                while (await rdrItems.ReadAsync())
                {
                    string trxId = rdrItems.GetString(0);
                    if (trxMap.TryGetValue(trxId, out var trx))
                    {
                        trx.Items.Add(new TransactionItem
                        {
                            Kode = rdrItems.GetString(1),
                            Nama = rdrItems.GetString(2),
                            HargaBeli = rdrItems.GetDecimal(3),
                            Harga = rdrItems.GetDecimal(4),
                            Qty = rdrItems.GetInt32(5)
                        });
                    }
                }
            }

            return list;
        }

        public async Task<Transaction> AddAsync(Transaction transaction)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();
            using var tx = await conn.BeginTransactionAsync();

            try
            {
                // Ambil payment_method_id
                int payMethodId = 1;
                using (var cmdPm = new MySqlCommand("SELECT id FROM payment_methods WHERE nama = @nama LIMIT 1;", conn, tx))
                {
                    cmdPm.Parameters.AddWithValue("@nama", string.IsNullOrWhiteSpace(transaction.MetodeBayar) ? "Tunai" : transaction.MetodeBayar);
                    var res = await cmdPm.ExecuteScalarAsync();
                    if (res != null) payMethodId = Convert.ToInt32(res);
                }

                // Insert header
                string insertTrx = @"
                    INSERT INTO transactions (id, tanggal, user_id, payment_method_id, total, bayar, catatan)
                    VALUES (@id, @tanggal, 1, @pmId, @total, @bayar, 'Transaksi KasirKu');";

                using (var cmd = new MySqlCommand(insertTrx, conn, tx))
                {
                    cmd.Parameters.AddWithValue("@id", transaction.Id);
                    cmd.Parameters.AddWithValue("@tanggal", transaction.Tanggal);
                    cmd.Parameters.AddWithValue("@pmId", payMethodId);
                    cmd.Parameters.AddWithValue("@total", transaction.Total);
                    cmd.Parameters.AddWithValue("@bayar", transaction.Bayar);
                    await cmd.ExecuteNonQueryAsync();
                }

                // Insert items & update product stock
                string insertItem = @"
                    INSERT INTO transaction_items (transaction_id, product_id, kode, nama, harga_beli, harga_jual, qty)
                    VALUES (@trxId, (SELECT id FROM products WHERE kode = @kode LIMIT 1), @kode, @nama, @beli, @jual, @qty);";

                string updateStock = @"
                    UPDATE products SET stok = GREATEST(0, stok - @qty) WHERE kode = @kode;";

                foreach (var item in transaction.Items)
                {
                    using (var cmdItem = new MySqlCommand(insertItem, conn, tx))
                    {
                        cmdItem.Parameters.AddWithValue("@trxId", transaction.Id);
                        cmdItem.Parameters.AddWithValue("@kode", item.Kode);
                        cmdItem.Parameters.AddWithValue("@nama", item.Nama);
                        cmdItem.Parameters.AddWithValue("@beli", item.HargaBeli);
                        cmdItem.Parameters.AddWithValue("@jual", item.Harga);
                        cmdItem.Parameters.AddWithValue("@qty", item.Qty);
                        await cmdItem.ExecuteNonQueryAsync();
                    }

                    using (var cmdStk = new MySqlCommand(updateStock, conn, tx))
                    {
                        cmdStk.Parameters.AddWithValue("@qty", item.Qty);
                        cmdStk.Parameters.AddWithValue("@kode", item.Kode);
                        await cmdStk.ExecuteNonQueryAsync();
                    }
                }

                await tx.CommitAsync();
                return transaction;
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
    }
}
