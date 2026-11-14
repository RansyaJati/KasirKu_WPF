# Analisis Normalisasi Database KasirKu

Dokumen ini menjelaskan proses normalisasi dari data lama (JSON `data.json`
dan/atau database klien) menuju skema target `kasirku` di MySQL.

Urutan pembahasan:

1. Bentuk data lama dan anomalinya
2. Functional dependency (FD)
3. 1NF
4. 2NF
5. 3NF
6. BCNF
7. Denormalisasi terkontrol (snapshot & stok)
8. Ringkasan pemetaan

---

## 1. Bentuk data lama dan anomalinya

Data lama disimpan sebagai satu objek JSON:

```
KasirKuData
├── Products[]      : id, kode, barcode, nama, satuan, kategori, supplier,
│                     hargaBeli, harga, stok
├── Transactions[]  : id, tanggal, total, bayar, metodeBayar, items[]
│   └── Items[]     : kode, nama, harga, hargaBeli, qty
└── Movements[]     : id, tanggal, productId, kode, nama, jenis, qty,
                      stokSebelum, stokSesudah, keterangan
```

Anggap seluruh data ini satu relasi universal:

```
R(TrxId, Tanggal, Total, Bayar, Metode,
  ItemKode, ItemNama, ItemHarga, ItemHargaBeli, ItemQty,
  Kategori, Satuan, Supplier, Stok)
```

### Anomali yang ditemukan

| No | Masalah | Jenis | Contoh |
|---|---|---|---|
| 1 | `Transactions[].Items[]` adalah *repeating group* | 1NF | Satu nota berisi banyak item dalam satu baris |
| 2 | `Ringkasan` berisi daftar item dipisah koma | 1NF (nilai tidak atomik) | `"Indomie x2, Aqua x1"` |
| 3 | `DisplayStok` menggabungkan kode + nama + stok | 1NF (nilai tidak atomik) | `"BRG001 — Indomie (stok: 10)"` |
| 4 | `Kategori`, `Satuan`, `Supplier` berupa teks bebas | Redundansi | `"Makanan"`, `"makanan"`, `"Mkn"` |
| 5 | `Movement.Kode` & `Movement.Nama` diulang dari produk | Redundansi | Nama produk berubah → log lama tidak konsisten |
| 6 | `Item.Nama/Kode` bergantung pada produk, bukan pada baris nota | Partial dependency | Hapus produk → nama item hilang |
| 7 | `Subtotal`, `Kembalian`, `JumlahItem`, `Margin` disimpan | Derived attribute | Bisa berbeda dari hasil hitung |
| 8 | `Stok` disimpan di produk, padahal bisa dihitung dari mutasi | Redundansi terkontrol | Stok bisa *drift* jika log gagal ditulis |
| 9 | Metode bayar & jenis mutasi berupa string bebas | Redundansi domain | `"Tunai"` vs `"tunai"` vs `"CASH"` |
| 10 | Tidak ada constraint (stok negatif, qty 0, harga negatif) | Integritas | Data kotor lolos |

Anomali operasional yang timbul:

- **Update anomaly** — mengubah nama kategori harus menyunting semua produk.
- **Insert anomaly** — tidak bisa menambah kategori baru tanpa produk.
- **Delete anomaly** — menghapus produk terakhir menghilangkan kategori/supplier.
- **Inkonsistensi** — `Total` nota tidak sama dengan jumlah `Subtotal` item.

---

## 2. Functional Dependency (FD)

Notasi: `X → Y` berarti X menentukan Y.

### 2.1 Produk

```
id        → kode, barcode, nama, satuan, kategori, supplier, harga_beli, harga_jual, stok
kode      → id, barcode, nama, satuan, kategori, supplier, harga_beli, harga_jual, stok
barcode   → id, kode, nama, satuan, kategori, supplier, harga_beli, harga_jual, stok
```

`kode` dan `barcode` adalah **candidate key** selain `id`.

### 2.2 Nota

```
id           → tanggal, total, bayar, metode_bayar, user_id
id, item_id  → qty, harga_jual, harga_beli
item_id      → kode, nama, harga_jual, harga_beli, qty, transaction_id
```

### 2.3 Mutasi stok

```
id         → tanggal, product_id, jenis, qty, stok_sebelum, stok_sesudah, keterangan
product_id → kode, nama            (transitif, seharusnya tidak disimpan)
```

### 2.4 Ketergantungan transitif yang harus dihilangkan

```
products.id → products.category_id → categories.nama
products.id → products.unit_id     → units.nama
products.id → products.supplier_id → suppliers.nama
stock_movements.id → stock_movements.product_id → products.kode, products.nama
transactions.id → transactions.payment_method_id → payment_methods.nama
stock_movements.id → stock_movements.movement_type_id → movement_types.nama
```

---

## 3. First Normal Form (1NF)

**Syarat:** setiap kolom bernilai atomik, tidak ada repeating group,
setiap baris unik.

### Pelanggaran

