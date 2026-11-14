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

        private static string Num(decimal v) =>
            v.ToString("#,##0", System.Globalization.CultureInfo.InvariantCulture);

        private static string NamaToko(StoreSettings? s) =>
            string.IsNullOrWhiteSpace(s?.NamaToko) ? "KASIRKU" : s!.NamaToko;

        private static string Label(string kiri, string kanan, int chars)
        {
            int pad = Math.Max(1, chars - kiri.Length - kanan.Length);
            return kiri + new string(' ', pad) + kanan;
        }

        private static string TruncateText(string s, int max)
            => s.Length <= max ? s : s[..(max - 1)] + ".";

        // ===== STRUK =====
        public static List<PdfLine> BuildStrukLines(Transaction t, StoreSettings? settings = null)
        {
            var s = settings ?? new StoreSettings();
            var L = new List<PdfLine>();
            const int W = 46;

            // Logo toko
            if (s.TampilkanLogo)
            {
                var img = LogoService.LoadRgbForPdf(s, 420);
                if (img.HasValue)
                {
                    L.Add(new PdfLine
                    {
                        Image = new PdfImage
                        {
                            Rgb = img.Value.Rgb,
                            PixelWidth = img.Value.Width,
                            PixelHeight = img.Value.Height,
                            DrawWidth = 165f
                        }
                    });
                }
            }

            L.Add(new PdfLine { Text = NamaToko(s).ToUpperInvariant(), Size = 15, Bold = true, Center = true });
            if (!string.IsNullOrWhiteSpace(s.Alamat))
                L.Add(new PdfLine { Text = s.Alamat, Size = 7.5f, Center = true });
            if (!string.IsNullOrWhiteSpace(s.Kota))
                L.Add(new PdfLine { Text = s.Kota, Size = 7.5f, Center = true });
            if (!string.IsNullOrWhiteSpace(s.Telepon))
                L.Add(new PdfLine { Text = s.Telepon, Size = 7.5f, Center = true });

            L.Add(new PdfLine { Text = new string('=', W), Size = 8.5f, Center = true });

            L.Add(new PdfLine { Text = Label("No. Nota", t.Id, W), Size = 8.5f });
            L.Add(new PdfLine { Text = Label("Tanggal", t.Tanggal.ToString("dd-MM-yyyy HH:mm"), W), Size = 8.5f });
            L.Add(new PdfLine { Text = Label("Kasir", string.IsNullOrWhiteSpace(t.KasirNama) ? "-" : t.KasirNama.ToUpperInvariant(), W), Size = 8.5f });
            L.Add(new PdfLine { Text = Label("Pembayaran", (t.MetodeBayar ?? "Tunai").ToUpperInvariant(), W), Size = 8.5f });

            L.Add(new PdfLine { Text = new string('-', W), Size = 8.5f, Center = true });

            foreach (var i in t.Items)
            {
                L.Add(new PdfLine { Text = TruncateText(i.Nama, W), Size = 9, Bold = true });
                L.Add(new PdfLine { Text = Label($"  {i.Qty}X {Num(i.Harga)}", $"= {Num(i.Subtotal)}", W), Size = 8.5f });
            }

            L.Add(new PdfLine { Text = new string('-', W), Size = 8.5f, Center = true });

            L.Add(new PdfLine { Text = Label($"Qty  {t.JumlahItem} item", "", W), Size = 8.5f });
            L.Add(new PdfLine { Text = Label("TOTAL", Num(t.Total), W), Size = 13, Bold = true });
            L.Add(new PdfLine { Text = Label($"BAYAR ({(t.MetodeBayar ?? "Tunai").ToUpperInvariant()})", Num(t.Bayar), W), Size = 9 });
            L.Add(new PdfLine { Text = Label("KEMBALI", Num(t.Kembalian), W), Size = 9 });

            L.Add(new PdfLine { Text = new string('=', W), Size = 8.5f, Center = true });

            if (!t.MetodeBayar.Equals("Tunai", StringComparison.OrdinalIgnoreCase))
                L.Add(new PdfLine { Text = "PEMBAYARAN NON-TUNAI LUNAS", Size = 8, Center = true });

            if (!string.IsNullOrWhiteSpace(s.FooterStruk))
            {
                foreach (var raw in s.FooterStruk.Replace("\r", "").Split('\n'))
                {
                    if (raw.Trim().Length == 0) continue;
                    L.Add(new PdfLine { Text = raw.Trim().ToUpperInvariant(), Size = 9.5f, Bold = true, Center = true });
                }
            }

            string kasir = string.IsNullOrWhiteSpace(t.KasirNama) ? "-" : t.KasirNama.ToUpperInvariant();
            L.Add(new PdfLine { Text = $"{t.Tanggal:HH:mm:ss}/{kasir}/{t.Id}", Size = 7, Center = true });

            return L;
        }

        public static string SimpanStrukPdf(Transaction t, StoreSettings? settings = null)
        {
            Directory.CreateDirectory(StrukDir);
            string safe = string.Concat(t.Id.Split(Path.GetInvalidFileNameChars()));
            string path = Path.Combine(StrukDir, $"Struk-{safe}.pdf");
            var lines = BuildStrukLines(t, settings);

            // Lebar kertas 80mm ~ 227pt; beri margin nyaman.
            const float pageWidth = 260f;
            float height = 40;
            foreach (var l in lines)
                height += l.Image != null ? l.Image.DrawHeight + 8f : Math.Max(13, l.Size * 1.4f);
            height = Math.Max(360, height + 40);

            SimplePdf.WriteLines(path, lines, pageWidth: pageWidth, pageHeight: height, margin: 12);
            return path;
        }

        /// <summary>Pratinjau teks struk untuk dialog konfirmasi di aplikasi kasir.</summary>
        public static string BuildStrukText(Transaction t, StoreSettings? settings = null)
        {
            var s = settings ?? new StoreSettings();
            var sb = new StringBuilder();
            const int W = 40;

            sb.AppendLine(Center2(NamaToko(s).ToUpperInvariant(), W));
            if (!string.IsNullOrWhiteSpace(s.Alamat)) sb.AppendLine(Center2(s.Alamat, W));
            if (!string.IsNullOrWhiteSpace(s.Kota)) sb.AppendLine(Center2(s.Kota, W));
            if (!string.IsNullOrWhiteSpace(s.Telepon)) sb.AppendLine(Center2(s.Telepon, W));
            sb.AppendLine(new string('=', W));

            sb.AppendLine(Label("No. Nota", t.Id, W));
            sb.AppendLine(Label("Tanggal", t.Tanggal.ToString("dd-MM-yyyy HH:mm"), W));
            sb.AppendLine(Label("Kasir", string.IsNullOrWhiteSpace(t.KasirNama) ? "-" : t.KasirNama.ToUpperInvariant(), W));
            sb.AppendLine(Label("Pembayaran", (t.MetodeBayar ?? "Tunai").ToUpperInvariant(), W));
            sb.AppendLine(new string('-', W));

            foreach (var i in t.Items)
            {
                sb.AppendLine(TruncateText(i.Nama, W));
                sb.AppendLine(Label($"  {i.Qty}X {Num(i.Harga)}", $"= {Num(i.Subtotal)}", W));
            }

            sb.AppendLine(new string('-', W));
            sb.AppendLine(Label($"Qty  {t.JumlahItem} item", "", W));
            sb.AppendLine(Label("TOTAL", Num(t.Total), W));
            sb.AppendLine(Label($"BAYAR ({(t.MetodeBayar ?? "Tunai").ToUpperInvariant()})", Num(t.Bayar), W));
            sb.AppendLine(Label("KEMBALI", Num(t.Kembalian), W));
            sb.AppendLine(new string('=', W));

            if (!string.IsNullOrWhiteSpace(s.FooterStruk))
                foreach (var raw in s.FooterStruk.Replace("\r", "").Split('\n'))
                    if (raw.Trim().Length > 0)
                        sb.AppendLine(Center2(raw.Trim().ToUpperInvariant(), W));

            string kasir = string.IsNullOrWhiteSpace(t.KasirNama) ? "-" : t.KasirNama.ToUpperInvariant();
            sb.AppendLine($"{t.Tanggal:HH:mm:ss}/{kasir}/{t.Id}");

            return sb.ToString();
        }

        private static string Center2(string text, int width)
        {
            if (text.Length >= width) return text[..width];
            int pad = (width - text.Length) / 2;
            return new string(' ', pad) + text;
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
