using System.Text;
using KasirKu.Shared.Models;

namespace KasirKu.Shared.Services
{
    /// <summary>
    /// Service untuk memformat struk belanja dan mengirimkan perintah ESC/POS
    /// ke printer thermal (58mm / 80mm) serta kontrol laci uang (cash drawer).
    ///
    /// Layout struk dirancang mengikuti kebiasaan struk toko retail Indonesia
    /// (gaya Indomaret/Alfamart):
    ///   [LOGO]  ->  Nama Toko  ->  Alamat/Kota/Telepon
    ///   Info nota (No, Tanggal, Kasir, Metode)
    ///   Item: "NAMA" lalu "1X 15,000 ........ = 15,000"
    ///   Ringkasan: Qty, TOTAL, BAYAR, KEMBALI
    ///   Footer + baris audit (jam/kasir/no. nota)
    /// </summary>
    public static class ThermalPrintService
    {
        // ── ESC/POS Commands ────────────────────────────────────────────
        private static readonly byte[] CmdInit = { 0x1B, 0x40 };                        // ESC @
        private static readonly byte[] CmdAlignLeft = { 0x1B, 0x61, 0x00 };             // ESC a 0
        private static readonly byte[] CmdAlignCenter = { 0x1B, 0x61, 0x01 };           // ESC a 1
        private static readonly byte[] CmdBoldOn = { 0x1B, 0x45, 0x01 };                // ESC E 1
        private static readonly byte[] CmdBoldOff = { 0x1B, 0x45, 0x00 };               // ESC E 0
        private static readonly byte[] CmdDoubleSize = { 0x1D, 0x21, 0x11 };            // GS ! 0x11
        private static readonly byte[] CmdDoubleHeight = { 0x1D, 0x21, 0x01 };          // GS ! 0x01
        private static readonly byte[] CmdNormalSize = { 0x1D, 0x21, 0x00 };            // GS ! 0x00
        private static readonly byte[] CmdCutPaper = { 0x1D, 0x56, 0x42, 0x00 };        // GS V 66 0
        private static readonly byte[] CmdFeed3 = { 0x1B, 0x64, 0x03 };                 // ESC d 3
        private static readonly byte[] CmdDrawerKickPin2 = { 0x1B, 0x70, 0x00, 0x19, 0xFA };
        private static readonly byte[] CmdDrawerKickPin5 = { 0x1B, 0x70, 0x01, 0x19, 0xFA };

        /// <summary>Format angka ribuan gaya struk retail: 15,000 (tanpa "Rp").</summary>
        private static string Num(decimal v) =>
            v.ToString("#,##0", System.Globalization.CultureInfo.InvariantCulture);

        private static Encoding GetThermalEncoding()
        {
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                return Encoding.GetEncoding(858);
            }
            catch
            {
                try { return Encoding.GetEncoding(1252); }
                catch { return Encoding.ASCII; }
            }
        }

        private static int GetColumnWidth(StoreSettings settings)
            => settings.PaperWidthMm <= 58 ? 32 : 48;

        private static string Divider(int width, char ch = '-') => new(ch, width);

        /// <summary>Rata kiri + kanan dalam satu baris.</summary>
        private static string TwoColumns(string left, string right, int width)
        {
            if (right.Length >= width) return right;

            int spaces = width - left.Length - right.Length;
            if (spaces < 1)
            {
                int maxLeft = Math.Max(0, width - right.Length - 1);
                if (left.Length > maxLeft) left = left[..maxLeft];
                spaces = Math.Max(1, width - left.Length - right.Length);
            }
            return left + new string(' ', spaces) + right;
        }