`Transactions[].Items[]` menyimpan banyak item dalam satu baris.
`Ringkasan` dan `DisplayStok` menggabungkan beberapa nilai dalam satu kolom.

### Perbaikan

| Sebelum | Sesudah |
|---|---|
| `Transactions[].Items[]` | Tabel `transaction_items` (satu baris = satu item) |
| `Ringkasan` (`"Indomie x2, Aqua x1"`) | Dihitung dari `transaction_items` saat dibutuhkan |
| `DisplayStok` (`"BRG001 — Indomie (stok: 10)"`) | Dihitung dari `products.kode`, `products.nama`, `products.stok` |
| `Kategori` teks bebas | Tabel `categories` + `products.category_id` |
| `Satuan` teks bebas | Tabel `units` + `products.unit_id` |
| `Supplier` teks bebas | Tabel `suppliers` + `products.supplier_id` |

Setelah 1NF:

```
products(id, kode, barcode, nama, kategori, supplier, harga_beli, harga_jual, stok)
transactions(id, tanggal, total, bayar, metode_bayar)
transaction_items(transaction_id, kode, nama, harga_jual, harga_beli, qty)
stock_movements(id, tanggal, product_id, kode, nama, jenis, qty, stok_sebelum, stok_sesudah)
```

---

## 4. Second Normal Form (2NF)

**Syarat:** sudah 1NF dan setiap atribut non-key bergantung penuh pada
**seluruh** primary key (tidak ada partial dependency).

Pada `transaction_items`, kunci alaminya adalah kombinasi:

```
(transaction_id, kode_produk)
```

Atribut berikut hanya bergantung pada `kode_produk`, bukan pada seluruh kunci:

```
kode_produk → nama, kategori, satuan, supplier, harga_beli, stok
```

Itu **partial dependency** → melanggar 2NF.

### Perbaikan

- Data master produk dipindah ke `products`.
- `transaction_items` hanya menyimpan data yang memang milik baris nota:

```
(transaction_id, product_id) → qty, harga_jual_saat_transaksi, harga_beli_saat_transaksi
```

- `stock_movements` menyimpan `product_id`, bukan `kode`/`nama` produk.

> Catatan: tabel target memakai **surrogate key** `transaction_items.id`.
> Dengan surrogate key, 2NF otomatis terpenuhi secara formal. Namun analisis
> tetap dilakukan memakai natural key untuk menunjukkan partial dependency
> yang ada pada data lama.

---

## 5. Third Normal Form (3NF)

**Syarat:** sudah 2NF dan tidak ada ketergantungan transitif
(atribut non-key tidak bergantung pada atribut non-key lain).

### 5.1 Pelanggaran pada data lama

| Relasi | FD transitif | Akibat |
|---|---|---|
| Produk | `product → kategori → nama kategori` | Nama kategori diulang di setiap produk |
| Produk | `product → supplier → nama supplier` | Nama supplier diulang |
| Mutasi stok | `movement → product_id → kode, nama` | Kode/nama produk diulang di log |
| Nota | `transaction → metode_bayar` (string) | Ejaan metode bisa berbeda-beda |

### 5.2 Perbaikan

| Atribut transitif | Dipindah ke | FK |
|---|---|---|
| `Kategori` | `categories(nama)` | `products.category_id` |
| `Satuan` | `units(nama)` | `products.unit_id` |
| `Supplier` | `suppliers(nama)` | `products.supplier_id` |
| `MetodeBayar` | `payment_methods(nama)` | `transactions.payment_method_id` |
| `Jenis` mutasi | `movement_types(nama, arah)` | `stock_movements.movement_type_id` |
| `Kode`/`Nama` di mutasi | `products(kode, nama)` | `stock_movements.product_id` |

Setelah 3NF:

```
categories(id, nama)
units(id, nama)
suppliers(id, nama, telepon, alamat)
payment_methods(id, nama)
movement_types(id, nama, arah)
products(id, kode, barcode, nama, category_id, unit_id, supplier_id,
         harga_beli, harga_jual, stok)
transactions(id, tanggal, user_id, payment_method_id, total, bayar)
transaction_items(id, transaction_id, product_id, kode, nama,
                  harga_beli, harga_jual, qty)
stock_movements(id, tanggal, product_id, movement_type_id, qty,
                stok_sebelum, stok_sesudah, keterangan, transaction_id, user_id)
```

### 5.3 Mengapa snapshot harga tetap 3NF?

`transaction_items.kode`, `nama`, `harga_beli`, `harga_jual` terlihat seperti
redundansi, tetapi sebenarnya **bukan** ketergantungan transitif:

```
transaction_items.id → kode, nama, harga_beli, harga_jual, qty
```

Nilai tersebut adalah **fakta historis** pada saat transaksi, bukan salinan
nilai produk saat ini. Jika dihapus dan diganti referensi ke `products`,
laporan lama akan berubah ketika produk di-edit — itu justru merusak
integritas data akuntansi.

