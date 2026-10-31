using KasirKu.Shared.Data;
using KasirKu.Shared.Models;
using KasirKu.Shared.Services;

namespace KasirKu.Shared.Repositories.Json
{
    public class JsonProductRepository : IProductRepository
    {
        public Task<List<Product>> GetAllAsync()
        {
            var data = DataStorageService.Load();
            return Task.FromResult(data.Products.ToList());
        }

        public Task<Product?> GetByIdAsync(int id)
        {
            var data = DataStorageService.Load();
            return Task.FromResult(data.Products.FirstOrDefault(p => p.Id == id));
        }

        public Task<Product?> GetByKodeOrBarcodeAsync(string codeOrBarcode)
        {
            var data = DataStorageService.Load();
            string key = codeOrBarcode.Trim();
            var p = data.Products.FirstOrDefault(x =>
                string.Equals(x.Kode, key, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Barcode, key, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(p);
        }

        public Task<Product> AddAsync(Product product)
        {
            var data = DataStorageService.Load();
            if (product.Id <= 0)
            {
                product.Id = data.NextProductId++;
            }
            data.Products.Add(product);
            DataStorageService.Save(data);
            return Task.FromResult(product);
        }

        public Task<bool> UpdateAsync(Product product)
        {
            var data = DataStorageService.Load();
            int idx = data.Products.FindIndex(p => p.Id == product.Id);
            if (idx >= 0)
            {
                data.Products[idx] = product;
                DataStorageService.Save(data);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task<bool> DeleteAsync(int id)
        {
            var data = DataStorageService.Load();
            int count = data.Products.RemoveAll(p => p.Id == id);
            if (count > 0)
            {
                DataStorageService.Save(data);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }
    }
}
