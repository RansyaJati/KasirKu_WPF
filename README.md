# KasirKu — BANYU FROZEN FOOD POS & MANAGER

Aplikasi Point of Sale & Manajemen toko berbasis **WPF .NET 10**, dipisah menjadi **2 aplikasi** yang berbagi data lewat file JSON bersama.
Tanpa database & tanpa NuGet — semua data disimpan di `Documents/KasirKu/data.json`.

---

## 1. Arsitektur 2 Aplikasi

```
KasirKu.slnx
├── KasirKu.Shared/          <- Class Library (Models + Services + Data Storage)
├── KasirKu.POS/             <- Aplikasi Kasir (Fullscreen, keyboard-first)
└── KasirKu.Manager/         <- Aplikasi Manajemen (Windowed, sidebar dashboard)
```

Kedua aplikasi berbagi **data.json** dan **settings.json** di `Documents/KasirKu/`.
Jika POS dan Manager dibuka bersamaan, tekan **F10 Refresh** di Manager untuk melihat data terbaru dari kasir.

---

## 2. Cara Menjalankan

```powershell
# dari folder solusi (yang berisi KasirKu.slnx)
dotnet build

# Jalankan aplikasi Kasir:
dotnet run --project KasirKu.POS

# Jalankan aplikasi Manajemen:
dotnet run --project KasirKu.Manager
```

Syarat: Windows + .NET 10 SDK + workload WPF.

File output otomatis:
```
Documents/KasirKu/data.json          <- data bersama (produk, transaksi, stok)
Documents/KasirKu/settings.json      <- profil toko (nama, alamat, telp)
Documents/KasirKu/Struk/Struk-*.pdf  <- struk per transaksi
Documents/KasirKu/Laporan/Laporan-*.pdf
Documents/KasirKu/Laporan/Laporan-*.csv
```

---

## 3. KasirKu.POS — Aplikasi Kasir

Fullscreen (`WindowStyle=None`, `WindowState=Maximized`). Dioptimalkan untuk scanner barcode dan keyboard.

### Halaman & Shortcut

| Tombol | Halaman / Fungsi |
|---|---|
| F1 | Kasir — scan barcode, keranjang, bayar |
| F2 | Cek Harga & Stok (read-only) |
| F3 | Riwayat Transaksi Hari Ini |
| F4 | Rekap Shift (omzet, tunai vs non-tunai) |
| F5 | Bayar |
| F6 | Reset Keranjang |
| F7 | Cetak Struk (Thermal / PDF) |
| F8 | Pengaturan Printer Thermal & Test Print |
| Esc | Tutup Modal / Kembali ke Kasir / Keluar (konfirmasi) |
| Enter di Scan | Masukkan barang ke keranjang |
| Enter di Bayar | Bayar langsung |

### Fitur:
- **Scan Barcode Cepat**: Kompatibel dengan semua scanner barcode USB (keyboard wedge) dan input manual. Format cepat: `3*BRG001` = qty 3. Auto-fokus kembali ke scanner setelah aksi.
- **Cetak Struk Thermal**: Langsung ke printer thermal (80mm / 58mm) via ESC/POS raw printing tanpa library eksternal.
- **Buka Laci Kasir (Cash Drawer)**: Otomatis memicu pulsa kick drawer (ESC p) saat selesai pembayaran tunai/cetak struk.
- **Pembayaran**: Tunai (pecahan cepat 10rb/20rb/50rb/100rb/Pas) & Non-tunai (QRIS, GoPay, OVO, DANA, ShopeePay, Transfer).
- **Validasi Stok Ketat**: Tidak bisa menjual melebihi stok yang tersedia.
- **Arsip Digital**: Otomatis menyimpan salinan struk PDF setiap transaksi.
- **Data Tersimpan Otomatis**: Tersinkronisasi ke `data.json` setelah setiap transaksi.
- **Rekap Shift**: Menghitung omzet, jumlah nota, perincian tunai vs non-tunai.

---

## 4. KasirKu.Manager — Aplikasi Manajemen

Windowed desktop dengan **sidebar navigasi** modern. Untuk pemilik toko / manajer.

### Halaman & Shortcut

| Tombol | Halaman / Fungsi |
|---|---|
| F1 | Dashboard (KPI hari ini: omzet, laba, transaksi, stok menipis) |
| F2 | Produk — CRUD master barang, margin otomatis |
| F3 | Stok — input masuk/keluar, log mutasi, koreksi |
| F4 | Riwayat — seluruh transaksi, detail nota, cetak ulang struk |
| F5 | Laporan — filter periode, produk terlaris, export PDF/CSV |
| F6 | Pengaturan — nama toko, alamat, telepon, footer struk |
| F7 | Cetak struk terpilih |
| F8 | Export Laporan PDF |
| F9 | Export Laporan Excel (CSV) |
| F10 | Refresh data dari disk |
| Esc | Keluar |

