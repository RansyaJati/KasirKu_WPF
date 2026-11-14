# Aturan Penyimpanan Database Lama Klien

Folder ini adalah tempat Anda menaruh file backup / dump database lama klien
yang akan dinormalisasi ke skema KasirKu.

## 1. Struktur Folder

```
database/legacy/
├── README.md               <- File panduan ini (di-commit ke git)
├── .gitignore              <- Mencegah data mentah klien bocor ke git
├── raw/                    <- Dump SQL / file .bak asli (GITIGNORED)
├── csv/                    <- File CSV hasil ekspor dari database lama (GITIGNORED)
└── anonymized/             <- Sampel data yang sudah disamarkan (boleh di-commit)
```

## 2. Di mana menaruh database yang belum dinormalisasi?

Jawaban praktis:

### A. Simpan file dump/backup di:
- **`database/legacy/raw/`** jika formatnya `.sql`, `.bak`, `.dump`, `.json`.
- Folder ini **sudah di-ignore oleh git**, jadi aman dari risiko data pelanggan atau harga rahasia klien ter-push ke repository publik/kampus.

### B. Restore ke database lokal dengan nama terpisah:
- **Jangan pernah me-restore dump lama ke database `kasirku`!**
- Buat database khusus dengan nama **`kasirku_legacy`**:
  ```sql
  CREATE DATABASE kasirku_legacy CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
  ```
- Jadikan database ini **Read-Only**: jangan lakukan normalisasi atau edit data di sini. Ini arsip bukti kondisi data sebelum diperbaiki.

### C. Jika database lama adalah SQL Server lawas:
1. Restore file `.bak` di SQL Server klien (atau SQL Server Express lokal Anda).
2. Ekspor tabel produk, transaksi, detail transaksi, dan kartu stok ke CSV bertipe UTF-8 dengan pemisah titik koma (`;`).
3. Taruh file CSV di folder **`database/legacy/csv/`**.
4. Tarik data CSV tersebut ke staging via query `LOAD DATA LOCAL INFILE` (lihat panduan di `database/README.md` Bagian 4 Langkah 5.B).

### D. Sampel untuk bukti/laporan TA:
- Jika dosen atau tim penguji butuh melihat contoh data lama, buat **10-20 baris data dummy / anonim** (samarkan nama toko, harga asli, atau nama kasir) dan taruh di:
  `database/legacy/anonymized/sample_legacy_data.sql` atau `.csv`.
- Folder `anonymized/` adalah satu-satunya subfolder di sini yang boleh masuk ke Git commit.

## 3. Checklist Keamanan Data Klien

Sebelum menjalankan normalisasi:
- [ ] File backup asli sudah dicadangkan di flashdisk / drive eksternal terpisah.
- [ ] Folder `database/legacy/raw/` dan `database/legacy/csv/` berstatus ignored oleh Git (`git status` tidak mendeteksi file dump).
- [ ] Database `kasirku_legacy` tidak disentuh operasi `UPDATE` / `DELETE` (jadikan data referensi audit).
