using System.ComponentModel;

namespace KasirKu.Shared.Models
{
    public class CartItem : INotifyPropertyChanged
    {
        private int _qty;

        public Product Product { get; set; } = null!;

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
        public decimal Harga => Product.Harga;
        public string HargaRp => $"Rp {Harga:N0}";
        public decimal Subtotal => Harga * Qty;
        public string SubtotalRp => $"Rp {Subtotal:N0}";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
