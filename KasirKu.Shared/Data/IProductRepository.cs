using KasirKu.Shared.Models;

namespace KasirKu.Shared.Data
{
    /// <summary>
    /// Abstraksi repositori master produk.
    /// Memungkinkan transisi mulus dari penyimpanan file JSON ke MySQL / SQL Server.
    /// </summary>
    public interface IProductRepository
    {
        Task<List<Product>> GetAllAsync();
        Task<Product?> GetByIdAsync(int id);
        Task<Product?> GetByKodeOrBarcodeAsync(string codeOrBarcode);
        Task<Product> AddAsync(Product product);
        Task<bool> UpdateAsync(Product product);
        Task<bool> DeleteAsync(int id);
    }
}
