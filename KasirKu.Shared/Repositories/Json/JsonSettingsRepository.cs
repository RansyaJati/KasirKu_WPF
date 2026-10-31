using KasirKu.Shared.Data;
using KasirKu.Shared.Models;
using KasirKu.Shared.Services;

namespace KasirKu.Shared.Repositories.Json
{
    public class JsonSettingsRepository : ISettingsRepository
    {
        public Task<StoreSettings> GetSettingsAsync()
        {
            var s = DataStorageService.LoadSettings();
            return Task.FromResult(s);
        }

        public Task SaveSettingsAsync(StoreSettings settings)
        {
            DataStorageService.SaveSettings(settings);
            return Task.CompletedTask;
        }
    }
}
