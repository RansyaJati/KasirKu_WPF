# KasirKu — BANYU FROZEN FOOD POS

Aplikasi kasir (Point of Sale) fullscreen berbasis **WPF .NET 10** untuk toko frozen food / kelontong.
Tanpa database & tanpa NuGet — semua data in-memory (`ObservableCollection`), struk & laporan dibuat jadi PDF murni + CSV Excel-compatible.

> Judul window: `KasirKu - POS Fullscreen` • Fullscreen `Maximized`, `WindowStyle=None`

---

## 1. Cara Menjalankan

```powershell
# dari folder solusi KasirKu/
dotnet build
dotnet run --project KasirKu
```

Syarat: Windows + .NET 10 SDK + workload WPF.

File output otomatis:
```
Documents/KasirKu/Struk/Struk-TRX-*.pdf
Documents/KasirKu/Laporan/Laporan-*.pdf
Documents/KasirKu/Laporan/Laporan-*.csv
```

---

## 2. Shortcut Keyboard (F1–F10 + Esc)

| Tombol | Kode di `MainWindow.xaml.cs: Window_PreviewKeyDown` | Fungsi |
|---|---|---|
| F1 | `ShowPage("Kasir")` | Halaman Kasir |
| F2 | `ShowPage("Produk")` | Data Produk |
| F3 | `ShowPage("Riwayat")` | Riwayat Transaksi |
| F4 | `ShowPage("Laporan")` | Laporan |
| F5 | `BtnBayar_Click()` | Bayar |
| F6 | `BtnResetKeranjang_Click()` | Reset keranjang |
| F7 | `BtnCetakStruk_Click()` | Cetak struk PDF |
| F8 | `BtnExportPdf_Click()` | Export laporan PDF |
| F9 | `BtnExportExcel_Click()` | Export laporan Excel (CSV) |
| F10 | `ShowPage("Stok")` | Log Stok Masuk/Keluar |
| Esc | `BtnKeluar_Click()` | Keluar (konfirmasi jika keranjang isi) |
| Enter di `TxtScan` | `ProsesScan()` | Masukkan barang ke keranjang |
| Enter di `TxtBayar` | `BtnBayar_Click()` | Bayar langsung |

---

## 3. Struktur Proyek

```
KasirKu.slnx
KasirKu/
  KasirKu.csproj        -> konfigurasi project WPF
  App.xaml / App.xaml.cs -> entry point, StartupUri="MainWindow.xaml"
  MainWindow.xaml        -> seluruh UI (5 halaman + style + topbar + statusbar)
  MainWindow.xaml.cs     -> seluruh logika (1100+ baris)
  Models/
    Product.cs           -> barang + harga beli/jual + margin + stok
    CartItem.cs          -> 1 baris keranjang (Product + Qty + Subtotal)
    Transaction.cs       -> nota + TransactionItem
    StockMovement.cs     -> 1 baris log stok Masuk/Keluar
  Services/
    ExportService.cs     -> bangun struk/laporan + simpan PDF/CSV
    SimplePdf.cs         -> penulis PDF manual tanpa library
```

---

## 4. Penjelasan Tiap Bagian Penting — "Kode Ini Apa?"

### 4.1 `KasirKu.csproj` — Konfigurasi Project
```xml
<OutputType>WinExe</OutputType>
<TargetFramework>net10.0-windows</TargetFramework>
<UseWPF>true</UseWPF>
```
**Ini kode apa?** Ini bukan logika, tapi "KTP" aplikasi: bilang ke .NET bahwa ini aplikasi desktop Windows (`WinExe`), pakai .NET 10 khusus Windows, dan aktifkan WPF. Tanpa `<UseWPF>true` maka file `.xaml` tidak akan dikenali.

### 4.2 `App.xaml` — Pintu Masuk Aplikasi
```xml
<Application StartupUri="MainWindow.xaml">
```
**Ini kode apa?** Perintah "saat aplikasi dibuka, langsung tampilkan `MainWindow.xaml`". `App.xaml.cs` kosong karena semua inisialisasi dilakukan di `MainWindow_Loaded`.

### 4.3 `Models/Product.cs` — Data Barang
```csharp
public class Product : INotifyPropertyChanged
public string Kode { get; set; }      // kode internal, ex: BRG001
public string Barcode { get; set; }   // kode scanner, ex: 8991111110011
public decimal HargaBeli { get; set; }// modal
public decimal Harga { get; set; }    // harga jual (= HargaJual alias)
public int Stok { get; set; }
public decimal Margin => Harga - HargaBeli;
public string MarginStr => $"Rp {Margin:N0} ({MarginPersen:N1}%)";
```
**Ini kode apa?** Cetakan data 1 produk. `INotifyPropertyChanged` = agar DataGrid otomatis refresh saat harga/stok diubah. Ada alias `Kategori == Kelompok` dan `Harga == HargaJual` supaya binding XAML (`{Binding Kelompok}`, `{Binding HargaJualRp}`) tidak error.

