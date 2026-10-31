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

namespace KasirKu.Manager
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<Product> _products = new();
        private readonly ObservableCollection<Transaction> _transactions = new();
        private readonly ObservableCollection<StockMovement> _movements = new();

        private StoreSettings _settings = new();
        private Product? _editingProduct;
        private User? _loggedInUser;
        private int _nextProductId = 1;
        private int _nextTrx = 1;
        private int _nextMovementId = 1;
        private bool _ready;
        private string _halaman = "Dashboard";

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
            MuatDariDisk();

            DpDari.SelectedDate = DateTime.Today;
            DpSampai.SelectedDate = DateTime.Today;

            _ready = true;

            TxtTanggal.Text = DateTime.Now.ToString("dddd, dd MMM yyyy");
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (s, _) => TxtJam.Text = DateTime.Now.ToString("HH:mm:ss");
            timer.Start();
            TxtJam.Text = DateTime.Now.ToString("HH:mm:ss");

            // Tampilkan info user yang login
            if (_loggedInUser != null)
                TxtUserLogin.Text = $"Login: {_loggedInUser.NamaLengkap}";

            ShowPage("Dashboard");
            IsiFormPengaturan();
            MuatDataUser();
        }

        private void MuatDariDisk()
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

            TxtLastRefresh.Text = $"Terakhir dimuat: {DateTime.Now:HH:mm:ss}";
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

        private void BtnRefreshData_Click(object sender, RoutedEventArgs e)
        {
            MuatDariDisk();
            RefreshHalamanAktif();
            MuatDataUser();
            MessageBox.Show("Data berhasil dimuat ulang dari disk.", "Refresh", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            var jawab = MessageBox.Show("Apakah Anda yakin ingin keluar dari aplikasi KasirKu Manager?", "Konfirmasi Keluar",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (jawab != MessageBoxResult.Yes) return;

            Application.Current.Shutdown();
        }

        // =========== NAVIGASI ===========
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.F1: ShowPage("Dashboard"); e.Handled = true; break;
                case Key.F2: ShowPage("Produk"); e.Handled = true; break;
                case Key.F3: ShowPage("Stok"); e.Handled = true; break;
                case Key.F4: ShowPage("Riwayat"); e.Handled = true; break;
                case Key.F5: ShowPage("Laporan"); e.Handled = true; break;
                case Key.F6: ShowPage("Pengaturan"); e.Handled = true; break;
                case Key.F7: BtnCetakStruk_Click(this, new RoutedEventArgs()); e.Handled = true; break;
                case Key.F8: BtnExportPdf_Click(this, new RoutedEventArgs()); e.Handled = true; break;
                case Key.F9: BtnExportExcel_Click(this, new RoutedEventArgs()); e.Handled = true; break;
                case Key.F10: BtnRefreshData_Click(this, new RoutedEventArgs()); e.Handled = true; break;
                case Key.Escape: Close(); e.Handled = true; break;
            }
        }

        private void Nav_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string page) ShowPage(page);
        }

        private void ShowPage(string page)
        {
            _halaman = page;
            PageDashboard.Visibility = Visibility.Collapsed;
            PageProduk.Visibility = Visibility.Collapsed;
            PageStok.Visibility = Visibility.Collapsed;
            PageRiwayat.Visibility = Visibility.Collapsed;
            PageLaporan.Visibility = Visibility.Collapsed;
            PagePengaturan.Visibility = Visibility.Collapsed;

            string title = page, sub = "";
            switch (page)
            {
                case "Dashboard":
                    PageDashboard.Visibility = Visibility.Visible;
                    title = "Dashboard";
                    sub = "Ringkasan bisnis hari ini. Tekan F10 untuk refresh data dari kasir.";
                    RefreshDashboard();
                    break;
                case "Produk":
                    PageProduk.Visibility = Visibility.Visible;
                    title = "Data Produk (F2)";
                    sub = "Kelola barang: kode, barcode, satuan, harga beli/jual, margin, supplier & kelompok.";
                    RefreshProduk();
                    break;
                case "Stok":
                    PageStok.Visibility = Visibility.Visible;
                    title = "Stok (F3)";
                    sub = "Jejak barang masuk & keluar. Penjualan kasir tercatat otomatis sebagai Keluar.";
                    RefreshStok();
                    break;
                case "Riwayat":
                    PageRiwayat.Visibility = Visibility.Visible;
                    title = "Riwayat Transaksi (F4)";
                    sub = "Seluruh transaksi. Klik baris untuk detail. F7 mencetak struk PDF.";
                    RefreshRiwayat();
                    break;
                case "Laporan":
                    PageLaporan.Visibility = Visibility.Visible;
                    title = "Laporan (F5)";
                    sub = "Filter periode, F8 export PDF, F9 export Excel (CSV).";
                    RefreshLaporan();
                    break;
                case "Pengaturan":
                    PagePengaturan.Visibility = Visibility.Visible;
                    title = "Pengaturan (F6)";
                    sub = "Profil toko yang tampil di struk & laporan. Dipakai bersama oleh POS dan Manager.";
                    IsiFormPengaturan();
                    break;
            }
            TxtHeaderTitle.Text = title;
            TxtHeaderSub.Text = sub;
            HighlightSidebar();
        }

        private void RefreshHalamanAktif()
        {
            switch (_halaman)
            {
                case "Dashboard": RefreshDashboard(); break;
                case "Produk": RefreshProduk(); break;
                case "Stok": RefreshStok(); break;
                case "Riwayat": RefreshRiwayat(); break;
                case "Laporan": RefreshLaporan(); break;
                case "Pengaturan": IsiFormPengaturan(); break;
            }
        }

        private void HighlightSidebar()
        {
            var def = Brushes.Transparent;
            var act = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
            var defFg = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
            var actFg = Brushes.White;
            BtnNavDashboard.Background = _halaman == "Dashboard" ? act : def; BtnNavDashboard.Foreground = _halaman == "Dashboard" ? actFg : defFg;
            BtnNavProduk.Background = _halaman == "Produk" ? act : def; BtnNavProduk.Foreground = _halaman == "Produk" ? actFg : defFg;
            BtnNavStok.Background = _halaman == "Stok" ? act : def; BtnNavStok.Foreground = _halaman == "Stok" ? actFg : defFg;
            BtnNavRiwayat.Background = _halaman == "Riwayat" ? act : def; BtnNavRiwayat.Foreground = _halaman == "Riwayat" ? actFg : defFg;
            BtnNavLaporan.Background = _halaman == "Laporan" ? act : def; BtnNavLaporan.Foreground = _halaman == "Laporan" ? actFg : defFg;
            BtnNavPengaturan.Background = _halaman == "Pengaturan" ? act : def; BtnNavPengaturan.Foreground = _halaman == "Pengaturan" ? actFg : defFg;
        }

        // =========== HELPER ===========
        private Func<string, decimal> HargaBeliLookup()
        {
            var dict = _products.ToDictionary(p => p.Kode, p => p.HargaBeli, StringComparer.OrdinalIgnoreCase);
            return kode => dict.TryGetValue(kode, out var v) ? v : 0m;
        }

        // =========== DASHBOARD ===========
        private void RefreshDashboard()
        {
            var hariIni = _transactions.Where(t => t.Tanggal.Date == DateTime.Today).ToList();
            decimal omzet = hariIni.Sum(t => t.Total);
            int jmlTrx = hariIni.Count;
            int jmlItem = hariIni.Sum(t => t.JumlahItem);
            var lookup = HargaBeliLookup();
            decimal laba = hariIni.Sum(t => ExportService.LabaTransaksi(t, lookup));

            CardDashOmzet.Text = $"Rp {omzet:N0}";
            CardDashLaba.Text = $"Rp {laba:N0}";
            CardDashTrx.Text = jmlTrx.ToString();
            CardDashItem.Text = jmlItem.ToString();

            // Stok menipis
            DgStokTipis.ItemsSource = _products.Where(p => p.Stok <= 10).OrderBy(p => p.Stok).Take(10).ToList();

            var perMetode = hariIni.GroupBy(t => t.MetodeBayar)
                .Select(g => $"{g.Key}: {g.Count()} trx (Rp {g.Sum(x => x.Total):N0})");
            int stokTipis = _products.Count(p => p.Stok <= 10);
            TxtDashRingkasan.Text = jmlTrx == 0
                ? $"Belum ada transaksi hari ini.\nTotal produk: {_products.Count} | Stok menipis: {stokTipis} jenis."
                : $"Hari ini: {jmlTrx} transaksi, {jmlItem} item.\n" +
                  $"Omzet: Rp {omzet:N0} | Laba: Rp {laba:N0}\n" +
                  $"Rincian: {string.Join(" | ", perMetode)}\n" +
                  $"Total produk: {_products.Count} | Stok menipis: {stokTipis} jenis.";
        }

        // =========== PRODUK (CRUD) ===========
        private void RefreshProduk()
        {
            string key = TxtCariProduk?.Text?.Trim().ToLower() ?? "";
            var data = string.IsNullOrEmpty(key)
                ? _products.ToList()
                : _products.Where(p =>
                    p.Kode.ToLower().Contains(key) ||
                    p.Barcode.ToLower().Contains(key) ||
                    p.Nama.ToLower().Contains(key) ||
                    p.Supplier.ToLower().Contains(key) ||
                    p.Kategori.ToLower().Contains(key)).ToList();
            DgProduk.ItemsSource = data;
        }

        private void TxtCariProduk_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_ready) return;
            RefreshProduk();
        }

        private void DgProduk_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DgProduk.SelectedItem is Product p)
            {
                _editingProduct = p;
                TxtFormTitle.Text = $"Edit Produk - {p.Kode}";
                TxtKode.Text = p.Kode;
                TxtBarcode.Text = p.Barcode;
                TxtNama.Text = p.Nama;
                SetComboText(CmbSatuan, p.Satuan);
                TxtHargaBeli.Text = p.HargaBeli.ToString();
                TxtHarga.Text = p.Harga.ToString();
                TxtStok.Text = p.Stok.ToString();
                SetComboText(CmbSupplier, p.Supplier);
                foreach (ComboBoxItem it in CmbKategori.Items)
                    if (it.Content?.ToString() == p.Kategori) { CmbKategori.SelectedItem = it; break; }
                UpdateMarginInfo();
            }
        }

        private static string GetComboText(ComboBox cmb, string fallback = "")
        {
            if (cmb == null) return fallback;
            string t = (cmb.Text ?? "").Trim();
            if (!string.IsNullOrEmpty(t)) return t;
            if (cmb.SelectedItem is ComboBoxItem it) return (it.Content?.ToString() ?? "").Trim();
            return fallback;
        }

        private static void SetComboText(ComboBox cmb, string value)
        {
            if (cmb == null) return;
            string v = (value ?? "").Trim();
            foreach (var obj in cmb.Items)
            {
                string c = obj is ComboBoxItem it ? (it.Content?.ToString() ?? "") : (obj?.ToString() ?? "");
                if (c.Equals(v, StringComparison.OrdinalIgnoreCase)) { cmb.SelectedItem = obj; break; }
            }
            cmb.Text = v;
        }

        private void TxtHargaMargin_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_ready) return;
            UpdateMarginInfo();
        }

        private void UpdateMarginInfo()
        {
            if (TxtMarginInfo == null) return;
            decimal.TryParse((TxtHargaBeli?.Text ?? "").Trim(), out var beli);
            decimal.TryParse((TxtHarga?.Text ?? "").Trim(), out var jual);
            decimal margin = jual - beli;
            decimal persen = beli > 0 ? margin / beli * 100 : 0;
            TxtMarginInfo.Text = (beli <= 0 || jual <= 0) ? "Margin: -" : $"Margin: Rp {margin:N0} ({persen:N1}%)";
        }

        private void BtnSimpanProduk_Click(object sender, RoutedEventArgs e)
        {
            string kode = TxtKode.Text.Trim();
            string barcode = TxtBarcode.Text.Trim();
            string nama = TxtNama.Text.Trim();
            string satuan = GetComboText(CmbSatuan, "Pcs");
            string kelompok = (CmbKategori.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Umum";
            string supplier = GetComboText(CmbSupplier, "Umum");

            if (string.IsNullOrEmpty(kode) || string.IsNullOrEmpty(nama))
            { MessageBox.Show("Kode Produk dan Nama wajib diisi!", "Validasi", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (string.IsNullOrEmpty(barcode)) barcode = kode;
            if (string.IsNullOrEmpty(satuan)) satuan = "Pcs";
            if (string.IsNullOrEmpty(supplier)) supplier = "Umum";
            if (!decimal.TryParse(TxtHargaBeli.Text.Trim(), out var hargaBeli) || hargaBeli < 0)
            { MessageBox.Show("Harga beli harus angka 0 atau lebih!", "Validasi", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (!decimal.TryParse(TxtHarga.Text.Trim(), out var hargaJual) || hargaJual <= 0)
            { MessageBox.Show("Harga jual harus angka lebih dari 0!", "Validasi", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (hargaJual < hargaBeli)
            {
                var lanjut = MessageBox.Show($"Harga jual (Rp {hargaJual:N0}) lebih kecil dari harga beli (Rp {hargaBeli:N0}).\nTetap simpan?",
                    "Margin Minus", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (lanjut != MessageBoxResult.Yes) return;
            }
            if (!int.TryParse(TxtStok.Text.Trim(), out var stok) || stok < 0)
            { MessageBox.Show("Qty/Stok harus angka 0 atau lebih!", "Validasi", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            if (_editingProduct == null)
            {
                if (_products.Any(p => p.Kode.Equals(kode, StringComparison.OrdinalIgnoreCase)))
                { MessageBox.Show("Kode produk sudah dipakai!", "Validasi", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
                if (_products.Any(p => !string.IsNullOrEmpty(p.Barcode) && p.Barcode.Equals(barcode, StringComparison.OrdinalIgnoreCase)))
                { MessageBox.Show("Kode barcode sudah dipakai!", "Validasi", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
                var baru = new Product
                {
                    Id = _nextProductId++, Kode = kode, Barcode = barcode, Nama = nama,
                    Satuan = satuan, Kategori = kelompok, Supplier = supplier,
                    HargaBeli = hargaBeli, Harga = hargaJual, Stok = 0
                };
                _products.Add(baru);
                if (stok > 0) CatatMasuk(baru, stok, "Stok awal produk baru");
                MessageBox.Show("Produk berhasil ditambahkan!", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                if (_products.Any(p => p.Id != _editingProduct.Id && p.Kode.Equals(kode, StringComparison.OrdinalIgnoreCase)))
                { MessageBox.Show("Kode produk sudah dipakai!", "Validasi", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
                if (_products.Any(p => p.Id != _editingProduct.Id && !string.IsNullOrEmpty(p.Barcode) && p.Barcode.Equals(barcode, StringComparison.OrdinalIgnoreCase)))
                { MessageBox.Show("Kode barcode sudah dipakai!", "Validasi", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
                int stokLama = _editingProduct.Stok;
                _editingProduct.Kode = kode; _editingProduct.Barcode = barcode;
                _editingProduct.Nama = nama; _editingProduct.Satuan = satuan;
                _editingProduct.Kategori = kelompok; _editingProduct.Supplier = supplier;
                _editingProduct.HargaBeli = hargaBeli; _editingProduct.Harga = hargaJual;
                _editingProduct.Stok = stok;
                int selisih = stok - stokLama;
                if (selisih > 0) TambahLog(_editingProduct, "Masuk", selisih, stokLama, stok, $"Koreksi via Data Produk ({stokLama} -> {stok})");
                else if (selisih < 0) TambahLog(_editingProduct, "Keluar", -selisih, stokLama, stok, $"Koreksi via Data Produk ({stokLama} -> {stok})");
                MessageBox.Show("Produk berhasil diupdate!", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            ClearFormProduk();
            RefreshProduk();
            SimpanKeDisk();
        }

        private void BtnBatalProduk_Click(object sender, RoutedEventArgs e) => ClearFormProduk();

        private void ClearFormProduk()
        {
            _editingProduct = null;
            DgProduk.SelectedItem = null;
            TxtFormTitle.Text = "Tambah Produk";
            TxtKode.Text = ""; TxtBarcode.Text = ""; TxtNama.Text = "";
            TxtHargaBeli.Text = ""; TxtHarga.Text = ""; TxtStok.Text = "";
            SetComboText(CmbSatuan, "Pcs");
            SetComboText(CmbSupplier, "Umum");
            CmbKategori.SelectedIndex = 0;
            UpdateMarginInfo();
        }

        private void BtnHapusProduk_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not Product p) return;
            if (MessageBox.Show($"Hapus '{p.Nama}'?", "Konfirmasi", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                if (p.Stok > 0) TambahLog(p, "Keluar", p.Stok, p.Stok, 0, "Produk dihapus");
                _products.Remove(p);
                if (_editingProduct?.Id == p.Id) ClearFormProduk();
                RefreshProduk();
                SimpanKeDisk();
            }
        }

        // =========== STOK ===========
        private void TambahLog(Product p, string jenis, int qty, int sebelum, int sesudah, string keterangan)
        {
            _movements.Insert(0, new StockMovement
            {
                Id = _nextMovementId++, Tanggal = DateTime.Now, ProductId = p.Id,
                Kode = p.Kode, Nama = p.Nama, Jenis = jenis, Qty = qty,
                StokSebelum = sebelum, StokSesudah = sesudah, Keterangan = keterangan
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

        private void RefreshStok()
        {
            if (DgStok == null) return;
            string key = TxtCariStok?.Text?.Trim().ToLower() ?? "";
            string jenis = (CmbFilterJenis?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Semua";
            DgStok.ItemsSource = _movements
                .Where(m => (jenis == "Semua" || m.Jenis == jenis) &&
                    (string.IsNullOrEmpty(key) || m.Kode.ToLower().Contains(key) ||
                     m.Nama.ToLower().Contains(key) || m.Keterangan.ToLower().Contains(key)))
                .ToList();

            var hariIni = _movements.Where(m => m.Tanggal.Date == DateTime.Today);
            CardStokMasuk.Text = "+" + hariIni.Where(m => m.IsMasuk).Sum(m => m.Qty);
            CardStokKeluar.Text = "-" + hariIni.Where(m => !m.IsMasuk).Sum(m => m.Qty);

            if (CmbStokProduk != null)
            {
                var pilih = CmbStokProduk.SelectedItem as Product;
                CmbStokProduk.ItemsSource = null;
                CmbStokProduk.ItemsSource = _products.ToList();
                if (pilih != null)
                {
                    var sama = _products.FirstOrDefault(p => p.Id == pilih.Id);
                    if (sama != null) CmbStokProduk.SelectedItem = sama;
                }
            }
            UpdateStokSaatIni();
        }

        private void UpdateStokSaatIni()
        {
            if (TxtStokSaatIni == null) return;
            TxtStokSaatIni.Text = CmbStokProduk?.SelectedItem is Product p ? $"Stok saat ini: {p.Stok}" : "Stok saat ini: -";
        }

        private void TxtCariStok_TextChanged(object sender, TextChangedEventArgs e) { if (_ready) RefreshStok(); }
        private void CmbFilterJenis_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (_ready) RefreshStok(); }
        private void CmbStokProduk_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (_ready) UpdateStokSaatIni(); }

        private void BtnSimpanStok_Click(object sender, RoutedEventArgs e)
        {
            if (CmbStokProduk.SelectedItem is not Product p)
            { MessageBox.Show("Pilih produk dulu!", "Stok", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            string jenis = (CmbJenisStok.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Masuk";
            if (!int.TryParse(TxtStokQty.Text.Trim(), out int qty) || qty <= 0)
            { MessageBox.Show("Jumlah harus angka lebih dari 0!", "Stok", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            string ket = (CmbStokKet.Text ?? "").Trim();
            if (string.IsNullOrEmpty(ket)) ket = "Penyesuaian manual";

            if (jenis == "Masuk")
            {
                CatatMasuk(p, qty, ket);
                MessageBox.Show($"Barang masuk: '{p.Nama}' +{qty} (stok: {p.Stok}).", "Stok", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                if (qty > p.Stok)
                { MessageBox.Show($"Stok tidak cukup! Sisa {p.Nama}: {p.Stok}", "Stok", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
                CatatKeluar(p, qty, ket);
                MessageBox.Show($"Barang keluar: '{p.Nama}' -{qty} (stok: {p.Stok}).", "Stok", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            TxtStokQty.Text = "";
            RefreshStok();
            SimpanKeDisk();
        }

        private void BtnHapusStokLog_Click(object sender, RoutedEventArgs e)
        {
            if (_movements.Count == 0) return;
            if (MessageBox.Show("Hapus semua log stok? (Stok produk tidak berubah)", "Konfirmasi", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            { _movements.Clear(); RefreshStok(); SimpanKeDisk(); }
        }

        // =========== RIWAYAT ===========
        private void RefreshRiwayat()
        {
            string key = TxtCariRiwayat?.Text?.Trim().ToLower() ?? "";
            var data = string.IsNullOrEmpty(key)
                ? _transactions.ToList()
                : _transactions.Where(t => t.Id.ToLower().Contains(key) || t.Ringkasan.ToLower().Contains(key) || t.MetodeBayar.ToLower().Contains(key)).ToList();
            DgRiwayat.ItemsSource = data;
        }

        private void TxtCariRiwayat_TextChanged(object sender, TextChangedEventArgs e) { if (_ready) RefreshRiwayat(); }

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

        private void BtnHapusRiwayat_Click(object sender, RoutedEventArgs e)
        {
            if (_transactions.Count == 0) return;
            if (MessageBox.Show("Hapus semua riwayat transaksi?", "Konfirmasi", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            { _transactions.Clear(); RefreshRiwayat(); SimpanKeDisk(); }
        }

        private void BtnCetakStruk_Click(object sender, RoutedEventArgs e)
        {
            _settings = DataStorageService.LoadSettings();
            Transaction? t = null;
            if (_halaman == "Riwayat" && DgRiwayat.SelectedItem is Transaction pilih)
                t = pilih;
            else
                t = _transactions.FirstOrDefault();

            if (t == null)
            { MessageBox.Show("Belum ada transaksi untuk dicetak.", "Struk", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            // Cetak ke printer thermal jika sudah dikonfigurasi
            if (!string.IsNullOrWhiteSpace(_settings.PrinterName))
            {
                var thermalResult = ThermalPrintService.CetakStruk(t, _settings);
                if (thermalResult.Success)
                {
                    MessageBox.Show(thermalResult.Message, "Struk Thermal", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                else
                {
                    var tanya = MessageBox.Show($"{thermalResult.Message}\n\nApakah ingin membuka versi PDF?",
                        "Gagal Cetak Thermal", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (tanya != MessageBoxResult.Yes) return;
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
            catch (Exception ex) { MessageBox.Show("Gagal: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        // =========== LAPORAN ===========
        private List<Transaction> TransaksiTerfilter()
        {
            DateTime? dari = DpDari.SelectedDate?.Date;
            DateTime? sampai = DpSampai.SelectedDate?.Date;
            var q = _transactions.AsEnumerable();
            if (dari != null) q = q.Where(t => t.Tanggal.Date >= dari.Value);
            if (sampai != null) q = q.Where(t => t.Tanggal.Date <= sampai.Value);
            return q.OrderBy(t => t.Tanggal).ToList();
        }

        private void BtnTerapkanFilter_Click(object sender, RoutedEventArgs e) => RefreshLaporan();
        private void BtnHariIni_Click(object sender, RoutedEventArgs e)
        { DpDari.SelectedDate = DateTime.Today; DpSampai.SelectedDate = DateTime.Today; RefreshLaporan(); }

        private void RefreshLaporan()
        {
            if (!_ready || CardOmzet == null) return;
            var data = TransaksiTerfilter();
            var lookup = HargaBeliLookup();
            decimal omzet = data.Sum(t => t.Total);
            int jmlTrx = data.Count;
            int jmlItem = data.Sum(t => t.JumlahItem);
            decimal rata = jmlTrx > 0 ? omzet / jmlTrx : 0;
            decimal laba = data.Sum(t => ExportService.LabaTransaksi(t, lookup));

            CardOmzet.Text = $"Rp {omzet:N0}";
            CardLaba.Text = $"Rp {laba:N0}";
            CardTransaksi.Text = jmlTrx.ToString();
            CardItem.Text = jmlItem.ToString();
            CardRata.Text = $"Rp {rata:N0}";

            var laris = ExportService.HitungProdukLaris(data, lookup);
            DgLaris.ItemsSource = laris;

            string periode = $"{DpDari.SelectedDate:dd/MM/yyyy} - {DpSampai.SelectedDate:dd/MM/yyyy}";
            TxtRingkasan.Text = jmlTrx == 0
                ? $"Periode {periode}: belum ada transaksi."
                : $"Periode {periode}: {jmlTrx} transaksi, {jmlItem} item, omzet Rp {omzet:N0}.\n" +
                  $"Laba estimasi: Rp {laba:N0} | Rata-rata: Rp {rata:N0}/nota." +
                  (laris.Count > 0 ? $"\nTerlaris: {laris[0].Nama} ({laris[0].Terjual} pcs)." : "");
        }

        private void BtnExportPdf_Click(object sender, RoutedEventArgs e)
        {
            var data = TransaksiTerfilter();
            try
            {
                string path = ExportService.SimpanLaporanPdf(data, DpDari.SelectedDate, DpSampai.SelectedDate, HargaBeliLookup(), _settings);
                TxtExportInfo.Text = $"PDF tersimpan: {path}";
                var jawab = MessageBox.Show($"Laporan PDF ({data.Count} transaksi):\n{path}\n\nBuka sekarang?",
                    "Export PDF", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (jawab == MessageBoxResult.Yes) Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex) { MessageBox.Show("Gagal: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void BtnExportExcel_Click(object sender, RoutedEventArgs e)
        {
            var data = TransaksiTerfilter();
            try
            {
                string path = ExportService.SimpanLaporanExcel(data, HargaBeliLookup());
                TxtExportInfo.Text = $"CSV tersimpan: {path}";
                var jawab = MessageBox.Show($"Laporan Excel CSV ({data.Count} transaksi):\n{path}\n\nBuka sekarang?",
                    "Export Excel", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (jawab == MessageBoxResult.Yes) Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex) { MessageBox.Show("Gagal: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        // =========== PENGATURAN ===========
        private void MuatDaftarPrinter()
        {
            if (CmbPrinterName == null) return;
            string current = _settings.PrinterName;
            var list = RawPrinterHelper.GetInstalledPrinters();
            CmbPrinterName.Items.Clear();
            foreach (var p in list)
            {
                CmbPrinterName.Items.Add(p);
            }

            if (!string.IsNullOrWhiteSpace(current))
            {
                if (!CmbPrinterName.Items.Contains(current))
                {
                    CmbPrinterName.Items.Add(current);
                }
                CmbPrinterName.Text = current;
                CmbPrinterName.SelectedItem = current;
            }
            else if (CmbPrinterName.Items.Count > 0)
            {
                CmbPrinterName.SelectedIndex = 0;
            }
        }

        private void IsiFormPengaturan()
        {
            if (TxtNamaToko == null) return;
            TxtNamaToko.Text = _settings.NamaToko;
            TxtAlamatToko.Text = _settings.Alamat;
            TxtTelpToko.Text = _settings.Telepon;
            TxtFooterStruk.Text = _settings.FooterStruk;

            MuatDaftarPrinter();

            if (CmbLebarKertas != null)
                CmbLebarKertas.SelectedIndex = _settings.PaperWidthMm <= 58 ? 1 : 0;

            if (ChkAutoCetakStruk != null)
                ChkAutoCetakStruk.IsChecked = _settings.AutoPrintReceipt;

            if (ChkKickDrawer != null)
                ChkKickDrawer.IsChecked = _settings.KickDrawer;
        }

        private void BtnRefreshPrinter_Click(object sender, RoutedEventArgs e)
        {
            MuatDaftarPrinter();
            MessageBox.Show($"Ditemukan {CmbPrinterName.Items.Count} printer Windows terpasang.", "Daftar Printer", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnTestPrint_Click(object sender, RoutedEventArgs e)
        {
            SimpanStatePengaturanKeSettings();
            var hasil = ThermalPrintService.CetakTest(_settings);
            if (hasil.Success)
                MessageBox.Show(hasil.Message, "Test Print Berhasil", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                MessageBox.Show(hasil.Message, "Test Print Gagal", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void BtnBukaLaci_Click(object sender, RoutedEventArgs e)
        {
            SimpanStatePengaturanKeSettings();
            var hasil = ThermalPrintService.BukaLaci(_settings);
            if (hasil.Success)
                MessageBox.Show(hasil.Message, "Buka Laci", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                MessageBox.Show(hasil.Message, "Buka Laci Gagal", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void SimpanStatePengaturanKeSettings()
        {
            _settings.NamaToko = TxtNamaToko?.Text?.Trim() ?? "";
            _settings.Alamat = TxtAlamatToko?.Text?.Trim() ?? "";
            _settings.Telepon = TxtTelpToko?.Text?.Trim() ?? "";
            _settings.FooterStruk = TxtFooterStruk?.Text?.Trim() ?? "";
            _settings.PrinterName = CmbPrinterName?.Text?.Trim() ?? "";
            _settings.PaperWidthMm = (CmbLebarKertas?.SelectedIndex == 1) ? 58 : 80;
            _settings.AutoPrintReceipt = ChkAutoCetakStruk?.IsChecked == true;
            _settings.KickDrawer = ChkKickDrawer?.IsChecked == true;
        }

        private void BtnSimpanPengaturan_Click(object sender, RoutedEventArgs e)
        {
            string nama = TxtNamaToko.Text.Trim();
            if (string.IsNullOrEmpty(nama))
            { MessageBox.Show("Nama toko tidak boleh kosong!", "Validasi", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            SimpanStatePengaturanKeSettings();
            DataStorageService.SaveSettings(_settings);
            MessageBox.Show("Pengaturan toko & printer thermal berhasil disimpan!", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // =========== MANAJEMEN USER ===========
        private UsersData _usersData = new();
        private readonly ObservableCollection<User> _usersList = new();

        private void MuatDataUser()
        {
            _usersData = AuthService.Load();
            _usersList.Clear();
            foreach (var u in _usersData.Users) _usersList.Add(u);
            if (DgUsers != null) DgUsers.ItemsSource = _usersList;
        }

        private void BtnTambahUser_Click(object sender, RoutedEventArgs e)
        {
            string username = TxtNewUsername.Text.Trim();
            string namaLengkap = TxtNewNamaLengkap.Text.Trim();
            string role = (CmbNewRole.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Kasir";
            string password = TxtNewPassword.Password;

            if (string.IsNullOrEmpty(username))
            {
                MessageBox.Show("Username tidak boleh kosong!", "Validasi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrEmpty(namaLengkap))
            {
                MessageBox.Show("Nama lengkap tidak boleh kosong!", "Validasi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Password tidak boleh kosong!", "Validasi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string? err = AuthService.AddUser(_usersData, username, namaLengkap, role, password);
            if (err != null)
            {
                MessageBox.Show(err, "Gagal Menambah Pengguna", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TxtNewUsername.Clear();
            TxtNewNamaLengkap.Clear();
            TxtNewPassword.Clear();
            CmbNewRole.SelectedIndex = 0;
            MuatDataUser();
            MessageBox.Show($"Akun \"{username}\" berhasil ditambahkan!", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnToggleAktifUser_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is User user)
            {
                // Jangan nonaktifkan diri sendiri
                if (_loggedInUser != null && user.Id == _loggedInUser.Id)
                {
                    MessageBox.Show("Tidak dapat menonaktifkan akun yang sedang login!", "Peringatan", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                AuthService.ToggleAktif(_usersData, user);
                MuatDataUser();
            }
        }

        private void BtnHapusUser_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is User user)
            {
                // Jangan hapus diri sendiri
                if (_loggedInUser != null && user.Id == _loggedInUser.Id)
                {
                    MessageBox.Show("Tidak dapat menghapus akun yang sedang login!", "Peringatan", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var jawab = MessageBox.Show($"Yakin ingin menghapus akun \"{user.Username}\"?", "Konfirmasi Hapus",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (jawab == MessageBoxResult.Yes)
                {
                    AuthService.DeleteUser(_usersData, user);
                    MuatDataUser();
                    MessageBox.Show("Akun berhasil dihapus.", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
    }
}
