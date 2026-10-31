using KasirKu.Shared.Data;
using KasirKu.Shared.Models;
using KasirKu.Shared.Services;

namespace KasirKu.Shared.Repositories.Json
{
    public class JsonUserRepository : IUserRepository
    {
        public Task<List<User>> GetAllAsync()
        {
            var data = AuthService.Load();
            return Task.FromResult(data.Users.ToList());
        }

        public Task<User?> GetByUsernameAsync(string username)
        {
            var data = AuthService.Load();
            var u = data.Users.FirstOrDefault(x => string.Equals(x.Username, username, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(u);
        }

        public Task<User> AddAsync(User user)
        {
            var data = AuthService.Load();
            data.Users.Add(user);
            AuthService.Save(data);
            return Task.FromResult(user);
        }

        public Task<bool> UpdateAsync(User user)
        {
            var data = AuthService.Load();
            int idx = data.Users.FindIndex(x => string.Equals(x.Username, user.Username, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
            {
                data.Users[idx] = user;
                AuthService.Save(data);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task<bool> DeleteAsync(string username)
        {
            var data = AuthService.Load();
            int count = data.Users.RemoveAll(x => string.Equals(x.Username, username, StringComparison.OrdinalIgnoreCase));
            if (count > 0)
            {
                AuthService.Save(data);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
    }
}
