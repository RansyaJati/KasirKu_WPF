namespace KasirKu.Shared.Models
{
    public class TransactionItem
    {
        public string Kode { get; set; } = "";
        public string Nama { get; set; } = "";
        public string Satuan { get; set; } = "pcs";
        public decimal Harga { get; set; }

        /// <summary>
        /// Snapshot harga beli (modal) saat transaksi dibuat.
        /// Dipakai aplikasi Manajemen untuk hitung laba; data lama bisa 0
        /// dan akan di-fallback lewat lookup produk.
        /// </summary>
        public decimal HargaBeli { get; set; }

        public int Qty { get; set; }
        public decimal Subtotal => Harga * Qty;
        public string SubtotalRp => $"Rp {Subtotal:N0}";
        public string HargaRp => $"Rp {Harga:N0}";
    }

    public class Transaction
    {
        public string Id { get; set; } = "";
        public DateTime Tanggal { get; set; } = DateTime.Now;

        private List<TransactionItem> _items = new();
        public List<TransactionItem> Items
        {
            get => _items ??= new();
            set => _items = value ?? new();
        }

        public decimal Total { get; set; }
        public decimal Bayar { get; set; }
        public decimal Kembalian => Bayar - Total;
        public string MetodeBayar { get; set; } = "Tunai";

        /// <summary>Nama kasir yang melayani transaksi (dicetak di struk).</summary>
        public string KasirNama { get; set; } = "";

        /// <summary>ID Member jika transaksi dilakukan oleh member.</summary>
        public int? MemberId { get; set; }
        /// <summary>Nama Member pelanggan jika ada.</summary>
        public string MemberNama { get; set; } = "";
        /// <summary>Poin yang didapat dari transaksi ini.</summary>
        public int PoinDidapat { get; set; } = 0;

        public string TotalRp => $"Rp {Total:N0}";
        public string BayarRp => $"Rp {Bayar:N0}";
        public string KembalianRp => $"Rp {Kembalian:N0}";
        public string TanggalStr => Tanggal.ToString("dd/MM/yyyy HH:mm");
        public int JumlahItem => Items?.Sum(i => i.Qty) ?? 0;
        public string Ringkasan => Items != null ? string.Join(", ", Items.Select(i => $"{i.Nama} x{i.Qty}")) : "";
    }
}
