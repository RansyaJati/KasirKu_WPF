using System.IO;
using System.Text;
using KasirKu.Shared.Models;

namespace KasirKu.Shared.Services
{
    public static class ExportService
    {
        public static string BaseDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "KasirKu");

        public static string StrukDir => Path.Combine(BaseDir, "Struk");
        public static string LaporanDir => Path.Combine(BaseDir, "Laporan");

        private static string Rp(decimal v) => $"Rp {v:N0}";

        private static string NamaToko(StoreSettings? s) =>
            string.IsNullOrWhiteSpace(s?.NamaToko) ? "KASIRKU" : s!.NamaToko;

        // ===== STRUK =====
        public static List<PdfLine> BuildStrukLines(Transaction t, StoreSettings? settings = null)
        {
            var s = settings ?? new StoreSettings();
            var L = new List<PdfLine>();
            L.Add(new PdfLine { Text = NamaToko(s), Size = 16, Bold = true, Center = true });
            L.Add(new PdfLine { Text = $"{s.Alamat} - Telp {s.Telepon}", Size = 8, Center = true });
            L.Add(new PdfLine { Text = new string('-', 38), Size = 9, Center = true });
            L.Add(new PdfLine { Text = $"{t.Id}  |  {t.Tanggal:dd/MM/yyyy HH:mm}", Size = 9 });
            L.Add(new PdfLine { Text = $"Metode: {t.MetodeBayar}", Size = 9, Bold = true });
            L.Add(new PdfLine { Text = new string('-', 38), Size = 9, Center = true });
            foreach (var i in t.Items)
            {
                L.Add(new PdfLine { Text = Truncate(i.Nama, 30), Size = 10, Bold = true });
                L.Add(new PdfLine { Text = $"  {i.Qty} x {Rp(i.Harga),-12} {Rp(i.Subtotal),12}", Size = 9 });
            }
            L.Add(new PdfLine { Text = new string('-', 38), Size = 9, Center = true });
            L.Add(new PdfLine { Text = $"TOTAL   : {Rp(t.Total),20}", Size = 12, Bold = true });
            L.Add(new PdfLine { Text = $"BAYAR ({t.MetodeBayar}): {Rp(t.Bayar),12}", Size = 10 });
            L.Add(new PdfLine { Text = $"KEMBALI : {Rp(t.Kembalian),20}", Size = 10 });
            L.Add(new PdfLine { Text = new string('-', 38), Size = 9, Center = true });
            if (!t.MetodeBayar.Equals("Tunai", StringComparison.OrdinalIgnoreCase))
                L.Add(new PdfLine { Text = "Pembayaran QRIS/e-wallet lunas.", Size = 9, Center = true });
            L.Add(new PdfLine { Text = s.FooterStruk, Size = 10, Bold = true, Center = true });
            L.Add(new PdfLine { Text = "Struk ini dicetak dari KasirKu (PDF)", Size = 8, Center = true });
            return L;
        }

        public static string SimpanStrukPdf(Transaction t, StoreSettings? settings = null)
        {
            Directory.CreateDirectory(StrukDir);
            string safe = string.Concat(t.Id.Split(Path.GetInvalidFileNameChars()));
            string path = Path.Combine(StrukDir, $"Struk-{safe}.pdf");
            var lines = BuildStrukLines(t, settings);
            // Kertas struk 80mm ~ 227pt, tinggi dinamis mengikuti jumlah baris
            float height = Math.Max(500, 220 + lines.Count * 15);
            SimplePdf.WriteLines(path, lines, pageWidth: 300, pageHeight: height, margin: 14);
            return path;
        }