        /// <summary>Pecah teks panjang menjadi beberapa baris sesuai lebar kolom.</summary>
        private static IEnumerable<string> WrapText(string text, int width, string indent = "")
        {
            text = (text ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
            if (text.Length == 0) yield break;

            int avail = Math.Max(1, width - indent.Length);
            if (text.Length <= avail)
            {
                yield return indent + text;
                yield break;
            }

            string rest = text;
            while (rest.Length > 0)
            {
                int take = Math.Min(avail, rest.Length);
                // Usahakan potong di spasi terdekat
                if (take < rest.Length)
                {
                    int sp = rest.LastIndexOf(' ', take - 1);
                    if (sp > avail / 2) take = sp;
                }
                string chunk = rest[..take].TrimEnd();
                yield return indent + chunk;
                rest = rest[take..].TrimStart();
            }
        }

        /// <summary>Rata kiri + kanan dengan titik-titik pengisi (gaya struk retail).</summary>
        private static string DottedColumns(string left, string right, int width)
        {
            if (left.Length + right.Length + 2 >= width)
                return TwoColumns(left, right, width);

            int dots = width - left.Length - right.Length - 2;
            return left + " " + new string('.', dots) + " " + right;
        }

        /// <summary>
        /// Membangun byte command ESC/POS lengkap untuk satu struk transaksi.
        /// </summary>
        public static byte[] BuildStrukBytes(Transaction t, StoreSettings settings)
        {
            var enc = GetThermalEncoding();
            using var ms = new MemoryStream();
            int width = GetColumnWidth(settings);

            // 1. Inisialisasi printer
            ms.Write(CmdInit, 0, CmdInit.Length);

            // 2. Buka laci kasir (jika diaktifkan)
            if (settings.KickDrawer)
            {
                ms.Write(CmdDrawerKickPin2, 0, CmdDrawerKickPin2.Length);
                ms.Write(CmdDrawerKickPin5, 0, CmdDrawerKickPin5.Length);
            }

            // 3. LOGO TOKO
            bool logoPrinted = false;
            if (settings.TampilkanLogo)
            {
                byte[] logo = LogoService.BuildEscPosLogo(settings);
                if (logo.Length > 0)
                {
                    ms.Write(logo, 0, logo.Length);
                    WriteLine(ms, enc, "");
                    logoPrinted = true;
                }
            }

            // 4. Identitas toko
            ms.Write(CmdAlignCenter, 0, CmdAlignCenter.Length);
            ms.Write(CmdBoldOn, 0, CmdBoldOn.Length);

            // Bila logo sudah memuat nama toko, nama dicetak lebih kecil
            // supaya tidak terlihat dobel/gemuk.
            if (!logoPrinted)
                ms.Write(CmdDoubleHeight, 0, CmdDoubleHeight.Length);

            string storeName = string.IsNullOrWhiteSpace(settings.NamaToko) ? "KASIRKU" : settings.NamaToko.Trim().ToUpperInvariant();
            WriteLine(ms, enc, storeName);

            if (!logoPrinted)
                ms.Write(CmdNormalSize, 0, CmdNormalSize.Length);

            ms.Write(CmdBoldOff, 0, CmdBoldOff.Length);

            if (!string.IsNullOrWhiteSpace(settings.Alamat))
                foreach (var l in WrapText(settings.Alamat, width)) WriteLine(ms, enc, l);

            if (!string.IsNullOrWhiteSpace(settings.Kota))
                WriteLine(ms, enc, settings.Kota.Trim());

            if (!string.IsNullOrWhiteSpace(settings.Telepon))
                WriteLine(ms, enc, settings.Telepon.Trim());

            WriteLine(ms, enc, Divider(width, '='));

            // 5. Informasi nota
            ms.Write(CmdAlignLeft, 0, CmdAlignLeft.Length);
            WriteLine(ms, enc, TwoColumns("No. Nota", t.Id, width));
            WriteLine(ms, enc, TwoColumns("Tanggal", t.Tanggal.ToString("dd-MM-yyyy HH:mm"), width));

            string kasir = string.IsNullOrWhiteSpace(t.KasirNama) ? "-" : t.KasirNama.ToUpperInvariant();
            WriteLine(ms, enc, TwoColumns("Kasir", kasir, width));
            WriteLine(ms, enc, TwoColumns("Pembayaran", (t.MetodeBayar ?? "Tunai").ToUpperInvariant(), width));

            WriteLine(ms, enc, Divider(width, '-'));

            // 6. Daftar item
            int totalQty = 0;
            foreach (var item in t.Items)
            {
                totalQty += item.Qty;

                // Nama produk (bold, wrap rapi)
                ms.Write(CmdBoldOn, 0, CmdBoldOn.Length);
                foreach (var l in WrapText(item.Nama, width)) WriteLine(ms, enc, l);
                ms.Write(CmdBoldOff, 0, CmdBoldOff.Length);

                // "1X 15,000 ......... = 15,000"
                string left = $"  {item.Qty}X {Num(item.Harga)}";
                string right = $"= {Num(item.Subtotal)}";
                WriteLine(ms, enc, DottedColumns(left, right, width));
            }

            WriteLine(ms, enc, Divider(width, '-'));

            // 7. Ringkasan
            WriteLine(ms, enc, TwoColumns($"Qty  {totalQty} item", "", width));

            ms.Write(CmdBoldOn, 0, CmdBoldOn.Length);
            ms.Write(CmdDoubleHeight, 0, CmdDoubleHeight.Length);
            WriteLine(ms, enc, TwoColumns("TOTAL", Num(t.Total), width));
            ms.Write(CmdNormalSize, 0, CmdNormalSize.Length);
            ms.Write(CmdBoldOff, 0, CmdBoldOff.Length);

            WriteLine(ms, enc, TwoColumns($"BAYAR ({(t.MetodeBayar ?? "Tunai").ToUpperInvariant()})", Num(t.Bayar), width));
            WriteLine(ms, enc, TwoColumns("KEMBALI", Num(t.Kembalian), width));

            WriteLine(ms, enc, Divider(width, '='));

            // 8. Footer
            ms.Write(CmdAlignCenter, 0, CmdAlignCenter.Length);

            if (!t.MetodeBayar.Equals("Tunai", StringComparison.OrdinalIgnoreCase))
                WriteLine(ms, enc, "PEMBAYARAN NON-TUNAI LUNAS");

            if (!string.IsNullOrWhiteSpace(settings.FooterStruk))
            {
                ms.Write(CmdBoldOn, 0, CmdBoldOn.Length);
                foreach (var raw in settings.FooterStruk.Replace("\r", "").Split('\n'))
                {
                    if (raw.Trim().Length == 0) continue;
                    foreach (var l in WrapText(raw, width)) WriteLine(ms, enc, l.ToUpperInvariant());
                }
                ms.Write(CmdBoldOff, 0, CmdBoldOff.Length);
            }

            // 9. Baris audit: jam/kasir/no-nota
            WriteLine(ms, enc, "");
            WriteLine(ms, enc, $"{DateTime.Now:HH:mm:ss}/{kasir}/{t.Id}");

            // 10. Feed + potong kertas
            ms.Write(CmdFeed3, 0, CmdFeed3.Length);
            ms.Write(CmdCutPaper, 0, CmdCutPaper.Length);

            return ms.ToArray();
        }

        /// <summary>
        /// Cetak halaman test untuk memastikan printer dan cash drawer berfungsi.
        /// </summary>
        public static (bool Success, string Message) CetakTest(StoreSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.PrinterName))
                return (false, "Printer belum dipilih di Pengaturan.");

