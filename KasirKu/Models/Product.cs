using System.ComponentModel;

namespace KasirKu.Models
{
    public class Product : INotifyPropertyChanged
    {
        private string _kode = "";
        private string _barcode = "";
        private string _nama = "";
        private string _satuan = "Pcs";
        private string _kategori = "Umum";
        private string _supplier = "";
        private decimal _hargaBeli;
        private decimal _harga;
        private int _stok;

        public int Id { get; set; }

        /// <summary>Kode Produk (kode internal toko).</summary>
        public string Kode
        {
            get => _kode;
            set { _kode = value; OnPropertyChanged(nameof(Kode)); }
        }

        /// <summary>Kode Barcode (untuk scanner barcode).</summary>
        public string Barcode
        {
            get => _barcode;
            set { _barcode = value; OnPropertyChanged(nameof(Barcode)); }
        }

        public string Nama
        {
            get => _nama;
            set { _nama = value; OnPropertyChanged(nameof(Nama)); }
        }

        /// <summary>Satuan: Pcs, Dus, Pack, Kg, Liter, dll.</summary>
        public string Satuan
        {
            get => _satuan;
            set { _satuan = value; OnPropertyChanged(nameof(Satuan)); }
        }

        /// <summary>Kelompok produk (Makanan, Minuman, Snack, ATK, Lainnya).</summary>
        public string Kategori
        {
            get => _kategori;
            set { _kategori = value; OnPropertyChanged(nameof(Kategori)); OnPropertyChanged(nameof(Kelompok)); }
        }

        /// <summary>Alias agar binding kolom "Kelompok" bisa dipakai langsung.</summary>
        public string Kelompok
        {
            get => _kategori;
            set { _kategori = value; OnPropertyChanged(nameof(Kategori)); OnPropertyChanged(nameof(Kelompok)); }
        }

        public string Supplier
        {
            get => _supplier;
            set { _supplier = value; OnPropertyChanged(nameof(Supplier)); }
        }

        /// <summary>Harga beli (modal).</summary>
        public decimal HargaBeli
        {
            get => _hargaBeli;
            set
            {
                _hargaBeli = value;
                OnPropertyChanged(nameof(HargaBeli));
                OnPropertyChanged(nameof(HargaBeliRp));
                OnPropertyChanged(nameof(Margin));
                OnPropertyChanged(nameof(MarginRp));
                OnPropertyChanged(nameof(MarginPersen));
                OnPropertyChanged(nameof(MarginStr));
            }
        }

        /// <summary>Harga jual.</summary>
        public decimal Harga
        {
            get => _harga;
            set
            {
                _harga = value;
                OnPropertyChanged(nameof(Harga));
                OnPropertyChanged(nameof(HargaJual));
                OnPropertyChanged(nameof(HargaRp));
                OnPropertyChanged(nameof(HargaJualRp));
                OnPropertyChanged(nameof(Margin));
                OnPropertyChanged(nameof(MarginRp));
                OnPropertyChanged(nameof(MarginPersen));
                OnPropertyChanged(nameof(MarginStr));
            }
        }

        /// <summary>Alias harga jual agar binding kolom "Harga Jual" jelas.</summary>
        public decimal HargaJual
        {
            get => _harga;
            set => Harga = value;
        }

        public int Stok
        {
            get => _stok;
            set { _stok = value; OnPropertyChanged(nameof(Stok)); OnPropertyChanged(nameof(DisplayStok)); }
        }

        public string HargaRp => $"Rp {Harga:N0}";
        public string HargaJualRp => $"Rp {Harga:N0}";
        public string HargaBeliRp => $"Rp {HargaBeli:N0}";

        /// <summary>Margin harga jual dalam rupiah (HargaJual - HargaBeli).</summary>
        public decimal Margin => Harga - HargaBeli;
        public decimal MarginRp => Margin;

        /// <summary>Margin dalam persen terhadap harga beli.</summary>
        public decimal MarginPersen => HargaBeli > 0 ? Margin / HargaBeli * 100 : 0;

        /// <summary>Tampilan kolom margin: Rp + persen.</summary>
        public string MarginStr => $"Rp {Margin:N0} ({MarginPersen:N1}%)";

        public string DisplayStok => $"{Kode} — {Nama} (stok: {Stok})";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
