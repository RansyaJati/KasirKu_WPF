using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using KasirKu.Models;
using KasirKu.Services;

namespace KasirKu
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<Product> _products = new();
        private readonly ObservableCollection<CartItem> _cart = new();
        private readonly ObservableCollection<Transaction> _transactions = new();
        private readonly ObservableCollection<StockMovement> _movements = new();

        private Product? _editingProduct;
        private Transaction? _lastTrx;
        private int _nextProductId = 1;
        private int _nextTrx = 1;
        private int _nextMovementId = 1;
        private bool _ready;
        private string _halaman = "Kasir";

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            IsiContohProduk();
            DpDari.SelectedDate = DateTime.Today;
            DpSampai.SelectedDate = DateTime.Today;
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

            ShowPage("Kasir");
            FocusScan();
        }

        //  CONTOH DATA 
        private void IsiContohProduk()
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

        // NAVIGASI F1-F10 
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.F1: ShowPage("Kasir"); e.Handled = true; break;
                case Key.F2: ShowPage("Produk"); e.Handled = true; break;
                case Key.F3: ShowPage("Riwayat"); e.Handled = true; break;
                case Key.F4: ShowPage("Laporan"); e.Handled = true; break;
                case Key.F5: BtnBayar_Click(this, new RoutedEventArgs()); e.Handled = true; break;
                case Key.F6: BtnResetKeranjang_Click(this, new RoutedEventArgs()); e.Handled = true; break;
                case Key.F7: BtnCetakStruk_Click(this, new RoutedEventArgs()); e.Handled = true; break;
                case Key.F8: BtnExportPdf_Click(this, new RoutedEventArgs()); e.Handled = true; break;
                case Key.F9: BtnExportExcel_Click(this, new RoutedEventArgs()); e.Handled = true; break;
                case Key.F10: ShowPage("Stok"); e.Handled = true; break;
                case Key.Escape: BtnKeluar_Click(this, new RoutedEventArgs()); e.Handled = true; break;
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
            Close();
        }

        private void ShowPage(string page)
        {
            _halaman = page;
            PageKasir.Visibility = Visibility.Collapsed;
            PageProduk.Visibility = Visibility.Collapsed;
            PageRiwayat.Visibility = Visibility.Collapsed;
            PageLaporan.Visibility = Visibility.Collapsed;
            PageStok.Visibility = Visibility.Collapsed;

            string title = page, sub = "";
            switch (page)
            {
                case "Kasir":
                    PageKasir.Visibility = Visibility.Visible;
                    title = "Kasir (F1)";
                    sub = "Scan barcode / ketik kode atau nama + Enter. F5 = Bayar, F6 = Reset, F7 = Struk PDF.";
                    FocusScan();
                    break;
                case "Produk":
                    PageProduk.Visibility = Visibility.Visible;
                    title = "Data Produk (F2)";
                    sub = "Kelola barang: kode produk, barcode, satuan, harga beli/jual, margin, supplier & kelompok.";
                    break;
                case "Riwayat":
                    PageRiwayat.Visibility = Visibility.Visible;
                    title = "Riwayat Transaksi (F3)";
                    sub = "Klik baris untuk detail. F7 mencetak struk terpilih ke PDF.";
                    RefreshRiwayat();
                    break;
                case "Laporan":
                    PageLaporan.Visibility = Visibility.Visible;
                    title = "Laporan (F4)";
                    sub = "Filter periode, lalu F8 export PDF atau F9 export Excel (CSV).";
                    RefreshLaporan();
                    break;
                case "Stok":
                    PageStok.Visibility = Visibility.Visible;
                    title = "Stok (F10)";
                    sub = "Jejak barang masuk & keluar. Penjualan kasir tercatat otomatis sebagai Keluar.";
                    RefreshStok();
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
            BtnF2Produk.Background = _halaman == "Produk" ? act : def;
            BtnF3Riwayat.Background = _halaman == "Riwayat" ? act : def;
            BtnF4Laporan.Background = _halaman == "Laporan" ? act : def;
            BtnF10Stok.Background = _halaman == "Stok" ? act : def;
        }

        private void FocusScan()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try { TxtScan.Focus(); TxtScan.SelectAll(); } catch { }
            }), DispatcherPriority.Input);
        }

        // SCAN BARCODE + INPUT MANUAL
        private void TxtScan_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ProsesScan(TxtScan.Text);
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                DgKeranjang.Focus();
                e.Handled = true;
            }
        }

        private void TxtScan_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_ready) return;
            string key = (TxtScan?.Text ?? "").Trim();
            if (string.IsNullOrEmpty(key))
            {
                SetScanStatus("Siap scan...");
                return;
            }
            int n = _products.Count(p =>
                p.Kode.Contains(key, StringComparison.OrdinalIgnoreCase) ||
                p.Barcode.Contains(key, StringComparison.OrdinalIgnoreCase) ||
                p.Nama.Contains(key, StringComparison.OrdinalIgnoreCase));
            SetScanStatus(n == 0
                ? $"'{key}' tidak ditemukan. Cek Data Produk (F2)."
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

            if (string.IsNullOrEmpty(kata))
            {
                SetScanStatus("Kode kosong.", true);
                return;
            }

            // 1. Cari kecocokan persis pada Kode Produk atau Barcode
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

            // 2. Cari yang mengandung kode produk / barcode / nama
            var cocok = _products.Where(p =>
                p.Kode.Contains(kata, StringComparison.OrdinalIgnoreCase) ||
                p.Barcode.Contains(kata, StringComparison.OrdinalIgnoreCase) ||
                p.Nama.Contains(kata, StringComparison.OrdinalIgnoreCase)).ToList();

            if (cocok.Count == 1)
            {
                TxtScan.Text = "";
                TambahKeKeranjang(cocok[0], qty);
                FocusScan();
            }
            else if (cocok.Count > 1)
            {
                string contoh = string.Join(", ", cocok.Take(3).Select(p => p.Nama));
                SetScanStatus($"{cocok.Count} hasil untuk '{kata}' ({contoh}...) — persempit kata kunci.", true);
            }
            else
            {
                SetScanStatus($"'{kata}' tidak ditemukan. Cek Data Produk (F2).", true);
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

        //  KERANJANG 
        private void RefreshSemua()
        {
            RefreshProduk();
            RefreshKeranjang();
            RefreshRiwayat();
            RefreshStok();
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
            TxtInfoKeranjang.Text = $"{_cart.Sum(c => c.Qty)} item • {_cart.Count} jenis barang";

            // Mode non-tunai: bayar selalu = total
            if (MetodeAktif() != "Tunai" && _ready)
                TxtBayar.Text = total.ToString();

            HitungKembalian();
        }

        //  PEMBAYARAN (Tunai / QRIS / e-wallet) 
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

        private void TxtBayar_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_ready) return;
            HitungKembalian();
        }

        private void TxtBayar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnBayar_Click(this, new RoutedEventArgs());
                e.Handled = true;
            }
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
            // F5 di halaman lain: arahkan ke kasir
            if (_halaman != "Kasir" && _cart.Count == 0)
            {
                ShowPage("Kasir");
                return;
            }

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

            // Kurangi stok + catat tiap item sebagai barang Keluar
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
                    Qty = c.Qty
                }).ToList()
            };
            _transactions.Insert(0, trx);
            _lastTrx = trx;

            // Otomatis simpan struk PDF
            string pdfPath;
            try
            {
                pdfPath = ExportService.SimpanStrukPdf(trx);
            }
            catch (Exception ex)
            {
                pdfPath = "(gagal simpan PDF: " + ex.Message + ")";
            }

            string struk = ExportService.BuildStrukText(trx);
            MessageBox.Show(struk + $"\n\nStruk PDF: {pdfPath}\nMetode: {metode}",
                "Transaksi Berhasil", MessageBoxButton.OK, MessageBoxImage.Information);

            _cart.Clear();
            TxtBayar.Text = "";
            RefreshSemua();
            UpdateMetodeUI();
            FocusScan();
        }

        //  CETAK STRUK PDF (F7) 
        private void BtnCetakStruk_Click(object sender, RoutedEventArgs e)
        {
            Transaction? t = null;
            if (_halaman == "Riwayat" && DgRiwayat.SelectedItem is Transaction pilih)
                t = pilih;
            else
                t = _lastTrx ?? _transactions.FirstOrDefault();

            if (t == null)
            {
                MessageBox.Show("Belum ada transaksi untuk dicetak.", "Struk PDF", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string path = ExportService.SimpanStrukPdf(t);
                var jawab = MessageBox.Show($"Struk {t.Id} tersimpan:\n{path}\n\nBuka file sekarang?",
                    "Struk PDF", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (jawab == MessageBoxResult.Yes)
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Gagal membuat PDF: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        //  PRODUK (CRUD) 
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
            if (cmb.SelectedItem is ComboBoxItem it)
                return (it.Content?.ToString() ?? "").Trim();
            return fallback;
        }

        private static void SetComboText(ComboBox cmb, string value)
        {
            if (cmb == null) return;
            string v = (value ?? "").Trim();
            bool found = false;
            foreach (var obj in cmb.Items)
            {
                string c = obj is ComboBoxItem it ? (it.Content?.ToString() ?? "") : (obj?.ToString() ?? "");
                if (c.Equals(v, StringComparison.OrdinalIgnoreCase))
                {
                    cmb.SelectedItem = obj;
                    found = true;
                    break;
                }
            }
            cmb.Text = v;
            if (!found && cmb.SelectedIndex >= 0 && string.IsNullOrEmpty(v))
                cmb.SelectedIndex = 0;
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
            TxtMarginInfo.Text = (beli <= 0 || jual <= 0)
                ? "Margin: -"
                : $"Margin: Rp {margin:N0} ({persen:N1}%)";
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
            {
                MessageBox.Show("Kode Produk dan Nama wajib diisi!", "Validasi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrEmpty(barcode))
                barcode = kode; // default: barcode = kode produk bila kosong
            if (string.IsNullOrEmpty(satuan)) satuan = "Pcs";
            if (string.IsNullOrEmpty(supplier)) supplier = "Umum";
            if (!decimal.TryParse(TxtHargaBeli.Text.Trim(), out var hargaBeli) || hargaBeli < 0)
            {
                MessageBox.Show("Harga beli harus angka 0 atau lebih!", "Validasi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!decimal.TryParse(TxtHarga.Text.Trim(), out var hargaJual) || hargaJual <= 0)
            {
                MessageBox.Show("Harga jual harus angka lebih dari 0!", "Validasi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (hargaJual < hargaBeli)
            {
                var lanjut = MessageBox.Show(
                    $"Harga jual (Rp {hargaJual:N0}) lebih kecil dari harga beli (Rp {hargaBeli:N0}).\nTetap simpan?",
                    "Margin Minus", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (lanjut != MessageBoxResult.Yes) return;
            }
            if (!int.TryParse(TxtStok.Text.Trim(), out var stok) || stok < 0)
            {
                MessageBox.Show("Qty/Stok harus angka 0 atau lebih!", "Validasi", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_editingProduct == null)
            {
                if (_products.Any(p => p.Kode.Equals(kode, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show("Kode produk sudah dipakai produk lain!", "Validasi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (_products.Any(p => !string.IsNullOrEmpty(p.Barcode) && p.Barcode.Equals(barcode, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show("Kode barcode sudah dipakai produk lain!", "Validasi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var baru = new Product
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
                _products.Add(baru);
                if (stok > 0) CatatMasuk(baru, stok, "Stok awal produk baru");
                MessageBox.Show("Produk berhasil ditambahkan!", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                if (_products.Any(p => p.Id != _editingProduct.Id && p.Kode.Equals(kode, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show("Kode produk sudah dipakai produk lain!", "Validasi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (_products.Any(p => p.Id != _editingProduct.Id && !string.IsNullOrEmpty(p.Barcode) && p.Barcode.Equals(barcode, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show("Kode barcode sudah dipakai produk lain!", "Validasi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                int stokLama = _editingProduct.Stok;
                _editingProduct.Kode = kode;
                _editingProduct.Barcode = barcode;
                _editingProduct.Nama = nama;
                _editingProduct.Satuan = satuan;
                _editingProduct.Kategori = kelompok;
                _editingProduct.Supplier = supplier;
                _editingProduct.HargaBeli = hargaBeli;
                _editingProduct.Harga = hargaJual;
                _editingProduct.Stok = stok;
                int selisih = stok - stokLama;
                if (selisih > 0)
                    TambahLog(_editingProduct, "Masuk", selisih, stokLama, stok, $"Koreksi via Data Produk ({stokLama} → {stok})");
                else if (selisih < 0)
                    TambahLog(_editingProduct, "Keluar", -selisih, stokLama, stok, $"Koreksi via Data Produk ({stokLama} → {stok})");
                MessageBox.Show("Produk berhasil diupdate!", "Sukses", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            ClearFormProduk();
            RefreshSemua();
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
            if (_cart.Any(c => c.Product.Id == p.Id))
            {
                MessageBox.Show("Produk ada di keranjang, kosongkan keranjang dulu!", "Hapus", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (MessageBox.Show($"Hapus '{p.Nama}'?", "Konfirmasi", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                if (p.Stok > 0)
                    TambahLog(p, "Keluar", p.Stok, p.Stok, 0, "Produk dihapus");
                _products.Remove(p);
                if (_editingProduct?.Id == p.Id) ClearFormProduk();
                RefreshSemua();
            }
        }

        //  RIWAYAT 
        private void RefreshRiwayat()
        {
            string key = TxtCariRiwayat?.Text?.Trim().ToLower() ?? "";
            var data = string.IsNullOrEmpty(key)
                ? _transactions.ToList()
                : _transactions.Where(t => t.Id.ToLower().Contains(key) || t.Ringkasan.ToLower().Contains(key) || t.MetodeBayar.ToLower().Contains(key)).ToList();
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

        private void BtnHapusRiwayat_Click(object sender, RoutedEventArgs e)
        {
            if (_transactions.Count == 0) return;
            if (MessageBox.Show("Hapus semua riwayat transaksi?", "Konfirmasi", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _transactions.Clear();
                _lastTrx = null;
                RefreshRiwayat();
                UpdateInfoGlobal();
            }
        }

        //  LAPORAN + EXPORT PDF/EXCEL 
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
        {
            DpDari.SelectedDate = DateTime.Today;
            DpSampai.SelectedDate = DateTime.Today;
            RefreshLaporan();
        }

        private void BtnRefreshLaporan_Click(object sender, RoutedEventArgs e) => RefreshLaporan();

        private void RefreshLaporan()
        {
            if (!_ready || CardOmzet == null) return;
            var data = TransaksiTerfilter();
            decimal omzet = data.Sum(t => t.Total);
            int jmlTrx = data.Count;
            int jmlItem = data.Sum(t => t.JumlahItem);
            decimal rata = jmlTrx > 0 ? omzet / jmlTrx : 0;

            CardOmzet.Text = $"Rp {omzet:N0}";
            CardTransaksi.Text = jmlTrx.ToString();
            CardItem.Text = jmlItem.ToString();
            CardRata.Text = $"Rp {rata:N0}";

            var laris = data
                .SelectMany(t => t.Items)
                .GroupBy(i => i.Nama)
                .Select(g => new LarisRow { Nama = g.Key, Terjual = g.Sum(x => x.Qty), Omzet = g.Sum(x => x.Subtotal) })
                .OrderByDescending(x => x.Terjual)
                .Take(10)
                .ToList();
            DgLaris.ItemsSource = laris;

            string periode = $"{DpDari.SelectedDate:dd/MM/yyyy} - {DpSampai.SelectedDate:dd/MM/yyyy}";
            TxtRingkasan.Text = jmlTrx == 0
                ? $"Periode {periode}: belum ada transaksi."
                : $"Periode {periode}: {jmlTrx} transaksi, {jmlItem} item, omzet Rp {omzet:N0}, rata-rata Rp {rata:N0}/nota." +
                  (laris.Count > 0 ? $" Terlaris: {laris[0].Nama} ({laris[0].Terjual} pcs)." : "");

            UpdateInfoGlobal();
        }

        private void UpdateInfoGlobal()
        {
            if (TxtInfoGlobal == null) return;
            var hariIni = _transactions.Where(t => t.Tanggal.Date == DateTime.Today).ToList();
            TxtInfoGlobal.Text = $"{hariIni.Count} transaksi hari ini • Rp {hariIni.Sum(t => t.Total):N0}";
        }

        private void BtnExportPdf_Click(object sender, RoutedEventArgs e)
        {
            var data = TransaksiTerfilter();
            try
            {
                string path = ExportService.SimpanLaporanPdf(data, DpDari.SelectedDate, DpSampai.SelectedDate);
                TxtExportInfo.Text = $"PDF tersimpan: {path}";
                var jawab = MessageBox.Show($"Laporan PDF tersimpan ({data.Count} transaksi):\n{path}\n\nBuka file sekarang?",
                    "Export PDF", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (jawab == MessageBoxResult.Yes)
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Gagal export PDF: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnExportExcel_Click(object sender, RoutedEventArgs e)
        {
            var data = TransaksiTerfilter();
            try
            {
                string path = ExportService.SimpanLaporanExcel(data);
                TxtExportInfo.Text = $"Excel (CSV) tersimpan: {path}";
                var jawab = MessageBox.Show($"Laporan Excel (CSV) tersimpan ({data.Count} transaksi):\n{path}\n\nBuka file sekarang?",
                    "Export Excel", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (jawab == MessageBoxResult.Yes)
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Gagal export Excel: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        //  STOK MASUK / KELUAR 
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
            TxtStokSaatIni.Text = CmbStokProduk?.SelectedItem is Product p
                ? $"Stok saat ini: {p.Stok}"
                : "Stok saat ini: -";
        }

        private void TxtCariStok_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_ready) return;
            RefreshStok();
        }

        private void CmbFilterJenis_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_ready) return;
            RefreshStok();
        }

        private void CmbStokProduk_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_ready) return;
            UpdateStokSaatIni();
        }

        private void BtnSimpanStok_Click(object sender, RoutedEventArgs e)
        {
            if (CmbStokProduk.SelectedItem is not Product p)
            {
                MessageBox.Show("Pilih produk dulu!", "Stok", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            string jenis = (CmbJenisStok.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Masuk";
            if (!int.TryParse(TxtStokQty.Text.Trim(), out int qty) || qty <= 0)
            {
                MessageBox.Show("Jumlah harus angka lebih dari 0!", "Stok", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
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
                {
                    MessageBox.Show($"Stok tidak cukup! Sisa {p.Nama}: {p.Stok}", "Stok", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                CatatKeluar(p, qty, ket);
                MessageBox.Show($"Barang keluar: '{p.Nama}' -{qty} (stok: {p.Stok}).", "Stok", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            TxtStokQty.Text = "";
            RefreshSemua();
        }

        private void BtnHapusStokLog_Click(object sender, RoutedEventArgs e)
        {
            if (_movements.Count == 0) return;
            if (MessageBox.Show("Hapus semua log stok? (Stok produk tidak berubah)", "Konfirmasi", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _movements.Clear();
                RefreshStok();
            }
        }

        private class LarisRow
        {
            public string Nama { get; set; } = "";
            public int Terjual { get; set; }
            public decimal Omzet { get; set; }
            public string OmzetRp => $"Rp {Omzet:N0}";
        }
    }

    internal static class IntExtensions
    {
        public static string D3(this int v) => v.ToString("D3");
    }
}