            var enc = GetThermalEncoding();
            using var ms = new MemoryStream();
            int width = GetColumnWidth(settings);

            ms.Write(CmdInit, 0, CmdInit.Length);

            if (settings.KickDrawer)
            {
                ms.Write(CmdDrawerKickPin2, 0, CmdDrawerKickPin2.Length);
                ms.Write(CmdDrawerKickPin5, 0, CmdDrawerKickPin5.Length);
            }

            // Logo
            if (settings.TampilkanLogo)
            {
                byte[] logo = LogoService.BuildEscPosLogo(settings);
                if (logo.Length > 0)
                {
                    ms.Write(logo, 0, logo.Length);
                    WriteLine(ms, enc, "");
                }
            }

            ms.Write(CmdAlignCenter, 0, CmdAlignCenter.Length);
            ms.Write(CmdBoldOn, 0, CmdBoldOn.Length);
            ms.Write(CmdDoubleHeight, 0, CmdDoubleHeight.Length);
            WriteLine(ms, enc, "TEST PRINTER THERMAL");
            ms.Write(CmdNormalSize, 0, CmdNormalSize.Length);
            ms.Write(CmdBoldOff, 0, CmdBoldOff.Length);

            WriteLine(ms, enc, string.IsNullOrWhiteSpace(settings.NamaToko) ? "KASIRKU" : settings.NamaToko);
            WriteLine(ms, enc, Divider(width, '='));

