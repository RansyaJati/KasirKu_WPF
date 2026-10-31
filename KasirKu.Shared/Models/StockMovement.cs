namespace KasirKu.Shared.Models
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

    /// <summary>
    /// Profil toko. Dipakai di header struk &amp; laporan.
    /// Bisa diatur lewat halaman Pengaturan aplikasi Manajemen.
    /// </summary>
    public class StoreSettings
    {
        public string NamaToko { get; set; } = "BANYU FROZEN FOOD";
        public string Alamat { get; set; } = "Jl. Contoh No. 123";
        public string Telepon { get; set; } = "0812-3456-7890";
        public string FooterStruk { get; set; } = "Terima kasih & sampai jumpa!";

        // Pengaturan Printer Thermal & Struk
        public string PrinterName { get; set; } = "";
        public int PaperWidthMm { get; set; } = 80;
        public bool AutoPrintReceipt { get; set; } = true;
        public bool KickDrawer { get; set; } = true;
    }
}
