# AGENT.md — KasirKu (BANYU FROZEN FOOD POS)

Instruksi kerja untuk AI agent di repo ini. Baca file ini dulu sebelum edit code.

## 1. Ringkasan proyek

Aplikasi kasir fullscreen WPF .NET 10 untuk toko frozen food / kelontong.
Judul window: `KasirKu - POS Fullscreen`, `WindowStyle=None`, `WindowState=Maximized`.
Tanpa database, tanpa NuGet. Semua data in-memory (`ObservableCollection`).
Output file hanya PDF manual + CSV Excel-compatible.

## 2. Perintah cepat

```powershell
# dari folder solusi (yang berisi KasirKu.slnx)
dotnet build
dotnet run --project KasirKu
```

Syarat: Windows + .NET 10 SDK + workload WPF.
Jangan tambah package NuGet. Jangan ubah `TargetFramework` / `UseWPF`.

Output otomatis saat dipakai:
```
Documents/KasirKu/Struk/Struk-TRX-*.pdf
Documents/KasirKu/Laporan/Laporan-*.pdf
Documents/KasirKu/Laporan/Laporan-*.csv
```

## 3. Struktur (jangan ubah sembarangan)

```
KasirKu.slnx
KasirKu/
  KasirKu.csproj        -> WinExe, net10.0-windows, UseWPF true. Jangan tambah PackageReference.
  App.xaml / App.xaml.cs -> StartupUri="MainWindow.xaml". Biarkan minimal.
  MainWindow.xaml        -> SELURUH UI: 5 halaman + style + topbar + statusbar (~650 baris)
  MainWindow.xaml.cs     -> SELURUH logika (~1086 baris). Hati-hati edit.
  Models/
    Product.cs           -> INotifyPropertyChanged. Ada alias: Kategori==Kelompok, Harga==HargaJual
    CartItem.cs          -> Product + Qty, Subtotal = Harga * Qty
    Transaction.cs       -> snapshot nota: TransactionItem salin Kode/Nama/Harga/Qty
    StockMovement.cs     -> Jenis Masuk/Keluar + StokSebelum/Sesudah + Keterangan
  Services/
    SimplePdf.cs         -> penulis PDF manual (%PDF-1.4, Helvetica/Helvetica-Bold saja)
    ExportService.cs     -> BuildStrukLines/LaporanLines + SimpanStrukPdf/LaporanPdf/Excel + BuildStrukText
```

## 4. Aturan keras (wajib dipatuhi)

1. **Tanpa NuGet, tanpa DB.** Jangan tambah SQLite, EF, iText, PdfSharp, Newtonsoft, dsb. Pakai BCL saja (`System.Text.Json` boleh karena bawaan .NET).
2. **Semua perubahan stok WAJIB lewat 2 pintu ini** di `MainWindow.xaml.cs`:
   - `CatatMasuk(p, qty, keterangan)` — tambah stok + `TambahLog(Masuk)`
   - `CatatKeluar(p, qty, keterangan)` — kurangi stok + `TambahLog(Keluar)`
   Jangan ubah `p.Stok` langsung kecuali di `BtnSimpanProduk_Click` (koreksi) yang sudah menulis `TambahLog` selisihnya.
3. **Transaction adalah snapshot.** `TransactionItem` harus salin nilai saat bayar. Jangan simpan referensi `Product` di riwayat — kalau harga berubah, riwayat lama tidak boleh ikut berubah.
4. **Validasi stok kasir:** tolak jika `Stok<=0` atau `diKeranjang+qty > Stok`. Berlaku di `TambahKeKeranjang` dan `BtnQtyPlus_Click`.
5. **Pembayaran:** Tunai cek `bayar >= total`. Non-tunai (QRIS/GoPay/OVO/DANA) → `bayar = total`, `TxtBayar.IsEnabled=false`, wajib konfirmasi MessageBox sebelum simpan. Format ID: `TRX-yyyyMMdd-###` via `_nextTrx++.D3()`.
6. **PDF:** hanya font Helvetica / Helvetica-Bold. Jangan masukkan emoji (font standar tidak support — `SimplePdf.Sanitize()` membuangnya). Struk pakai `pageWidth:300` (kertas 80mm), laporan pakai A4 `595x842`.
7. **CSV Excel:** separator `;` + BOM UTF-8 supaya Excel Indonesia langsung jadi kolom. Jangan ganti ke `,`.
8. **Bahasa UI Indonesia.** MessageBox dan status pakai Bahasa Indonesia seperti kode existing.

