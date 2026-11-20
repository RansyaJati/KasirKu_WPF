namespace KasirKu.Shared.Models
{
    /// <summary>
    /// Catatan pengeluaran uang dari laci kasir (Cash Drawer)
    /// misalnya untuk setoran ke owner, beli perlengkapan toko, bayar galon, dll.
    /// </summary>
    public class CashExpense
    {
        public string Id { get; set; } = "";
        public DateTime Tanggal { get; set; } = DateTime.Now;
        public decimal Nominal { get; set; }
        public string Kategori { get; set; } = "Operasional"; // Setoran, Operasional, Belanja Toko, Lainnya
        public string Keterangan { get; set; } = "";
        public string KasirNama { get; set; } = "";

        public string NominalRp => $"Rp {Nominal:N0}";
        public string TanggalStr => Tanggal.ToString("dd/MM/yyyy HH:mm");
    }

    /// <summary>
    /// Keranjang belanja yang ditahan sementara (Hold) agar kasir
    /// bisa melayani antrean pelanggan berikutnya tanpa kehilangan data.
    /// </summary>
    public class HoldCartItem
    {
        public string Kode { get; set; } = "";
        public string Nama { get; set; } = "";
        public string Satuan { get; set; } = "pcs";
        public decimal Harga { get; set; }
        public decimal HargaBeli { get; set; }
        public int Qty { get; set; }
        public bool IsCustomPrice { get; set; }
        public decimal Subtotal => Harga * Qty;
    }

    public class HoldTransaction
    {
        public string Id { get; set; } = "";
        public DateTime Tanggal { get; set; } = DateTime.Now;
        public string Catatan { get; set; } = "";
        public string KasirNama { get; set; } = "";
        public List<HoldCartItem> Items { get; set; } = new();

        public int TotalItem => Items?.Sum(i => i.Qty) ?? 0;
        public decimal TotalHarga => Items?.Sum(i => i.Subtotal) ?? 0;
        public string TotalHargaRp => $"Rp {TotalHarga:N0}";
        public string WaktuStr => Tanggal.ToString("HH:mm:ss");
    }

    /// <summary>
    /// Data rekonsiliasi kas saat kasir tutup shift (sesuai struk LAPORAN KASIR).
    /// </summary>
    public class CashierShiftReport
    {
        public string Lokasi { get; set; } = "TOKO 1";
        public DateTime Tanggal { get; set; } = DateTime.Now;
        public string Kassa { get; set; } = "A";
        public string KasirNama { get; set; } = "KASIR";
        public int Shift { get; set; } = 1;

        public decimal ModalAwal { get; set; } = 0;
        public decimal PenjualanKotor { get; set; } = 0;
        public decimal Diskon { get; set; } = 0;
        public decimal Pajak { get; set; } = 0;
        public decimal PenjualanNeto => PenjualanKotor - Diskon + Pajak;

        public decimal TotalTunai { get; set; } = 0;
        public decimal TotalNonTunai { get; set; } = 0;
        public decimal TotalPengeluaran { get; set; } = 0;

        /// <summary>Uang fisik di drawer yang dihitung kasir saat tutup shift.</summary>
        public decimal DrawerFisik { get; set; } = 0;

        /// <summary>Uang tunai yang seharusnya ada menurut komputer (Modal Awal + Penjualan Tunai - Pengeluaran).</summary>
        public decimal KomputerTunai => ModalAwal + TotalTunai - TotalPengeluaran;

        /// <summary>Selisih = DrawerFisik - KomputerTunai (Negatif jika kurang, Positif jika lebih).</summary>
        public decimal Selisih => DrawerFisik - KomputerTunai;
    }
}
