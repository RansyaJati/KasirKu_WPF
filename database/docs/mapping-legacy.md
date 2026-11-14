# Pemetaan Database Lama → Skema KasirKu

Dokumen kerja untuk Anda sendiri saat menormalisasi database klien.
Isi tabel pemetaan setelah struktur database lama diketahui.

---

## 1. Alur kerja yang disarankan

```
dump lama (read-only)
        │
        ▼
kasirku_legacy (MySQL)  atau  CSV di legacy/csv/ (SQL Server)
        │
        ▼
kasirku_staging.stg_*  ← bersihkan format: tanggal, angka, spasi, duplikat
        │
        ▼
kasirku.*  ← hasil normalisasi 3NF
```

Aturan:

1. **Jangan menyunting sumber.** Kalau ada data salah, perbaiki di staging.
2. **Satu batch per file.** Catat di `import_batches`.
3. **`legacy_id` wajib ada dan unik** untuk produk, nota, dan user.
4. **Normalisasi lookup dulu** (kategori, satuan, supplier, metode bayar,
   jenis mutasi), baru produk/nota.
5. **Jangan mengimpor hash password lama.** Buat akun baru atau gunakan
   password sementara dari `tools/New-KasirKuPasswordHash.ps1`.

---

## 2. Format data yang harus diseragamkan di staging

| Jenis | Format target | Contoh salah | Contoh benar |
|---|---|---|---|
| Tanggal | `YYYY-MM-DD HH:MM:SS` | `01/02/2024`, `2024-02-01T10:30:00` | `2024-02-01 10:30:00` |
| Desimal | titik, tanpa pemisah ribuan | `Rp 10.000,50`, `10,000.50` | `10000.50` |
| Integer | angka tanpa pemisah | `1.000` | `1000` |
| Teks | di-trim, tanpa spasi ganda | `" Indomie  Goreng "` | `"Indomie Goreng"` |
| Barcode kosong | `NULL` | `""`, `"-"`, `"0"` | `NULL` |
| Metode bayar | nama konsisten | `tunai`, `CASH` | `Tunai` |
| Jenis mutasi | `Masuk` / `Keluar` | `IN`, `masuk ` | `Masuk` |

> **Perhatian tanggal:** `01/02/2024` bisa berarti 1 Februari atau 2 Januari.
> Konfirmasi ke pemilik toko sebelum konversi. Jangan menebak.

---

## 3. Template pemetaan tabel

Salin blok ini untuk setiap tabel lama.

### 3.1 Tabel produk lama

| Item | Isi |
|---|---|
| Nama tabel lama | `……………………` |
| Perkiraan jumlah baris | `……………………` |
| Primary key lama | `……………………` |
| Kolom kode produk | `……………………` |
| Kolom barcode | `……………………` |
| Kolom nama | `……………………` |
| Kolom satuan | `……………………` |
| Kolom kategori | `……………………` |
| Kolom supplier | `……………………` |
| Kolom harga beli | `……………………` |
| Kolom harga jual | `……………………` |
| Kolom stok | `……………………` |
| Catatan khusus | `……………………` |

Pemetaan kolom:

| Kolom lama | Tipe lama | Target | Transformasi | Status |
|---|---|---|---|---|
| `id_produk` | int | `stg_products.legacy_id` | `CAST(... AS CHAR)` | ☐ |
| `kode` | varchar | `stg_products.kode` | `TRIM`, uppercase | ☐ |
| `barcode` | varchar | `stg_products.barcode` | `NULL` jika kosong/`-`/`0` | ☐ |
| `nama` | varchar | `stg_products.nama` | `TRIM`, rapikan spasi | ☐ |
| `satuan` | varchar | `stg_products.satuan` | map ke `units` | ☐ |
| `kategori` | varchar | `stg_products.kategori` | map ke `categories` | ☐ |
| `supplier` | varchar | `stg_products.supplier` | map ke `suppliers` | ☐ |
| `harga_beli` | decimal | `stg_products.harga_beli_raw` | titik desimal | ☐ |
| `harga_jual` | decimal | `stg_products.harga_jual_raw` | titik desimal | ☐ |
| `stok` | int | `stg_products.stok_raw` | angka bulat | ☐ |

### 3.2 Tabel nota lama

| Item | Isi |
|---|---|
| Nama tabel lama | `……………………` |
| Primary key lama | `……………………` |
| Nomor nota | `……………………` |
| Tanggal | `……………………` |
| Total | `……………………` |
| Bayar | `……………………` |
| Metode bayar | `……………………` |
| Kasir | `……………………` |

Pemetaan kolom:

