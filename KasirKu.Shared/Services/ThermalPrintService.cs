using System.Text;
using KasirKu.Shared.Models;

namespace KasirKu.Shared.Services
{
    /// <summary>
    /// Service untuk memformat struk belanja dan mengirimkan perintah ESC/POS
    /// ke printer thermal (58mm / 80mm) serta kontrol laci uang (cash drawer).
    /// </summary>
    public static class ThermalPrintService
    {
        // ESC/POS Commands
        private static readonly byte[] CmdInit = new byte[] { 0x1B, 0x40 }; // ESC @ (Reset/Init)
        private static readonly byte[] CmdAlignLeft = new byte[] { 0x1B, 0x61, 0x00 }; // ESC a 0
        private static readonly byte[] CmdAlignCenter = new byte[] { 0x1B, 0x61, 0x01 }; // ESC a 1
        private static readonly byte[] CmdAlignRight = new byte[] { 0x1B, 0x61, 0x02 }; // ESC a 2
        private static readonly byte[] CmdBoldOn = new byte[] { 0x1B, 0x45, 0x01 }; // ESC E 1
        private static readonly byte[] CmdBoldOff = new byte[] { 0x1B, 0x45, 0x00 }; // ESC E 0
        private static readonly byte[] CmdDoubleSize = new byte[] { 0x1D, 0x21, 0x11 }; // GS ! 0x11 (Double Width & Height)
        private static readonly byte[] CmdDoubleHeight = new byte[] { 0x1D, 0x21, 0x01 }; // GS ! 0x01 (Double Height)
        private static readonly byte[] CmdNormalSize = new byte[] { 0x1D, 0x21, 0x00 }; // GS ! 0x00 (Normal)
        private static readonly byte[] CmdCutPaper = new byte[] { 0x1D, 0x56, 0x42, 0x00 }; // GS V 66 0 (Feed and Partial Cut)
        private static readonly byte[] CmdDrawerKickPin2 = new byte[] { 0x1B, 0x70, 0x00, 0x19, 0xFA }; // ESC p 0 25 250
        private static readonly byte[] CmdDrawerKickPin5 = new byte[] { 0x1B, 0x70, 0x01, 0x19, 0xFA }; // ESC p 1 25 250

        private static string Rp(decimal v) => $"Rp {v:N0}";

        private static Encoding GetThermalEncoding()
        {
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                return Encoding.GetEncoding(858); // Code Page 858 (Multilingual Latin I + Euro)
            }
            catch
            {
                try
                {
                    return Encoding.GetEncoding(1252);
                }
                catch
                {
                    return Encoding.ASCII;
                }
            }
        }

        private static int GetColumnWidth(StoreSettings settings)
        {
            return settings.PaperWidthMm <= 58 ? 32 : 48;
        }

        /// <summary>
        /// Membuat divider line sesuai lebar kertas (80mm: 48 char, 58mm: 32 char).
        /// </summary>
        private static string Divider(int width, char ch = '-') => new string(ch, width);

        /// <summary>
        /// Meratakan teks kiri dan kanan dalam satu baris.
        /// </summary>
        private static string TwoColumns(string left, string right, int width)
        {
            int spaces = width - left.Length - right.Length;
            if (spaces < 1)
            {
                int maxLeft = Math.Max(0, width - right.Length - 1);
                left = left.Length > maxLeft ? left[..maxLeft] : left;
                spaces = Math.Max(1, width - left.Length - right.Length);
            }
            return left + new string(' ', spaces) + right;
        }

