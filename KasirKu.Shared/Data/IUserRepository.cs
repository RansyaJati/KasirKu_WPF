using KasirKu.Shared.Models;

namespace KasirKu.Shared.Data
{
    /// <summary>
    /// Abstraksi repositori akun pengguna dan otentikasi.
    /// </summary>
    public interface IUserRepository
    {
        Task<List<User>> GetAllAsync();
        Task<User?> GetByUsernameAsync(string username);
        Task<User> AddAsync(User user);
        Task<bool> UpdateAsync(User user);
        Task<bool> DeleteAsync(string username);
    }
}
