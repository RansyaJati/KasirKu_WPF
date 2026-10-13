namespace KasirKu.Models
{
    public class TransactionItem
    {
        public string Kode { get; set; } = "";
        public string Nama { get; set; } = "";
        public decimal Harga { get; set; }
        public int Qty { get; set; }
        public decimal Subtotal => Harga * Qty;
        public string SubtotalRp => $"Rp {Subtotal:N0}";
        public string HargaRp => $"Rp {Harga:N0}";
    }

    public class Transaction
    {
        public string Id { get; set; } = "";
        public DateTime Tanggal { get; set; } = DateTime.Now;
        public List<TransactionItem> Items { get; set; } = new();
        public decimal Total { get; set; }
        public decimal Bayar { get; set; }
        public decimal Kembalian => Bayar - Total;
        public string MetodeBayar { get; set; } = "Tunai";

        public string TotalRp => $"Rp {Total:N0}";
        public string BayarRp => $"Rp {Bayar:N0}";
        public string KembalianRp => $"Rp {Kembalian:N0}";
        public string TanggalStr => Tanggal.ToString("dd/MM/yyyy HH:mm");
        public int JumlahItem => Items.Sum(i => i.Qty);
        public string Ringkasan => string.Join(", ", Items.Select(i => $"{i.Nama} x{i.Qty}"));
    }
}