        /// <summary>
        /// Membangun byte command ESC/POS untuk struk transaksi.
        /// </summary>
        public static byte[] BuildStrukBytes(Transaction t, StoreSettings settings)
        {
            var enc = GetThermalEncoding();
            using var ms = new MemoryStream();

            // 1. Inisialisasi printer
            ms.Write(CmdInit, 0, CmdInit.Length);

            // 2. Buka laci kasir (jika diaktifkan)
            if (settings.KickDrawer)
            {
                ms.Write(CmdDrawerKickPin2, 0, CmdDrawerKickPin2.Length);
                ms.Write(CmdDrawerKickPin5, 0, CmdDrawerKickPin5.Length);
            }

            int width = GetColumnWidth(settings);

            // 3. Header Toko (Center)
            ms.Write(CmdAlignCenter, 0, CmdAlignCenter.Length);
            ms.Write(CmdBoldOn, 0, CmdBoldOn.Length);
            ms.Write(CmdDoubleHeight, 0, CmdDoubleHeight.Length);

            string storeName = string.IsNullOrWhiteSpace(settings.NamaToko) ? "KASIRKU" : settings.NamaToko;
            WriteText(ms, enc, storeName + "\n");

            ms.Write(CmdNormalSize, 0, CmdNormalSize.Length);
            ms.Write(CmdBoldOff, 0, CmdBoldOff.Length);

            if (!string.IsNullOrWhiteSpace(settings.Alamat))
                WriteText(ms, enc, settings.Alamat + "\n");

            if (!string.IsNullOrWhiteSpace(settings.Telepon))
                WriteText(ms, enc, $"Telp: {settings.Telepon}\n");

            WriteText(ms, enc, Divider(width, '=') + "\n");

            // 4. Informasi Nota (Left)
            ms.Write(CmdAlignLeft, 0, CmdAlignLeft.Length);
            WriteText(ms, enc, TwoColumns($"No: {t.Id}", t.Tanggal.ToString("dd/MM/yyyy HH:mm"), width) + "\n");
            WriteText(ms, enc, $"Metode: {t.MetodeBayar}\n");
            WriteText(ms, enc, Divider(width, '-') + "\n");

            // 5. Daftar Item
            foreach (var item in t.Items)
            {
                // Baris nama barang
                ms.Write(CmdBoldOn, 0, CmdBoldOn.Length);
                string nama = item.Nama;
                if (nama.Length > width) nama = nama[..(width - 1)] + ".";
                WriteText(ms, enc, nama + "\n");
                ms.Write(CmdBoldOff, 0, CmdBoldOff.Length);

                // Baris qty x harga = subtotal
                string qtyHarga = $"  {item.Qty} x {Rp(item.Harga)}";
                string subtotal = Rp(item.Subtotal);
                WriteText(ms, enc, TwoColumns(qtyHarga, subtotal, width) + "\n");
            }

            WriteText(ms, enc, Divider(width, '-') + "\n");

            // 6. Ringkasan Total, Bayar, Kembalian
            ms.Write(CmdBoldOn, 0, CmdBoldOn.Length);
            WriteText(ms, enc, TwoColumns("TOTAL", Rp(t.Total), width) + "\n");
            ms.Write(CmdBoldOff, 0, CmdBoldOff.Length);

            WriteText(ms, enc, TwoColumns($"BAYAR ({t.MetodeBayar})", Rp(t.Bayar), width) + "\n");
            WriteText(ms, enc, TwoColumns("KEMBALIAN", Rp(t.Kembalian), width) + "\n");

            WriteText(ms, enc, Divider(width, '=') + "\n");

            // 7. Footer
            ms.Write(CmdAlignCenter, 0, CmdAlignCenter.Length);
            if (!t.MetodeBayar.Equals("Tunai", StringComparison.OrdinalIgnoreCase))
            {
                WriteText(ms, enc, "Pembayaran non-tunai lunas.\n");
            }

            if (!string.IsNullOrWhiteSpace(settings.FooterStruk))
            {
                ms.Write(CmdBoldOn, 0, CmdBoldOn.Length);
                WriteText(ms, enc, settings.FooterStruk + "\n");
                ms.Write(CmdBoldOff, 0, CmdBoldOff.Length);
            }

            // 8. Feed baris kosong dan potong kertas
            WriteText(ms, enc, "\n\n\n");
            ms.Write(CmdCutPaper, 0, CmdCutPaper.Length);

            return ms.ToArray();
        }

