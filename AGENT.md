# AGENT.md — KasirKu (BANYU FROZEN FOOD POS & MANAGER)

Instruksi kerja untuk AI agent di repo ini. Baca file ini dulu sebelum edit code.

## 1. Ringkasan proyek

Aplikasi kasir + manajemen toko WPF .NET 10, dipisah jadi **2 aplikasi** dalam 1 solution:
- **KasirKu.POS** — kasir fullscreen, keyboard-first, scan barcode, bayar, struk.
- **KasirKu.Manager** — manajemen windowed sidebar, dashboard, CRUD produk, stok, laporan, pengaturan.

Kedua app berbagi **KasirKu.Shared** (Models, Services, DataStorage) dan file `Documents/KasirKu/data.json`.

## 2. Perintah cepat

```powershell
dotnet build                           # build semua
dotnet run --project KasirKu.POS       # jalankan kasir
dotnet run --project KasirKu.Manager   # jalankan manajemen
```

Syarat: Windows + .NET 10 SDK + workload WPF.
Jangan tambah package NuGet.

## 3. Struktur

```
KasirKu.slnx
KasirKu.Shared/                  <- net10.0 classlib
  Models/Product.cs, CartItem.cs, Transaction.cs, StockMovement.cs (+ StoreSettings), User.cs
  Services/SimplePdf.cs, ExportService.cs, DataStorageService.cs, AuthService.cs,
           RawPrinterHelper.cs, ThermalPrintService.cs
  Data/IProductRepository.cs, ITransactionRepository.cs, IStockMovementRepository.cs,
       ISettingsRepository.cs, IUserRepository.cs
  Repositories/Json/*Repository.cs (Persiapan migrasi ke MySQL/SQL Server)
  Extensions.cs (D3())

KasirKu.POS/                     <- net10.0-windows WinExe, ref Shared
  App.xaml, MainWindow.xaml/.cs   <- 4 halaman: Kasir, Cek, Riwayat, Rekap

KasirKu.Manager/                 <- net10.0-windows WinExe, ref Shared
  App.xaml, MainWindow.xaml/.cs   <- 6 halaman: Dashboard, Produk, Stok, Riwayat, Laporan, Pengaturan
```

## 4. Aturan keras

1. **Tanpa NuGet, tanpa DB.** `System.Text.Json` bawaan .NET boleh. Tidak lain.
2. **Stok via CatatMasuk/CatatKeluar + TambahLog.** Jangan ubah `p.Stok` langsung kecuali di koreksi Data Produk yang sudah menulis TambahLog selisih.
3. **Transaction = snapshot.** TransactionItem salin Kode/Nama/Harga/HargaBeli/Qty. Jangan simpan referensi Product.
4. **Validasi stok kasir**: tolak jika Stok<=0 atau diKeranjang+qty > Stok.
5. **Pembayaran**: Tunai cek bayar >= total. Non-tunai -> bayar = total, TxtBayar disabled, konfirmasi MessageBox. ID: `TRX-yyyyMMdd-###`.
6. **PDF**: Helvetica/Helvetica-Bold only. Jangan emoji. Struk 300pt, laporan A4 595x842.
7. **CSV**: separator `;` + BOM UTF-8.
8. **Bahasa UI Indonesia.**
9. **Data bersama**: data.json + settings.json di Documents/KasirKu/. POS simpan setelah bayar. Manager refresh manual (F10).

## 5. Navigasi & shortcut

### KasirKu.POS (Window_PreviewKeyDown)
F1=Kasir, F2=Cek Barang (read-only), F3=Riwayat Hari Ini, F4=Rekap Shift,
F5=BtnBayar_Click, F6=BtnResetKeranjang_Click, F7=BtnCetakStruk_Click,
F8=BukaModalPrinter, Esc=Tutup Modal / Kembali ke Kasir / Keluar (konfirmasi).
Enter di TxtScan=ProsesScan(), Enter di TxtBayar=BtnBayar_Click.

### KasirKu.Manager (Window_PreviewKeyDown)
F1=Dashboard, F2=Produk, F3=Stok, F4=Riwayat, F5=Laporan, F6=Pengaturan,
F7=Cetak Struk, F8=Export PDF, F9=Export Excel, F10=Refresh Data, Esc=Close.

## 6. Binding XAML yang sensitif (jangan rename tanpa cek)

Model properties yang dipakai XAML binding:
- Product: Kode, Barcode, Nama, Satuan, Kelompok, Supplier, Stok, HargaJualRp, HargaBeliRp, HargaRp, MarginStr, DisplayStok
- CartItem: Nama, HargaRp, Qty, SubtotalRp
- Transaction: Id, TanggalStr, MetodeBayar, JumlahItem, TotalRp, Ringkasan
- TransactionItem: Nama, Qty, SubtotalRp
- StockMovement: TanggalStr, Kode, Nama, Jenis, Perubahan, StokSesudah, Keterangan
- ProdukLarisRow: Nama, Terjual, OmzetRp, LabaRp

Manager kontrol: DgProduk, DgStok, DgRiwayat, DgRiwayatDetail, DgLaris, DgStokTipis,
TxtKode/Barcode/Nama/HargaBeli/Harga/Stok, CmbSatuan/Supplier/Kategori,
DpDari/DpSampai, CardOmzet/Laba/Transaksi/Item/Rata, CmbStokProduk/JenisStok/StokKet,
TxtNamaToko/AlamatToko/TelpToko/FooterStruk.

POS kontrol: TxtScan/ScanStatus, DgKeranjang, TxtTotal/Kembalian, CmbMetode, TxtBayar,
DgRiwayat/RiwayatDetail, CardOmzetShift/TrxShift/ItemShift/Tunai/NonTunai, DgCek.

## 7. Cara tambah fitur

- Shared: tambah model/service di KasirKu.Shared, namespace KasirKu.Shared.Models/Services.
- POS: edit MainWindow.xaml/.cs di KasirKu.POS. Simpan ke disk via SimpanKeDisk() setelah mutasi.
- Manager: edit MainWindow.xaml/.cs di KasirKu.Manager. Simpan via SimpanKeDisk(). Refresh via MuatDariDisk().
- Export: tambah builder List<PdfLine> di ExportService, tulis via SimplePdf.WriteLines.
- Selalu `dotnet build` setelah edit.

## 8. Checklist sebelum selesai

- [ ] `dotnet build` sukses dari folder solusi.
- [ ] Tidak ada `PackageReference` baru di csproj manapun.
- [ ] Stok konsisten (cek setelah jual/edit/hapus).
- [ ] PDF/CSV tetap berfungsi (struk + laporan).
- [ ] Shortcut sesuai tabel di atas.
- [ ] data.json tersimpan setelah mutasi di POS dan Manager.