Karena setiap atribut bergantung pada PK baris nota (bukan pada
`product_id`), tabel ini tetap memenuhi 3NF. Ini disebut
**denormalisasi terkontrol untuk kebutuhan audit**, bukan pelanggaran
normalisasi.

---

## 6. Boyce-Codd Normal Form (BCNF)

**Syarat:** untuk setiap FD nontrivial `X → Y`, X harus candidate key.

| Tabel | Determinant | Candidate key | BCNF? |
|---|---|---|---|
| `categories` | `id`, `nama` | `id`, `nama` | Ya |
| `units` | `id`, `nama` | `id`, `nama` | Ya |
| `suppliers` | `id`, `nama` | `id`, `nama` | Ya |
| `payment_methods` | `id`, `nama` | `id`, `nama` | Ya |
| `movement_types` | `id`, `nama` | `id`, `nama` | Ya |
| `products` | `id`, `kode`, `barcode` | `id`, `kode`, `barcode` | Ya |
| `transactions` | `id` | `id` | Ya |
| `transaction_items` | `id` | `id` | Ya |
| `stock_movements` | `id` | `id` | Ya |
| `users` | `id`, `username` | `id`, `username` | Ya |
| `store_settings` | `id` | `id` | Ya |

Tidak ada determinant yang bukan candidate key, sehingga seluruh tabel
memenuhi BCNF.

---

## 7. Denormalisasi terkontrol

Dua hal sengaja tidak dinormalisasi penuh. Keduanya harus dipertahankan
dengan aturan aplikasi dan divalidasi oleh query di
`database/mysql/03_validation.sql`.

### 7.1 `products.stok`

Secara teori stok = agregat mutasi:

```
stok = Σ (arah × qty) dari stock_movements
```

Menyimpan `stok` mempercepat validasi checkout (tidak perlu agregasi ribuan
baris setiap scan barcode). Ini **redundansi terkontrol** dengan syarat:

- setiap perubahan stok wajib menulis `stock_movements`,
- `products.stok` hanya diubah bersamaan dengan mutasi,
- query B8/B9 memverifikasi stok akhir = mutasi terakhir dan rantai stok nyambung.

### 7.2 Snapshot pada `transaction_items`

Sudah dijelaskan di bagian 5.3: dibutuhkan agar laporan historis tidak
berubah saat master produk di-edit.

### 7.3 Generated column

| Kolom | Rumus | Alasan |
|---|---|---|
| `transaction_items.subtotal` | `harga_jual * qty` | Menghilangkan risiko salah hitung |
| `transactions.kembalian` | `bayar - total` | Nilai turunan, tidak perlu input |
| `stock_movements.perubahan` | `stok_sesudah - stok_sebelum` | Memudahkan audit kartu stok |

Generated column bukan pelanggaran normalisasi karena nilainya tidak
disimpan sebagai data independen; MySQL menghitungnya dari kolom lain.

---

## 8. Ringkasan pemetaan JSON → tabel

| Sumber lama | Tabel target | Transformasi |
|---|---|---|
| `Products[].Kategori` | `categories` + `products.category_id` | `DISTINCT`, trim, fallback `Umum` |
| `Products[].Satuan` | `units` + `products.unit_id` | `DISTINCT`, trim, fallback `Pcs` |
| `Products[].Supplier` | `suppliers` + `products.supplier_id` | `DISTINCT`, NULL jika kosong |
| `Products[]` | `products` | Buang kolom turunan (`Margin`, `HargaRp`, `DisplayStok`) |
| `Transactions[]` | `transactions` | Buang `Kembalian`, `JumlahItem`, `Ringkasan` |
| `Transactions[].MetodeBayar` | `payment_methods` | `DISTINCT`, fallback `Tunai` |
| `Transactions[].Items[]` | `transaction_items` | Pecah repeating group, snapshot dipertahankan |
| `Movements[]` | `stock_movements` | `Jenis` → `movement_type_id`, buang `kode`/`nama` |
| `Movements[].Jenis` | `movement_types` | `Masuk` = +1, `Keluar` = −1 |
| `settings.json` | `store_settings` | Satu baris |
| `users.json` | `users` | Hash PBKDF2 dipertahankan/dibuat ulang |
| `NextProductId`, `NextTrx`, `NextMovementId` | Tidak ada | Diganti `AUTO_INCREMENT` / format ID |
| ID `TRX-yyyyMMdd-###` | `transactions.id` | Tetap dipertahankan sebagai business key |

---

## 9. Kesimpulan

1. Data lama melanggar **1NF** karena menyimpan daftar item di dalam nota dan
   beberapa kolom gabungan.
2. Data lama melanggar **2NF** karena atribut produk menempel pada baris nota.
3. Data lama melanggar **3NF** karena kategori, satuan, supplier, metode bayar,
   dan jenis mutasi disimpan sebagai teks berulang.
4. Skema target sudah **3NF/BCNF**.
5. Snapshot harga pada item nota dan stok pada produk adalah
   **denormalisasi terkontrol** yang dipertahankan untuk audit dan performa,
   dengan validasi integritas yang eksplisit.
