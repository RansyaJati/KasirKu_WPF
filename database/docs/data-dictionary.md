# Kamus Data KasirKu

Database: `kasirku` · Engine: InnoDB · Charset: `utf8mb4` · Collation: `utf8mb4_unicode_ci`

Keterangan kolom **Asal**: dari mana data berasal saat migrasi dari JSON / DB lama.

---

## 1. `categories`

Master kategori produk. Menggantikan kolom `Kategori`/`Kelompok` yang dulu teks bebas.

| Kolom | Tipe | Null | Default | Kunci | Keterangan |
|---|---|---|---|---|---|
| `id` | TINYINT UNSIGNED | Tidak | AUTO_INCREMENT | PK | ID kategori |
| `nama` | VARCHAR(50) | Tidak | — | UNIQUE | Nama kategori, unik (case-insensitive) |
| `keterangan` | VARCHAR(255) | Ya | NULL | — | Catatan opsional |
| `created_at` | TIMESTAMP | Tidak | CURRENT_TIMESTAMP | — | Waktu dibuat |

Asal: `DISTINCT Product.Kategori` dari JSON / kolom kategori DB lama.

---

## 2. `units`

Master satuan produk.

| Kolom | Tipe | Null | Default | Kunci | Keterangan |
|---|---|---|---|---|---|
| `id` | TINYINT UNSIGNED | Tidak | AUTO_INCREMENT | PK | ID satuan |
| `nama` | VARCHAR(20) | Tidak | — | UNIQUE | Pcs, Pack, Dus, Karton, Kg, Gram, Liter, Botol, Lusin |
| `created_at` | TIMESTAMP | Tidak | CURRENT_TIMESTAMP | — | Waktu dibuat |

Asal: `DISTINCT Product.Satuan`.

---

## 3. `suppliers`

Master supplier / distributor.

| Kolom | Tipe | Null | Default | Kunci | Keterangan |
|---|---|---|---|---|---|
| `id` | SMALLINT UNSIGNED | Tidak | AUTO_INCREMENT | PK | ID supplier |
| `nama` | VARCHAR(100) | Tidak | — | UNIQUE | Nama supplier |
| `telepon` | VARCHAR(30) | Ya | NULL | — | Nomor telepon |
| `alamat` | VARCHAR(255) | Ya | NULL | — | Alamat |
| `created_at` | TIMESTAMP | Tidak | CURRENT_TIMESTAMP | — | Waktu dibuat |
| `updated_at` | TIMESTAMP | Tidak | CURRENT_TIMESTAMP | — | Otomatis diperbarui |

Asal: `DISTINCT Product.Supplier`. Produk boleh tanpa supplier (`NULL`).

---

## 4. `payment_methods`

Master metode pembayaran.

| Kolom | Tipe | Null | Default | Kunci | Keterangan |
|---|---|---|---|---|---|
| `id` | TINYINT UNSIGNED | Tidak | AUTO_INCREMENT | PK | ID metode |
| `nama` | VARCHAR(30) | Tidak | — | UNIQUE | Tunai, QRIS, GoPay, OVO, DANA, ShopeePay, Transfer Bank |
| `created_at` | TIMESTAMP | Tidak | CURRENT_TIMESTAMP | — | Waktu dibuat |

Asal: `DISTINCT Transaction.MetodeBayar`; kosong → `'Tunai'`.

---

## 5. `movement_types`

Jenis mutasi stok + arah perubahan.

| Kolom | Tipe | Null | Default | Kunci | Keterangan |
|---|---|---|---|---|---|
| `id` | TINYINT UNSIGNED | Tidak | AUTO_INCREMENT | PK | ID jenis |
| `nama` | VARCHAR(20) | Tidak | — | UNIQUE | `Masuk` / `Keluar` |
| `arah` | TINYINT | Tidak | — | CHECK (−1/1) | +1 masuk, −1 keluar |
| `created_at` | TIMESTAMP | Tidak | CURRENT_TIMESTAMP | — | Waktu dibuat |

Seed: `(1, 'Masuk', 1)`, `(2, 'Keluar', -1)`.

---

## 6. `users`

Akun login POS dan Manager.

| Kolom | Tipe | Null | Default | Kunci | Keterangan |
|---|---|---|---|---|---|
| `id` | INT UNSIGNED | Tidak | AUTO_INCREMENT | PK | ID user |
| `username` | VARCHAR(50) | Tidak | — | UNIQUE | Login, disimpan huruf kecil |
| `nama_lengkap` | VARCHAR(100) | Tidak | `''` | — | Nama tampilan |
| `role` | ENUM('Kasir','Manager') | Tidak | `'Kasir'` | INDEX | Menentukan aplikasi yang boleh login |
| `password_hash` | VARCHAR(128) | Tidak | — | — | PBKDF2-SHA256, Base64, 100.000 iterasi |
| `salt` | VARCHAR(64) | Tidak | — | — | Salt 16 byte, Base64 |
| `aktif` | BOOLEAN | Tidak | TRUE | INDEX | Akun nonaktif tidak bisa login |
| `created_at` | TIMESTAMP | Tidak | CURRENT_TIMESTAMP | — | Waktu dibuat |
| `updated_at` | TIMESTAMP | Tidak | CURRENT_TIMESTAMP | — | Otomatis diperbarui |

