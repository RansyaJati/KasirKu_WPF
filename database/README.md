# Database KasirKu — Skema Target & Panduan Migrasi

Folder ini berisi **skema database target ternormalisasi (3NF)** untuk KasirKu,
lengkap dengan tabel staging dan panduan memindahkan data dari database lama klien.

> **Penting:** aplikasi KasirKu saat ini masih menyimpan data di JSON
> (`Documents/KasirKu/*.json`). Folder ini adalah **target migrasi + dokumen TA**,
> bukan koneksi database yang sudah aktif di aplikasi.

---

## 1. Jawaban singkat: database lama ditaruh di mana?

**Jangan dicampur ke schema `kasirku`.** Pakai tiga schema terpisah:

```
┌─────────────────────┐     ┌──────────────────────┐     ┌─────────────────┐
│  kasirku_legacy     │     │  kasirku_staging     │     │  kasirku        │
│  dump asli klien    │ ──> │  stg_* + peta ID     │ ──> │  target 3NF     │
│  JANGAN diedit      │     │  boleh diubah        │     │  siap dipakai   │
└─────────────────────┘     └──────────────────────┘     └─────────────────┘
```

| Schema | Isi | Boleh diubah? |
|---|---|---|
| `kasirku_legacy` | Hasil restore dump database lama | **Tidak.** Read-only |
| `kasirku_staging` | Salinan mentah + kolom hasil normalisasi + peta ID | Ya, selama migrasi |
| `kasirku` | Skema target 3NF | Hanya lewat ETL, bukan manual |

**Aturan praktis:**

- File dump/backup asli simpan di `database/legacy/raw/` — folder ini
  **di-ignore git** karena berisi data pelanggan asli.
- Jika database lama **MySQL/MariaDB**: restore ke schema `kasirku_legacy`
  di server yang sama, lalu tarik ke staging dengan `INSERT ... SELECT`.
- Jika database lama **SQL Server lawas**: **jangan** jadikan target utama.
  Tetap di server klien, lalu ekspor per tabel ke CSV di
  `database/legacy/csv/` (juga di-ignore git), baru masukkan ke staging
  dengan `LOAD DATA LOCAL INFILE`. Detail ada di bagian 5.
- Yang di-commit ke git hanya **contoh data anonim** (mis. 10 baris) di
  `database/legacy/anonymized/`, bukan dump penuh.

---

## 2. Isi folder

```
database/
├── README.md                          <- file ini
├── mysql/
│   ├── 01_schema.sql                  <- DDL target 3NF + seed referensi
│   ├── 02_staging.sql                 <- schema staging + view validasi
│   ├── 03_validation.sql              <- query cek kualitas data
│   └── 04_etl_template.sql            <- template pemindahan staging -> target
├── docs/
│   ├── erd.md                         <- ERD Mermaid
│   ├── data-dictionary.md             <- kamus data lengkap
│   ├── normalisasi.md                 <- bukti 1NF/2NF/3NF + anomali data lama
│   └── mapping-legacy.md              <- template pemetaan kolom DB lama
├── legacy/
│   ├── README.md                      <- aturan penyimpanan dump lama
│   ├── raw/                           <- dump asli (gitignored)
│   ├── csv/                           <- hasil ekspor CSV (gitignored)
│   └── anonymized/                    <- sampel aman untuk git
└── tools/
    └── New-KasirKuPasswordHash.ps1    <- pembuat hash PBKDF2 yang kompatibel
```

---

## 3. Kebutuhan server

- **MySQL 8.0.16+** (disarankan) atau **MariaDB 10.6+**
  - `CHECK` constraint baru benar-benar dijalankan di MySQL 8.0.16+
  - `utf8mb4` wajib agar nama produk/emoji tidak rusak
- Akses `CREATE DATABASE`, `CREATE TABLE`, `INSERT`, `UPDATE`
- Jika memakai `LOAD DATA LOCAL INFILE`, aktifkan:
  - server: `local_infile = ON`
  - client: jalankan `mysql --local-infile=1`
- Timezone data disimpan sebagai `DATETIME` (tanpa zona); script menyetel
  `time_zone = '+07:00'` agar konsisten WIB.

**Catatan SQL Server lama:** skema ini sengaja tidak dibuat dalam dialect
T-SQL. Versi lawas (2005/2008/2008 R2) sudah EOL, keterbatasan tipe data
(`DATETIME2`, `OFFSET FETCH`, `STRING_AGG`) dan driver .NET modern membuat
biaya perawatannya lebih besar daripada manfaatnya untuk aplikasi standalone.
Gunakan SQL Server lama hanya sebagai **sumber data**, bukan target.