| Kolom lama | Tipe lama | Target | Transformasi | Status |
|---|---|---|---|---|
| `id_nota` | int | `stg_transactions.legacy_id` | `CAST(... AS CHAR)` | ☐ |
| `no_nota` | varchar | `stg_transactions.trx_id` | `TRIM`, cek unik, maks. 20 char | ☐ |
| `tanggal` | datetime | `stg_transactions.tanggal_raw` | format ISO | ☐ |
| `total` | decimal | `stg_transactions.total_raw` | titik desimal | ☐ |
| `bayar` | decimal | `stg_transactions.bayar_raw` | kosong → isi `total` | ☐ |
| `metode` | varchar | `stg_transactions.metode_bayar` | map ke `payment_methods` | ☐ |
| `kasir` | varchar | `stg_transactions.kasir` | samakan dengan `users.username` | ☐ |

### 3.3 Tabel item nota lama

| Kolom lama | Tipe lama | Target | Transformasi | Status |
|---|---|---|---|---|
| `id_nota` | int | `stg_transaction_items.legacy_trx_id` | samakan dengan nota | ☐ |
| `id_produk` | int | `stg_transaction_items.legacy_product_id` | samakan dengan produk | ☐ |
| `kode_produk` | varchar | `stg_transaction_items.kode` | `TRIM` | ☐ |
| `nama_produk` | varchar | `stg_transaction_items.nama` | `TRIM` | ☐ |
| `harga` | decimal | `stg_transaction_items.harga_raw` | titik desimal | ☐ |
| `harga_beli` | decimal | `stg_transaction_items.harga_beli_raw` | kosong → ambil dari produk | ☐ |
| `qty` | int | `stg_transaction_items.qty_raw` | angka bulat > 0 | ☐ |

Jika tabel lama tidak menyimpan `id_produk`, gunakan `kode_produk` sebagai
`legacy_product_id` (asalkan unik) atau biarkan kosong dan andalkan fallback
`kode` saat ETL.

### 3.4 Tabel mutasi stok lama

| Kolom lama | Tipe lama | Target | Transformasi | Status |
|---|---|---|---|---|
| `id_mutasi` | int | `stg_stock_movements.legacy_id` | — | ☐ |
| `id_produk` | int | `stg_stock_movements.legacy_product_id` | samakan dengan produk | ☐ |
| `tanggal` | datetime | `stg_stock_movements.tanggal_raw` | format ISO | ☐ |
| `jenis` | varchar | `stg_stock_movements.jenis` | `Masuk`/`Keluar` | ☐ |
| `qty` | int | `stg_stock_movements.qty_raw` | absolut, > 0 | ☐ |
| `stok_awal` | int | `stg_stock_movements.stok_sebelum_raw` | angka bulat | ☐ |
| `stok_akhir` | int | `stg_stock_movements.stok_sesudah_raw` | angka bulat | ☐ |
| `keterangan` | varchar | `stg_stock_movements.keterangan` | `TRIM` | ☐ |

### 3.5 Tabel user lama

| Kolom lama | Tipe lama | Target | Transformasi | Status |
|---|---|---|---|---|
| `id_user` | int | `stg_users.legacy_id` | — | ☐ |
| `username` | varchar | `stg_users.username` | `LOWER(TRIM(...))` | ☐ |
| `nama` | varchar | `stg_users.nama_lengkap` | `TRIM` | ☐ |
| `role` | varchar | `stg_users.role_raw` | map ke `Kasir`/`Manager` | ☐ |
| `password` | varchar | — | **jangan dipakai** | ☐ |
| `aktif` | bit/varchar | `stg_users.aktif_raw` | `1/0`, `true/false` | ☐ |

---

## 4. Contoh pemetaan nama kolom umum

Nama kolom di database lama sering berbeda. Berikut padanan yang sering muncul:

| Kemungkinan nama lama | Target staging |
|---|---|
| `kd_brg`, `kode_barang`, `kode_produk`, `plu` | `kode` |
| `barcode`, `kode_barcode`, `ean` | `barcode` |
| `nm_brg`, `nama_barang`, `nama_produk` | `nama` |
| `sat`, `satuan`, `unit` | `satuan` |
| `kat`, `kategori`, `kelompok`, `grup` | `kategori` |
| `supp`, `supplier`, `distributor`, `vendor` | `supplier` |
| `hbeli`, `harga_beli`, `modal`, `cost` | `harga_beli_raw` |
| `hjual`, `harga_jual`, `harga`, `price` | `harga_jual_raw` |
| `stok`, `stock`, `qty_stok`, `saldo` | `stok_raw` |
| `no_nota`, `nota`, `invoice`, `faktur` | `trx_id` |
| `tgl`, `tanggal`, `tgl_transaksi`, `date` | `tanggal_raw` |
| `total`, `grand_total`, `jumlah` | `total_raw` |
| `bayar`, `dibayar`, `paid` | `bayar_raw` |
| `metode`, `payment`, `cara_bayar` | `metode_bayar` |
| `kasir`, `user`, `operator`, `id_kasir` | `kasir` |
| `jenis`, `tipe`, `type`, `in_out` | `jenis` |
| `keterangan`, `ket`, `note`, `remark` | `keterangan` |

