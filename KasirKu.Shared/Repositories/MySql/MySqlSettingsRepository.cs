using KasirKu.Shared.Data;
using KasirKu.Shared.Models;
using MySqlConnector;

namespace KasirKu.Shared.Repositories.MySql
{
    public class MySqlSettingsRepository : ISettingsRepository
    {
        private readonly string _connectionString;

        public MySqlSettingsRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        private MySqlConnection CreateConnection() => new MySqlConnection(_connectionString);

        public async Task<StoreSettings> GetSettingsAsync()
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            string query = @"
                SELECT 
                    nama_toko, alamat, COALESCE(kota, '') AS kota, telepon, footer_struk,
                    COALESCE(logo_path, '') AS logo_path, COALESCE(tampilkan_logo, 1) AS tampilkan_logo,
                    COALESCE(printer_name, '') AS printer_name,
                    COALESCE(paper_width_mm, 80) AS paper_width_mm,
                    COALESCE(auto_print_receipt, 1) AS auto_print_receipt,
                    COALESCE(kick_drawer, 1) AS kick_drawer
                FROM store_settings 
                ORDER BY id ASC LIMIT 1;";

            using var cmd = new MySqlCommand(query, conn);
            using var rdr = await cmd.ExecuteReaderAsync();
            if (await rdr.ReadAsync())
            {
                return new StoreSettings
                {
                    NamaToko = rdr.IsDBNull(0) ? "KasirKu" : rdr.GetString(0),
                    Alamat = rdr.IsDBNull(1) ? "" : rdr.GetString(1),
                    Kota = rdr.IsDBNull(2) ? "" : rdr.GetString(2),
                    Telepon = rdr.IsDBNull(3) ? "" : rdr.GetString(3),
                    FooterStruk = rdr.IsDBNull(4) ? "" : rdr.GetString(4),
                    LogoPath = rdr.IsDBNull(5) ? "" : rdr.GetString(5),
                    TampilkanLogo = rdr.IsDBNull(6) || rdr.GetBoolean(6),
                    PrinterName = rdr.IsDBNull(7) ? "" : rdr.GetString(7),
                    PaperWidthMm = rdr.IsDBNull(8) ? 80 : rdr.GetInt32(8),
                    AutoPrintReceipt = rdr.IsDBNull(9) || rdr.GetBoolean(9),
                    KickDrawer = rdr.IsDBNull(10) || rdr.GetBoolean(10)
                };
            }

            return new StoreSettings();
        }

        public async Task SaveSettingsAsync(StoreSettings settings)
        {
            using var conn = CreateConnection();
            await conn.OpenAsync();

            string query = @"
                INSERT INTO store_settings 
                    (id, nama_toko, alamat, kota, telepon, footer_struk, logo_path, tampilkan_logo,
                     printer_name, paper_width_mm, auto_print_receipt, kick_drawer)
                VALUES 
                    (1, @nama, @alamat, @kota, @telp, @footer, @logo, @tampilLogo,
                     @printer, @width, @autoPrint, @kickDrawer)
                ON DUPLICATE KEY UPDATE
                    nama_toko = VALUES(nama_toko),
                    alamat = VALUES(alamat),
                    kota = VALUES(kota),
                    telepon = VALUES(telepon),
                    footer_struk = VALUES(footer_struk),
                    logo_path = VALUES(logo_path),
                    tampilkan_logo = VALUES(tampilkan_logo),
                    printer_name = VALUES(printer_name),
                    paper_width_mm = VALUES(paper_width_mm),
                    auto_print_receipt = VALUES(auto_print_receipt),
                    kick_drawer = VALUES(kick_drawer);";

            using var cmd = new MySqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@nama", settings.NamaToko ?? "");
            cmd.Parameters.AddWithValue("@alamat", settings.Alamat ?? "");
            cmd.Parameters.AddWithValue("@kota", settings.Kota ?? "");
            cmd.Parameters.AddWithValue("@telp", settings.Telepon ?? "");
            cmd.Parameters.AddWithValue("@footer", settings.FooterStruk ?? "");
            cmd.Parameters.AddWithValue("@logo", settings.LogoPath ?? "");
            cmd.Parameters.AddWithValue("@tampilLogo", settings.TampilkanLogo);
            cmd.Parameters.AddWithValue("@printer", settings.PrinterName ?? "");
            cmd.Parameters.AddWithValue("@width", settings.PaperWidthMm);
            cmd.Parameters.AddWithValue("@autoPrint", settings.AutoPrintReceipt);
            cmd.Parameters.AddWithValue("@kickDrawer", settings.KickDrawer);

            await cmd.ExecuteNonQueryAsync();
        }
    }
}