        public static string BuildStrukText(Transaction t, StoreSettings? settings = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"======== {NamaToko(settings)} ========");
            sb.AppendLine($"{t.Id}");
            sb.AppendLine($"{t.Tanggal:dd/MM/yyyy HH:mm} | {t.MetodeBayar}");
            sb.AppendLine("------------------------------");
            foreach (var i in t.Items)
                sb.AppendLine($"{i.Nama} x{i.Qty} = Rp {i.Subtotal:N0}");
            sb.AppendLine("------------------------------");
            sb.AppendLine($"Total   : Rp {t.Total:N0}");
            sb.AppendLine($"Bayar   : Rp {t.Bayar:N0} ({t.MetodeBayar})");
            sb.AppendLine($"Kembali : Rp {t.Kembalian:N0}");
            sb.AppendLine("Terima kasih!");
            return sb.ToString();
        }

        // ===== LAPORAN =====

        /// <summary>Estimasi laba 1 transaksi = sum((harga jual - harga beli) x qty).</summary>
        public static decimal LabaTransaksi(Transaction t, Func<string, decimal>? hargaBeliLookup = null)
        {
            if (hargaBeliLookup != null)
                return t.Items.Sum(i => (i.Harga - hargaBeliLookup(i.Kode)) * i.Qty);
            // Fallback: pakai snapshot HargaBeli di item transaksi
            return t.Items.Sum(i => (i.Harga - i.HargaBeli) * i.Qty);
        }

        public static List<ProdukLarisRow> HitungProdukLaris(List<Transaction> trxs, Func<string, decimal>? hargaBeliLookup = null, int take = 10)
        {
            if (hargaBeliLookup == null)
            {
                var beliByKode = new Dictionary<string, decimal>();
                foreach (var t in trxs)
                    foreach (var i in t.Items.Where(x => x.HargaBeli != 0))
                        beliByKode[i.Kode] = i.HargaBeli;
                hargaBeliLookup = k => beliByKode.TryGetValue(k, out var v) ? v : 0m;
            }

            return trxs.SelectMany(t => t.Items)
                .GroupBy(i => i.Nama)
                .Select(g => new ProdukLarisRow
                {
                    Nama = g.Key,
                    Terjual = g.Sum(x => x.Qty),
                    Omzet = g.Sum(x => x.Subtotal),
                    Laba = g.Sum(x => (x.Harga - hargaBeliLookup(x.Kode)) * x.Qty)
                })
                .OrderByDescending(x => x.Terjual)
                .Take(take)
                .ToList();
        }

        /// <param name="hargaBeliLookup">
        /// Opsional: lookup harga beli per kode produk. Jika diberikan,
        /// laporan menampilkan Laba (estimasi) di ringkasan &amp; tabel terlaris.
        /// </param>
        public static List<PdfLine> BuildLaporanLines(
            List<Transaction> trxs, DateTime? dari = null, DateTime? sampai = null,
            Func<string, decimal>? hargaBeliLookup = null, StoreSettings? settings = null)
        {
            var L = new List<PdfLine>();
            L.Add(new PdfLine { Text = $"LAPORAN PENJUALAN - {NamaToko(settings)}", Size = 16, Bold = true, Center = true });
            string periode = (dari == null && sampai == null)
                ? DateTime.Today.ToString("dd MMM yyyy")
                : $"{dari:dd/MM/yyyy} s/d {sampai:dd/MM/yyyy}";
            L.Add(new PdfLine { Text = $"Periode: {periode} | Dicetak: {DateTime.Now:dd/MM/yyyy HH:mm}", Size = 9, Center = true });
            L.Add(new PdfLine { Text = new string('=', 72), Size = 9 });

            decimal omzet = trxs.Sum(t => t.Total);
            int jmlItem = trxs.Sum(t => t.JumlahItem);
            decimal rata = trxs.Count > 0 ? omzet / trxs.Count : 0;
            var perMetode = trxs.GroupBy(t => t.MetodeBayar)
                .Select(g => $"{g.Key}: {g.Count()} trx (Rp {g.Sum(x => x.Total):N0})").ToList();

            L.Add(new PdfLine { Text = $"Total Transaksi : {trxs.Count}", Size = 11, Bold = true });
            L.Add(new PdfLine { Text = $"Total Item      : {jmlItem} pcs", Size = 11 });
            L.Add(new PdfLine { Text = $"Total Omzet     : {Rp(omzet)}", Size = 12, Bold = true });
            L.Add(new PdfLine { Text = $"Rata-rata/nota  : {Rp(rata)}", Size = 11 });
            if (hargaBeliLookup != null)
                L.Add(new PdfLine { Text = $"Laba (estimasi) : {Rp(LabaTransaksiAll(trxs, hargaBeliLookup))}", Size = 12, Bold = true });
            L.Add(new PdfLine { Text = "Per Metode: " + (perMetode.Count == 0 ? "-" : string.Join(" | ", perMetode)), Size = 9 });
            L.Add(new PdfLine { Text = new string('=', 72), Size = 9 });

            L.Add(new PdfLine { Text = "DAFTAR TRANSAKSI", Size = 12, Bold = true });
            if (trxs.Count == 0)
            {
                L.Add(new PdfLine { Text = "Belum ada transaksi pada periode ini.", Size = 10 });
            }
            else
            {
                L.Add(new PdfLine { Text = "ID              TANGGAL           METODE      ITEM  TOTAL", Size = 9, Bold = true });
                foreach (var t in trxs.OrderBy(x => x.Tanggal))
                    L.Add(new PdfLine { Text = $"{Truncate(t.Id, 15),-15} {t.Tanggal:dd/MM HH:mm}  {Truncate(t.MetodeBayar, 10),-10} {t.JumlahItem,4}  {Rp(t.Total),12}", Size = 9 });
            }

            L.Add(new PdfLine { Text = new string('=', 72), Size = 9 });
            L.Add(new PdfLine { Text = "PRODUK TERLARIS", Size = 12, Bold = true });
            var laris = HitungProdukLaris(trxs, hargaBeliLookup, take: 15);
            if (laris.Count == 0)
            {
                L.Add(new PdfLine { Text = "-", Size = 10 });
            }
            else if (hargaBeliLookup != null)
            {
                L.Add(new PdfLine { Text = $"{"PRODUK",-30} {"TERJUAL",7} {"OMZET",14} {"LABA",14}", Size = 9, Bold = true });
                foreach (var r in laris)
                    L.Add(new PdfLine { Text = $"{Truncate(r.Nama, 30),-30} {r.Terjual,7} {Rp(r.Omzet),14} {Rp(r.Laba),14}", Size = 9 });
            }
            else
            {
                foreach (var r in laris)
                    L.Add(new PdfLine { Text = $"{Truncate(r.Nama, 30),-30} {r.Terjual,5} pcs  {Rp(r.Omzet),14}", Size = 9 });
            }

            return L;
        }

        private static decimal LabaTransaksiAll(List<Transaction> trxs, Func<string, decimal>? hargaBeliLookup = null)
            => trxs.Sum(t => LabaTransaksi(t, hargaBeliLookup));

        public static string SimpanLaporanPdf(List<Transaction> trxs, DateTime? dari = null, DateTime? sampai = null,
            Func<string, decimal>? hargaBeliLookup = null, StoreSettings? settings = null)
        {
            Directory.CreateDirectory(LaporanDir);
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string path = Path.Combine(LaporanDir, $"Laporan-{stamp}.pdf");
            SimplePdf.WriteLines(path, BuildLaporanLines(trxs, dari, sampai, hargaBeliLookup, settings));
            return path;
        }

        /// <summary>
        /// Ekspor Excel-compatible CSV (bisa dibuka langsung di Excel).
        /// Separator ';' + BOM UTF-8 supaya langsung jadi kolom di Excel Indonesia.
        /// </summary>
        public static string SimpanLaporanExcel(List<Transaction> trxs, Func<string, decimal>? hargaBeliLookup = null)
        {
            Directory.CreateDirectory(LaporanDir);
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string path = Path.Combine(LaporanDir, $"Laporan-{stamp}.csv");
            var sb = new StringBuilder();
            if (hargaBeliLookup != null)
                sb.AppendLine("ID;Tanggal;Metode;Kode;Nama;HargaJual;HargaBeli;Qty;Subtotal;Laba;TotalNota;Bayar;Kembalian");
            else
                sb.AppendLine("ID;Tanggal;Metode;Kode;Nama;Harga;Qty;Subtotal;TotalNota;Bayar;Kembalian");

            foreach (var t in trxs.OrderBy(x => x.Tanggal))
            {
                foreach (var i in t.Items)
                {
                    if (hargaBeliLookup != null)
                    {
                        var beli = hargaBeliLookup(i.Kode);
                        sb.AppendLine(string.Join(";",
                            Csv(t.Id), Csv(t.Tanggal.ToString("yyyy-MM-dd HH:mm")), Csv(t.MetodeBayar),
                            Csv(i.Kode), Csv(i.Nama), i.Harga, beli, i.Qty, i.Subtotal,
                            (i.Harga - beli) * i.Qty, t.Total, t.Bayar, t.Kembalian));
                    }
                    else
                    {
                        sb.AppendLine(string.Join(";",
                            Csv(t.Id), Csv(t.Tanggal.ToString("yyyy-MM-dd HH:mm")), Csv(t.MetodeBayar),
                            Csv(i.Kode), Csv(i.Nama), i.Harga, i.Qty, i.Subtotal,
                            t.Total, t.Bayar, t.Kembalian));
                    }
                }
                if (t.Items.Count == 0)
                {
                    if (hargaBeliLookup != null)
                        sb.AppendLine(string.Join(";", Csv(t.Id), Csv(t.Tanggal.ToString("yyyy-MM-dd HH:mm")), Csv(t.MetodeBayar),
                            "", "", 0, 0, 0, 0, 0, t.Total, t.Bayar, t.Kembalian));
                    else
                        sb.AppendLine(string.Join(";", Csv(t.Id), Csv(t.Tanggal.ToString("yyyy-MM-dd HH:mm")), Csv(t.MetodeBayar),
                            "", "", 0, 0, 0, t.Total, t.Bayar, t.Kembalian));
                }
            }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
            return path;
        }

        private static string Csv(string s)
        {
            if (s.Contains(';') || s.Contains('"') || s.Contains('\n'))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }

        private static string Truncate(string s, int max)
            => s.Length <= max ? s : s[..(max - 1)] + ".";

        public class ProdukLarisRow
        {
            public string Nama { get; set; } = "";
            public int Terjual { get; set; }
            public decimal Omzet { get; set; }
            public decimal Laba { get; set; }
            public string OmzetRp => $"Rp {Omzet:N0}";
            public string LabaRp => $"Rp {Laba:N0}";
        }
    }
}
