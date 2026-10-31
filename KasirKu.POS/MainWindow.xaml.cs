using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using KasirKu.Shared;
using KasirKu.Shared.Models;
using KasirKu.Shared.Services;

namespace KasirKu.POS
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<Product> _products = new();
        private readonly ObservableCollection<CartItem> _cart = new();
        private readonly ObservableCollection<Transaction> _transactions = new();
        private readonly ObservableCollection<StockMovement> _movements = new();

        private StoreSettings _settings = new();
        private Transaction? _lastTrx;
        private User? _loggedInUser;
        private int _nextProductId = 1;
        private int _nextTrx = 1;
        private int _nextMovementId = 1;
        private bool _ready;
        private string _halaman = "Kasir";

        /// <summary>Dipanggil oleh App.xaml.cs setelah login berhasil.</summary>
        public void SetLoggedInUser(User user)
        {
            _loggedInUser = user;
        }

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        // =========== LOAD / SAVE ===========
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadOrSeed();

            if (CmbMetode.Items.Count > 0 && CmbMetode.SelectedIndex < 0)
                CmbMetode.SelectedIndex = 0;

            _ready = true;
            RefreshSemua();
            UpdateMetodeUI();

            TxtTanggal.Text = DateTime.Now.ToString("dddd, dd MMM yyyy");
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (s, _) => TxtJam.Text = DateTime.Now.ToString("HH:mm:ss");
            timer.Start();
            TxtJam.Text = DateTime.Now.ToString("HH:mm:ss");

            // Tampilkan info user yang login
            if (_loggedInUser != null)
                TxtUserInfo.Text = $"Kasir: {_loggedInUser.NamaLengkap}";

            ShowPage("Kasir");
            FocusScan();
        }

        private void LoadOrSeed()
        {
            var data = DataStorageService.Load();
            _settings = DataStorageService.LoadSettings();
            _nextProductId = data.NextProductId;
            _nextTrx = data.NextTrx;
            _nextMovementId = data.NextMovementId;

            _products.Clear();
            foreach (var p in data.Products) _products.Add(p);
            _transactions.Clear();
            foreach (var t in data.Transactions) _transactions.Add(t);
            _movements.Clear();
            foreach (var m in data.Movements) _movements.Add(m);

            // Jika belum ada produk: seed contoh data bawaan
            if (_products.Count == 0)
            {
                SeedContoh();
                SimpanKeDisk();
            }
        }

        private void SeedContoh()
        {
            TambahProdukAwal("BRG001", "8991111110011", "Indomie Goreng", "Pcs", "Makanan", "Indofood", 2500, 3500, 100);
            TambahProdukAwal("BRG002", "8992222220022", "Kopi Kapal Api", "Pack", "Minuman", "Distributor A", 6500, 8000, 50);
            TambahProdukAwal("BRG003", "8993333330033", "Teh Botol Sosro", "Botol", "Minuman", "Distributor A", 4000, 5000, 60);
            TambahProdukAwal("BRG004", "8994444440044", "Chitato Sapi Panggang", "Pcs", "Snack", "Distributor B", 9000, 11000, 40);
            TambahProdukAwal("BRG005", "8995555550055", "Aqua 600ml", "Botol", "Minuman", "Agen Frozen", 3000, 4000, 80);
            TambahProdukAwal("BRG006", "8996666660066", "Roti Aoka Coklat", "Pcs", "Makanan", "Distributor B", 2200, 3000, 70);
            TambahProdukAwal("BRG007", "8997777770077", "Buku Tulis Sidu", "Pcs", "ATK", "Umum", 4500, 6000, 30);
            TambahProdukAwal("BRG008", "8998888880088", "Pulpen Pilot", "Pcs", "ATK", "Umum", 3200, 4500, 45);
        }

        private void TambahProdukAwal(string kode, string barcode, string nama, string satuan, string kelompok, string supplier, decimal hargaBeli, decimal hargaJual, int stok)
        {
            var p = new Product
            {
                Id = _nextProductId++,
                Kode = kode,
                Barcode = barcode,
                Nama = nama,
                Satuan = satuan,
                Kategori = kelompok,
                Supplier = supplier,
                HargaBeli = hargaBeli,
                Harga = hargaJual,
                Stok = 0
            };
            _products.Add(p);
            if (stok > 0) CatatMasuk(p, stok, "Stok awal");
        }

        private void SimpanKeDisk()
        {
            var data = new KasirKuData
            {
                NextProductId = _nextProductId,
                NextTrx = _nextTrx,
                NextMovementId = _nextMovementId,
                Products = _products.ToList(),
                Transactions = _transactions.ToList(),
                Movements = _movements.ToList()
            };
            DataStorageService.Save(data);
        }

        // =========== NAVIGASI F1-F7 + Esc ===========
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.F1: ShowPage("Kasir"); e.Handled = true; break;
                case Key.F2: ShowPage("Cek"); e.Handled = true; break;
                case Key.F3: ShowPage("Riwayat"); e.Handled = true; break;
                case Key.F4: ShowPage("Rekap"); e.Handled = true; break;
                case Key.F5: BtnBayar_Click(this, new RoutedEventArgs()); e.Handled = true; break;
                case Key.F6: BtnResetKeranjang_Click(this, new RoutedEventArgs()); e.Handled = true; break;
                case Key.F7: BtnCetakStruk_Click(this, new RoutedEventArgs()); e.Handled = true; break;
                case Key.F8: BukaModalPrinter(); e.Handled = true; break;
                case Key.Escape:
                    if (ModalPrinter.Visibility == Visibility.Visible)
                    {
                        TutupModalPrinter();
                        e.Handled = true;
                    }
                    else if (_halaman != "Kasir")
                    {
                        ShowPage("Kasir");
                        e.Handled = true;
                    }
                    else
                    {
                        BtnKeluar_Click(this, new RoutedEventArgs());
                        e.Handled = true;
                    }
                    break;
            }
        }

        private void FNav_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string page)
                ShowPage(page);
        }

        private void BtnKeluar_Click(object sender, RoutedEventArgs e)
        {
            if (_cart.Count > 0)
            {
                var jawab = MessageBox.Show(
                    "Masih ada barang di keranjang yang belum dibayar.\n\nTetap keluar aplikasi?",
                    "Konfirmasi Keluar", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (jawab != MessageBoxResult.Yes) return;
            }
            else
            {
                var jawab = MessageBox.Show(
                    "Apakah Anda yakin ingin keluar dari aplikasi KasirKu?",
                    "Konfirmasi Keluar", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (jawab != MessageBoxResult.Yes) return;
            }

            // Langsung keluar aplikasi (tidak memunculkan login window lagi)
            Application.Current.Shutdown();
        }

        // =========== PENGATURAN PRINTER & BUKA LACI DI SISI KASIR ===========
        private void BtnBukaLaciKasir_Click(object sender, RoutedEventArgs e)
        {
            _settings = DataStorageService.LoadSettings();
            var hasil = ThermalPrintService.BukaLaci(_settings);
            if (hasil.Success)
            {
                SetScanStatus("Laci kasir (Cash Drawer) berhasil dibuka.");
            }
            else
            {
                MessageBox.Show(hasil.Message, "Buka Laci", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            FocusScan();
        }

        private void BtnF8Printer_Click(object sender, RoutedEventArgs e) => BukaModalPrinter();

        private void BukaModalPrinter()
        {
            _settings = DataStorageService.LoadSettings();

            // Isi daftar printer yang terpasang
            MuatDaftarPrinterModal();

            if (CmbModalLebarKertas != null)
                CmbModalLebarKertas.SelectedIndex = _settings.PaperWidthMm <= 58 ? 1 : 0;

            if (ChkModalAutoCetakStruk != null)
                ChkModalAutoCetakStruk.IsChecked = _settings.AutoPrintReceipt;

            if (ChkModalKickDrawer != null)
                ChkModalKickDrawer.IsChecked = _settings.KickDrawer;

            ModalPrinter.Visibility = Visibility.Visible;
        }

        private void MuatDaftarPrinterModal()
        {
            if (CmbModalPrinterName == null) return;
            string current = _settings.PrinterName;
            var list = RawPrinterHelper.GetInstalledPrinters();
            CmbModalPrinterName.Items.Clear();
            foreach (var p in list)
            {
                CmbModalPrinterName.Items.Add(p);
            }

            if (!string.IsNullOrWhiteSpace(current))
            {
                if (!CmbModalPrinterName.Items.Contains(current))
                {
                    CmbModalPrinterName.Items.Add(current);
                }
                CmbModalPrinterName.Text = current;
                CmbModalPrinterName.SelectedItem = current;
            }
            else if (CmbModalPrinterName.Items.Count > 0)
            {
                CmbModalPrinterName.SelectedIndex = 0;
            }
        }

        private void BtnModalRefreshPrinter_Click(object sender, RoutedEventArgs e)
        {
            MuatDaftarPrinterModal();
            MessageBox.Show($"Ditemukan {CmbModalPrinterName.Items.Count} printer Windows terpasang.", "Daftar Printer", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void TutupModalPrinter()
        {
            ModalPrinter.Visibility = Visibility.Collapsed;
            FocusScan();
        }

        private void BtnTutupModalPrinter_Click(object sender, RoutedEventArgs e) => TutupModalPrinter();

        private void SimpanStateModalKeSettings()
        {
            _settings.PrinterName = CmbModalPrinterName?.Text?.Trim() ?? "";
            _settings.PaperWidthMm = (CmbModalLebarKertas?.SelectedIndex == 1) ? 58 : 80;
            _settings.AutoPrintReceipt = ChkModalAutoCetakStruk?.IsChecked == true;
            _settings.KickDrawer = ChkModalKickDrawer?.IsChecked == true;
        }

        private void BtnModalTestPrint_Click(object sender, RoutedEventArgs e)
        {
            SimpanStateModalKeSettings();
            var hasil = ThermalPrintService.CetakTest(_settings);
            if (hasil.Success)
                MessageBox.Show(hasil.Message, "Test Print Berhasil", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                MessageBox.Show(hasil.Message, "Test Print Gagal", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void BtnModalBukaLaci_Click(object sender, RoutedEventArgs e)
        {
            SimpanStateModalKeSettings();
            var hasil = ThermalPrintService.BukaLaci(_settings);
            if (hasil.Success)
                MessageBox.Show(hasil.Message, "Buka Laci", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                MessageBox.Show(hasil.Message, "Buka Laci Gagal", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void BtnSimpanModalPrinter_Click(object sender, RoutedEventArgs e)
        {
            SimpanStateModalKeSettings();
            DataStorageService.SaveSettings(_settings);
            MessageBox.Show("Pengaturan printer thermal berhasil disimpan!", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
            TutupModalPrinter();
        }

        private void ShowPage(string page)
        {
            _halaman = page;
            PageKasir.Visibility = Visibility.Collapsed;
            PageCek.Visibility = Visibility.Collapsed;
            PageRiwayat.Visibility = Visibility.Collapsed;
            PageRekap.Visibility = Visibility.Collapsed;

            string title = page, sub = "";
            switch (page)
            {
                case "Kasir":
                    PageKasir.Visibility = Visibility.Visible;
                    title = "Kasir (F1)";
                    sub = "Scan barcode / ketik kode atau nama + Enter. F5 = Bayar, F6 = Reset, F7 = Struk PDF.";
                    FocusScan();
                    break;
                case "Cek":
                    PageCek.Visibility = Visibility.Visible;
                    title = "Cek Harga & Stok (F2)";
                    sub = "Lihat harga dan ketersediaan stok barang. Kelola data lewat aplikasi Manager.";
                    RefreshCek();
                    break;
                case "Riwayat":
                    PageRiwayat.Visibility = Visibility.Visible;
                    title = "Riwayat Transaksi Hari Ini (F3)";
                    sub = "Klik baris untuk detail. F7 mencetak struk terpilih ke PDF.";
                    RefreshRiwayat();
                    break;
                case "Rekap":
                    PageRekap.Visibility = Visibility.Visible;
                    title = "Rekap Shift (F4)";
                    sub = "Ringkasan omzet, jumlah transaksi, dan rincian pembayaran hari ini.";
                    RefreshRekap();
                    break;
            }

            TxtHeaderTitle.Text = title;
            TxtHeaderSub.Text = sub;
            HighlightFButtons();
        }

        private void HighlightFButtons()
        {
            var def = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
            var act = new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB));
            BtnF1Kasir.Background = _halaman == "Kasir" ? act : def;
            BtnF2Cek.Background = _halaman == "Cek" ? act : def;
            BtnF3Riwayat.Background = _halaman == "Riwayat" ? act : def;
            BtnF4Rekap.Background = _halaman == "Rekap" ? act : def;
        }

        private void FocusScan()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try { TxtScan.Focus(); TxtScan.SelectAll(); } catch { }
            }), DispatcherPriority.Input);
        }

        // =========== SCAN BARCODE + INPUT MANUAL ===========
        private void TxtScan_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { ProsesScan(TxtScan.Text); e.Handled = true; }
            else if (e.Key == Key.Down) { DgKeranjang.Focus(); e.Handled = true; }
        }

        private void TxtScan_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_ready) return;
            string key = (TxtScan?.Text ?? "").Trim();
            if (string.IsNullOrEmpty(key)) { SetScanStatus("Siap scan..."); return; }
            int n = _products.Count(p =>
                p.Kode.Contains(key, StringComparison.OrdinalIgnoreCase) ||
                p.Barcode.Contains(key, StringComparison.OrdinalIgnoreCase) ||
                p.Nama.Contains(key, StringComparison.OrdinalIgnoreCase));
            SetScanStatus(n == 0
                ? $"'{key}' tidak ditemukan."
                : $"{n} produk cocok — tekan Enter untuk masukkan ke keranjang.", n == 0);
        }

        private void ProsesScan(string raw)
        {
            string input = (raw ?? "").Trim();
            if (string.IsNullOrEmpty(input)) return;

            int qty = 1;
            string kata = input;
            int bintang = input.IndexOf('*');
            if (bintang > 0 && int.TryParse(input[..bintang].Trim(), out int q) && q > 0 && q <= 999)
            {
                qty = q;
                kata = input[(bintang + 1)..].Trim();
            }
            if (string.IsNullOrEmpty(kata)) { SetScanStatus("Kode kosong.", true); return; }

            var exact = _products.FirstOrDefault(p =>
                p.Kode.Equals(kata, StringComparison.OrdinalIgnoreCase) ||
                p.Barcode.Equals(kata, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
            {
                TxtScan.Text = "";
                TambahKeKeranjang(exact, qty);
                FocusScan();
                return;
            }

            var cocok = _products.Where(p =>
                p.Kode.Contains(kata, StringComparison.OrdinalIgnoreCase) ||
                p.Barcode.Contains(kata, StringComparison.OrdinalIgnoreCase) ||
                p.Nama.Contains(kata, StringComparison.OrdinalIgnoreCase)).ToList();

            if (cocok.Count == 1) { TxtScan.Text = ""; TambahKeKeranjang(cocok[0], qty); FocusScan(); }
            else if (cocok.Count > 1)
            {
                string contoh = string.Join(", ", cocok.Take(3).Select(p => p.Nama));
                SetScanStatus($"{cocok.Count} hasil untuk '{kata}' ({contoh}...) — persempit kata kunci.", true);
            }
            else
            {
                SetScanStatus($"'{kata}' tidak ditemukan.", true);
                System.Media.SystemSounds.Beep.Play();
            }
        }

        private void SetScanStatus(string msg, bool warning = false)
        {
            TxtScanStatus.Text = msg;
            TxtScanStatus.Foreground = warning
                ? new SolidColorBrush(Color.FromRgb(0xB9, 0x1C, 0x1C))
                : new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB));
        }

        // =========== KERANJANG ===========
        private void RefreshSemua()
        {
            RefreshKeranjang();
            UpdateInfoGlobal();
        }

        private void TambahKeKeranjang(Product p, int qty = 1)
        {
            if (p.Stok <= 0)
            {
                SetScanStatus($"Stok '{p.Nama}' habis!", true);
                MessageBox.Show($"Stok '{p.Nama}' habis!", "Stok Habis", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var existing = _cart.FirstOrDefault(c => c.Product.Id == p.Id);
            int diKeranjang = existing?.Qty ?? 0;
            if (diKeranjang + qty > p.Stok)
            {
                SetScanStatus($"Stok tidak cukup! Sisa {p.Nama}: {p.Stok}", true);
                MessageBox.Show($"Stok tidak cukup! Sisa stok {p.Nama}: {p.Stok}", "Stok Kurang", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (existing == null)
                _cart.Add(new CartItem { Product = p, Qty = qty });
            else
                existing.Qty += qty;
            SetScanStatus($"OK: {p.Nama} x{qty} masuk keranjang.");
            RefreshKeranjang();
        }

        private void BtnHapusItem_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not CartItem item) return;
            _cart.Remove(item);
            RefreshKeranjang();
            FocusScan();
        }

        private void BtnQtyPlus_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not CartItem item) return;
            if (item.Qty + 1 > item.Product.Stok)
            {
                MessageBox.Show($"Stok maksimal {item.Product.Stok} untuk {item.Nama}", "Stok", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            item.Qty++;
            RefreshKeranjang();
        }

        private void BtnQtyMin_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not CartItem item) return;
            item.Qty--;
            if (item.Qty <= 0) _cart.Remove(item);
            RefreshKeranjang();
        }

        private decimal TotalBelanja() => _cart.Sum(c => c.Subtotal);

        private void RefreshKeranjang()
        {
            DgKeranjang.ItemsSource = null;
            DgKeranjang.ItemsSource = _cart;
            decimal total = TotalBelanja();
            TxtTotal.Text = $"Rp {total:N0}";
            TxtInfoKeranjang.Text = $"{_cart.Sum(c => c.Qty)} item - {_cart.Count} jenis barang";

            if (MetodeAktif() != "Tunai" && _ready)
                TxtBayar.Text = total.ToString();
            HitungKembalian();
        }

        // =========== PEMBAYARAN ===========
        private string MetodeAktif()
        {
            if (CmbMetode?.SelectedItem is ComboBoxItem it)
                return it.Content?.ToString() ?? "Tunai";
            return "Tunai";
        }

        private void CmbMetode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_ready) return;
            UpdateMetodeUI();
            RefreshKeranjang();
        }

        private void UpdateMetodeUI()
        {
            if (TxtMetodeInfo == null || TxtBayar == null) return;
            string m = MetodeAktif();
            if (m == "Tunai")
            {
                TxtBayar.IsEnabled = true;
                TxtMetodeInfo.Text = "";
            }
            else
            {
                TxtBayar.IsEnabled = false;
                TxtMetodeInfo.Text = $"Mode {m}: nominal otomatis = total. Pastikan pembayaran {m} berhasil sebelum tekan F5.";
            }
            HitungKembalian();
        }

        private decimal ParseBayar()
        {
            string t = (TxtBayar.Text ?? "").Replace("Rp", "").Replace(".", "").Replace(",", "").Trim();
            return decimal.TryParse(t, out var v) ? v : 0;
        }

        private void TxtBayar_TextChanged(object sender, TextChangedEventArgs e) { if (_ready) HitungKembalian(); }

        private void TxtBayar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { BtnBayar_Click(this, new RoutedEventArgs()); e.Handled = true; }
        }

        private void HitungKembalian()
        {
            if (TxtKembalian == null) return;
            string m = MetodeAktif();
            decimal kembali = m == "Tunai" ? ParseBayar() - TotalBelanja() : 0;
            TxtKembalian.Text = $"Rp {kembali:N0}";
            TxtKembalian.Foreground = kembali < 0
                ? new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26))
                : new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A));
        }

        private void BtnNominal_Click(object sender, RoutedEventArgs e)
        {
            if (MetodeAktif() != "Tunai") return;
            if ((sender as Button)?.Tag is string s && decimal.TryParse(s, out var n))
                TxtBayar.Text = (ParseBayar() + n).ToString();
        }

        private void BtnUangPas_Click(object sender, RoutedEventArgs e)
        {
            if (MetodeAktif() != "Tunai") return;
            TxtBayar.Text = TotalBelanja().ToString();
        }

        private void BtnResetKeranjang_Click(object sender, RoutedEventArgs e)
        {
            if (_cart.Count == 0 && string.IsNullOrWhiteSpace(TxtBayar.Text)) return;
            _cart.Clear();
            TxtBayar.Text = "";
            RefreshKeranjang();
            if (_halaman == "Kasir") FocusScan();
        }

        private void BtnBayar_Click(object sender, RoutedEventArgs e)
        {
            if (_halaman != "Kasir" && _cart.Count == 0) { ShowPage("Kasir"); return; }
            if (_cart.Count == 0)
            {
                MessageBox.Show("Keranjang masih kosong! Scan barcode dulu.", "Kasir", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string metode = MetodeAktif();
            decimal total = TotalBelanja();
            decimal bayar = metode == "Tunai" ? ParseBayar() : total;

            if (metode == "Tunai" && bayar < total)
            {
                MessageBox.Show($"Uang kurang! Total Rp {total:N0}, bayar Rp {bayar:N0}.", "Pembayaran", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (metode != "Tunai")
            {
                var konfirmasi = MessageBox.Show(
                    $"Pastikan pembayaran {metode} sebesar Rp {total:N0} sudah BERHASIL.\n\nLanjutkan simpan transaksi?",
                    $"Konfirmasi {metode}", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (konfirmasi != MessageBoxResult.Yes) return;
            }

            string trxId = $"TRX-{DateTime.Now:yyyyMMdd}-{_nextTrx++.D3()}";

            foreach (var c in _cart)
                CatatKeluar(c.Product, c.Qty, $"Penjualan {trxId}");

            var trx = new Transaction
            {
                Id = trxId,
                Tanggal = DateTime.Now,
                Total = total,
                Bayar = bayar,
                MetodeBayar = metode,
                Items = _cart.Select(c => new TransactionItem
                {
                    Kode = c.Kode,
                    Nama = c.Nama,
                    Harga = c.Harga,
                    HargaBeli = c.Product.HargaBeli,
                    Qty = c.Qty
                }).ToList()
            };
            _transactions.Insert(0, trx);
            _lastTrx = trx;

            // Auto simpan struk PDF (arsip digital)
            _settings = DataStorageService.LoadSettings();
            string pdfPath;
            try { pdfPath = ExportService.SimpanStrukPdf(trx, _settings); }
            catch (Exception ex) { pdfPath = "(gagal simpan PDF: " + ex.Message + ")"; }

            // Cetak ke Printer Thermal jika diaktifkan
            string infoThermal = "";
            if (_settings.AutoPrintReceipt && !string.IsNullOrWhiteSpace(_settings.PrinterName))
            {
                var thermalResult = ThermalPrintService.CetakStruk(trx, _settings);
                if (thermalResult.Success)
                    infoThermal = $"\nThermal: Struk tercetak ke '{_settings.PrinterName}'.";
                else
                    infoThermal = $"\nThermal: {thermalResult.Message}";
            }
            else if (string.IsNullOrWhiteSpace(_settings.PrinterName))
            {
                infoThermal = "\nThermal: Belum diatur (atur printer di Aplikasi Manager).";
            }

            string struk = ExportService.BuildStrukText(trx, _settings);
            MessageBox.Show(struk + $"\n\nMetode: {metode}{infoThermal}\nStruk PDF: {pdfPath}",
                "Transaksi Berhasil", MessageBoxButton.OK, MessageBoxImage.Information);

            _cart.Clear();
            TxtBayar.Text = "";
            RefreshSemua();
            UpdateMetodeUI();
            FocusScan();

            // Simpan data ke disk setelah transaksi
            SimpanKeDisk();
        }

        // =========== CETAK STRUK THERMAL / PDF (F7) ===========
        private void BtnCetakStruk_Click(object sender, RoutedEventArgs e)
        {
            _settings = DataStorageService.LoadSettings();
            Transaction? t = null;
            if (_halaman == "Riwayat" && DgRiwayat.SelectedItem is Transaction pilih)
                t = pilih;
            else
                t = _lastTrx ?? _transactions.FirstOrDefault();

            if (t == null)
            {
                MessageBox.Show("Belum ada transaksi untuk dicetak.", "Cetak Struk", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Coba cetak ke printer thermal jika sudah diatur
            if (!string.IsNullOrWhiteSpace(_settings.PrinterName))
            {
                var thermalResult = ThermalPrintService.CetakStruk(t, _settings);
                if (thermalResult.Success)
                {
                    MessageBox.Show(thermalResult.Message, "Struk Thermal", MessageBoxButton.OK, MessageBoxImage.Information);
                    FocusScan();
                    return;
                }
                else
                {
                    var tanya = MessageBox.Show($"{thermalResult.Message}\n\nApakah Anda ingin membuka versi PDF?",
                        "Gagal Cetak Thermal", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (tanya != MessageBoxResult.Yes)
                    {
                        FocusScan();
                        return;
                    }
                }
            }

            try
            {
                string path = ExportService.SimpanStrukPdf(t, _settings);
                var jawab = MessageBox.Show($"Struk {t.Id} tersimpan:\n{path}\n\nBuka file sekarang?",
                    "Struk PDF", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (jawab == MessageBoxResult.Yes)
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Gagal membuat PDF: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                FocusScan();
            }
        }

        // =========== CEK BARANG (READ-ONLY) ===========
        private void RefreshCek()
        {
            string key = TxtCariCek?.Text?.Trim().ToLower() ?? "";
            var data = string.IsNullOrEmpty(key)
                ? _products.ToList()
                : _products.Where(p =>
                    p.Kode.ToLower().Contains(key) ||
                    p.Barcode.ToLower().Contains(key) ||
                    p.Nama.ToLower().Contains(key) ||
                    p.Kategori.ToLower().Contains(key)).ToList();
            DgCek.ItemsSource = data;
        }

        private void TxtCariCek_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_ready) return;
            RefreshCek();
        }

        // =========== RIWAYAT SHIFT (HARI INI) ===========
        private List<Transaction> TransaksiHariIni()
            => _transactions.Where(t => t.Tanggal.Date == DateTime.Today).ToList();

        private void RefreshRiwayat()
        {
            string key = TxtCariRiwayat?.Text?.Trim().ToLower() ?? "";
            var hariIni = TransaksiHariIni();
            var data = string.IsNullOrEmpty(key)
                ? hariIni
                : hariIni.Where(t => t.Id.ToLower().Contains(key) || t.Ringkasan.ToLower().Contains(key) || t.MetodeBayar.ToLower().Contains(key)).ToList();
            DgRiwayat.ItemsSource = data;
        }

        private void TxtCariRiwayat_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_ready) return;
            RefreshRiwayat();
        }

        private void DgRiwayat_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DgRiwayat.SelectedItem is Transaction t)
            {
                TxtDetailJudul.Text = $"Detail - {t.Id}";
                DgRiwayatDetail.ItemsSource = t.Items;
                TxtDetailTotal.Text = $"Metode: {t.MetodeBayar}\nTotal: Rp {t.Total:N0} | Bayar: Rp {t.Bayar:N0} | Kembali: Rp {t.Kembalian:N0}\n{t.Tanggal:dd MMM yyyy HH:mm} | {t.JumlahItem} item";
            }
            else
            {
                TxtDetailJudul.Text = "Detail Transaksi";
                DgRiwayatDetail.ItemsSource = null;
                TxtDetailTotal.Text = "";
            }
        }

        // =========== REKAP SHIFT ===========
        private void RefreshRekap()
        {
            var data = TransaksiHariIni();
            decimal omzet = data.Sum(t => t.Total);
            int jmlTrx = data.Count;
            int jmlItem = data.Sum(t => t.JumlahItem);
            decimal tunai = data.Where(t => t.MetodeBayar == "Tunai").Sum(t => t.Total);
            decimal nonTunai = omzet - tunai;

            CardOmzetShift.Text = $"Rp {omzet:N0}";
            CardTrxShift.Text = jmlTrx.ToString();
            CardItemShift.Text = jmlItem.ToString();
            CardTunai.Text = $"Rp {tunai:N0}";
            CardNonTunai.Text = $"Rp {nonTunai:N0}";

            var perMetode = data.GroupBy(t => t.MetodeBayar)
                .Select(g => $"{g.Key}: {g.Count()} trx (Rp {g.Sum(x => x.Total):N0})");
            TxtRingkasanShift.Text = jmlTrx == 0
                ? "Belum ada transaksi hari ini."
                : $"Hari ini: {jmlTrx} transaksi, {jmlItem} item terjual.\n" +
                  $"Omzet: Rp {omzet:N0} | Tunai: Rp {tunai:N0} | Non-Tunai: Rp {nonTunai:N0}\n" +
                  $"Rincian: {string.Join(" | ", perMetode)}";
        }

        private void BtnCetakRekap_Click(object sender, RoutedEventArgs e)
        {
            var data = TransaksiHariIni();
            try
            {
                string path = ExportService.SimpanLaporanPdf(data, DateTime.Today, DateTime.Today, settings: _settings);
                var jawab = MessageBox.Show($"Rekap shift tersimpan ({data.Count} transaksi):\n{path}\n\nBuka file sekarang?",
                    "Rekap Shift", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (jawab == MessageBoxResult.Yes)
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Gagal membuat PDF: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // =========== STOK MASUK / KELUAR ===========
        private void TambahLog(Product p, string jenis, int qty, int sebelum, int sesudah, string keterangan)
        {
            _movements.Insert(0, new StockMovement
            {
                Id = _nextMovementId++,
                Tanggal = DateTime.Now,
                ProductId = p.Id,
                Kode = p.Kode,
                Nama = p.Nama,
                Jenis = jenis,
                Qty = qty,
                StokSebelum = sebelum,
                StokSesudah = sesudah,
                Keterangan = keterangan
            });
        }

        private void CatatMasuk(Product p, int qty, string keterangan)
        {
            if (qty <= 0) return;
            int sebelum = p.Stok;
            p.Stok += qty;
            TambahLog(p, "Masuk", qty, sebelum, p.Stok, keterangan);
        }

        private bool CatatKeluar(Product p, int qty, string keterangan)
        {
            if (qty <= 0 || qty > p.Stok) return false;
            int sebelum = p.Stok;
            p.Stok -= qty;
            TambahLog(p, "Keluar", qty, sebelum, p.Stok, keterangan);
            return true;
        }

        // =========== INFO GLOBAL ===========
        private void UpdateInfoGlobal()
        {
            if (TxtInfoGlobal == null) return;
            var hariIni = TransaksiHariIni();
            TxtInfoGlobal.Text = $"{hariIni.Count} transaksi hari ini - Rp {hariIni.Sum(t => t.Total):N0}";
        }
    }
}