## 5. Navigasi & shortcut (jangan merusak mapping ini)

`Window_PreviewKeyDown` di `MainWindow.xaml.cs`:
F1=Kasir, F2=Produk, F3=Riwayat, F4=Laporan, F5=`BtnBayar_Click`, F6=`BtnResetKeranjang_Click`, F7=`BtnCetakStruk_Click`, F8=`BtnExportPdf_Click`, F9=`BtnExportExcel_Click`, F10=Stok, Esc=`BtnKeluar_Click` (konfirmasi jika keranjang isi), Enter di `TxtScan`=`ProsesScan()`, Enter di `TxtBayar`=`BtnBayar_Click`.

`ShowPage(page)`: collapse 5 Grid (`PageKasir/Produk/Riwayat/Laporan/Stok`), show 1, update `TxtHeaderTitle/Sub`, `HighlightFButtons()`, panggil `RefreshRiwayat/Laporan/Stok` sesuai halaman. Pola multi-page tanpa Frame — jangan ganti ke NavigationService.

Scan `ProsesScan(raw)`: dukung `3*BRG001` (=qty 3). Prioritas: exact `Kode==kata || Barcode==kata` dulu, baru `Contains(kode/barcode/nama)`. Live hint di `TxtScan_TextChanged`.

## 6. Binding XAML yang sensitif (jangan rename tanpa cek XAML)

- `Product.Kelompok` (alias Kategori), `HargaJualRp`/`HargaRp`, `MarginStr`, `DisplayStok` dipakai di DataGrid/ComboBox.
- `CmbStokProduk.DisplayMemberPath=DisplayStok`.
- Nama kontrol di `MainWindow.xaml`: `TxtScan/TxtScanStatus/DgKeranjang/TxtTotal/CmbMetode/TxtBayar/TxtKembalian/TxtMetodeInfo`, `DgProduk/TxtKode/TxtBarcode/TxtNama/CmbSatuan/TxtHargaBeli/TxtHarga/TxtStok/CmbSupplier/CmbKategori/TxtMarginInfo`, `DgRiwayat/DgRiwayatDetail/TxtDetailJudul/TxtDetailTotal`, `DpDari/DpSampai/CardOmzet/CardTransaksi/CardItem/CardRata/DgLaris/TxtRingkasan/TxtExportInfo`, `DgStok/CmbStokProduk/CmbJenisStok/TxtStokQty/CmbStokKet/TxtStokSaatIni/CardStokMasuk/CardStokKeluar`, `TxtJam/TxtTanggal/TxtInfoGlobal/TxtHeaderTitle/TxtHeaderSub`.
- `DataGrid` global `IsReadOnly` — edit hanya lewat form. Jangan aktifkan edit sel langsung.

## 7. Cara tambah fitur dengan aman

- Fitur kecil: edit method terkait saja, lalu `RefreshSemua()` / `RefreshKeranjang()` / `UpdateInfoGlobal()`.
- State ada 4: `_products`, `_cart`, `_transactions`, `_movements` + counter `_nextProductId/_nextTrx/_nextMovementId`. Kalau tambah persistensi, simpan counter juga agar ID tidak duplikat setelah restart.
- Export baru: tambah builder `List<PdfLine>` di `ExportService`, tulis via `SimplePdf.WriteLines`. Buka file opsional via `Process.Start(UseShellExecute:true)`.
- Selalu `dotnet build` setelah edit XAML/code-behind. Error binding XAML biasanya muncul saat build/run.

## 8. Backlog resmi (kerjakan jika diminta user)

1. Persistensi JSON: simpan `products/transactions/movements` + counter ke `Documents/KasirKu/data.json` saat `Close()`, load saat `Loaded()`. Jika file rusak → fallback `IsiContohProduk()`. Tetap tanpa NuGet (pakai `System.Text.Json`).
2. Login/kasir multi-user, diskon/pajak, cetak thermal langsung, logo/QR gambar di PDF (butuh library — konfirmasi user dulu karena melanggar aturan no-NuGet).

## 9. Checklist sebelum selesai

- [ ] `dotnet build` sukses dari folder solusi.
- [ ] Tidak ada `PackageReference` baru di csproj.
- [ ] Stok masih konsisten (cek halaman Stok F10 setelah jual/edit/hapus).
- [ ] Struk & laporan PDF tetap kebuka normal, CSV tetap `;` + BOM.
- [ ] Shortcut F1–F10 + Esc masih sesuai tabel di README.
