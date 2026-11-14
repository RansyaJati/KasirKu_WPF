using System.IO;
using System.Text.Json;

namespace KasirKu.Shared.Data
{
    public class DatabaseConfig
    {
        public string Server { get; set; } = "localhost";
        public int Port { get; set; } = 3306;
        public string Database { get; set; } = "kasirku";
        public string User { get; set; } = "root";
        public string Password { get; set; } = "";
        public bool UseMySQL { get; set; } = true;

        public string GetConnectionString()
        {
            return $"Server={Server};Port={Port};Database={Database};Uid={User};Pwd={Password};AllowZeroDateTime=True;";
        }

        private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };
        private static string ConfigPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "KasirKu", "db_config.json");

        public static DatabaseConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    var cfg = JsonSerializer.Deserialize<DatabaseConfig>(json, JsonOpts);
                    if (cfg != null) return cfg;
                }
            }
            catch { }

            var defaultConfig = new DatabaseConfig();
            Save(defaultConfig);
            return defaultConfig;
        }

        public static void Save(DatabaseConfig config)
        {
            try
            {
                string dir = Path.GetDirectoryName(ConfigPath)!;
                Directory.CreateDirectory(dir);
                File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, JsonOpts));
            }
            catch { }
        }
    }
}