### 4.4 `Models/CartItem.cs` — 1 Baris Keranjang
```csharp
public Product Product { get; set; }
public int Qty { get; set; }
public decimal Subtotal => Harga * Qty;
```
**Ini kode apa?** Pembungkus "produk + jumlah beli". `Subtotal` selalu dihitung ulang (`Harga x Qty`), jadi tombol `+ / −` di keranjang cukup ubah `Qty`.

### 4.5 `Models/Transaction.cs` — Nota / Struk
```csharp
public class Transaction {
  public string Id { get; set; } // TRX-20250922-001
  public DateTime Tanggal { get; set; }
  public List<TransactionItem> Items { get; set; }
  public decimal Total { get; set; }
  public decimal Bayar { get; set; }
  public decimal Kembalian => Bayar - Total;
  public string MetodeBayar { get; set; } // Tunai, QRIS, GoPay...
}
```
**Ini kode apa?** Snapshot permanen 1 penjualan. `TransactionItem` menyalin `Kode/Nama/Harga/Qty` saat bayar, jadi kalau harga produk diubah besok, riwayat lama tidak ikut berubah.

### 4.6 `Models/StockMovement.cs` — Jejak Stok
```csharp
public string Jenis { get; set; } // "Masuk" atau "Keluar"
public int Qty { get; set; }
public int StokSebelum { get; set; }
public int StokSesudah { get; set; }
public string Perubahan => Jenis=="Masuk" ? $"+{Qty}" : $"-{Qty}";
```
**Ini kode apa?** Buku kas stok. Setiap tambah produk baru, koreksi stok, penjualan kasir, atau input manual F10 selalu menulis 1 baris ini. Dipakai untuk kartu `Masuk Hari Ini / Keluar Hari Ini` di halaman Stok.

### 4.7 `Services/SimplePdf.cs` — Bikin PDF Tanpa NuGet
```csharp
SimplePdf.WriteLines(path, lines, pageWidth:300, pageHeight:height, margin:14);
```
**Ini kode apa?** Penulis file PDF dari nol (header `%PDF-1.4`, objek `Catalog/Pages/Page/Content/Font`, `xref`, `trailer`). Font hanya `Helvetica / Helvetica-Bold` bawaan PDF jadi tidak perlu embed. `Paginate()` memecah baris jadi beberapa halaman, `Sanitize()` membuang emoji karena font PDF standar tidak support emoji. Dipakai untuk struk kertas 80mm (`pageWidth:300`) dan laporan A4 (`595x842`).

### 4.8 `Services/ExportService.cs` — Isi Struk & Laporan
```csharp
ExportService.SimpanStrukPdf(trx) // -> Documents/KasirKu/Struk/Struk-*.pdf
ExportService.SimpanLaporanPdf(data, dari, sampai)
ExportService.SimpanLaporanExcel(data) // CSV ; separator agar Excel Indonesia langsung kolom
ExportService.BuildStrukText(trx) // teks untuk MessageBox "Transaksi Berhasil"
```
**Ini kode apa?** "Tukang cetak". `BuildStrukLines()` menyusun `List<PdfLine>` (judul toko, ID, item `Qty x Harga = Subtotal`, Total/Bayar/Kembalian). `BuildLaporanLines()` menghitung omzet, rata-rata/nota, grup per metode bayar, dan top produk terlaris. `SimpanLaporanExcel()` menulis CSV dengan `;` + BOM UTF-8 supaya dibuka di Excel langsung rapi.

### 4.9 `MainWindow.xaml` — Seluruh Tampilan (650 baris)
Bagian penting:

```xml
<Window WindowStyle="None" WindowState="Maximized" PreviewKeyDown="Window_PreviewKeyDown">
```
**Ini kode apa?** Kunci fullscreen kasir: hilangkan title-bar Windows, langsung maksimal, dan tangkap semua tombol F1-F10 di level window.

```xml
<Style x:Key="FButton" TargetType="Button"> ... </Style>
<Style x:Key="PrimaryButton" ...> <Style x:Key="SuccessButton" BasedOn="PrimaryButton">
<Style x:Key="Card" TargetType="Border"> <!-- kartu putih rounded-14 -->
<Style TargetType="DataGrid"> <!-- IsReadOnly, AlternatingRow, header abu -->
```
**Ini kode apa?** Tema global. `FButton` = tombol hitam topbar yang biru saat hover/aktif. `Primary/Success/Danger/Ghost` = tombol aksi. `Card` = panel putih. Style `DataGrid` mematikan edit langsung supaya edit hanya lewat form.

