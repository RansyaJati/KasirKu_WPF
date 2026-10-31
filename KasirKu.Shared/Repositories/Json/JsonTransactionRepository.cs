using KasirKu.Shared.Data;
using KasirKu.Shared.Models;
using KasirKu.Shared.Services;

namespace KasirKu.Shared.Repositories.Json
{
    public class JsonTransactionRepository : ITransactionRepository
    {
        public Task<List<Transaction>> GetAllAsync()
        {
            var data = DataStorageService.Load();
            return Task.FromResult(data.Transactions.ToList());
        }

        public Task<Transaction?> GetByIdAsync(string id)
        {
            var data = DataStorageService.Load();
            var t = data.Transactions.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(t);
        }

        public Task<List<Transaction>> GetByDateRangeAsync(DateTime from, DateTime to)
        {
            var data = DataStorageService.Load();
            var list = data.Transactions
                .Where(t => t.Tanggal.Date >= from.Date && t.Tanggal.Date <= to.Date)
                .OrderByDescending(t => t.Tanggal)
                .ToList();
            return Task.FromResult(list);
        }

        public Task<Transaction> AddAsync(Transaction transaction)
        {
            var data = DataStorageService.Load();
            data.Transactions.Insert(0, transaction);
            DataStorageService.Save(data);
            return Task.FromResult(transaction);
        }
    }
}