---

## 4. Urutan eksekusi

### Langkah 1 — Buat schema target

```bash
mysql -u root -p < mysql/01_schema.sql
```

Cek:

```sql
SHOW TABLES FROM kasirku;
SELECT * FROM kasirku.payment_methods;
SELECT * FROM kasirku.schema_migrations;
```

### Langkah 2 — Buat schema staging

```bash
mysql -u root -p < mysql/02_staging.sql
```

### Langkah 3 — Siapkan database lama

Pilih salah satu:

**A. Sumber MySQL/MariaDB**

```bash
mysql -u root -p -e "CREATE DATABASE kasirku_legacy CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"
mysql -u root -p kasirku_legacy < ../legacy/raw/legacy_20240101.sql
```

Jangan edit isi `kasirku_legacy`. Kalau perlu memperbaiki data, lakukan di staging.

**B. Sumber SQL Server lama**

1. Restore `.bak` di SQL Server klien (atau instance lokal terpisah).
2. Ekspor tiap tabel ke CSV UTF-8 dengan pemisah `;`:
   - SSMS → klik kanan database → **Tasks → Export Data**
   - atau `bcp "SELECT ... FROM db.dbo.produk" queryout produk.csv -c -C 65001 -t ";" -S server -U user -P pass`
3. Simpan CSV ke `database/legacy/csv/`.
4. Normalisasi format saat ekspor:
   - tanggal: `YYYY-MM-DD HH:MM:SS`
   - desimal: titik (`.`), tanpa pemisah ribuan
   - kolom teks dibungkus tanda kutip bila mengandung `;`

### Langkah 4 — Buat batch import

```sql
INSERT INTO kasirku_staging.import_batches
    (nama_file, sumber_db, versi_legacy, catatan)
VALUES
    ('legacy_20240101.sql', 'SQL Server 2008', 'v1', 'Migrasi awal');

SET @batch_id = LAST_INSERT_ID();
SELECT @batch_id;
```

### Langkah 5 — Masukkan data lama ke staging

**A. Dari `kasirku_legacy` (MySQL)**

```sql
SET @batch_id = 1;   -- sesuaikan

INSERT INTO kasirku_staging.stg_products
    (batch_id, legacy_id, kode, barcode, nama, satuan, kategori, supplier,
     harga_beli_raw, harga_jual_raw, stok_raw)
SELECT
    @batch_id,
    CAST(p.id_produk AS CHAR),
    p.kode_produk,
    p.barcode,
    p.nama_produk,
    p.satuan,
    p.kategori,
    p.supplier,
    CAST(p.harga_beli AS CHAR),
    CAST(p.harga_jual AS CHAR),
    CAST(p.stok AS CHAR)
FROM kasirku_legacy.produk p;
```

Sesuaikan nama tabel/kolom dengan database lama (lihat
`docs/mapping-legacy.md`). Lakukan hal yang sama untuk nota, item nota,
mutasi stok, dan user.

**B. Dari CSV (SQL Server / Excel)**

```sql
SET @batch_id = 1;

LOAD DATA LOCAL INFILE 'D:/TA/KasirKu/database/legacy/csv/produk.csv'
INTO TABLE kasirku_staging.stg_products
CHARACTER SET utf8mb4
FIELDS TERMINATED BY ';' OPTIONALLY ENCLOSED BY '"'
LINES TERMINATED BY '\r\n'
IGNORE 1 LINES
(legacy_id, kode, barcode, nama, satuan, kategori, supplier,
 harga_beli_raw, harga_jual_raw, stok_raw)
SET batch_id = @batch_id;
```

Ulangi untuk `stg_transactions`, `stg_transaction_items`,
`stg_stock_movements`, dan `stg_users`.

### Langkah 6 — Validasi sebelum ETL

```bash
mysql -u root -p < mysql/03_validation.sql
```

Perhatikan bagian **A (staging)**. Target: **A12 menunjukkan semua 0**.
Perbaiki baris bermasalah di staging, bukan di `kasirku_legacy`.

### Langkah 7 — Jalankan ETL

1. Buka `mysql/04_etl_template.sql`.
2. Sesuaikan `@batch_id`, `@temp_hash`, dan `@temp_salt` (bagian atas file).
3. Jalankan:

```bash
mysql -u root -p --local-infile=1 < mysql/04_etl_template.sql
```