```xml
<Border Background="#0F172A"> <!-- TOPBAR -->
  <Button Content="F1 • Kasir" Tag="Kasir" Click="FNav_Click"/>
  <Button Content="F5 • Bayar" Click="BtnBayar_Click"/>
  <Button Content="⏻ Keluar (Esc)" Click="BtnKeluar_Click"/>
<TextBlock x:Name="TxtJam"/> <TextBlock x:Name="TxtTanggal"/>
```
**Ini kode apa?** Bar atas: judul toko + jam hidup + 10 tombol navigasi/aksi. `Tag="Kasir"` dipakai `FNav_Click` untuk tahu halaman tujuan.

```xml
<Grid x:Name="PageKasir"> <Grid x:Name="PageProduk" Visibility="Collapsed"> ...
```
**Ini kode apa?** 5 halaman ditumpuk dalam 1 window, hanya 1 yang `Visible`. `ShowPage()` di code-behind yang mengatur buka/tutupnya. Ini pola "multi-page tanpa Frame/Navigation".

Halaman Kasir:
```xml
<DataGrid x:Name="DgKeranjang" ... Binding Nama/HargaRp/Qty/SubtotalRp />
<TextBox x:Name="TxtScan" KeyDown="TxtScan_KeyDown"/>
<ComboBox x:Name="CmbMetode">Tunai, QRIS, GoPay, OVO, DANA...</ComboBox>
<TextBox x:Name="TxtBayar" KeyDown="TxtBayar_KeyDown"/>
<TextBlock x:Name="TxtTotal"/> <TextBlock x:Name="TxtKembalian"/>
```
**Ini kode apa?** Kiri = tabel belanja + tombol `+ / − / X` per baris. Kanan = kolom scan + total + metode + uang bayar + kembalian + tombol `F5 BAYAR / F6 Reset`.

Halaman lain: `PageProduk` (DgProduk + form Kode/Barcode/Nama/Satuan/HargaBeli/Stok/Supplier/HargaJual/Margin/Kelompok), `PageRiwayat` (DgRiwayat + DgRiwayatDetail + BtnCetakStruk), `PageLaporan` (DpDari/DpSampai + CardOmzet/CardTransaksi/CardItem/CardRata + DgLaris), `PageStok` (DgStok + CmbStokProduk + CmbJenisStok + TxtStokQty).

### 4.10 `MainWindow.xaml.cs` — Otak Aplikasi (1086 baris)

**a) State + Load awal (`MainWindow_Loaded`):**
```csharp
ObservableCollection<Product> _products; // master barang
ObservableCollection<CartItem> _cart;     // keranjang aktif
ObservableCollection<Transaction> _transactions; // semua nota
ObservableCollection<StockMovement> _movements;  // semua log stok
IsiContohProduk(); // 8 produk demo Indomie, Kopi, Teh Botol...
DispatcherTimer // jam tick tiap 1 detik -> TxtJam
ShowPage("Kasir"); FocusScan();
```
**Ini kode apa?** Menyiapkan 4 "tabel" in-memory, mengisi contoh barang, menyetel filter laporan = hari ini, dan menyalakan jam. `ObservableCollection` = list yang otomatis update UI saat tambah/hapus.

**b) Navigasi (`ShowPage`, `FNav_Click`, `HighlightFButtons`):**
```csharp
PageKasir.Visibility = Collapsed; ... PageKasir.Visibility = Visible;
TxtHeaderTitle.Text = "Kasir (F1)";
```
**Ini kode apa?** Saklar halaman. Sembunyikan semua, tampilkan 1, ganti judul/sub-judul, warnai tombol F aktif jadi biru, refresh data halaman tujuan (`RefreshRiwayat/Laporan/Stok`).

**c) Scan barcode (`ProsesScan`, `TxtScan_TextChanged`):**
```csharp
// exact dulu: Kode == kata || Barcode == kata
// kalau tidak ketemu: Contains(kode/barcode/nama)
// format cepat: "3*BRG001" => qty 3
TambahKeKeranjang(exact, qty);
```
**Ini kode apa?** Jantung kasir. Mendukung scanner fisik (yang mengetik kode + Enter) maupun ketik manual. Live-search menampilkan `"{n} produk cocok — tekan Enter"`. Prioritas kecocokan persis dulu agar scan barcode tidak nyasar.

**d) Keranjang (`TambahKeKeranjang`, `BtnQtyPlus/Min`, `BtnHapusItem`, `RefreshKeranjang`):**
```csharp
if (p.Stok <= 0) -> tolak "Stok habis"
if (diKeranjang + qty > p.Stok) -> tolak "Stok tidak cukup"
TotalBelanja() => _cart.Sum(c => c.Subtotal);
```
**Ini kode apa?** Validasi stok ketat: tidak bisa jual melebihi stok. `RefreshKeranjang()` selalu hitung ulang `TxtTotal` + `"{x} item • {y} jenis"` + kembalian. Mode non-tunai otomatis `TxtBayar = total`.