### Fitur:
- **Dashboard**: omzet, laba estimasi, jumlah transaksi hari ini, daftar stok menipis.
- **Produk CRUD**: tambah/edit/hapus barang, harga beli/jual, margin live (Rp + %), barcode, supplier, kelompok.
- **Stok**: restok supplier, retur, koreksi, log mutasi lengkap.
- **Laporan**: filter periode, 5 kartu KPI (omzet, laba, transaksi, item, rata-rata), top 10 terlaris + laba per produk, export PDF & CSV.
- **Pengaturan**: profil toko (nama/alamat/telp/footer) + pilihan printer thermal Windows, lebar kertas 80mm/58mm, tombol test print thermal, tombol buka cash drawer, dan manajemen akun pengguna (Kasir & Manager).
- **Persiapan Migrasi Database**: Arsitektur Repository Pattern (`IProductRepository`, `ITransactionRepository`, dsb.) siap ditukar ke MySQL / SQL Server di masa depan.
- Laba estimasi = sum((harga jual - harga beli) x qty) per transaksi.

---

## 5. Struktur Proyek

```
KasirKu.slnx
KasirKu.Shared/
  KasirKu.Shared.csproj     -> Class Library, net10.0
  Models/
    Product.cs               -> INotifyPropertyChanged, alias Kelompok/HargaJual/MarginStr
    CartItem.cs              -> Product + Qty, Subtotal = Harga * Qty
    Transaction.cs           -> snapshot nota + TransactionItem (+ HargaBeli snapshot)
    StockMovement.cs         -> log Masuk/Keluar + StoreSettings (profil toko)
  Services/
    SimplePdf.cs             -> penulis PDF manual (Helvetica/Helvetica-Bold)
    ExportService.cs         -> struk/laporan + laba + export PDF/CSV
    DataStorageService.cs    -> simpan/muat data.json + settings.json (System.Text.Json)
  Extensions.cs              -> D3() helper

KasirKu.POS/
  KasirKu.POS.csproj        -> WinExe, net10.0-windows, UseWPF, ref Shared
  App.xaml / App.xaml.cs     -> StartupUri=MainWindow.xaml
  MainWindow.xaml            -> UI Kasir fullscreen (4 halaman)
  MainWindow.xaml.cs         -> logika kasir (scan, keranjang, bayar, cetak, rekap)

KasirKu.Manager/
  KasirKu.Manager.csproj    -> WinExe, net10.0-windows, UseWPF, ref Shared
  App.xaml / App.xaml.cs     -> StartupUri=MainWindow.xaml
  MainWindow.xaml            -> UI Manager sidebar (6 halaman)
  MainWindow.xaml.cs         -> logika manajemen (dashboard, CRUD, stok, laporan)
```

---

## 6. Data & Sinkronisasi

- **data.json**: berisi produk, transaksi, mutasi stok, dan counter ID (NextProductId/NextTrx/NextMovementId).
- **settings.json**: profil toko (nama, alamat, telepon, footer struk).
- POS menyimpan data setelah setiap transaksi.
- Manager memuat data saat startup dan saat tekan **F10 Refresh**.
- Jika data.json rusak, otomatis di-backup ke `data.json.corrupt-*` dan dimulai dari data kosong (seed 8 produk contoh).

---

## 7. Contoh Data Bawaan

Jika belum ada `data.json`, POS otomatis membuat 8 produk contoh:
BRG001-BRG008 (Indomie Goreng, Kopi Kapal Api, Teh Botol Sosro, Chitato, Aqua, Roti Aoka, Buku Sidu, Pulpen Pilot).

---

## 8. Aturan Pengembangan

1. **Tanpa NuGet, tanpa DB.** Pakai BCL saja (`System.Text.Json` bawaan .NET).
2. **Stok lewat CatatMasuk/CatatKeluar.** Jangan ubah `p.Stok` langsung kecuali di koreksi produk yang sudah menulis TambahLog.
3. **Transaction = snapshot.** TransactionItem salin Kode/Nama/Harga/HargaBeli/Qty saat bayar.
4. **PDF: Helvetica only.** Jangan masukkan emoji. Struk 300pt (80mm), laporan A4 595x842.
5. **CSV: separator `;` + BOM UTF-8.**
6. **Bahasa UI Indonesia.**
