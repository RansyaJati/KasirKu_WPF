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
using KasirKu.UI;

namespace KasirKu.POS
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<Product> _products = new();
        private readonly ObservableCollection<CartItem> _cart = new();
        private readonly ObservableCollection<Transaction> _transactions = new();
        private readonly ObservableCollection<StockMovement> _movements = new();
        private readonly ObservableCollection<CashExpense> _expenses = new();
        private readonly ObservableCollection<HoldTransaction> _holdTransactions = new();
        private readonly ObservableCollection<Member> _members = new();
        private readonly ObservableCollection<Supplier> _suppliers = new();

        private StoreSettings _settings = new();
        private decimal? _pendingCustomHarga;
        private Member? _selectedMember;
        private Transaction? _lastTrx;
        private User? _loggedInUser;
        private int _nextProductId = 1;
        private int _nextTrx = 1;
        private int _nextMovementId = 1;
        private int _nextExpenseId = 1;
        private int _nextMemberId = 1;
        private int _nextSupplierId = 1;
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
            _nextExpenseId = data.NextExpenseId;
            _nextMemberId = data.NextMemberId;
            _nextSupplierId = data.NextSupplierId;

            _products.Clear();
            foreach (var p in data.Products) _products.Add(p);
            _transactions.Clear();
            foreach (var t in data.Transactions) _transactions.Add(t);
            _movements.Clear();
            foreach (var m in data.Movements) _movements.Add(m);
            _expenses.Clear();
            foreach (var exp in data.Expenses) _expenses.Add(exp);
            _holdTransactions.Clear();
            foreach (var h in data.HoldTransactions) _holdTransactions.Add(h);
            _members.Clear();
            foreach (var mem in data.Members) _members.Add(mem);
            _suppliers.Clear();
            foreach (var sup in data.Suppliers) _suppliers.Add(sup);
            UpdateHoldCartButtonBadge();
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
                NextExpenseId = _nextExpenseId,
                NextMemberId = _nextMemberId,
                NextSupplierId = _nextSupplierId,
                Products = _products.ToList(),
                Transactions = _transactions.ToList(),
                Movements = _movements.ToList(),
                Expenses = _expenses.ToList(),
                HoldTransactions = _holdTransactions.ToList(),
                Members = _members.ToList(),
                Suppliers = _suppliers.ToList()
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
                case Key.F9:
                    if (_halaman != "Kasir") ShowPage("Kasir");
                    BukaModalCustomHarga();
                    e.Handled = true;
                    break;
                case Key.F11:
                    if (_halaman == "Kasir")
                    {
                        BtnHoldCart_Click(this, new RoutedEventArgs());
                        e.Handled = true;
                    }
                    break;
                case Key.Escape:
                    if (ModalPengeluaranKas.Visibility == Visibility.Visible)
                    {
                        TutupModalPengeluaranKas();
                        e.Handled = true;
                    }
                    else if (ModalHoldCart.Visibility == Visibility.Visible)
                    {
                        TutupModalHoldCart();
                        e.Handled = true;
                    }
                    else if (ModalCustomHarga.Visibility == Visibility.Visible)
                    {
                        TutupModalCustomHarga();
                        e.Handled = true;
                    }
                    else if (ModalPrinter.Visibility == Visibility.Visible)
                    {
                        TutupModalPrinter();
                        e.Handled = true;
                    }
                    else if (_pendingCustomHarga != null)
                    {
                        ResetCustomHarga();
                        SetScanStatus("Mode Custom Harga dibatalkan.");
                        e.Handled = true;
                    }
                    else if (PupProduk != null && PupProduk.IsOpen)
                    {
                        TutupPopupCari();
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
            {
                if (_settings.PaperWidthMm == 76 || (_settings.PrinterName ?? "").ToUpperInvariant().Contains("U220"))
                    CmbModalLebarKertas.SelectedIndex = 1;
                else if (_settings.PaperWidthMm <= 58)
                    CmbModalLebarKertas.SelectedIndex = 2;
                else
                    CmbModalLebarKertas.SelectedIndex = 0;
            }

            if (ChkModalAutoCetakStruk != null)
                ChkModalAutoCetakStruk.IsChecked = _settings.AutoPrintReceipt;

            if (ChkModalKickDrawer != null)
                ChkModalKickDrawer.IsChecked = _settings.KickDrawer;

            if (ChkModalCetakSatuanStruk != null)
                ChkModalCetakSatuanStruk.IsChecked = _settings.CetakSatuanStruk;

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
            _settings.PaperWidthMm = CmbModalLebarKertas?.SelectedIndex switch
            {
                1 => 76,
                2 => 58,
                _ => 80
            };
            _settings.AutoPrintReceipt = ChkModalAutoCetakStruk?.IsChecked == true;
            _settings.KickDrawer = ChkModalKickDrawer?.IsChecked == true;
            _settings.CetakSatuanStruk = ChkModalCetakSatuanStruk?.IsChecked == true;
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

        // =========== MODAL CUSTOM HARGA (F9) ===========
        private void BtnF9CustomHarga_Click(object sender, RoutedEventArgs e)
        {
            if (_halaman != "Kasir") ShowPage("Kasir");
            BukaModalCustomHarga();
        }

        private void BukaModalCustomHarga()
        {
            ModalCustomHarga.Visibility = Visibility.Visible;
            TxtNominalCustomHarga.Text = _pendingCustomHarga.HasValue ? _pendingCustomHarga.Value.ToString("F0") : "";
            TxtNominalCustomHarga.Focus();
            TxtNominalCustomHarga.SelectAll();
        }

        private void TutupModalCustomHarga()
        {
            ModalCustomHarga.Visibility = Visibility.Collapsed;
            FocusScan();
        }

        private void BtnTutupModalCustomHarga_Click(object sender, RoutedEventArgs e) => TutupModalCustomHarga();

        private void BtnBatalCustomHarga_Click(object sender, RoutedEventArgs e)
        {
            ResetCustomHarga();
            SetScanStatus("Mode Custom Harga dibatalkan.");
            FocusScan();
        }

        private void ResetCustomHarga()
        {
            _pendingCustomHarga = null;
            if (PnlCustomHargaAktif != null) PnlCustomHargaAktif.Visibility = Visibility.Collapsed;
        }

        private void TxtNominalCustomHarga_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnTerapkanCustomHarga_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                TutupModalCustomHarga();
                e.Handled = true;
            }
        }

        private void BtnTerapkanCustomHarga_Click(object sender, RoutedEventArgs e)
        {
            string input = (TxtNominalCustomHarga.Text ?? "").Replace(".", "").Replace(",", "").Trim();
            if (!decimal.TryParse(input, out decimal nominal) || nominal < 0)
            {
                MessageBox.Show("Masukkan nominal harga yang valid (angka 0 atau lebih)!", "Custom Harga", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtNominalCustomHarga.Focus();
                TxtNominalCustomHarga.SelectAll();
                return;
            }

            _pendingCustomHarga = nominal;
            PnlCustomHargaAktif.Visibility = Visibility.Visible;
            TxtCustomHargaLabel.Text = $"Rp {nominal:N0}";
            TutupModalCustomHarga();
            SetScanStatus($"🏷️ Mode Custom Harga aktif: Rp {nominal:N0}. Scan barcode atau pilih produk sekarang!");
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
                    sub = "Scan barcode / ketik kode atau nama + Enter. F5 = Bayar, F6 = Reset, F7 = Pratinjau Struk.";
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
        private void TxtScan_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (PupProduk != null && PupProduk.IsOpen)
            {
                if (e.Key == Key.Down)
                {
                    if (LstCariProduk.Items.Count > 0)
                    {
                        int next = LstCariProduk.SelectedIndex + 1;
                        if (next >= LstCariProduk.Items.Count) next = 0;
                        LstCariProduk.SelectedIndex = next;
                        LstCariProduk.ScrollIntoView(LstCariProduk.SelectedItem);
                    }
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.Up)
                {
                    if (LstCariProduk.Items.Count > 0)
                    {
                        int prev = LstCariProduk.SelectedIndex - 1;
                        if (prev < 0) prev = LstCariProduk.Items.Count - 1;
                        LstCariProduk.SelectedIndex = prev;
                        LstCariProduk.ScrollIntoView(LstCariProduk.SelectedItem);
                    }
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.Escape)
                {
                    TutupPopupCari();
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.Enter)
                {
                    if (LstCariProduk.SelectedItem is Product pilih)
                    {
                        TutupPopupCari();
                        TxtScan.Text = "";
                        TambahKeKeranjang(pilih, 1);
                        FocusScan();
                        e.Handled = true;
                        return;
                    }
                }
            }

            if (e.Key == Key.Enter)
            {
                ProsesScan(TxtScan.Text);
                e.Handled = true;
            }
            else if (e.Key == Key.Down && (PupProduk == null || !PupProduk.IsOpen))
            {
                DgKeranjang.Focus();
                e.Handled = true;
            }
        }

        private void LstCariProduk_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && LstCariProduk.SelectedItem is Product p)
            {
                TutupPopupCari();
                TxtScan.Text = "";
                TambahKeKeranjang(p, 1);
                FocusScan();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                TutupPopupCari();
                FocusScan();
                e.Handled = true;
            }
        }

        private void LstCariProduk_Click(object sender, RoutedEventArgs e)
        {
            if (LstCariProduk.SelectedItem is Product p)
            {
                TutupPopupCari();
                TxtScan.Text = "";
                TambahKeKeranjang(p, 1);
                FocusScan();
            }
        }

        private void TutupPopupCari()
        {
            if (PupProduk != null) { PupProduk.IsOpen = false; LstCariProduk.ItemsSource = null; }
        }

        private int ParseQtyPrefix(Product p, out string kataBersih)
        {
            kataBersih = p.Kode;
            return 1;
        }

        private void TxtScan_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_ready) return;
            string key = (TxtScan?.Text ?? "").Trim();
            if (string.IsNullOrEmpty(key))
            {
                SetScanStatus("Siap scan...");
                TutupPopupCari();
                return;
            }

            var cocok = _products.Where(p =>
                p.Kode.Contains(key, StringComparison.OrdinalIgnoreCase) ||
                p.Barcode.Contains(key, StringComparison.OrdinalIgnoreCase) ||
                p.Nama.Contains(key, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => !p.Kode.Equals(key, StringComparison.OrdinalIgnoreCase) && !p.Barcode.Equals(key, StringComparison.OrdinalIgnoreCase))
                .ThenBy(p => p.Nama)
                .Take(12)
                .ToList();

            // Popup hanya muncul bila bukan barcode scan exact tunggal (barcode scanner sudah exact match)
            if (cocok.Count == 1 && (cocok[0].Kode.Equals(key, StringComparison.OrdinalIgnoreCase) ||
                                     cocok[0].Barcode.Equals(key, StringComparison.OrdinalIgnoreCase)))
            {
                SetScanStatus($"Exact match: {cocok[0].Nama}. Tekan Enter untuk masukkan.");
                TutupPopupCari();
                return;
            }

            if (cocok.Count > 0)
            {
                LstCariProduk.ItemsSource = cocok;
                LstCariProduk.SelectedIndex = 0; // Otomatis sorot item pertama
                PupProduk.IsOpen = true;
                SetScanStatus($"{cocok.Count} produk cocok — Panah Atas/Bawah + Enter.");
            }
            else
            {
                TutupPopupCari();
                SetScanStatus($"'{key}' tidak ditemukan.", true);
            }
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

            decimal hargaItem = _pendingCustomHarga ?? p.Harga;
            bool isCustom = _pendingCustomHarga.HasValue;

            // Cari apakah item dengan harga yang sama sudah ada di keranjang
            var existing = _cart.FirstOrDefault(c => c.Product.Id == p.Id && c.Harga == hargaItem);
            int diKeranjangTotal = _cart.Where(c => c.Product.Id == p.Id).Sum(c => c.Qty);
            if (diKeranjangTotal + qty > p.Stok)
            {
                SetScanStatus($"Stok tidak cukup! Sisa {p.Nama}: {p.Stok}", true);
                MessageBox.Show($"Stok tidak cukup! Sisa stok {p.Nama}: {p.Stok}", "Stok Kurang", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (existing == null)
                _cart.Add(new CartItem { Product = p, Qty = qty, CustomHarga = _pendingCustomHarga });
            else
                existing.Qty += qty;

            if (isCustom)
            {
                SetScanStatus($"OK: {p.Nama} x{qty} masuk dengan harga khusus Rp {hargaItem:N0}.");
                ResetCustomHarga(); // Opsi 1: Otomatis kembali normal untuk item berikutnya
            }
            else
            {
                SetScanStatus($"OK: {p.Nama} x{qty} masuk keranjang.");
            }

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
            ResetCustomHarga();
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

            // Hitung akumulasi poin pelanggan jika member dipilih (setiap Rp 10.000 belanja = 1 poin)
            int poinDiperoleh = 0;
            if (_selectedMember != null)
            {
                poinDiperoleh = (int)Math.Floor(total / 10000m);
                _selectedMember.Poin += poinDiperoleh;
            }

            var trx = new Transaction
            {
                Id = trxId,
                Tanggal = DateTime.Now,
                Total = total,
                Bayar = bayar,
                MetodeBayar = metode,
                KasirNama = _loggedInUser?.NamaLengkap ?? _loggedInUser?.Username ?? "Kasir",
                MemberId = _selectedMember?.Id,
                MemberNama = _selectedMember?.Nama ?? "",
                PoinDidapat = poinDiperoleh,
                Items = _cart.Select(c => new TransactionItem
                {
                    Kode = c.Kode,
                    Nama = c.NamaDisplay,
                    Satuan = string.IsNullOrWhiteSpace(c.Product.Satuan) ? "pcs" : c.Product.Satuan,
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

            _cart.Clear();
            TxtBayar.Text = "";
            ResetMemberPos();
            RefreshSemua();
            UpdateMetodeUI();

            // Simpan data ke disk setelah transaksi
            SimpanKeDisk();

            // Tampilkan pratinjau struk (seperti hasil cetak thermal)
            string info = $"Struk PDF: {pdfPath}{infoThermal}";

            var preview = new StrukPreviewWindow(trx, _settings, info) { Owner = this };
            preview.ShowDialog();

            FocusScan();
        }

        // =========== MEMBER SELECTION AUTOCOMPLETE ===========
        private void TxtCariMemberPos_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_ready) return;
            string key = (TxtCariMemberPos?.Text ?? "").Trim();
            if (string.IsNullOrEmpty(key))
            {
                TutupPopupMemberPos();
                return;
            }

            var cocok = _members.Where(m =>
                m.Nama.Contains(key, StringComparison.OrdinalIgnoreCase) ||
                m.NoHp.Contains(key, StringComparison.OrdinalIgnoreCase))
                .Take(6).ToList();

            if (cocok.Count > 0)
            {
                LstCariMemberPos.ItemsSource = cocok;
                LstCariMemberPos.SelectedIndex = 0;
                PupMemberPos.IsOpen = true;
            }
            else
            {
                TutupPopupMemberPos();
            }
        }

        private void TxtCariMemberPos_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (PupMemberPos != null && PupMemberPos.IsOpen)
            {
                if (e.Key == Key.Down)
                {
                    if (LstCariMemberPos.Items.Count > 0)
                    {
                        int next = (LstCariMemberPos.SelectedIndex + 1) % LstCariMemberPos.Items.Count;
                        LstCariMemberPos.SelectedIndex = next;
                        LstCariMemberPos.ScrollIntoView(LstCariMemberPos.SelectedItem);
                    }
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.Up)
                {
                    if (LstCariMemberPos.Items.Count > 0)
                    {
                        int prev = LstCariMemberPos.SelectedIndex - 1;
                        if (prev < 0) prev = LstCariMemberPos.Items.Count - 1;
                        LstCariMemberPos.SelectedIndex = prev;
                        LstCariMemberPos.ScrollIntoView(LstCariMemberPos.SelectedItem);
                    }
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.Escape)
                {
                    TutupPopupMemberPos();
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.Enter)
                {
                    if (LstCariMemberPos.SelectedItem is Member pilih)
                    {
                        PilihMemberPos(pilih);
                        e.Handled = true;
                        return;
                    }
                }
            }
        }

        private void LstCariMemberPos_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && LstCariMemberPos.SelectedItem is Member m)
            {
                PilihMemberPos(m);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                TutupPopupMemberPos();
                e.Handled = true;
            }
        }

        private void LstCariMemberPos_Click(object sender, RoutedEventArgs e)
        {
            if (LstCariMemberPos.SelectedItem is Member m)
            {
                PilihMemberPos(m);
            }
        }

        private void PilihMemberPos(Member m)
        {
            _selectedMember = m;
            TutupPopupMemberPos();
            TxtCariMemberPos.Text = $"{m.Nama} ({m.NoHp})";
            TxtCariMemberPos.IsReadOnly = true;
            BtnBatalMemberPos.Visibility = Visibility.Visible;
            TxtMemberPoinInfo.Text = $"⭐ Saldo: {m.Poin:N0} Poin (Belanja Rp 10.000 = +1 poin)";
            TxtMemberPoinInfo.Visibility = Visibility.Visible;
            FocusScan();
        }

        private void BtnBatalMemberPos_Click(object sender, RoutedEventArgs e)
        {
            ResetMemberPos();
            FocusScan();
        }

        private void ResetMemberPos()
        {
            _selectedMember = null;
            TxtCariMemberPos.IsReadOnly = false;
            TxtCariMemberPos.Text = "";
            BtnBatalMemberPos.Visibility = Visibility.Collapsed;
            TxtMemberPoinInfo.Text = "";
            TxtMemberPoinInfo.Visibility = Visibility.Collapsed;
            TutupPopupMemberPos();
        }

        private void TutupPopupMemberPos()
        {
            if (PupMemberPos != null) { PupMemberPos.IsOpen = false; LstCariMemberPos.ItemsSource = null; }
        }

        // =========== PRATINJAU / CETAK STRUK (F7) ===========
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

            // Tampilkan pratinjau dulu; cetak/PDF/PNG bisa dipilih dari jendela pratinjau.
            var preview = new StrukPreviewWindow(t, _settings) { Owner = this };
            preview.ShowDialog();
            FocusScan();
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

            // Filter metode bayar
            int filterIdx = CmbFilterMetodeRiwayat?.SelectedIndex ?? 0;
            if (filterIdx == 1) // Hanya Tunai
                hariIni = hariIni.Where(t => (t.MetodeBayar ?? "").Equals("Tunai", StringComparison.OrdinalIgnoreCase)).ToList();
            else if (filterIdx == 2) // Hanya Non-Tunai
                hariIni = hariIni.Where(t => !(t.MetodeBayar ?? "").Equals("Tunai", StringComparison.OrdinalIgnoreCase)).ToList();

            var data = string.IsNullOrEmpty(key)
                ? hariIni
                : hariIni.Where(t => t.Id.ToLower().Contains(key) || t.Ringkasan.ToLower().Contains(key) || t.MetodeBayar.ToLower().Contains(key)).ToList();
            DgRiwayat.ItemsSource = data;
        }

        private void CmbFilterMetodeRiwayat_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_ready) RefreshRiwayat();
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

        // =========== REKAP SHIFT & TUTUP KASIR ===========
        private DateTime _rekapDari = DateTime.Today;
        private DateTime _rekapSampai = DateTime.Today;

        private void BtnRekapHariIni_Click(object sender, RoutedEventArgs e)
        {
            _rekapDari = DateTime.Today;
            _rekapSampai = DateTime.Today;
            if (DpRekapDari != null) DpRekapDari.SelectedDate = _rekapDari;
            if (DpRekapSampai != null) DpRekapSampai.SelectedDate = _rekapSampai;
            RefreshRekap();
        }

        private void BtnTerapkanFilterRekap_Click(object sender, RoutedEventArgs e)
        {
            _rekapDari = DpRekapDari?.SelectedDate ?? DateTime.Today;
            _rekapSampai = DpRekapSampai?.SelectedDate ?? DateTime.Today;
            RefreshRekap();
        }

        private void RefreshRekap()
        {
            if (!_ready) return;
            DateTime dari = (_rekapDari.Date);
            DateTime sampai = (_rekapSampai.Date.AddDays(1).AddTicks(-1));

            var data = _transactions.Where(t => t.Tanggal >= dari && t.Tanggal <= sampai).ToList();
            var expenses = _expenses.Where(x => x.Tanggal >= dari && x.Tanggal <= sampai).ToList();

            decimal omzet = data.Sum(t => t.Total);
            int jmlTrx = data.Count;
            int jmlItem = data.Sum(t => t.JumlahItem);
            decimal tunai = data.Where(t => (t.MetodeBayar ?? "").Equals("Tunai", StringComparison.OrdinalIgnoreCase)).Sum(t => t.Total);
            decimal nonTunai = omzet - tunai;
            decimal totalKeluar = expenses.Sum(x => x.Nominal);

            CardOmzetShift.Text = $"Rp {omzet:N0}";
            CardTrxShift.Text = jmlTrx.ToString();
            CardItemShift.Text = jmlItem.ToString();
            CardTunai.Text = $"Rp {tunai:N0}";
            CardNonTunai.Text = $"Rp {nonTunai:N0}";
            if (CardPengeluaranShift != null) CardPengeluaranShift.Text = $"Rp {totalKeluar:N0}";

            if (DgPengeluaranKas != null) DgPengeluaranKas.ItemsSource = expenses;

            UpdateRekonsiliasiShiftUI(tunai, totalKeluar);

            var perMetode = data.GroupBy(t => string.IsNullOrWhiteSpace(t.MetodeBayar) ? "Tunai" : t.MetodeBayar)
                .Select(g => $"{g.Key}: {g.Count()} trx (Rp {g.Sum(x => x.Total):N0})");
            string periodeStr = dari.Date == sampai.Date ? dari.ToString("dd MMM yyyy") : $"{dari:dd/MM/yyyy} - {sampai:dd/MM/yyyy}";
            TxtRingkasanShift.Text = jmlTrx == 0 && expenses.Count == 0
                ? $"Periode {periodeStr}: Belum ada aktivitas transaksi maupun pengeluaran kas."
                : $"Periode {periodeStr}:\n" +
                  $"• Omzet Penjualan : Rp {omzet:N0} ({jmlTrx} transaksi, {jmlItem} item)\n" +
                  $"  - 💵 Tunai (Kas Masuk) : Rp {tunai:N0}\n" +
                  $"  - 📱 Non-Tunai (Digital) : Rp {nonTunai:N0}\n" +
                  $"• 💸 Uang Keluar Drawer  : Rp {totalKeluar:N0} ({expenses.Count} kali pengeluaran)\n" +
                  $"• Rincian Metode Bayar   : {string.Join(" | ", perMetode)}";
        }

        private void TxtShiftDrawer_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_ready) return;
            DateTime dari = (_rekapDari.Date);
            DateTime sampai = (_rekapSampai.Date.AddDays(1).AddTicks(-1));
            var data = _transactions.Where(t => t.Tanggal >= dari && t.Tanggal <= sampai).ToList();
            var expenses = _expenses.Where(x => x.Tanggal >= dari && x.Tanggal <= sampai).ToList();
            decimal tunai = data.Where(t => (t.MetodeBayar ?? "").Equals("Tunai", StringComparison.OrdinalIgnoreCase)).Sum(t => t.Total);
            decimal totalKeluar = expenses.Sum(x => x.Nominal);
            UpdateRekonsiliasiShiftUI(tunai, totalKeluar);
        }

        private void UpdateRekonsiliasiShiftUI(decimal penjualanTunai, decimal pengeluaranKas)
        {
            if (TxtLabelDrawerFisik == null) return;
            string rawModal = (TxtShiftModalAwal?.Text ?? "0").Replace(".", "").Replace(",", "").Trim();
            string rawDrawer = (TxtShiftDrawerFisik?.Text ?? "0").Replace(".", "").Replace(",", "").Trim();
            decimal.TryParse(rawModal, out decimal modalAwal);
            decimal.TryParse(rawDrawer, out decimal drawerFisik);

            decimal komputerTunai = modalAwal + penjualanTunai - pengeluaranKas;
            decimal selisih = drawerFisik - komputerTunai;

            TxtLabelDrawerFisik.Text = $"Rp {drawerFisik:N0}";
            TxtLabelKomputerTunai.Text = $"Rp {komputerTunai:N0}";
            TxtLabelSelisihKas.Text = (selisih >= 0 ? "+Rp " : "-Rp ") + $"{Math.Abs(selisih):N0}";
            TxtLabelSelisihKas.Foreground = selisih < 0
                ? new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26))
                : (selisih > 0 ? new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB)) : new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A)));
        }

        private void BtnCetakStrukLaporanShift_Click(object sender, RoutedEventArgs e)
        {
            _settings = DataStorageService.LoadSettings();
            if (string.IsNullOrWhiteSpace(_settings.PrinterName))
            {
                MessageBox.Show("Printer thermal belum diatur. Tekan F8 untuk memilih printer.", "Printer", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DateTime dari = (_rekapDari.Date);
            DateTime sampai = (_rekapSampai.Date.AddDays(1).AddTicks(-1));
            var data = _transactions.Where(t => t.Tanggal >= dari && t.Tanggal <= sampai).ToList();
            var expenses = _expenses.Where(x => x.Tanggal >= dari && x.Tanggal <= sampai).ToList();

            decimal totalPenjualan = data.Sum(t => t.Total);
            decimal tunai = data.Where(t => (t.MetodeBayar ?? "").Equals("Tunai", StringComparison.OrdinalIgnoreCase)).Sum(t => t.Total);
            decimal nonTunai = totalPenjualan - tunai;
            decimal pengeluaran = expenses.Sum(x => x.Nominal);

            string rawModal = (TxtShiftModalAwal?.Text ?? "0").Replace(".", "").Replace(",", "").Trim();
            string rawDrawer = (TxtShiftDrawerFisik?.Text ?? "0").Replace(".", "").Replace(",", "").Trim();
            decimal.TryParse(rawModal, out decimal modalAwal);
            decimal.TryParse(rawDrawer, out decimal drawerFisik);

            var report = new CashierShiftReport
            {
                Lokasi = "TOKO 1",
                Tanggal = DateTime.Now,
                Kassa = "A",
                KasirNama = _loggedInUser?.NamaLengkap ?? _loggedInUser?.Username ?? "Kasir",
                Shift = 1,
                ModalAwal = modalAwal,
                PenjualanKotor = totalPenjualan,
                TotalTunai = tunai,
                TotalNonTunai = nonTunai,
                TotalPengeluaran = pengeluaran,
                DrawerFisik = drawerFisik
            };

            var hasil = ThermalPrintService.CetakLaporanShift(report, _settings);
            if (hasil.Success)
                MessageBox.Show("Laporan Kasir / Tutup Shift berhasil dicetak ke printer!", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                MessageBox.Show(hasil.Message, "Gagal Cetak", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void BtnCetakRekap_Click(object sender, RoutedEventArgs e)
        {
            DateTime dari = (_rekapDari.Date);
            DateTime sampai = (_rekapSampai.Date.AddDays(1).AddTicks(-1));
            var data = _transactions.Where(t => t.Tanggal >= dari && t.Tanggal <= sampai).ToList();
            try
            {
                string path = ExportService.SimpanLaporanPdf(data, _rekapDari, _rekapSampai, settings: _settings);
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

        // =========== FITUR HOLD TRANSACTION (TAHAN & MUAT ANTREAN) ===========
        private void UpdateHoldCartButtonBadge()
        {
            if (BtnRecallCart != null)
            {
                BtnRecallCart.Content = $"📋 Buka Antrean Tertahan ({_holdTransactions.Count})";
                BtnRecallCart.Background = _holdTransactions.Count > 0
                    ? new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26)) // Merah mencolok jika ada antrean tertahan
                    : new SolidColorBrush(Color.FromRgb(0x43, 0x38, 0xCA));
            }
        }

        private void BtnHoldCart_Click(object sender, RoutedEventArgs e)
        {
            if (_cart.Count == 0)
            {
                MessageBox.Show("Keranjang masih kosong, tidak ada transaksi yang perlu ditahan.", "Hold Antrean", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string catatan = $"Antrean #{_holdTransactions.Count + 1} ({_cart.Sum(x => x.Qty)} item - {_cart[0].NamaDisplay})";
            var hold = new HoldTransaction
            {
                Id = $"HOLD-{DateTime.Now:yyyyMMddHHmmss}",
                Tanggal = DateTime.Now,
                Catatan = catatan,
                KasirNama = _loggedInUser?.NamaLengkap ?? _loggedInUser?.Username ?? "Kasir",
                Items = _cart.Select(c => new HoldCartItem
                {
                    Kode = c.Kode,
                    Nama = c.NamaDisplay,
                    Satuan = c.Product.Satuan,
                    Harga = c.Harga,
                    HargaBeli = c.Product.HargaBeli,
                    Qty = c.Qty,
                    IsCustomPrice = c.IsCustomPrice
                }).ToList()
            };

            _holdTransactions.Add(hold);
            SimpanKeDisk();
            UpdateHoldCartButtonBadge();

            // Kosongkan keranjang agar bisa melayani customer berikutnya
            _cart.Clear();
            RefreshSemua();
            FocusScan();

            MessageBox.Show($"Transaksi berhasil ditahan sementara!\n{catatan}\n\nKeranjang sekarang bersih dan siap melayani customer antrean berikutnya.",
                "Transaksi Ditahan (Hold)", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnRecallCart_Click(object sender, RoutedEventArgs e)
        {
            if (_holdTransactions.Count == 0)
            {
                MessageBox.Show("Saat ini tidak ada antrean transaksi yang sedang ditahan.", "Antrean Tertahan", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            DgHoldCart.ItemsSource = null;
            DgHoldCart.ItemsSource = _holdTransactions;
            ModalHoldCart.Visibility = Visibility.Visible;
        }

        private void TutupModalHoldCart()
        {
            ModalHoldCart.Visibility = Visibility.Collapsed;
            FocusScan();
        }

        private void BtnTutupModalHoldCart_Click(object sender, RoutedEventArgs e)
        {
            TutupModalHoldCart();
        }

        private void BtnRecallItemGrid_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not HoldTransaction hold) return;

            if (_cart.Count > 0)
            {
                var konfirm = MessageBox.Show(
                    "Keranjang saat ini sedang berisi barang. Apakah Anda ingin menahan (Hold) keranjang yang sedang aktif ini terlebih dahulu sebelum memuat antrean yang dipilih?",
                    "Konfirmasi", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                if (konfirm == MessageBoxResult.Cancel) return;
                if (konfirm == MessageBoxResult.Yes)
                {
                    // Hold keranjang aktif dulu
                    BtnHoldCart_Click(this, new RoutedEventArgs());
                }
                else
                {
                    // Timpa keranjang aktif
                    _cart.Clear();
                }
            }

            // Muat item hold ke keranjang aktif
            foreach (var item in hold.Items)
            {
                var prod = _products.FirstOrDefault(p => p.Kode.Equals(item.Kode, StringComparison.OrdinalIgnoreCase))
                           ?? new Product { Kode = item.Kode, Nama = item.Nama, Harga = item.Harga, HargaBeli = item.HargaBeli, Satuan = item.Satuan };

                var cItem = new CartItem(prod, item.Qty, item.IsCustomPrice ? item.Harga : null);
                _cart.Add(cItem);
            }

            _holdTransactions.Remove(hold);
            SimpanKeDisk();
            UpdateHoldCartButtonBadge();
            ModalHoldCart.Visibility = Visibility.Collapsed;

            RefreshSemua();
            FocusScan();
            MessageBox.Show($"Antrean belanja {hold.Catatan} berhasil dimuat kembali ke keranjang kasir!", "Sukses Recall", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnHapusHoldGrid_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not HoldTransaction hold) return;
            if (MessageBox.Show($"Hapus antrean tertahan \"{hold.Catatan}\"?", "Konfirmasi Hapus", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _holdTransactions.Remove(hold);
                SimpanKeDisk();
                UpdateHoldCartButtonBadge();
                DgHoldCart.ItemsSource = null;
                DgHoldCart.ItemsSource = _holdTransactions;
            }
        }

        // =========== FITUR PENGELUARAN KAS (CASH OUT / SETORAN) ===========
        private void BtnKasKeluar_Click(object sender, RoutedEventArgs e)
        {
            TxtPengeluaranNominal.Text = "";
            TxtPengeluaranKeterangan.Text = "";
            CmbPengeluaranKategori.SelectedIndex = 0;
            ModalPengeluaranKas.Visibility = Visibility.Visible;
            TxtPengeluaranNominal.Focus();
        }

        private void TutupModalPengeluaranKas()
        {
            ModalPengeluaranKas.Visibility = Visibility.Collapsed;
            FocusScan();
        }

        private void BtnTutupModalPengeluaranKas_Click(object sender, RoutedEventArgs e)
        {
            TutupModalPengeluaranKas();
        }

        private void BtnSimpanPengeluaranKas_Click(object sender, RoutedEventArgs e)
        {
            string raw = (TxtPengeluaranNominal.Text ?? "").Replace(".", "").Replace(",", "").Trim();
            if (!decimal.TryParse(raw, out decimal nominal) || nominal <= 0)
            {
                MessageBox.Show("Masukkan nominal pengeluaran kas yang valid!", "Validasi", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtPengeluaranNominal.Focus();
                return;
            }

            string kategori = (CmbPengeluaranKategori.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Operasional";
            string ket = TxtPengeluaranKeterangan.Text.Trim();
            if (string.IsNullOrWhiteSpace(ket)) ket = kategori;

            var exp = new CashExpense
            {
                Id = $"OUT-{DateTime.Now:yyyyMMdd}-{_nextExpenseId++:D4}",
                Tanggal = DateTime.Now,
                Nominal = nominal,
                Kategori = kategori,
                Keterangan = ket,
                KasirNama = _loggedInUser?.NamaLengkap ?? _loggedInUser?.Username ?? "Kasir"
            };

            _expenses.Insert(0, exp);
            SimpanKeDisk();

            ModalPengeluaranKas.Visibility = Visibility.Collapsed;

            // Cetak struk bukti pengeluaran kas
            _settings = DataStorageService.LoadSettings();
            var printHasil = ThermalPrintService.CetakPengeluaranKas(exp, _settings);

            string msg = $"Pengeluaran kas sebesar Rp {nominal:N0} ({kategori}) berhasil dicatat!";
            if (printHasil.Success)
                msg += "\n\nStruk bukti pengeluaran kas telah otomatis dicetak.";
            else if (!string.IsNullOrWhiteSpace(_settings.PrinterName))
                msg += $"\n\nCatatan cetak: {printHasil.Message}";

            MessageBox.Show(msg, "Pengeluaran Kas Berhasil", MessageBoxButton.OK, MessageBoxImage.Information);
            RefreshRekap();
            FocusScan();
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