Asal: `users.json` / tabel user DB lama. **Hash lama tidak kompatibel** —
lihat `database/tools/New-KasirKuPasswordHash.ps1`.

---

## 7. `store_settings`

Profil toko + pengaturan printer. Selalu satu baris (`id = 1`).

| Kolom | Tipe | Null | Default | Kunci | Keterangan |
|---|---|---|---|---|---|
| `id` | TINYINT UNSIGNED | Tidak | 1 | PK, CHECK id = 1 | Baris tunggal |
| `nama_toko` | VARCHAR(100) | Tidak | — | — | Header struk/laporan |
| `alamat` | VARCHAR(255) | Tidak | `''` | — | Alamat toko |
| `telepon` | VARCHAR(30) | Tidak | `''` | — | Telepon toko |
| `footer_struk` | VARCHAR(255) | Tidak | `''` | — | Ucapan di bawah struk |
| `printer_name` | VARCHAR(150) | Tidak | `''` | — | Nama printer Windows |
| `paper_width_mm` | SMALLINT UNSIGNED | Tidak | 80 | CHECK 58/80 | Lebar kertas thermal |
| `auto_print_receipt` | BOOLEAN | Tidak | TRUE | — | Cetak struk otomatis |
| `kick_drawer` | BOOLEAN | Tidak | TRUE | — | Pulsa buka laci kasir |
| `updated_at` | TIMESTAMP | Tidak | CURRENT_TIMESTAMP | — | Otomatis diperbarui |

Asal: `settings.json`.

---

## 8. `products`

Master produk. `stok` adalah stok terkini; riwayatnya di `stock_movements`.

| Kolom | Tipe | Null | Default | Kunci | Keterangan |
|---|---|---|---|---|---|
| `id` | INT UNSIGNED | Tidak | AUTO_INCREMENT | PK | ID produk |
| `kode` | VARCHAR(20) | Tidak | — | UNIQUE | Kode internal, mis. `BRG001` |
| `barcode` | VARCHAR(50) | Ya | NULL | UNIQUE | Barcode scanner; NULL diabaikan unique |
| `nama` | VARCHAR(150) | Tidak | — | INDEX | Nama produk |
| `category_id` | TINYINT UNSIGNED | Tidak | — | FK → categories | Kategori |
| `unit_id` | TINYINT UNSIGNED | Tidak | — | FK → units | Satuan |
| `supplier_id` | SMALLINT UNSIGNED | Ya | NULL | FK → suppliers | Supplier |
| `harga_beli` | DECIMAL(15,2) | Tidak | 0.00 | CHECK ≥ 0 | Harga modal |
| `harga_jual` | DECIMAL(15,2) | Tidak | 0.00 | CHECK ≥ 0 | Harga jual |
| `stok` | INT | Tidak | 0 | CHECK ≥ 0 | Stok terkini |
| `aktif` | BOOLEAN | Tidak | TRUE | — | Soft delete / produk nonaktif |
| `created_at` | TIMESTAMP | Tidak | CURRENT_TIMESTAMP | — | Waktu dibuat |
| `updated_at` | TIMESTAMP | Tidak | CURRENT_TIMESTAMP | — | Otomatis diperbarui |

Asal: `data.json → Products[]`. Kolom turunan `Margin`, `MarginPersen`,
`HargaRp`, `DisplayStok` **tidak disimpan** (dihitung aplikasi).

---

## 9. `transactions`

Header nota penjualan.

| Kolom | Tipe | Null | Default | Kunci | Keterangan |
|---|---|---|---|---|---|
| `id` | VARCHAR(20) | Tidak | — | PK | Format `TRX-yyyyMMdd-###` |
| `tanggal` | DATETIME | Tidak | CURRENT_TIMESTAMP | INDEX | Waktu transaksi |
| `user_id` | INT UNSIGNED | Ya | NULL | FK → users | Kasir; NULL untuk data lama |
| `payment_method_id` | TINYINT UNSIGNED | Tidak | — | FK → payment_methods | Metode bayar |
| `total` | DECIMAL(15,2) | Tidak | 0.00 | CHECK ≥ 0 | Total belanja |
| `bayar` | DECIMAL(15,2) | Tidak | 0.00 | CHECK ≥ total | Uang diterima |
| `kembalian` | DECIMAL(15,2) | Ya (generated) | `bayar - total` | STORED | Dihitung otomatis |
| `catatan` | VARCHAR(255) | Ya | NULL | — | Catatan opsional |
| `created_at` | TIMESTAMP | Tidak | CURRENT_TIMESTAMP | — | Waktu baris dibuat |

