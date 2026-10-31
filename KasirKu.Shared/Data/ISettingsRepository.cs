using KasirKu.Shared.Models;

namespace KasirKu.Shared.Data
{
    /// <summary>
    /// Abstraksi repositori profil toko dan pengaturan sistem.
    /// </summary>
    public interface ISettingsRepository
    {
        Task<StoreSettings> GetSettingsAsync();
        Task SaveSettingsAsync(StoreSettings settings);
    }
}
