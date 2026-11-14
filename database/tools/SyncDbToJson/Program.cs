using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using KasirKu.Shared.Models;
using KasirKu.Shared.Services;
using MySqlConnector;

class Program
{
    static void Main()
    {
        string connStr = "Server=localhost;Port=3306;Database=kasirku;Uid=root;Pwd=;AllowZeroDateTime=True;";
        string targetDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "KasirKu");
        Directory.CreateDirectory(targetDir);

        Console.WriteLine("Mengekstrak data dari MySQL `kasirku`...");

        using var conn = new MySqlConnection(connStr);
        conn.Open();

        // 1. Ekstrak Users
        var usersData = new
        {
            NextUserId = 100,
            Users = new List<User>()
        };

        using (var cmd = new MySqlCommand("SELECT id, username, nama_lengkap, role, password_hash, salt, aktif FROM users ORDER BY id;", conn))
        using (var rdr = cmd.ExecuteReader())
        {
            while (rdr.Read())
            {
                usersData.Users.Add(new User
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
        }
        Console.WriteLine($"-> {usersData.Users.Count} user berhasil diekstrak.");

        // 2. Ekstrak Settings
        var settings = new StoreSettings();
        using (var cmd = new MySqlCommand(@"
            SELECT nama_toko, alamat, COALESCE(kota,'') AS kota, telepon, footer_struk,
                   COALESCE(logo_path,'') AS logo_path, COALESCE(tampilkan_logo,1) AS tampilkan_logo,
                   COALESCE(printer_name,'') AS printer_name,
                   COALESCE(paper_width_mm,80) AS paper_width_mm,
                   COALESCE(auto_print_receipt,1) AS auto_print_receipt,
                   COALESCE(kick_drawer,1) AS kick_drawer
            FROM store_settings WHERE id = 1;", conn))
        using (var rdr = cmd.ExecuteReader())
        {
            if (rdr.Read())
            {
                settings.NamaToko = rdr.GetString(0);
                settings.Alamat = rdr.GetString(1);
                settings.Kota = rdr.GetString(2);
                settings.Telepon = rdr.GetString(3);
                settings.FooterStruk = rdr.GetString(4);
                settings.LogoPath = rdr.GetString(5);
                settings.TampilkanLogo = rdr.GetBoolean(6);
                settings.PrinterName = rdr.GetString(7);
                settings.PaperWidthMm = rdr.GetInt32(8);
                settings.AutoPrintReceipt = rdr.GetBoolean(9);
                settings.KickDrawer = rdr.GetBoolean(10);
            }
        }
        Console.WriteLine($"-> Profil Toko: {settings.NamaToko} ({settings.Kota})");

        // 3. Ekstrak Products
        var products = new List<Product>();
        using (var cmd = new MySqlCommand(@"
            SELECT 
                p.id, p.kode, COALESCE(p.barcode, ''), p.nama, 
                u.nama AS satuan, c.nama AS kategori, COALESCE(s.nama, '') AS supplier,
                p.harga_beli, p.harga_jual, p.stok
            FROM products p
            JOIN categories c ON c.id = p.category_id
            JOIN units u ON u.id = p.unit_id
            LEFT JOIN suppliers s ON s.id = p.supplier_id
            ORDER BY p.id;", conn))
        using (var rdr = cmd.ExecuteReader())
        {
            while (rdr.Read())
            {
                products.Add(new Product
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
        }
        Console.WriteLine($"-> {products.Count} master produk berhasil diekstrak.");

        // 4. Ekstrak Transaksi Terakhir untuk Cache Lokal (50 transaksi untuk cetak ulang / riwayat kasir)
        // Catatan: Seluruh riwayat transaksi lengkap (113.000+) tetap tersimpan aman di MySQL dan diakses on-demand
        var transactions = new List<Transaction>();
        var trxIds = new HashSet<string>();

        using (var cmd = new MySqlCommand(@"
            SELECT 
                t.id, t.tanggal, pm.nama AS metode, t.total, t.bayar
            FROM transactions t
            JOIN payment_methods pm ON pm.id = t.payment_method_id
            WHERE t.tanggal < CURDATE()
            ORDER BY t.tanggal DESC, t.id DESC
            LIMIT 50;", conn))
        using (var rdr = cmd.ExecuteReader())
        {
            while (rdr.Read())
            {
                string id = rdr.GetString(0);
                trxIds.Add(id);
                transactions.Add(new Transaction
                {
                    Id = id,
                    Tanggal = rdr.IsDBNull(1) ? DateTime.Now : rdr.GetDateTime(1),
                    MetodeBayar = rdr.GetString(2),
                    Total = rdr.GetDecimal(3),
                    Bayar = rdr.GetDecimal(4),
                    Items = new List<TransactionItem>()
                });
            }
        }
        Console.WriteLine($"-> {transactions.Count} transaksi terbaru berhasil diekstrak.");

        // 5. Ekstrak Item Transaksi untuk transaksi yang dipilih
        var trxMap = new Dictionary<string, Transaction>();
        foreach (var t in transactions) trxMap[t.Id] = t;

        using (var cmd = new MySqlCommand(@"
            DROP TEMPORARY TABLE IF EXISTS tmp_top_trxs;
            CREATE TEMPORARY TABLE tmp_top_trxs AS
            SELECT t.id FROM transactions t WHERE t.tanggal < CURDATE() ORDER BY t.tanggal DESC, t.id DESC LIMIT 50;

            SELECT 
                ti.transaction_id, ti.kode, ti.nama, ti.harga_beli, ti.harga_jual, ti.qty
            FROM transaction_items ti
            JOIN tmp_top_trxs top ON top.id = ti.transaction_id;", conn))
        {
            cmd.CommandTimeout = 120;
            using var rdr = cmd.ExecuteReader();
            int itemCount = 0;
            while (rdr.Read())
            {
                string trxId = rdr.GetString(0);
                if (trxMap.TryGetValue(trxId, out var trx))
                {
                    trx.Items.Add(new TransactionItem
                    {
                        Kode = rdr.GetString(1),
                        Nama = rdr.GetString(2),
                        HargaBeli = rdr.GetDecimal(3),
                        Harga = rdr.GetDecimal(4),
                        Qty = rdr.GetInt32(5)
                    });
                    itemCount++;
                }
            }
            Console.WriteLine($"-> {itemCount} detail item transaksi dipetakan.");
        }

        // 6. Ekstrak Mutasi Stok (100 log terbaru untuk snapshot kasir)
        var movements = new List<StockMovement>();
        using (var cmd = new MySqlCommand(@"
            SELECT 
                m.id, m.tanggal, m.product_id, p.kode, p.nama, mt.nama, m.qty,
                m.stok_sebelum, m.stok_sesudah, m.keterangan
            FROM stock_movements m
            JOIN products p ON p.id = m.product_id
            JOIN movement_types mt ON mt.id = m.movement_type_id
            ORDER BY m.tanggal DESC
            LIMIT 100;", conn))
        using (var rdr = cmd.ExecuteReader())
        {
            while (rdr.Read())
            {
                movements.Add(new StockMovement
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
        }
        Console.WriteLine($"-> {movements.Count} mutasi stok berhasil diekstrak.");

        // 7. Simpan ke JSON Lokal KasirKu
        var opts = new JsonSerializerOptions { WriteIndented = true };

        var mainData = new
        {
            NextProductId = products.Count + 10,
            NextTrx = transactions.Count + 10,
            NextMovementId = movements.Count + 10,
            Products = products,
            Transactions = transactions,
            Movements = movements
        };

        File.WriteAllText(Path.Combine(targetDir, "data.json"), JsonSerializer.Serialize(mainData, opts));
        File.WriteAllText(Path.Combine(targetDir, "settings.json"), JsonSerializer.Serialize(settings, opts));
        File.WriteAllText(Path.Combine(targetDir, "users.json"), JsonSerializer.Serialize(usersData, opts));

        Console.WriteLine("\n[BERHASIL] Sinkronisasi data ke Documents/KasirKu/ selesai!");

        Console.WriteLine("\nPengujian Autentikasi Pengguna:");
        var admin = AuthService.Login("admin", "admin123", "Manager", out string err1);
        Console.WriteLine($"-> Admin: {(admin != null ? "LOGIN SUKSES (" + admin.NamaLengkap + ")" : "GAGAL: " + err1)}");

        var ana = AuthService.Login("ana", "kasir123", "Kasir", out string err2);
        Console.WriteLine($"-> Kasir 'ana': {(ana != null ? "LOGIN SUKSES (" + ana.NamaLengkap + ")" : "GAGAL: " + err2)}");

        var kasir = AuthService.Login("kasir", "kasir123", "Kasir", out string err3);
        Console.WriteLine($"-> Kasir 'kasir': {(kasir != null ? "LOGIN SUKSES (" + kasir.NamaLengkap + ")" : "GAGAL: " + err3)}");

        // Verifikasi kondisi data.json lokal
        var diskData = DataStorageService.Load();
        int todayTrx = 0;
        foreach (var t in diskData.Transactions)
        {
            if (t.Tanggal.Date == DateTime.Today) todayTrx++;
        }
        Console.WriteLine($"\nVerifikasi data.json:");
        Console.WriteLine($"-> Produk dimuat        : {diskData.Products.Count}");
        Console.WriteLine($"-> Transaksi dimuat     : {diskData.Transactions.Count}");
        Console.WriteLine($"-> Transaksi Hari Ini   : {todayTrx} (Seharusnya 0 saat belum ada penjualan)");
        Console.WriteLine($"-> Riwayat Mutasi dimuat: {diskData.Movements.Count}");

        try
        {
            string logo = LogoService.EnsureDefaultLogoFile();
            Console.WriteLine($"-> Logo struk tersedia  : {logo}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"-> Logo struk GAGAL     : {ex.Message}");
        }
    }
}
