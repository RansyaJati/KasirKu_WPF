using System.IO;
using System.Text;
using System.Text.Json;
using KasirKu.Shared.Models;

namespace KasirKu.Shared.Services
{
    /// <summary>
    /// Seluruh data aplikasi (produk, transaksi, mutasi stok, counter ID).
    /// Kedua aplikasi (POS &amp; Manajemen) berbagi file ini sehingga
    /// stok &amp; omzet selalu sinkron.
    /// </summary>
    public class KasirKuData
    {
        public int NextProductId { get; set; } = 1;
        public int NextTrx { get; set; } = 1;
        public int NextMovementId { get; set; } = 1;
        public List<Product> Products { get; set; } = new();
        public List<Transaction> Transactions { get; set; } = new();
        public List<StockMovement> Movements { get; set; } = new();
    }

    /// <summary>
    /// Simpan/muat data ke file JSON bersama di Documents/KasirKu/
    /// Tanpa database &amp; tanpa NuGet (System.Text.Json bawaan .NET).
    /// Tulis file dilakukan atomik (file sementara lalu di-move),
    /// supaya data tidak rusak saat aplikasi mati di tengah menulis.
    /// </summary>
    public static class DataStorageService
    {
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            WriteIndented = true
        };

        public static string DataDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "KasirKu");

        public static string DataPath => Path.Combine(DataDir, "data.json");
        public static string SettingsPath => Path.Combine(DataDir, "settings.json");

        /// <summary>
        /// Muat data. Jika file belum ada → data kosong (app melakukan seed).
        /// Jika file rusak → backup lama ke *.corrupt lalu mulai kosong.
        /// </summary>
        public static KasirKuData Load()
        {
            if (!File.Exists(DataPath)) return new KasirKuData();
            try
            {
                string json = File.ReadAllText(DataPath);
                var data = JsonSerializer.Deserialize<KasirKuData>(json, JsonOpts);
                return data ?? new KasirKuData();
            }
            catch (Exception)
            {
                // Backup file rusak agar data lama tidak hilang selamanya
                try
                {
                    string backup = DataPath + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                    File.Move(DataPath, backup);
                }
                catch { }
                return new KasirKuData();
            }
        }

        /// <summary>Tulis data ke data.json secara atomik.</summary>
        public static void Save(KasirKuData data)
        {
            Directory.CreateDirectory(DataDir);
            string json = JsonSerializer.Serialize(data, JsonOpts);
            string tmp = DataPath + ".tmp";
            File.WriteAllText(tmp, json, new UTF8Encoding(false));
            File.Move(tmp, DataPath, overwrite: true);
        }

        public static StoreSettings LoadSettings()
        {
            if (!File.Exists(SettingsPath)) return new StoreSettings();
            try
            {
                string json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<StoreSettings>(json, JsonOpts) ?? new StoreSettings();
            }
            catch
            {
                return new StoreSettings();
            }
        }

        public static void SaveSettings(StoreSettings settings)
        {
            Directory.CreateDirectory(DataDir);
            string json = JsonSerializer.Serialize(settings, JsonOpts);
            string tmp = SettingsPath + ".tmp";
            File.WriteAllText(tmp, json, new UTF8Encoding(false));
            File.Move(tmp, SettingsPath, overwrite: true);
        }
    }
}
