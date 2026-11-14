using System.Diagnostics;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using KasirKu.Shared.Models;
using KasirKu.Shared.Services;

namespace KasirKu.UI
{
    /// <summary>
    /// Menampilkan pratinjau struk persis seperti hasil cetak printer thermal
    /// (dirender dari byte ESC/POS), sehingga kasir bisa memeriksa tampilan
    /// sebelum/sesudah mencetak tanpa membuang kertas.
    /// </summary>
    public partial class StrukPreviewWindow : Window
    {
        private readonly Transaction _trx;
        private readonly StoreSettings _settings;
        private bool _sudahCetak;

        public StrukPreviewWindow(Transaction trx, StoreSettings settings, string? info = null)
        {
            InitializeComponent();

            // Jaga agar jendela tetap muat di layar kasir (mis. laptop 768px)
            double maxH = SystemParameters.WorkArea.Height - 40;
            double maxW = SystemParameters.WorkArea.Width - 40;
            if (Height > maxH) Height = maxH;
            if (Width > maxW) Width = maxW;

            _trx = trx;
            _settings = settings;

            string metode = trx.MetodeBayar ?? "Tunai";
            string lebarkertas = settings.PaperWidthMm <= 58 ? "58 mm / 32 kolom" : "80 mm / 48 kolom";
            string printer = string.IsNullOrWhiteSpace(settings.PrinterName)
                ? "belum diatur (bisa simpan PNG/PDF)"
                : settings.PrinterName;

            TxtInfo.Text = $"Nota {trx.Id}  •  {trx.Tanggal:dd-MM-yyyy HH:mm}  •  {metode}  •  " +
                           $"Kertas {lebarkertas}\nPrinter: {printer}";

            if (!string.IsNullOrWhiteSpace(info))
                TxtInfo.Text += $"\n{info}";

            MuatPratinjau();
        }

        private void MuatPratinjau()
        {
            try
            {
                using var bmp = StrukRenderer.RenderStruk(_trx, _settings);

                using var ms = new MemoryStream();
                bmp.Save(ms, ImageFormat.Png);
                ms.Position = 0;

                var img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.StreamSource = ms;
                img.EndInit();
                img.Freeze();

                ImgStruk.Source = img;
                ImgStruk.Width = bmp.Width;
                ImgStruk.Height = bmp.Height;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Gagal membuat pratinjau struk: " + ex.Message,
                    "Pratinjau Struk", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnCetak_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_settings.PrinterName))
            {
                MessageBox.Show(
                    "Printer thermal belum diatur.\n\nAtur di aplikasi Manager → Pengaturan, atau tekan F8 di aplikasi kasir.",
                    "Cetak Struk", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var hasil = ThermalPrintService.CetakStruk(_trx, _settings);
            if (hasil.Success)
            {
                _sudahCetak = true;
                TxtInfo.Text = $"Struk {_trx.Id} berhasil dikirim ke printer '{_settings.PrinterName}'.";
            }
            else
            {
                MessageBox.Show(hasil.Message, "Gagal Cetak", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnPng_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string path = StrukRenderer.SimpanStrukPng(_trx, _settings);
                MessageBox.Show($"Pratinjau struk disimpan:\n{path}", "Simpan PNG",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Gagal menyimpan PNG: " + ex.Message, "Simpan PNG",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnPdf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string path = ExportService.SimpanStrukPdf(_trx, _settings);
                MessageBox.Show($"Struk PDF disimpan:\n{path}", "Simpan PDF",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Gagal menyimpan PDF: " + ex.Message, "Simpan PDF",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnBukaPdf_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string path = ExportService.SimpanStrukPdf(_trx, _settings);
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Gagal membuka PDF: " + ex.Message, "Buka PDF",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnTutup_Click(object sender, RoutedEventArgs e) => Close();

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
                return;
            }

            base.OnKeyDown(e);
        }

        /// <summary>True bila struk sudah berhasil dikirim ke printer dari jendela ini.</summary>
        public bool SudahCetak => _sudahCetak;
    }
}
