using KasirKu.Shared.Data;
using KasirKu.Shared.Models;
using KasirKu.Shared.Services;

namespace KasirKu.Shared.Repositories.Json
{
    public class JsonStockMovementRepository : IStockMovementRepository
    {
        public Task<List<StockMovement>> GetAllAsync()
        {
            var data = DataStorageService.Load();
            return Task.FromResult(data.Movements.ToList());
        }

        public Task<List<StockMovement>> GetByProductIdAsync(int productId)
        {
            var data = DataStorageService.Load();
            return Task.FromResult(data.Movements.Where(m => m.ProductId == productId).ToList());
        }

        public Task<StockMovement> AddAsync(StockMovement movement)
        {
            var data = DataStorageService.Load();
            if (movement.Id <= 0)
            {
                movement.Id = data.NextMovementId++;
            }
            data.Movements.Insert(0, movement);
            DataStorageService.Save(data);
            return Task.FromResult(movement);
        }
    }
}
