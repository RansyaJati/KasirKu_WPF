using System.Drawing.Imaging;
using System.IO;
using System.Text;
using KasirKu.Shared.Models;
using KasirKu.Shared.Services;

namespace StrukPreview
{
    /// <summary>
    /// Alat bantu: merender struk contoh menjadi PNG (80mm &amp; 58mm) memakai
    /// emulator ESC/POS yang sama dengan pratinjau di aplikasi kasir.
    /// </summary>
    internal static class Program
    {
        private static readonly string OutDir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "KasirKu", "Preview");

        private static void Main()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Directory.CreateDirectory(OutDir);

            var settings = new StoreSettings
            {
                NamaToko = "BANYU FROZEN FOOD",
                Alamat = "Jl. RH. Abdul Halim No. 83",
                Kota = "Bandung",
                Telepon = "081221317779",
                FooterStruk = "TERIMA KASIH\nATAS KUNJUNGANNYA",
                PaperWidthMm = 80,
                PrinterName = "",
                TampilkanLogo = true,
                KickDrawer = false
            };

            var trx = new Transaction
            {
                Id = "01A150926000128",
                Tanggal = new DateTime(2026, 9, 15, 10, 56, 52),
                KasirNama = "ANA",
                MetodeBayar = "Tunai",
                Bayar = 50000m,
                Items = new List<TransactionItem>
                {
                    new() { Kode = "KD7", Nama = "Kulit Dimsum PAPIC 7cm/pcs", Harga = 15000m, Qty = 1 },
                    new() { Kode = "BF01", Nama = "Belfoods Chicken Nugget 170gr", Harga = 24500m, Qty = 1 },
                    new() { Kode = "FS02", Nama = "Fiesta Chicken Nugget 200g", Harga = 8500m, Qty = 1 }
                }
            };
            trx.Total = trx.Items.Sum(x => x.Harga * x.Qty);

            // 1. Byte stream yang akan dikirim ke printer sesungguhnya
            byte[] bytes = ThermalPrintService.BuildStrukBytes(trx, settings);
            File.WriteAllBytes(Path.Combine(OutDir, "struk_escpos.bin"), bytes);
            Console.WriteLine($"-> ESC/POS bytes : {bytes.Length:N0} byte");

            // 2. Pratinjau 80mm
            using (var bmp = StrukRenderer.RenderEscPos(bytes, 576))
            {
                string path = Path.Combine(OutDir, "struk_preview.png");
                bmp.Save(path, ImageFormat.Png);
                Console.WriteLine($"-> Pratinjau PNG : {path}");
            }

            // 3. Struk PDF
            string pdfPath = ExportService.SimpanStrukPdf(trx, settings);
            Console.WriteLine($"-> Struk PDF     : {pdfPath}");

            // 4. Pratinjau 58mm
            var s58 = new StoreSettings
            {
                NamaToko = settings.NamaToko,
                Alamat = settings.Alamat,
                Kota = settings.Kota,
                Telepon = settings.Telepon,
                FooterStruk = settings.FooterStruk,
                PaperWidthMm = 58,
                TampilkanLogo = true
            };
            byte[] bytes58 = ThermalPrintService.BuildStrukBytes(trx, s58);
            using (var bmp58 = StrukRenderer.RenderEscPos(bytes58, 384))
            {
                string path58 = Path.Combine(OutDir, "struk_preview_58mm.png");
                bmp58.Save(path58, ImageFormat.Png);
                Console.WriteLine($"-> Pratinjau 58mm: {path58}");
            }
        }
    }
}