        /// <summary>
        /// Cetak halaman test untuk memastikan printer dan cash drawer berfungsi.
        /// </summary>
        public static (bool Success, string Message) CetakTest(StoreSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.PrinterName))
            {
                return (false, "Printer belum dipilih di Pengaturan.");
            }

            var enc = GetThermalEncoding();
            using var ms = new MemoryStream();
            int width = GetColumnWidth(settings);

            ms.Write(CmdInit, 0, CmdInit.Length);

            if (settings.KickDrawer)
            {
                ms.Write(CmdDrawerKickPin2, 0, CmdDrawerKickPin2.Length);
                ms.Write(CmdDrawerKickPin5, 0, CmdDrawerKickPin5.Length);
            }

            ms.Write(CmdAlignCenter, 0, CmdAlignCenter.Length);
            ms.Write(CmdBoldOn, 0, CmdBoldOn.Length);
            ms.Write(CmdDoubleHeight, 0, CmdDoubleHeight.Length);
            WriteText(ms, enc, "TEST PRINTER THERMAL\n");
            ms.Write(CmdNormalSize, 0, CmdNormalSize.Length);
            ms.Write(CmdBoldOff, 0, CmdBoldOff.Length);

            WriteText(ms, enc, $"{settings.NamaToko}\n");
            WriteText(ms, enc, Divider(width, '=') + "\n");

            ms.Write(CmdAlignLeft, 0, CmdAlignLeft.Length);
            WriteText(ms, enc, $"Waktu: {DateTime.Now:dd/MM/yyyy HH:mm:ss}\n");
            WriteText(ms, enc, $"Printer: {settings.PrinterName}\n");
            WriteText(ms, enc, $"Lebar Kertas: {settings.PaperWidthMm} mm ({width} kolom)\n");
            WriteText(ms, enc, $"Auto Print: {(settings.AutoPrintReceipt ? "Aktif" : "Non-aktif")}\n");
            WriteText(ms, enc, $"Cash Drawer: {(settings.KickDrawer ? "Aktif" : "Non-aktif")}\n");
            WriteText(ms, enc, Divider(width, '-') + "\n");

            ms.Write(CmdAlignCenter, 0, CmdAlignCenter.Length);
            WriteText(ms, enc, "Printer thermal siap digunakan!\n");
            WriteText(ms, enc, "\n\n\n");
            ms.Write(CmdCutPaper, 0, CmdCutPaper.Length);

            bool ok = RawPrinterHelper.SendBytesToPrinter(settings.PrinterName, ms.ToArray(), "Test Printer Thermal");
            if (ok)
                return (true, $"Berhasil mengirim test print ke '{settings.PrinterName}'.");
            else
                return (false, $"Gagal mencetak ke '{settings.PrinterName}'. Pastikan printer menyala dan terhubung.");
        }

        /// <summary>
        /// Mengirimkan perintah kick drawer ke printer yang dikonfigurasi.
        /// </summary>
        public static (bool Success, string Message) BukaLaci(StoreSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.PrinterName))
                return (false, "Printer belum dipilih.");

            using var ms = new MemoryStream();
            ms.Write(CmdDrawerKickPin2, 0, CmdDrawerKickPin2.Length);
            ms.Write(CmdDrawerKickPin5, 0, CmdDrawerKickPin5.Length);

            bool ok = RawPrinterHelper.SendBytesToPrinter(settings.PrinterName, ms.ToArray(), "Buka Laci Kasir");
            if (ok)
                return (true, "Perintah buka laci kasir berhasil dikirim.");
            else
                return (false, "Gagal mengirim perintah buka laci kasir.");
        }

        /// <summary>
        /// Mencetak transaksi ke printer thermal.
        /// </summary>
        public static (bool Success, string Message) CetakStruk(Transaction t, StoreSettings settings)
        {
            if (string.IsNullOrWhiteSpace(settings.PrinterName))
            {
                return (false, "Printer thermal belum diatur. Silakan atur printer di halaman Pengaturan Aplikasi Manager.");
            }

            try
            {
                byte[] bytes = BuildStrukBytes(t, settings);
                bool ok = RawPrinterHelper.SendBytesToPrinter(settings.PrinterName, bytes, $"Struk-{t.Id}");
                if (ok)
                {
                    return (true, $"Struk {t.Id} berhasil dicetak ke printer '{settings.PrinterName}'.");
                }
                else
                {
                    return (false, $"Gagal mencetak ke printer '{settings.PrinterName}'. Periksa koneksi printer.");
                }
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
    }
}
