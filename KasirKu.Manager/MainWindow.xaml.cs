using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
        private readonly ObservableCollection<CashExpense> _expenses = new();
        private readonly ObservableCollection<HoldTransaction> _holdTransactions = new();
        private readonly ObservableCollection<Member> _members = new();
        private readonly ObservableCollection<Supplier> _suppliers = new();

        private StoreSettings _settings = new();
        private Product? _editingProduct;
        private Member? _editingMember;
        private Supplier? _editingSupplier;
        private Product? _stokSelectedProduct;
        private Transaction? _editingTransaction;
        private readonly ObservableCollection<EditableTrxItem> _editTrxItems = new();
        private User? _loggedInUser;
        private int _nextProductId = 1;
        private int _nextTrx = 1;
        private int _nextMovementId = 1;
        private int _nextExpenseId = 1;
        private int _nextMemberId = 1;
        private int _nextSupplierId = 1;
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

            TxtLastRefresh.Text = $"Terakhir dimuat: {DateTime.Now:HH:mm:ss}";
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
                case Key.F7: ShowPage("Member"); e.Handled = true; break;
                case Key.F8: ShowPage("Supplier"); e.Handled = true; break;
                case Key.F10: BtnRefreshData_Click(this, new RoutedEventArgs()); e.Handled = true; break;
                case Key.Escape:
                    if (ModalEditTransaksi.Visibility == Visibility.Visible)
                    {
                        TutupModalEditTrx();
                        e.Handled = true;
                    }
                    else if (ModalProduk.Visibility == Visibility.Visible)
                    {
                        TutupModalProduk();
                        e.Handled = true;
                    }
                    else if (ModalMember.Visibility == Visibility.Visible)
                    {
                        TutupModalMember();
                        e.Handled = true;
                    }
                    else if (ModalSupplier.Visibility == Visibility.Visible)
                    {
                        TutupModalSupplier();
                        e.Handled = true;
                    }
                    else
                    {
                        Close();
                        e.Handled = true;
                    }
                    break;
            }
        }

        private void Nav_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string page) ShowPage(page);
        }

        private void ShowPage(string page)
        {
            _halaman = page;

            // Muat data terbaru dari disk secara otomatis saat ganti halaman (agar transaksi kasir langsung masuk)
            if (_ready && page != "Pengaturan")
            {
                MuatDariDisk();
            }

            PageDashboard.Visibility = Visibility.Collapsed;
            PageProduk.Visibility = Visibility.Collapsed;
            PageStok.Visibility = Visibility.Collapsed;
            PageRiwayat.Visibility = Visibility.Collapsed;
            PageLaporan.Visibility = Visibility.Collapsed;
            PagePengaturan.Visibility = Visibility.Collapsed;
            PageMember.Visibility = Visibility.Collapsed;
            PageSupplier.Visibility = Visibility.Collapsed;

            string title = page, sub = "";
            switch (page)
            {
                case "Dashboard":
                    PageDashboard.Visibility = Visibility.Visible;
                    title = "Dashboard";
                    sub = "Ringkasan bisnis hari ini. Data otomatis ter-update dari kasir.";
                    RefreshDashboard();
                    break;
                case "Produk":
                    PageProduk.Visibility = Visibility.Visible;
                    title = "Data Master Produk (F2)";
                    sub = "Kelola barang: kode, barcode, nama, merek, satuan, harga beli/jual, margin & supplier.";
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
                    sub = "Seluruh transaksi kasir. Klik baris untuk detail & cetak struk.";
                    RefreshRiwayat();
                    break;
                case "Laporan":
                    PageLaporan.Visibility = Visibility.Visible;
                    title = "Laporan Penjualan (F5)";
                    sub = "Filter periode, export PDF, export Excel (CSV).";
                    RefreshLaporan();
                    break;
                case "Pengaturan":
                    PagePengaturan.Visibility = Visibility.Visible;
                    title = "Pengaturan (F6)";
                    sub = "Profil toko yang tampil di struk & laporan. Dipakai bersama oleh POS dan Manager.";
                    IsiFormPengaturan();
                    break;
                case "Member":
                    PageMember.Visibility = Visibility.Visible;
                    title = "Data Member / Pelanggan (F7)";
                    sub = "Kelola pelanggan setia toko & akumulasi poin belanja.";
                    RefreshMember();
                    break;
                case "Supplier":
                    PageSupplier.Visibility = Visibility.Visible;
                    title = "Data Supplier / Pemasok (F8)";
                    sub = "Kelola daftar pemasok barang, kontak & alamat.";
                    RefreshSupplier();
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
                case "Member": RefreshMember(); break;
                case "Supplier": RefreshSupplier(); break;
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
            BtnNavMember.Background = _halaman == "Member" ? act : def; BtnNavMember.Foreground = _halaman == "Member" ? actFg : defFg;
            BtnNavSupplier.Background = _halaman == "Supplier" ? act : def; BtnNavSupplier.Foreground = _halaman == "Supplier" ? actFg : defFg;
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
            decimal omzetTunai = hariIni.Where(t => (t.MetodeBayar ?? "").Equals("Tunai", StringComparison.OrdinalIgnoreCase)).Sum(t => t.Total);
            decimal omzetNonTunai = hariIni.Where(t => !(t.MetodeBayar ?? "").Equals("Tunai", StringComparison.OrdinalIgnoreCase)).Sum(t => t.Total);
            int jmlTrx = hariIni.Count;
            int trxTunai = hariIni.Count(t => (t.MetodeBayar ?? "").Equals("Tunai", StringComparison.OrdinalIgnoreCase));
            int trxNonTunai = jmlTrx - trxTunai;
            int jmlItem = hariIni.Sum(t => t.JumlahItem);
            var lookup = HargaBeliLookup();
            decimal laba = hariIni.Sum(t => ExportService.LabaTransaksi(t, lookup));

            CardDashOmzet.Text = $"Rp {omzet:N0}";
            if (CardDashCash != null) CardDashCash.Text = $"Rp {omzetTunai:N0}";
            if (CardDashNonCash != null) CardDashNonCash.Text = $"Rp {omzetNonTunai:N0}";
            CardDashLaba.Text = $"Rp {laba:N0}";
            CardDashTrx.Text = jmlTrx.ToString();
            CardDashItem.Text = jmlItem.ToString();

            // Total Master Entitas
            if (CardDashTotalProduk != null) CardDashTotalProduk.Text = _products.Count.ToString("N0");
            if (CardDashTotalMember != null) CardDashTotalMember.Text = _members.Count.ToString("N0");
            if (CardDashTotalSupplier != null) CardDashTotalSupplier.Text = _suppliers.Count.ToString("N0");

            // Stok menipis
            DgStokTipis.ItemsSource = _products.Where(p => p.Stok <= 10).OrderBy(p => p.Stok).Take(10).ToList();

            // Transaksi terbaru (maksimal 10 trx hari ini atau terbaru)
            if (DgTrxTerbaru != null)
            {
                var trxBaru = _transactions.OrderByDescending(t => t.Tanggal).Take(10).ToList();
                DgTrxTerbaru.ItemsSource = trxBaru;
            }

            var perMetode = hariIni.GroupBy(t => string.IsNullOrWhiteSpace(t.MetodeBayar) ? "Tunai" : t.MetodeBayar)
                .Select(g => $"{g.Key}: {g.Count()} trx (Rp {g.Sum(x => x.Total):N0})");
            int stokTipis = _products.Count(p => p.Stok <= 10);
            TxtDashRingkasan.Text = jmlTrx == 0
                ? $"Belum ada transaksi hari ini.\nTotal produk: {_products.Count} | Total Member: {_members.Count} | Total Supplier: {_suppliers.Count} | Stok menipis: {stokTipis} jenis."
                : $"Hari ini: {jmlTrx} transaksi, {jmlItem} item.\n" +
                  $"Total Omzet: Rp {omzet:N0} (💵 Tunai: Rp {omzetTunai:N0} [{trxTunai} trx] | 📱 Non-Tunai: Rp {omzetNonTunai:N0} [{trxNonTunai} trx])\n" +
                  $"Laba Estimasi: Rp {laba:N0}\n" +
                  $"Rincian Metode: {string.Join(" | ", perMetode)}\n" +
                  $"Total produk: {_products.Count} | Member: {_members.Count} | Supplier: {_suppliers.Count} | Stok menipis: {stokTipis} jenis.";
        }

        // =========== PRODUK (CRUD MODAL) ===========
        private void RefreshProduk()
        {
            string key = TxtCariProduk?.Text?.Trim() ?? "";
            string filterKat = (CmbFilterKategoriProduk?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Semua Kelompok";

            var query = _products.AsEnumerable();

            if (!string.IsNullOrEmpty(key))
            {
                query = query.Where(p =>
                    p.Kode.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    p.Barcode.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    p.Nama.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (p.Merek ?? "").IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    p.Supplier.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    p.Kategori.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (!string.IsNullOrEmpty(filterKat) && filterKat != "Semua Kelompok")
            {
                query = query.Where(p => (p.Kategori ?? "").Equals(filterKat, StringComparison.OrdinalIgnoreCase));
            }

            DgProduk.ItemsSource = query.ToList();
        }

        private void TxtCariProduk_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_ready) return;
            RefreshProduk();
        }

        private void CmbFilterKategoriProduk_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_ready) return;
            RefreshProduk();
        }

        private void MuatSupplierKeComboProduk()
        {
            CmbModalSupplier.Items.Clear();
            CmbModalSupplier.Items.Add("Umum");
            foreach (var sup in _suppliers.OrderBy(s => s.Nama))
            {
                if (!string.IsNullOrWhiteSpace(sup.Nama) && !CmbModalSupplier.Items.Contains(sup.Nama))
                    CmbModalSupplier.Items.Add(sup.Nama);
            }
            // fallback supplier default
            if (!CmbModalSupplier.Items.Contains("Distributor A")) CmbModalSupplier.Items.Add("Distributor A");
            if (!CmbModalSupplier.Items.Contains("Distributor B")) CmbModalSupplier.Items.Add("Distributor B");
            if (!CmbModalSupplier.Items.Contains("Agen Frozen")) CmbModalSupplier.Items.Add("Agen Frozen");
        }

        private void BtnBukaModalTambahProduk_Click(object sender, RoutedEventArgs e)
        {
            _editingProduct = null;
            TxtModalProdukJudul.Text = "Tambah Produk Baru";
            TxtModalKode.Text = "";
            TxtModalBarcode.Text = "";
            TxtModalNama.Text = "";
            TxtModalMerek.Text = "";
            SetComboText(CmbModalSatuan, "Pcs");
            TxtModalHargaBeli.Text = "";
            TxtModalHargaJual.Text = "";
            TxtModalStok.Text = "0";
            MuatSupplierKeComboProduk();
            SetComboText(CmbModalSupplier, "Umum");
            CmbModalKategori.SelectedIndex = 0;
            UpdateModalMarginInfo();

            ModalProduk.Visibility = Visibility.Visible;
            TxtModalKode.Focus();
        }

        private void DgProduk_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DgProduk.SelectedItem is Product p)
            {
                BukaModalEditProduk(p);
            }
        }

        private void BtnEditProdukGrid_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is Product p)
            {
                BukaModalEditProduk(p);
            }
        }

        private void BukaModalEditProduk(Product p)
        {
            _editingProduct = p;
            TxtModalProdukJudul.Text = $"Edit Produk - {p.Kode}";
            TxtModalKode.Text = p.Kode;
            TxtModalBarcode.Text = p.Barcode;
            TxtModalNama.Text = p.Nama;
            TxtModalMerek.Text = p.Merek ?? "";
            SetComboText(CmbModalSatuan, p.Satuan);
            TxtModalHargaBeli.Text = p.HargaBeli.ToString("F0");
            TxtModalHargaJual.Text = p.Harga.ToString("F0");
            TxtModalStok.Text = p.Stok.ToString();
            MuatSupplierKeComboProduk();
            SetComboText(CmbModalSupplier, p.Supplier);

            foreach (ComboBoxItem it in CmbModalKategori.Items)
            {
                if (it.Content?.ToString() == p.Kategori) { CmbModalKategori.SelectedItem = it; break; }
            }

            UpdateModalMarginInfo();
            ModalProduk.Visibility = Visibility.Visible;
            TxtModalNama.Focus();
        }

        private void BtnTutupModalProduk_Click(object sender, RoutedEventArgs e)
        {
            TutupModalProduk();
        }

        private void TutupModalProduk()
        {
            ModalProduk.Visibility = Visibility.Collapsed;
            _editingProduct = null;
        }

        private void TxtModalHargaMargin_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateModalMarginInfo();
        }

        private void UpdateModalMarginInfo()
        {
            if (TxtModalMarginInfo == null) return;
            decimal.TryParse((TxtModalHargaBeli?.Text ?? "").Trim(), out var beli);
            decimal.TryParse((TxtModalHargaJual?.Text ?? "").Trim(), out var jual);
            decimal margin = jual - beli;
            decimal persen = beli > 0 ? margin / beli * 100 : 0;
            TxtModalMarginInfo.Text = (beli <= 0 || jual <= 0) ? "Margin: -" : $"Margin: Rp {margin:N0} ({persen:N1}%)";
        }

        private void BtnSimpanModalProduk_Click(object sender, RoutedEventArgs e)
        {
            string kode = TxtModalKode.Text.Trim();
            string barcode = TxtModalBarcode.Text.Trim();
            string nama = TxtModalNama.Text.Trim();
            string merek = TxtModalMerek.Text.Trim();
            string satuan = GetComboText(CmbModalSatuan, "Pcs");
            string kelompok = (CmbModalKategori.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Umum";
            string supplier = GetComboText(CmbModalSupplier, "Umum");

            if (string.IsNullOrEmpty(kode) || string.IsNullOrEmpty(nama))
            {
                MessageBox.Show("Kode Produk dan Nama wajib diisi!", "Validasi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrEmpty(barcode)) barcode = kode;
            if (string.IsNullOrEmpty(satuan)) satuan = "Pcs";
            if (string.IsNullOrEmpty(supplier)) supplier = "Umum";

            if (!decimal.TryParse(TxtModalHargaBeli.Text.Trim(), out var hargaBeli) || hargaBeli < 0)
            {
                MessageBox.Show("Harga beli harus angka 0 atau lebih!", "Validasi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!decimal.TryParse(TxtModalHargaJual.Text.Trim(), out var hargaJual) || hargaJual <= 0)
            {
                MessageBox.Show("Harga jual harus angka lebih dari 0!", "Validasi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (hargaJual < hargaBeli)
            {
                var lanjut = MessageBox.Show($"Harga jual (Rp {hargaJual:N0}) lebih kecil dari harga beli (Rp {hargaBeli:N0}).\nTetap simpan?",
                    "Margin Minus", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (lanjut != MessageBoxResult.Yes) return;
            }
            if (!int.TryParse(TxtModalStok.Text.Trim(), out var stok) || stok < 0)
            {
                MessageBox.Show("Qty/Stok harus angka 0 atau lebih!", "Validasi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_editingProduct == null)
            {
                if (_products.Any(p => p.Kode.Equals(kode, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show("Kode produk sudah dipakai!", "Validasi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (_products.Any(p => !string.IsNullOrEmpty(p.Barcode) && p.Barcode.Equals(barcode, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show("Kode barcode sudah dipakai!", "Validasi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var baru = new Product
                {
                    Id = _nextProductId++,
                    Kode = kode,
                    Barcode = barcode,
                    Nama = nama,
                    Merek = merek,
                    Satuan = satuan,
                    Kategori = kelompok,
                    Supplier = supplier,
                    HargaBeli = hargaBeli,
                    Harga = hargaJual,
                    Stok = 0
                };
                _products.Add(baru);
                if (stok > 0) CatatMasuk(baru, stok, "Stok awal produk baru");
                MessageBox.Show("Produk berhasil ditambahkan!", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                if (_products.Any(p => p.Id != _editingProduct.Id && p.Kode.Equals(kode, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show("Kode produk sudah dipakai!", "Validasi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (_products.Any(p => p.Id != _editingProduct.Id && !string.IsNullOrEmpty(p.Barcode) && p.Barcode.Equals(barcode, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show("Kode barcode sudah dipakai!", "Validasi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                int stokLama = _editingProduct.Stok;
                _editingProduct.Kode = kode;
                _editingProduct.Barcode = barcode;
                _editingProduct.Nama = nama;
                _editingProduct.Merek = merek;
                _editingProduct.Satuan = satuan;
                _editingProduct.Kategori = kelompok;
                _editingProduct.Supplier = supplier;
                _editingProduct.HargaBeli = hargaBeli;
                _editingProduct.Harga = hargaJual;
                _editingProduct.Stok = stok;
                int selisih = stok - stokLama;
                if (selisih > 0) TambahLog(_editingProduct, "Masuk", selisih, stokLama, stok, $"Koreksi via Data Produk ({stokLama} -> {stok})");
                else if (selisih < 0) TambahLog(_editingProduct, "Keluar", -selisih, stokLama, stok, $"Koreksi via Data Produk ({stokLama} -> {stok})");
                MessageBox.Show("Produk berhasil diupdate!", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            TutupModalProduk();
            RefreshProduk();
            SimpanKeDisk();
        }

        private static string GetComboText(ComboBox cmb, string fallback = "")
        {
            if (cmb == null) return fallback;
            string t = (cmb.Text ?? "").Trim();
            if (!string.IsNullOrEmpty(t)) return t;
            if (cmb.SelectedItem is ComboBoxItem it) return (it.Content?.ToString() ?? "").Trim();
            if (cmb.SelectedItem is string str) return str.Trim();
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

        private void BtnHapusProduk_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not Product p) return;
            if (MessageBox.Show($"Hapus '{p.Nama}'?", "Konfirmasi", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                if (p.Stok > 0) TambahLog(p, "Keluar", p.Stok, p.Stok, 0, "Produk dihapus");
                _products.Remove(p);
                RefreshProduk();
                SimpanKeDisk();
            }
        }

        // =========== MEMBER (CRUD MODAL) ===========
        private void RefreshMember()
        {
            string key = TxtCariMember?.Text?.Trim() ?? "";
            var data = string.IsNullOrEmpty(key)
                ? _members.ToList()
                : _members.Where(m =>
                    m.Nama.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    m.NoHp.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    m.Alamat.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            DgMember.ItemsSource = data;
        }

        private void TxtCariMember_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_ready) return;
            RefreshMember();
        }

        private void BtnBukaModalTambahMember_Click(object sender, RoutedEventArgs e)
        {
            _editingMember = null;
            TxtModalMemberJudul.Text = "Tambah Member Baru";
            TxtModalMemberNama.Text = "";
            TxtModalMemberNoHp.Text = "";
            TxtModalMemberAlamat.Text = "";
            TxtModalMemberPoin.Text = "0";

            ModalMember.Visibility = Visibility.Visible;
            TxtModalMemberNama.Focus();
        }

        private void DgMember_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DgMember.SelectedItem is Member m) BukaModalEditMember(m);
        }

        private void BtnEditMemberGrid_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is Member m) BukaModalEditMember(m);
        }

        private void BukaModalEditMember(Member m)
        {
            _editingMember = m;
            TxtModalMemberJudul.Text = $"Edit Member - {m.Nama}";
            TxtModalMemberNama.Text = m.Nama;
            TxtModalMemberNoHp.Text = m.NoHp;
            TxtModalMemberAlamat.Text = m.Alamat;
            TxtModalMemberPoin.Text = m.Poin.ToString();

            ModalMember.Visibility = Visibility.Visible;
            TxtModalMemberNama.Focus();
        }

        private void BtnTutupModalMember_Click(object sender, RoutedEventArgs e) => TutupModalMember();

        private void TutupModalMember()
        {
            ModalMember.Visibility = Visibility.Collapsed;
            _editingMember = null;
        }

        private void BtnSimpanModalMember_Click(object sender, RoutedEventArgs e)
        {
            string nama = TxtModalMemberNama.Text.Trim();
            string noHp = TxtModalMemberNoHp.Text.Trim();
            string alamat = TxtModalMemberAlamat.Text.Trim();

            if (string.IsNullOrEmpty(nama) || string.IsNullOrEmpty(noHp))
            {
                MessageBox.Show("Nama Lengkap dan No. Handphone wajib diisi!", "Validasi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int.TryParse(TxtModalMemberPoin.Text.Trim(), out var poin);

            if (_editingMember == null)
            {
                var baru = new Member
                {
                    Id = _nextMemberId++,
                    Nama = nama,
                    NoHp = noHp,
                    Alamat = alamat,
                    Poin = poin,
                    TanggalDaftar = DateTime.Now
                };
                _members.Add(baru);
                MessageBox.Show("Member baru berhasil ditambahkan!", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                _editingMember.Nama = nama;
                _editingMember.NoHp = noHp;
                _editingMember.Alamat = alamat;
                _editingMember.Poin = poin;
                MessageBox.Show("Data member berhasil diupdate!", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            TutupModalMember();
            RefreshMember();
            SimpanKeDisk();
        }

        private void BtnHapusMember_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not Member m) return;
            if (MessageBox.Show($"Hapus data member '{m.Nama}'?", "Konfirmasi Hapus", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _members.Remove(m);
                RefreshMember();
                SimpanKeDisk();
            }
        }

        // =========== SUPPLIER (CRUD MODAL) ===========
        private void RefreshSupplier()
        {
            string key = TxtCariSupplier?.Text?.Trim() ?? "";
            var data = string.IsNullOrEmpty(key)
                ? _suppliers.ToList()
                : _suppliers.Where(s =>
                    s.Nama.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    s.NoHp.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    s.Alamat.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    s.Email.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            DgSupplier.ItemsSource = data;
        }

        private void TxtCariSupplier_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_ready) return;
            RefreshSupplier();
        }

        private void BtnBukaModalTambahSupplier_Click(object sender, RoutedEventArgs e)
        {
            _editingSupplier = null;
            TxtModalSupplierJudul.Text = "Tambah Supplier Baru";
            TxtModalSupplierNama.Text = "";
            TxtModalSupplierNoHp.Text = "";
            TxtModalSupplierAlamat.Text = "";
            TxtModalSupplierEmail.Text = "";

            ModalSupplier.Visibility = Visibility.Visible;
            TxtModalSupplierNama.Focus();
        }

        private void DgSupplier_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DgSupplier.SelectedItem is Supplier s) BukaModalEditSupplier(s);
        }

        private void BtnEditSupplierGrid_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is Supplier s) BukaModalEditSupplier(s);
        }

        private void BukaModalEditSupplier(Supplier s)
        {
            _editingSupplier = s;
            TxtModalSupplierJudul.Text = $"Edit Supplier - {s.Nama}";
            TxtModalSupplierNama.Text = s.Nama;
            TxtModalSupplierNoHp.Text = s.NoHp;
            TxtModalSupplierAlamat.Text = s.Alamat;
            TxtModalSupplierEmail.Text = s.Email;

            ModalSupplier.Visibility = Visibility.Visible;
            TxtModalSupplierNama.Focus();
        }

        private void BtnTutupModalSupplier_Click(object sender, RoutedEventArgs e) => TutupModalSupplier();

        private void TutupModalSupplier()
        {
            ModalSupplier.Visibility = Visibility.Collapsed;
            _editingSupplier = null;
        }

        private void BtnSimpanModalSupplier_Click(object sender, RoutedEventArgs e)
        {
            string nama = TxtModalSupplierNama.Text.Trim();
            string noHp = TxtModalSupplierNoHp.Text.Trim();
            string alamat = TxtModalSupplierAlamat.Text.Trim();
            string email = TxtModalSupplierEmail.Text.Trim();

            if (string.IsNullOrEmpty(nama) || string.IsNullOrEmpty(noHp))
            {
                MessageBox.Show("Nama Pemasok dan No. Handphone wajib diisi!", "Validasi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_editingSupplier == null)
            {
                var baru = new Supplier
                {
                    Id = _nextSupplierId++,
                    Nama = nama,
                    NoHp = noHp,
                    Alamat = alamat,
                    Email = email
                };
                _suppliers.Add(baru);
                MessageBox.Show("Supplier baru berhasil ditambahkan!", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                _editingSupplier.Nama = nama;
                _editingSupplier.NoHp = noHp;
                _editingSupplier.Alamat = alamat;
                _editingSupplier.Email = email;
                MessageBox.Show("Data supplier berhasil diupdate!", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            TutupModalSupplier();
            RefreshSupplier();
            SimpanKeDisk();
        }

        private void BtnHapusSupplier_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not Supplier s) return;
            if (MessageBox.Show($"Hapus data supplier '{s.Nama}'?", "Konfirmasi Hapus", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _suppliers.Remove(s);
                RefreshSupplier();
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

            if (_stokSelectedProduct != null)
            {
                var sama = _products.FirstOrDefault(p => p.Id == _stokSelectedProduct.Id);
                _stokSelectedProduct = sama;
            }
            UpdateStokSaatIni();
        }

        private void UpdateStokSaatIni()
        {
            if (TxtStokSaatIni == null) return;
            TxtStokSaatIni.Text = _stokSelectedProduct != null
                ? $"Stok saat ini: {_stokSelectedProduct.Stok} ({_stokSelectedProduct.Satuan})"
                : "Stok saat ini: -";
        }

        // ===== Live Search Produk untuk Input Stok =====
        private void TxtInputStokProduk_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_ready) return;
            string key = (TxtInputStokProduk?.Text ?? "").Trim();
            if (string.IsNullOrEmpty(key))
            {
                TutupPopupStokProduk();
                _stokSelectedProduct = null;
                UpdateStokSaatIni();
                return;
            }

            // Jika user memilih item dan text di-set sama dengan nama/kode, abaikan trigger popup
            if (_stokSelectedProduct != null &&
                (key.Equals(_stokSelectedProduct.Nama, StringComparison.OrdinalIgnoreCase) ||
                 key.Equals($"{_stokSelectedProduct.Kode} - {_stokSelectedProduct.Nama}", StringComparison.OrdinalIgnoreCase)))
            {
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

            if (cocok.Count > 0)
            {
                LstCariStokProduk.ItemsSource = cocok;
                LstCariStokProduk.SelectedIndex = 0;
                PupStokProduk.IsOpen = true;
            }
            else
            {
                TutupPopupStokProduk();
            }
        }

        private void TxtInputStokProduk_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (PupStokProduk != null && PupStokProduk.IsOpen)
            {
                if (e.Key == Key.Down)
                {
                    if (LstCariStokProduk.Items.Count > 0)
                    {
                        int next = LstCariStokProduk.SelectedIndex + 1;
                        if (next >= LstCariStokProduk.Items.Count) next = 0;
                        LstCariStokProduk.SelectedIndex = next;
                        LstCariStokProduk.ScrollIntoView(LstCariStokProduk.SelectedItem);
                    }
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.Up)
                {
                    if (LstCariStokProduk.Items.Count > 0)
                    {
                        int prev = LstCariStokProduk.SelectedIndex - 1;
                        if (prev < 0) prev = LstCariStokProduk.Items.Count - 1;
                        LstCariStokProduk.SelectedIndex = prev;
                        LstCariStokProduk.ScrollIntoView(LstCariStokProduk.SelectedItem);
                    }
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.Escape)
                {
                    TutupPopupStokProduk();
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.Enter)
                {
                    if (LstCariStokProduk.SelectedItem is Product pilih)
                    {
                        PilihProdukStok(pilih);
                        e.Handled = true;
                        return;
                    }
                }
            }

            if (e.Key == Key.Enter)
            {
                string key = (TxtInputStokProduk?.Text ?? "").Trim();
                var exact = _products.FirstOrDefault(p =>
                    p.Kode.Equals(key, StringComparison.OrdinalIgnoreCase) ||
                    p.Barcode.Equals(key, StringComparison.OrdinalIgnoreCase));
                if (exact != null)
                {
                    PilihProdukStok(exact);
                    e.Handled = true;
                }
            }
        }

        private void LstCariStokProduk_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && LstCariStokProduk.SelectedItem is Product p)
            {
                PilihProdukStok(p);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                TutupPopupStokProduk();
                TxtInputStokProduk.Focus();
                e.Handled = true;
            }
        }

        private void LstCariStokProduk_Click(object sender, RoutedEventArgs e)
        {
            if (LstCariStokProduk.SelectedItem is Product p)
            {
                PilihProdukStok(p);
            }
        }

        private void PilihProdukStok(Product p)
        {
            _stokSelectedProduct = p;
            TxtInputStokProduk.Text = $"{p.Kode} - {p.Nama}";
            TutupPopupStokProduk();
            UpdateStokSaatIni();
            TxtStokQty.Focus();
            TxtStokQty.SelectAll();
        }

        private void TutupPopupStokProduk()
        {
            if (PupStokProduk != null) { PupStokProduk.IsOpen = false; LstCariStokProduk.ItemsSource = null; }
        }

        private void TxtCariStok_TextChanged(object sender, TextChangedEventArgs e) { if (_ready) RefreshStok(); }
        private void CmbFilterJenis_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (_ready) RefreshStok(); }

        private void BtnSimpanStok_Click(object sender, RoutedEventArgs e)
        {
            if (_stokSelectedProduct == null)
            { MessageBox.Show("Pilih produk dulu! Ketik nama atau kode di kolom Produk.", "Stok", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            Product p = _stokSelectedProduct;
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
            try
            {
                string key = TxtCariRiwayat?.Text?.Trim().ToLower() ?? "";
                var list = _transactions.ToList();

                int filterIdx = CmbFilterMetodeRiwayat?.SelectedIndex ?? 0;
                if (filterIdx == 1) // Hanya Tunai
                    list = list.Where(t => (t.MetodeBayar ?? "").Equals("Tunai", StringComparison.OrdinalIgnoreCase)).ToList();
                else if (filterIdx == 2) // Hanya Non-Tunai
                    list = list.Where(t => !(t.MetodeBayar ?? "").Equals("Tunai", StringComparison.OrdinalIgnoreCase)).ToList();

                var data = string.IsNullOrEmpty(key)
                    ? list
                    : list.Where(t => (t.Id ?? "").ToLower().Contains(key) ||
                                      (t.Ringkasan ?? "").ToLower().Contains(key) ||
                                      (t.MetodeBayar ?? "").ToLower().Contains(key)).ToList();
                DgRiwayat.ItemsSource = data;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("RefreshRiwayat error: " + ex.Message);
            }
        }

        private void CmbFilterMetodeRiwayat_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_ready) RefreshRiwayat();
        }

        private void TxtCariRiwayat_TextChanged(object sender, TextChangedEventArgs e) { if (_ready) RefreshRiwayat(); }

        private void DgRiwayat_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (DgRiwayat.SelectedItem is Transaction t)
                {
                    TxtDetailJudul.Text = $"Detail - {t.Id}";
                    DgRiwayatDetail.ItemsSource = t.Items;
                    TxtDetailTotal.Text = $"Metode: {t.MetodeBayar ?? "Tunai"}\nTotal: Rp {t.Total:N0} | Bayar: Rp {t.Bayar:N0} | Kembali: Rp {t.Kembalian:N0}\n{t.Tanggal:dd MMM yyyy HH:mm} | {t.JumlahItem} item";
                }
                else
                {
                    TxtDetailJudul.Text = "Detail Transaksi";
                    DgRiwayatDetail.ItemsSource = null;
                    TxtDetailTotal.Text = "";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("DgRiwayat_SelectionChanged error: " + ex.Message);
            }
        }

        private void BtnHapusRiwayat_Click(object sender, RoutedEventArgs e)
        {
            if (_transactions.Count == 0) return;
            if (MessageBox.Show("Hapus semua riwayat transaksi?", "Konfirmasi", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            { _transactions.Clear(); RefreshRiwayat(); SimpanKeDisk(); }
        }

        // ===== EDIT & HAPUS SATU TRANSAKSI (PER TRANSAKSI) =====
        private void BtnEditTransaksi_Click(object sender, RoutedEventArgs e)
        {
            if (DgRiwayat.SelectedItem is not Transaction t)
            {
                MessageBox.Show("Pilih transaksi di tabel terlebih dahulu yang ingin diedit!", "Edit Transaksi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            BukaModalEditTrx(t);
        }

        private void BtnEditSatuTrxGrid_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not Transaction t) return;
            DgRiwayat.SelectedItem = t;
            BukaModalEditTrx(t);
        }

        private void BtnHapusSatuTransaksi_Click(object sender, RoutedEventArgs e)
        {
            if (DgRiwayat.SelectedItem is not Transaction t)
            {
                MessageBox.Show("Pilih transaksi di tabel terlebih dahulu yang ingin dihapus!", "Hapus Transaksi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            HapusSatuTransaksi(t);
        }

        private void BtnHapusSatuTrxGrid_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not Transaction t) return;
            DgRiwayat.SelectedItem = t;
            HapusSatuTransaksi(t);
        }

        private void HapusSatuTransaksi(Transaction t)
        {
            var tanya = MessageBox.Show(
                $"Yakin ingin menghapus transaksi {t.Id} senilai Rp {t.Total:N0}?\n\n" +
                $"Stok seluruh barang ({t.JumlahItem} item) akan dikembalikan ke inventaris toko secara otomatis.",
                "Konfirmasi Hapus Transaksi", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (tanya != MessageBoxResult.Yes) return;

            // Kembalikan stok
            foreach (var item in t.Items)
            {
                var p = _products.FirstOrDefault(x => x.Kode.Equals(item.Kode, StringComparison.OrdinalIgnoreCase) ||
                                                      x.Nama.Equals(item.Nama, StringComparison.OrdinalIgnoreCase));
                if (p != null)
                {
                    CatatMasuk(p, item.Qty, $"Batal/hapus transaksi {t.Id}");
                }
            }

            _transactions.Remove(t);
            SimpanKeDisk();
            RefreshRiwayat();
            RefreshStok();
            RefreshDashboard();
            DgRiwayat_SelectionChanged(this, null!);
            MessageBox.Show($"Transaksi {t.Id} berhasil dihapus dan stok telah dikembalikan.", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BukaModalEditTrx(Transaction t)
        {
            _editingTransaction = t;
            TxtModalEditTrxJudul.Text = $"✏️ Edit Transaksi - {t.Id}";
            DpEditTrxTanggal.SelectedDate = t.Tanggal.Date;
            TxtEditTrxJam.Text = t.Tanggal.ToString("HH:mm");
            TxtEditTrxKasir.Text = t.KasirNama;

            // Set ComboBox Metode
            bool metodeFound = false;
            foreach (ComboBoxItem item in CmbEditTrxMetode.Items)
            {
                if (item.Content?.ToString()?.Equals(t.MetodeBayar, StringComparison.OrdinalIgnoreCase) == true)
                {
                    CmbEditTrxMetode.SelectedItem = item;
                    metodeFound = true;
                    break;
                }
            }
            if (!metodeFound && CmbEditTrxMetode.Items.Count > 0)
                CmbEditTrxMetode.SelectedIndex = 0;

            _editTrxItems.Clear();
            foreach (var item in t.Items)
            {
                _editTrxItems.Add(new EditableTrxItem
                {
                    Kode = item.Kode,
                    Nama = item.Nama,
                    Satuan = string.IsNullOrWhiteSpace(item.Satuan) ? "pcs" : item.Satuan,
                    Harga = item.Harga,
                    HargaBeli = item.HargaBeli,
                    Qty = item.Qty
                });
            }
            DgEditTrxItems.ItemsSource = _editTrxItems;

            TxtEditTrxBayar.Text = t.Bayar.ToString("F0");
            RecalculateEditTrxSummary();

            ModalEditTransaksi.Visibility = Visibility.Visible;
        }

        private void TutupModalEditTrx()
        {
            ModalEditTransaksi.Visibility = Visibility.Collapsed;
            _editingTransaction = null;
        }

        private void BtnTutupModalEditTrx_Click(object sender, RoutedEventArgs e) => TutupModalEditTrx();

        private void BtnEditTrxQtyMin_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not EditableTrxItem item) return;
            if (item.Qty > 1)
            {
                item.Qty--;
                RecalculateEditTrxSummary();
            }
        }

        private void BtnEditTrxQtyPlus_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not EditableTrxItem item) return;
            item.Qty++;
            RecalculateEditTrxSummary();
        }

        private void BtnEditTrxHapusItem_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not EditableTrxItem item) return;
            if (_editTrxItems.Count <= 1)
            {
                MessageBox.Show("Transaksi minimal harus memiliki 1 item barang!\nJika ingin menghapus seluruh transaksi, gunakan tombol 'Hapus Transaksi'.",
                    "Peringatan", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            _editTrxItems.Remove(item);
            RecalculateEditTrxSummary();
        }

        private void TxtEditTrxHarga_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tb && tb.DataContext is EditableTrxItem item)
            {
                string raw = tb.Text.Replace(".", "").Replace(",", "").Trim();
                if (decimal.TryParse(raw, out decimal h) && h >= 0)
                {
                    item.Harga = h;
                    RecalculateEditTrxSummary();
                }
            }
        }

        private void TxtEditTrxBayar_TextChanged(object sender, TextChangedEventArgs e)
        {
            RecalculateEditTrxKembali();
        }

        private void RecalculateEditTrxSummary()
        {
            decimal total = _editTrxItems.Sum(i => i.Subtotal);
            if (TxtEditTrxTotal != null) TxtEditTrxTotal.Text = $"Rp {total:N0}";
            RecalculateEditTrxKembali();
        }

        private void RecalculateEditTrxKembali()
        {
            if (TxtEditTrxKembali == null) return;
            decimal total = _editTrxItems.Sum(i => i.Subtotal);
            string raw = (TxtEditTrxBayar?.Text ?? "").Replace(".", "").Replace(",", "").Trim();
            decimal.TryParse(raw, out decimal bayar);
            decimal kembali = bayar - total;
            TxtEditTrxKembali.Text = $"Rp {kembali:N0}";
            TxtEditTrxKembali.Foreground = kembali < 0
                ? new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26))
                : new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A));
        }

        private void BtnSimpanEditTrx_Click(object sender, RoutedEventArgs e)
        {
            if (_editingTransaction == null) return;
            if (_editTrxItems.Count == 0)
            {
                MessageBox.Show("Transaksi tidak boleh kosong!", "Validasi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DateTime tgl = DpEditTrxTanggal.SelectedDate ?? DateTime.Today;
            if (TimeSpan.TryParse(TxtEditTrxJam.Text.Trim(), out TimeSpan jam))
                tgl = tgl.Date + jam;
            else
                tgl = tgl.Date + DateTime.Now.TimeOfDay;

            decimal newTotal = _editTrxItems.Sum(i => i.Subtotal);
            string rawBayar = (TxtEditTrxBayar?.Text ?? "").Replace(".", "").Replace(",", "").Trim();
            decimal.TryParse(rawBayar, out decimal bayar);
            string metode = (CmbEditTrxMetode.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Tunai";

            if (metode == "Tunai" && bayar < newTotal)
            {
                MessageBox.Show($"Nominal bayar (Rp {bayar:N0}) kurang dari total belanja (Rp {newTotal:N0})!",
                    "Validasi Pembayaran", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (metode != "Tunai" && bayar < newTotal)
            {
                bayar = newTotal;
            }

            // Selaraskan stok berdasarkan perubahan item
            var oldItems = _editingTransaction.Items.ToList();
            foreach (var old in oldItems)
            {
                var matchingNew = _editTrxItems.FirstOrDefault(x => x.Kode.Equals(old.Kode, StringComparison.OrdinalIgnoreCase));
                var prod = _products.FirstOrDefault(p => p.Kode.Equals(old.Kode, StringComparison.OrdinalIgnoreCase));
                if (prod != null)
                {
                    if (matchingNew == null)
                    {
                        // Item dihapus dari transaksi -> kembalikan stok
                        CatatMasuk(prod, old.Qty, $"Hapus item di edit trx {_editingTransaction.Id}");
                    }
                    else
                    {
                        int diff = matchingNew.Qty - old.Qty;
                        if (diff > 0)
                            CatatKeluar(prod, diff, $"Tambah qty edit trx {_editingTransaction.Id}");
                        else if (diff < 0)
                            CatatMasuk(prod, -diff, $"Kurang qty edit trx {_editingTransaction.Id}");
                    }
                }
            }

            // Terapkan perubahan ke objek transaksi
            _editingTransaction.Tanggal = tgl;
            _editingTransaction.MetodeBayar = metode;
            _editingTransaction.KasirNama = TxtEditTrxKasir.Text.Trim();
            _editingTransaction.Total = newTotal;
            _editingTransaction.Bayar = bayar;
            _editingTransaction.Items = _editTrxItems.Select(x => new TransactionItem
            {
                Kode = x.Kode,
                Nama = x.Nama,
                Satuan = x.Satuan,
                Harga = x.Harga,
                HargaBeli = x.HargaBeli,
                Qty = x.Qty
            }).ToList();

            string savedId = _editingTransaction.Id;
            SimpanKeDisk();
            RefreshRiwayat();
            RefreshStok();
            RefreshDashboard();

            // Refresh Detail panel
            DgRiwayat_SelectionChanged(this, null!);

            TutupModalEditTrx();
            MessageBox.Show($"Perubahan transaksi {savedId} berhasil disimpan!",
                "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
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

        private void BtnTerapkanFilter_Click(object sender, RoutedEventArgs e)
        {
            MuatDariDisk();
            RefreshLaporan();
        }

        private void BtnHariIni_Click(object sender, RoutedEventArgs e)
        {
            MuatDariDisk();
            DpDari.SelectedDate = DateTime.Today;
            DpSampai.SelectedDate = DateTime.Today;
            RefreshLaporan();
        }

        private void RefreshLaporan()
        {
            if (!_ready || CardOmzet == null) return;
            var data = TransaksiTerfilter();
            var lookup = HargaBeliLookup();

            DateTime? dari = DpDari.SelectedDate?.Date;
            DateTime? sampai = DpSampai.SelectedDate?.Date.AddDays(1).AddTicks(-1);
            var expenses = _expenses.AsEnumerable();
            if (dari != null) expenses = expenses.Where(x => x.Tanggal >= dari.Value);
            if (sampai != null) expenses = expenses.Where(x => x.Tanggal <= sampai.Value);
            var expList = expenses.OrderByDescending(x => x.Tanggal).ToList();
            decimal totalKeluar = expList.Sum(x => x.Nominal);

            decimal omzet = data.Sum(t => t.Total);
            decimal omzetTunai = data.Where(t => (t.MetodeBayar ?? "").Equals("Tunai", StringComparison.OrdinalIgnoreCase)).Sum(t => t.Total);
            decimal omzetNonTunai = data.Where(t => !(t.MetodeBayar ?? "").Equals("Tunai", StringComparison.OrdinalIgnoreCase)).Sum(t => t.Total);
            int jmlTrx = data.Count;
            int trxTunai = data.Count(t => (t.MetodeBayar ?? "").Equals("Tunai", StringComparison.OrdinalIgnoreCase));
            int trxNonTunai = jmlTrx - trxTunai;
            int jmlItem = data.Sum(t => t.JumlahItem);
            decimal rata = jmlTrx > 0 ? omzet / jmlTrx : 0;
            decimal laba = data.Sum(t => ExportService.LabaTransaksi(t, lookup));

            CardOmzet.Text = $"Rp {omzet:N0}";
            if (CardOmzetCash != null) CardOmzetCash.Text = $"Rp {omzetTunai:N0}";
            if (CardOmzetNonCash != null) CardOmzetNonCash.Text = $"Rp {omzetNonTunai:N0}";
            if (CardOmzetKeluar != null) CardOmzetKeluar.Text = $"Rp {totalKeluar:N0}";
            CardLaba.Text = $"Rp {laba:N0}";
            CardTransaksi.Text = jmlTrx.ToString();
            CardItem.Text = jmlItem.ToString();
            CardRata.Text = $"Rp {rata:N0}";

            if (DgPengeluaranLaporan != null) DgPengeluaranLaporan.ItemsSource = expList;

            var laris = ExportService.HitungProdukLaris(data, lookup);
            DgLaris.ItemsSource = laris;

            string periode = $"{DpDari.SelectedDate:dd/MM/yyyy} - {DpSampai.SelectedDate:dd/MM/yyyy}";
            TxtRingkasan.Text = jmlTrx == 0 && expList.Count == 0
                ? $"Periode {periode}: belum ada aktivitas transaksi maupun pengeluaran kas."
                : $"Periode {periode}:\n" +
                  $"• Total Omzet  : Rp {omzet:N0}\n" +
                  $"  - 💵 Tunai (Cash di Laci) : Rp {omzetTunai:N0} ({trxTunai} trx)\n" +
                  $"  - 📱 Non-Tunai (Digital)  : Rp {omzetNonTunai:N0} ({trxNonTunai} trx)\n" +
                  $"• 💸 Uang Keluar Drawer   : Rp {totalKeluar:N0} ({expList.Count} pengeluaran)\n" +
                  $"• Laba Estimasi: Rp {laba:N0}\n" +
                  $"• Transaksi    : {jmlTrx} nota ({jmlItem} item), rata-rata Rp {rata:N0}/nota" +
                  (laris.Count > 0 ? $"\n• Terlaris      : {laris[0].Nama} ({laris[0].Terjual} pcs)." : "");
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
            TxtKotaToko.Text = _settings.Kota;
            TxtTelpToko.Text = _settings.Telepon;
            TxtFooterStruk.Text = _settings.FooterStruk;

            if (ChkTampilkanLogo != null)
                ChkTampilkanLogo.IsChecked = _settings.TampilkanLogo;

            MuatPratinjauLogo();

            MuatDaftarPrinter();

            if (CmbLebarKertas != null)
            {
                if (_settings.PaperWidthMm == 76 || (_settings.PrinterName ?? "").ToUpperInvariant().Contains("U220"))
                    CmbLebarKertas.SelectedIndex = 1;
                else if (_settings.PaperWidthMm <= 58)
                    CmbLebarKertas.SelectedIndex = 2;
                else
                    CmbLebarKertas.SelectedIndex = 0;
            }

            if (ChkAutoCetakStruk != null)
                ChkAutoCetakStruk.IsChecked = _settings.AutoPrintReceipt;

            if (ChkKickDrawer != null)
                ChkKickDrawer.IsChecked = _settings.KickDrawer;

            if (ChkCetakSatuanStruk != null)
                ChkCetakSatuanStruk.IsChecked = _settings.CetakSatuanStruk;
        }

        // ===== LOGO STRUK =====
        private void MuatPratinjauLogo()
        {
            try
            {
                string? path = LogoService.ResolveLogoPath(_settings);
                TxtLogoPath.Text = string.IsNullOrWhiteSpace(_settings.LogoPath)
                    ? "(logo bawaan)"
                    : _settings.LogoPath;

                if (path != null && File.Exists(path))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(path, UriKind.Absolute);
                    bmp.EndInit();
                    ImgLogoPreview.Source = bmp;
                }
                else
                {
                    ImgLogoPreview.Source = null;
                }
            }
            catch
            {
                ImgLogoPreview.Source = null;
            }
        }

        private void BtnPilihLogo_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Pilih file logo toko",
                Filter = "Gambar (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|Semua file (*.*)|*.*"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                // Simpan salinan ke folder data supaya path stabil
                Directory.CreateDirectory(LogoService.DataDir);
                string ext = Path.GetExtension(dlg.FileName);
                string target = Path.Combine(LogoService.DataDir, "logo" + ext);

                if (File.Exists(target) && !string.Equals(Path.GetFullPath(dlg.FileName), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
                    File.Delete(target);

                if (!string.Equals(Path.GetFullPath(dlg.FileName), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase))
                    File.Copy(dlg.FileName, target, overwrite: true);

                _settings.LogoPath = target;
                MuatPratinjauLogo();

                MessageBox.Show("Logo berhasil dipasang. Klik 'Simpan Pengaturan' untuk menerapkan.",
                    "Logo Struk", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Gagal memasang logo: " + ex.Message, "Logo Struk", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnResetLogo_Click(object sender, RoutedEventArgs e)
        {
            _settings.LogoPath = "";
            MuatPratinjauLogo();
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

        private void BtnCetakContohStruk_Click(object sender, RoutedEventArgs e)
        {
            SimpanStatePengaturanKeSettings();

            var contoh = new Transaction
            {
                Id = $"CONTOH-{DateTime.Now:HHmmss}",
                Tanggal = DateTime.Now,
                KasirNama = _loggedInUser?.NamaLengkap ?? "KASIR",
                MetodeBayar = "Tunai",
                Items = new List<TransactionItem>
                {
                    new() { Kode = "CNT1", Nama = "Kulit Dimsum PAPIC 7cm/pcs", Harga = 15000m, Qty = 1 },
                    new() { Kode = "CNT2", Nama = "Belfoods Chicken Nugget 170gr", Harga = 24500m, Qty = 1 },
                    new() { Kode = "CNT3", Nama = "Fiesta Chicken Nugget 200g", Harga = 8500m, Qty = 2 }
                }
            };
            contoh.Total = contoh.Items.Sum(x => x.Harga * x.Qty);
            contoh.Bayar = 100000m;

            // Pratinjau dulu (tanpa membuang kertas); tombol "Cetak Struk"
            // di jendela pratinjau yang akan mengirim ke printer.
            var preview = new KasirKu.UI.StrukPreviewWindow(contoh, _settings,
                "Ini struk contoh. Periksa logo, alamat, dan kerapian kolom.")
            {
                Owner = this
            };
            preview.ShowDialog();
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
            _settings.Kota = TxtKotaToko?.Text?.Trim() ?? "";
            _settings.Telepon = TxtTelpToko?.Text?.Trim() ?? "";
            _settings.FooterStruk = TxtFooterStruk?.Text?.Trim() ?? "";
            _settings.TampilkanLogo = ChkTampilkanLogo?.IsChecked != false;
            _settings.PrinterName = CmbPrinterName?.Text?.Trim() ?? "";
            _settings.PaperWidthMm = CmbLebarKertas?.SelectedIndex switch
            {
                1 => 76,
                2 => 58,
                _ => 80
            };
            _settings.AutoPrintReceipt = ChkAutoCetakStruk?.IsChecked == true;
            _settings.KickDrawer = ChkKickDrawer?.IsChecked == true;
            _settings.CetakSatuanStruk = ChkCetakSatuanStruk?.IsChecked == true;
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

    public class EditableTrxItem : INotifyPropertyChanged
    {
        public string Kode { get; set; } = "";
        public string Nama { get; set; } = "";
        public string Satuan { get; set; } = "pcs";
        public decimal HargaBeli { get; set; }

        private decimal _harga;
        private int _qty;

        public decimal Harga
        {
            get => _harga;
            set
            {
                if (_harga != value)
                {
                    _harga = value;
                    OnPropertyChanged(nameof(Harga));
                    OnPropertyChanged(nameof(HargaRp));
                    OnPropertyChanged(nameof(Subtotal));
                    OnPropertyChanged(nameof(SubtotalRp));
                }
            }
        }

        public int Qty
        {
            get => _qty;
            set
            {
                if (_qty != value)
                {
                    _qty = value;
                    OnPropertyChanged(nameof(Qty));
                    OnPropertyChanged(nameof(Subtotal));
                    OnPropertyChanged(nameof(SubtotalRp));
                }
            }
        }

        public decimal Subtotal => Harga * Qty;
        public string SubtotalRp => $"Rp {Subtotal:N0}";
        public string HargaRp => $"Rp {Harga:N0}";

        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