Asal: `data.json → Transactions[]` (header). `Kembalian`, `JumlahItem`,
`Ringkasan` dihitung, tidak disimpan.

---

## 10. `transaction_items`

Baris nota. Menyimpan **snapshot** harga & nama saat transaksi.

| Kolom | Tipe | Null | Default | Kunci | Keterangan |
|---|---|---|---|---|---|
| `id` | BIGINT UNSIGNED | Tidak | AUTO_INCREMENT | PK | ID baris |
| `transaction_id` | VARCHAR(20) | Tidak | — | FK → transactions | Header nota |
| `product_id` | INT UNSIGNED | Ya | NULL | FK → products | NULL jika produk sudah dihapus |
| `kode` | VARCHAR(20) | Tidak | — | — | Snapshot kode produk |
| `nama` | VARCHAR(150) | Tidak | — | — | Snapshot nama produk |
| `harga_beli` | DECIMAL(15,2) | Tidak | 0.00 | CHECK ≥ 0 | Snapshot modal (untuk laba) |
| `harga_jual` | DECIMAL(15,2) | Tidak | 0.00 | CHECK ≥ 0 | Snapshot harga jual |
| `qty` | INT | Tidak | — | CHECK > 0 | Jumlah terjual |
| `subtotal` | DECIMAL(18,2) | Ya (generated) | `harga_jual * qty` | STORED | Dihitung otomatis |

Asal: `Transactions[].Items[]`. `HargaBeli` lama yang 0 diisi dari
`products.harga_beli` saat ETL (lihat `mapping-legacy.md`).

---

## 11. `stock_movements`

Kartu stok / log mutasi.

| Kolom | Tipe | Null | Default | Kunci | Keterangan |
|---|---|---|---|---|---|
| `id` | BIGINT UNSIGNED | Tidak | AUTO_INCREMENT | PK | ID mutasi |
| `tanggal` | DATETIME | Tidak | CURRENT_TIMESTAMP | INDEX | Waktu mutasi |
| `product_id` | INT UNSIGNED | Tidak | — | FK → products | Produk yang dimutasi |
| `movement_type_id` | TINYINT UNSIGNED | Tidak | — | FK → movement_types | Masuk / Keluar |
| `qty` | INT | Tidak | — | CHECK > 0 | Jumlah, selalu positif |
| `stok_sebelum` | INT | Tidak | — | CHECK ≥ 0 | Stok sebelum mutasi |
| `stok_sesudah` | INT | Tidak | — | CHECK ≥ 0 | Stok setelah mutasi |
| `perubahan` | INT | Ya (generated) | `stok_sesudah - stok_sebelum` | STORED | Bertanda +/− |
| `keterangan` | VARCHAR(255) | Tidak | `''` | — | Alasan mutasi |
| `transaction_id` | VARCHAR(20) | Ya | NULL | FK → transactions | Diisi jika dari penjualan |
| `user_id` | INT UNSIGNED | Ya | NULL | FK → users | Pencatat mutasi |
| `created_at` | TIMESTAMP | Tidak | CURRENT_TIMESTAMP | — | Waktu baris dibuat |

Asal: `data.json → Movements[]`. `Perubahan`, `IsMasuk`, `TanggalStr`
dihitung, tidak disimpan.

---

## 12. `schema_migrations`

Riwayat versi skema.

| Kolom | Tipe | Null | Default | Kunci | Keterangan |
|---|---|---|---|---|---|
| `versi` | VARCHAR(20) | Tidak | — | PK | Versi skema, mis. `1.0.0` |
| `keterangan` | VARCHAR(255) | Tidak | — | — | Deskripsi perubahan |
| `diterapkan_pada` | TIMESTAMP | Tidak | CURRENT_TIMESTAMP | — | Waktu diterapkan |

---

## Ringkasan relasi

| FK | Tabel anak → induk | On Delete |
|---|---|---|
| `fk_products_category` | products → categories | RESTRICT |
| `fk_products_unit` | products → units | RESTRICT |
| `fk_products_supplier` | products → suppliers | SET NULL |
| `fk_transactions_user` | transactions → users | SET NULL |
| `fk_transactions_payment` | transactions → payment_methods | RESTRICT |
| `fk_transaction_items_trx` | transaction_items → transactions | RESTRICT |
| `fk_transaction_items_product` | transaction_items → products | SET NULL |
| `fk_stock_movements_product` | stock_movements → products | RESTRICT |
| `fk_stock_movements_type` | stock_movements → movement_types | RESTRICT |
| `fk_stock_movements_trx` | stock_movements → transactions | SET NULL |
| `fk_stock_movements_user` | stock_movements → users | SET NULL |

`RESTRICT` dipakai untuk mencegah riwayat keuangan/stok hilang tanpa sengaja.
`SET NULL` dipakai jika keberadaan referensi bersifat historis/opsional.