---

## 5. Contoh query menarik data lama (MySQL)

```sql
-- Produk
INSERT INTO kasirku_staging.stg_products
    (batch_id, legacy_id, kode, barcode, nama, satuan, kategori, supplier,
     harga_beli_raw, harga_jual_raw, stok_raw)
SELECT
    @batch_id,
    CAST(id_barang AS CHAR),
    TRIM(kd_brg),
    NULLIF(TRIM(barcode), ''),
    TRIM(nm_brg),
    TRIM(satuan),
    TRIM(kategori),
    TRIM(supplier),
    CAST(harga_beli AS CHAR),
    CAST(harga_jual AS CHAR),
    CAST(stok AS CHAR)
FROM kasirku_legacy.barang;
```

```sql
-- Nota
INSERT INTO kasirku_staging.stg_transactions
    (batch_id, legacy_id, trx_id, tanggal_raw, metode_bayar,
     total_raw, bayar_raw, kasir)
SELECT
    @batch_id,
    CAST(id_nota AS CHAR),
    TRIM(no_nota),
    DATE_FORMAT(tgl, '%Y-%m-%d %H:%i:%s'),
    TRIM(metode),
    CAST(total AS CHAR),
    CAST(bayar AS CHAR),
    TRIM(kasir)
FROM kasirku_legacy.nota;
```

```sql
-- Item nota
INSERT INTO kasirku_staging.stg_transaction_items
    (batch_id, legacy_id, legacy_trx_id, legacy_product_id,
     kode, nama, harga_raw, harga_beli_raw, qty_raw)
SELECT
    @batch_id,
    CAST(d.id_detail AS CHAR),
    CAST(d.id_nota AS CHAR),
    CAST(d.id_barang AS CHAR),
    TRIM(b.kd_brg),
    TRIM(b.nm_brg),
    CAST(d.harga AS CHAR),
    CAST(d.harga_beli AS CHAR),
    CAST(d.qty AS CHAR)
FROM kasirku_legacy.detail_nota d
LEFT JOIN kasirku_legacy.barang b ON b.id_barang = d.id_barang;
```

---

## 6. Keputusan yang harus diambil saat migrasi

Catat keputusan Anda di sini agar bisa dipertanggungjawabkan di laporan TA.

| Pertanyaan | Keputusan | Alasan |
|---|---|---|
| Produk lama tanpa kode? | `……………………` | `……………………` |
| Barcode duplikat? | `……………………` | `……………………` |
| Kategori kosong? | fallback `Umum` | Agar FK wajib tetap terpenuhi |
| Satuan kosong? | fallback `Pcs` | Agar FK wajib tetap terpenuhi |
| Harga beli item 0? | ambil `products.harga_beli` | Laba historis tetap bisa dihitung |
| Nota tanpa item? | `……………………` | `……………………` |
| Item tanpa produk? | `product_id = NULL`, snapshot dipertahankan | Riwayat nota tidak boleh hilang |
| Mutasi stok tidak konsisten? | `……………………` | `……………………` |
| Stok awal tanpa mutasi? | `……………………` | Perlu baseline |
| Password lama? | akun baru / password sementara | Hash lama tidak kompatibel |
| Tanggal ambigu (`dd/mm` vs `mm/dd`)? | `……………………` | Konfirmasi ke pemilik toko |

---

## 7. Checklist akhir migrasi

- [ ] Dump asli tersimpan di `legacy/raw/` (tidak diedit, tidak di-commit).
- [ ] `kasirku_legacy` hanya dibaca, tidak pernah di-`UPDATE`/`DELETE`.
- [ ] `import_batches` mencatat sumber, tanggal, dan jumlah baris.
- [ ] Semua `legacy_id` produk/nota/user terisi dan unik.
- [ ] Lookup kategori/satuan/supplier/metode bayar sudah bersih.
- [ ] `03_validation.sql` bagian A12 = 0 sebelum ETL.
- [ ] ETL selesai tanpa error, batch berstatus `selesai`.
- [ ] `03_validation.sql` bagian B & C bersih setelah ETL.
- [ ] Total omzet & qty item staging = target.
- [ ] Stok produk = `stok_sesudah` mutasi terakhir.
- [ ] Akun login baru bisa dipakai di POS/Manager.
- [ ] Backup `kasirku` setelah migrasi tersimpan.
- [ ] Dokumen ini diisi lengkap untuk lampiran laporan.
