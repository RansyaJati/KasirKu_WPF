using System.ComponentModel;

namespace KasirKu.Shared.Models
{
    public class CartItem : INotifyPropertyChanged
    {
        private int _qty;
        private decimal? _customHarga;

        public Product Product { get; set; } = null!;

        public decimal? CustomHarga
        {
            get => _customHarga;
            set
            {
                _customHarga = value;
                OnPropertyChanged(nameof(CustomHarga));
                OnPropertyChanged(nameof(Harga));
                OnPropertyChanged(nameof(HargaRp));
                OnPropertyChanged(nameof(Subtotal));
                OnPropertyChanged(nameof(SubtotalRp));
                OnPropertyChanged(nameof(IsCustomHarga));
                OnPropertyChanged(nameof(NamaDisplay));
            }
        }

        public int Qty
        {
            get => _qty;
            set
            {
                _qty = value;
                OnPropertyChanged(nameof(Qty));
                OnPropertyChanged(nameof(Subtotal));
                OnPropertyChanged(nameof(SubtotalRp));
            }
        }

        public string Kode => Product.Kode;
        public string Nama => Product.Nama;
        public string NamaDisplay => IsCustomHarga ? $"{Product.Nama} (Khusus)" : Product.Nama;
        public decimal Harga => _customHarga ?? Product.Harga;
        public string HargaRp => $"Rp {Harga:N0}";
        public decimal Subtotal => Harga * Qty;
        public string SubtotalRp => $"Rp {Subtotal:N0}";
        public bool IsCustomHarga => _customHarga.HasValue && _customHarga.Value != Product.Harga;
        public bool IsCustomPrice => IsCustomHarga;

        public CartItem() { }
        public CartItem(Product product, int qty = 1, decimal? customHarga = null)
        {
            Product = product;
            Qty = qty;
            CustomHarga = customHarga;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