Seluruh langkah dibungkus satu transaksi. Jika ada error SQL: `ROLLBACK`,
perbaiki staging, jalankan ulang. Baris berstatus `imported` tidak diduplikasi.

### Langkah 8 — Validasi setelah ETL

```bash
mysql -u root -p < mysql/03_validation.sql
```

Bagian **B** dan **C** harus bersih (0 baris untuk pemeriksaan integritas,
selisih omzet/qty = 0).

### Langkah 9 — Backup hasil

```bash
mysqldump -u root -p --single-transaction --routines --triggers kasirku > kasirku_setelah_migrasi.sql
```

---

## 5. Akun & password hasil migrasi

Aplikasi memakai **PBKDF2-SHA256, 100.000 iterasi, salt 16 byte, hash 32 byte**.
Hash lama (MD5, SHA1, plaintext) **tidak bisa dikonversi** tanpa password asli.

Dua pilihan aman:

1. **Buat akun baru lewat aplikasi** (paling sederhana), lalu biarkan
   `user_id` di transaksi lama bernilai `NULL`.
2. **Buat password sementara** untuk tiap akun, lalu minta pengguna menggantinya:

```powershell
pwsh -File database/tools/New-KasirKuPasswordHash.ps1 -Password "GantiSaya123!"
```

Salin `salt` dan `hash` ke variabel `@temp_salt` / `@temp_hash` pada
`04_etl_template.sql`. Jika variabel masih placeholder, ETL sengaja menandai
baris user sebagai error — supaya tidak ada akun dengan hash rusak.

---

## 6. Aturan penting saat migrasi

| Aturan | Alasan |
|---|---|
| `legacy_id` wajib unik & tidak boleh kosong | Dipakai untuk peta ID lama → baru |
| Jangan mengubah `kasirku_legacy` | Keaslian data harus bisa diaudit |
| Nominal harus angka titik | `DECIMAL` MySQL tidak menerima `Rp 10.000,00` |
| Tanggal staging format `YYYY-MM-DD HH:MM:SS` | `STR_TO_DATE` gagal pada format lain |
| Barcode boleh kosong, tapi kalau ada harus unik | Unique key `uk_products_barcode` |
| Kode produk wajib unik, maks. 20 karakter | Unique key `uk_products_kode` |
| `bayar >= total` | Constraint `chk_transactions_bayar_cukup` |
| Stok tidak boleh negatif | Constraint `chk_products_stok` |
| Arah mutasi harus cocok dengan stok sebelum/sesudah | Validasi integritas kartu stok |
| Item nota menyimpan snapshot harga | Harga lama tidak berubah saat produk di-edit |

---

## 7. Kalau ada data yang gagal

1. Lihat `v_stg_error_semua` dan `v_stg_*_invalid`.
2. Perbaiki **hanya** baris staging yang error (`UPDATE ... WHERE row_id = ...`).
3. Reset baris itu ke `pending`:

```sql
UPDATE kasirku_staging.stg_products
SET status = 'pending', error_msg = NULL
WHERE row_id = 123;
```

4. Jalankan ulang `04_etl_template.sql`.

Baris yang sudah `imported` tidak akan dimasukkan dua kali karena:
- produk & transaksi memakai `LEFT JOIN ... WHERE target IS NULL`,
- item nota dicek per nota (`transaction_id` sudah punya item → dilewati),
- `legacy_id_map` memakai `PRIMARY KEY (entity, old_id)`.

---

## 8. Reset saat pengembangan / demo TA

Urutan yang aman:

```sql
-- 1. Backup dulu
--    mysqldump -u root -p kasirku > backup_kasirku.sql

-- 2. Hapus target & staging (JANGAN hapus kasirku_legacy)
DROP DATABASE IF EXISTS kasirku;
DROP DATABASE IF EXISTS kasirku_staging;

-- 3. Jalankan ulang
--    mysql/01_schema.sql
--    mysql/02_staging.sql
```

---

## 9. Hubungan dengan aplikasi

- Skema ini **belum** dipakai aplikasi. `AGENT.md` masih berlaku:
  aplikasi memakai JSON, tanpa NuGet, tanpa DB.
- Kalau nanti aplikasi benar-benar dihubungkan ke MySQL, perlu keputusan
  sadar untuk mengubah aturan proyek dan menambah provider NuGet
  (mis. `MySqlConnector`) serta mengganti implementasi
  `KasirKu.Shared/Repositories/Json/*` dengan repository MySQL.
- Dokumen normalisasi untuk laporan TA ada di `docs/normalisasi.md`.
