using KasirKu.Shared.Models;

namespace KasirKu.Shared.Data
{
    /// <summary>
    /// Abstraksi repositori transaksi penjualan.
    /// </summary>
    public interface ITransactionRepository
    {
        Task<List<Transaction>> GetAllAsync();
        Task<Transaction?> GetByIdAsync(string id);
        Task<List<Transaction>> GetByDateRangeAsync(DateTime from, DateTime to);
        Task<Transaction> AddAsync(Transaction transaction);
    }
}
