namespace KasirKu.Models
{
    /// <summary>
    /// Satu baris jejak perubahan stok barang.
    /// Jenis = "Masuk" (stok bertambah) atau "Keluar" (stok berkurang).
    /// </summary>
    public class StockMovement
    {
        public int Id { get; set; }
        public DateTime Tanggal { get; set; } = DateTime.Now;
        public int ProductId { get; set; }
        public string Kode { get; set; } = "";
        public string Nama { get; set; } = "";
        public string Jenis { get; set; } = "Masuk";
        public int Qty { get; set; }
        public int StokSebelum { get; set; }
        public int StokSesudah { get; set; }
        public string Keterangan { get; set; } = "";

        public string TanggalStr => Tanggal.ToString("dd/MM/yyyy HH:mm");
        public string Perubahan => Jenis == "Masuk" ? $"+{Qty}" : $"-{Qty}";
        public bool IsMasuk => Jenis == "Masuk";
    }
}