**e) Pembayaran (`BtnBayar_Click`):**
```csharp
string trxId = $"TRX-{DateTime.Now:yyyyMMdd}-{_nextTrx++.D3()}";
foreach (var c in _cart) CatatKeluar(c.Product, c.Qty, $"Penjualan {trxId}");
_transactions.Insert(0, trx);
ExportService.SimpanStrukPdf(trx); // auto-simpan
_cart.Clear(); RefreshSemua();
```
**Ini kode apa?** Transaksi F5: cek keranjang kosong, cek uang kurang (khusus Tunai), konfirmasi QRIS/e-wallet, buat ID nota, kurangi stok + catat log Keluar per item, simpan nota, auto-bikin PDF struk, tampilkan struk teks, kosongkan keranjang.

**f) Produk CRUD (`BtnSimpanProduk_Click`, `BtnHapusProduk_Click`):**
```csharp
if (barcode == "") barcode = kode;
if (hargaJual < hargaBeli) -> konfirmasi "Margin Minus?"
selisih = stokBaru - stokLama -> TambahLog(Masuk/Keluar, "Koreksi via Data Produk")
```
**Ini kode apa?** Tambah/edit dengan validasi duplikat Kode & Barcode, harga harus angka, stok >=0. Edit stok otomatis menulis log koreksi sehingga jejak stok tidak putus. Hapus ditolak jika barang masih di keranjang.

**g) Riwayat (`RefreshRiwayat`, `DgRiwayat_SelectionChanged`, `BtnCetakStruk_Click`):**
```csharp
DgRiwayatDetail.ItemsSource = t.Items;
TxtDetailTotal.Text = $"Metode... Total... Bayar... Kembali...";
```
**Ini kode apa?** Daftar nota + pencarian ID/isi/metode. Klik baris menampilkan detail item. F7 mencetak ulang struk nota terpilih (atau terakhir).

**h) Laporan (`TransaksiTerfilter`, `RefreshLaporan`, `BtnExportPdf/Excel_Click`):**
```csharp
q.Where(t.Tanggal.Date >= dari && <= sampai);
CardOmzet, CardTransaksi, CardItem, CardRata
laris = GroupBy(Nama).OrderByDescending(Terjual).Take(10)
```
**Ini kode apa?** Filter periode via `DatePicker`, hitung 4 kartu KPI, top-10 terlaris, ringkasan kalimat otomatis, export PDF/CSV + tawarkan "Buka file sekarang?" via `Process.Start(UseShellExecute:true)`.

**i) Stok F10 (`CatatMasuk/Keluar`, `TambahLog`, `BtnSimpanStok_Click`, `RefreshStok`):**
```csharp
p.Stok += qty; TambahLog(p,"Masuk",...);
p.Stok -= qty; TambahLog(p,"Keluar",...);
CardStokMasuk / CardStokKeluar // total hari ini
```
**Ini kode apa?** Semua perubahan stok lewat 2 pintu ini agar log konsisten. Penjualan kasir otomatis `Keluar`, stok awal & koreksi produk juga tercatat. Filter `Semua/Masuk/Keluar` + pencarian kode/nama/keterangan.

---

## 5. Alur Pakai Cepat (Demo ke Dosen)

1. F1 Kasir → scan `8991111110011` + Enter (atau ketik `indomie`), coba `3*BRG001`.
2. Klik `+ / − / X` di tabel, pilih metode `QRIS`, tekan F5 → struk PDF otomatis tersimpan.
3. F2 Produk → tambah barang baru, lihat `Margin: Rp X (Y%)` update live.
4. F10 Stok → pilih produk, `Masuk 20 Restok supplier` → Simpan.
5. F3 Riwayat → klik nota → F7 cetak ulang struk.
6. F4 Laporan → Terapkan periode → F8 PDF / F9 Excel → buka di `Documents/KasirKu/Laporan/`.

Contoh data bawaan: BRG001–BRG008 (Indomie, Kopal Api, Teh Botol, Chitato, Aqua, Aoka, Sidu, Pilot).

---

## 6. Keterbatasan & Ide Pengembangan

* Data hilang saat aplikasi ditutup (belum ada SQLite/JSON persistensi).
* Belum ada login/kasir multi-user, diskon/pajak, dan cetak thermal langsung.
* PDF hanya teks (tanpa logo/QR gambar) karena tanpa library eksternal.

Next: simpan `products/transactions/movements` ke JSON di `Documents/KasirKu/data.json` saat `Close()`, load saat `Loaded()`.