            ms.Write(CmdAlignLeft, 0, CmdAlignLeft.Length);
            WriteLine(ms, enc, TwoColumns("Waktu", DateTime.Now.ToString("dd-MM-yyyy HH:mm:ss"), width));
            WriteLine(ms, enc, TwoColumns("Printer", settings.PrinterName, width));
            WriteLine(ms, enc, TwoColumns("Lebar Kertas", $"{settings.PaperWidthMm} mm ({width} kolom)", width));
            WriteLine(ms, enc, TwoColumns("Logo Struk", settings.TampilkanLogo ? "Aktif" : "Non-aktif", width));
            WriteLine(ms, enc, TwoColumns("Auto Print", settings.AutoPrintReceipt ? "Aktif" : "Non-aktif", width));
            WriteLine(ms, enc, TwoColumns("Cash Drawer", settings.KickDrawer ? "Aktif" : "Non-aktif", width));
            WriteLine(ms, enc, Divider(width, '-'));

            ms.Write(CmdAlignCenter, 0, CmdAlignCenter.Length);
            ms.Write(CmdBoldOn, 0, CmdBoldOn.Length);
            WriteLine(ms, enc, "PRINTER SIAP DIGUNAKAN");
            ms.Write(CmdBoldOff, 0, CmdBoldOff.Length);

            ms.Write(CmdFeed3, 0, CmdFeed3.Length);
            ms.Write(CmdCutPaper, 0, CmdCutPaper.Length);

            bool ok = RawPrinterHelper.SendBytesToPrinter(settings.PrinterName, ms.ToArray(), "Test Printer Thermal");
            return ok
                ? (true, $"Berhasil mengirim test print ke '{settings.PrinterName}'.")
                : (false, $"Gagal mencetak ke '{settings.PrinterName}'. Pastikan printer menyala dan terhubung.");
        }

        /// <summary>Mengirim perintah kick drawer ke printer yang dikonfigurasi.</summary>
        public static (bool Success, string Message) BukaLaci(StoreSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.PrinterName))
                return (false, "Printer belum dipilih.");

            using var ms = new MemoryStream();
            ms.Write(CmdInit, 0, CmdInit.Length);
            ms.Write(CmdDrawerKickPin2, 0, CmdDrawerKickPin2.Length);
            ms.Write(CmdDrawerKickPin5, 0, CmdDrawerKickPin5.Length);

            bool ok = RawPrinterHelper.SendBytesToPrinter(settings.PrinterName, ms.ToArray(), "Buka Laci Kasir");
            return ok
                ? (true, "Perintah buka laci kasir berhasil dikirim.")
                : (false, "Gagal mengirim perintah buka laci kasir.");
        }

        /// <summary>Mencetak transaksi ke printer thermal.</summary>
        public static (bool Success, string Message) CetakStruk(Transaction t, StoreSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.PrinterName))
                return (false, "Printer thermal belum diatur. Silakan atur printer di halaman Pengaturan Aplikasi Manager.");

            try
            {
                byte[] bytes = BuildStrukBytes(t, settings);
                bool ok = RawPrinterHelper.SendBytesToPrinter(settings.PrinterName, bytes, $"Struk-{t.Id}");
                return ok
                    ? (true, $"Struk {t.Id} berhasil dicetak ke printer '{settings.PrinterName}'.")
                    : (false, $"Gagal mencetak ke printer '{settings.PrinterName}'. Periksa koneksi printer.");
            }
            catch (Exception ex)
            {
                return (false, $"Error cetak thermal: {ex.Message}");
            }
        }

        private static void WriteText(MemoryStream ms, Encoding enc, string text)
        {
            byte[] b = enc.GetBytes(text);
            ms.Write(b, 0, b.Length);
        }

        private static void WriteLine(MemoryStream ms, Encoding enc, string text)
            => WriteText(ms, enc, text + "\n");
    }
}
