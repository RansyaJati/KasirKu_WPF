using KasirKu.Shared.Models;

namespace KasirKu.Shared.Data
{
    /// <summary>
    /// Abstraksi repositori jejak mutasi stok.
    /// </summary>
    public interface IStockMovementRepository
    {
        Task<List<StockMovement>> GetAllAsync();
        Task<List<StockMovement>> GetByProductIdAsync(int productId);
        Task<StockMovement> AddAsync(StockMovement movement);
    }
}
