-- ============================================================================
-- KasirKu — 04_etl_template.sql
-- Template ETL: staging (data mentah) -> target `kasirku` (3NF).
--
-- ⚠  JANGAN langsung dijalankan tanpa menyesuaikan bagian bertanda [SESUAIKAN].
-- ⚠  Jalankan di server MySQL yang sama dengan schema kasirku & kasirku_staging.
-- ⚠  Seluruh langkah DML dibungkus satu transaksi. Jika ada error: ROLLBACK,
--    perbaiki staging, lalu jalankan ulang dari awal.
--
-- Urutan:
--   1. Lookup       : kategori, satuan, supplier, metode bayar, jenis mutasi
--   2. Users        : opsional (hash password lama tidak kompatibel)
--   3. Products
--   4. Transactions
--   5. Transaction items
--   6. Stock movements
--   7. Finalisasi batch
--
-- Sebelum menjalankan: pastikan 03_validation.sql bagian A12 = 0 masalah.
-- ============================================================================

USE kasirku_staging;

SET NAMES utf8mb4;
SET time_zone = '+07:00';

-- [SESUAIKAN] batch yang akan diproses. NULL = batch terakhir.
SET @batch_id = (SELECT MAX(id) FROM kasirku_staging.import_batches);

-- [SESUAIKAN] Hash & salt sementara untuk akun hasil migrasi.
-- Buat dengan:
--   pwsh -File database/tools/New-KasirKuPasswordHash.ps1 -Password "GantiSaya123!"
-- Jika dibiarkan placeholder, langkah import user akan ditandai error
-- (memang disengaja, supaya tidak ada akun dengan password tidak valid).
SET @temp_hash = '<isi-hash-dari-script-PowerShell>';
SET @temp_salt = '<isi-salt-dari-script-PowerShell>';

SELECT @batch_id AS batch_yang_diproses;

-- ============================================================================
-- STEP 0. RESET BARIS YANG BELUM TER-IMPORT (supaya aman dijalankan ulang)
-- Baris berstatus 'imported' tidak disentuh.
-- ============================================================================

UPDATE stg_products
SET status = 'pending', error_msg = NULL,
    resolved_category_id = NULL, resolved_unit_id = NULL,
    resolved_supplier_id = NULL, resolved_product_id = NULL
WHERE batch_id = @batch_id AND status IN ('pending', 'error');

UPDATE stg_transactions
SET status = 'pending', error_msg = NULL,
    resolved_payment_method_id = NULL, resolved_user_id = NULL,
    resolved_transaction_id = NULL
WHERE batch_id = @batch_id AND status IN ('pending', 'error');

UPDATE stg_transaction_items
SET status = 'pending', error_msg = NULL,
    resolved_transaction_id = NULL, resolved_product_id = NULL
WHERE batch_id = @batch_id AND status IN ('pending', 'error');

UPDATE stg_stock_movements
SET status = 'pending', error_msg = NULL,
    resolved_product_id = NULL, resolved_movement_type_id = NULL,
    resolved_transaction_id = NULL
WHERE batch_id = @batch_id AND status IN ('pending', 'error');

UPDATE stg_users
SET status = 'pending', error_msg = NULL,
    resolved_role = NULL, resolved_user_id = NULL
WHERE batch_id = @batch_id AND status IN ('pending', 'error');

START TRANSACTION;

-- ============================================================================
-- STEP 1. NORMALISASI LOOKUP
-- Nilai teks bebas dari data lama diubah menjadi baris tabel referensi.
-- ============================================================================

-- Pastikan nilai default selalu ada.
INSERT IGNORE INTO kasirku.categories (nama) VALUES ('Umum');
INSERT IGNORE INTO kasirku.units (nama) VALUES ('Pcs');
INSERT IGNORE INTO kasirku.payment_methods (nama) VALUES ('Tunai');

-- 1.1 Kategori (fallback: 'Umum')
INSERT IGNORE INTO kasirku.categories (nama)
SELECT DISTINCT LEFT(TRIM(kategori), 50)
FROM stg_products
WHERE batch_id = @batch_id
  AND NULLIF(TRIM(kategori), '') IS NOT NULL;

-- 1.2 Satuan (fallback: 'Pcs')
INSERT IGNORE INTO kasirku.units (nama)
SELECT DISTINCT LEFT(TRIM(satuan), 20)
FROM stg_products
WHERE batch_id = @batch_id
  AND NULLIF(TRIM(satuan), '') IS NOT NULL;

-- 1.3 Supplier (boleh kosong)
INSERT IGNORE INTO kasirku.suppliers (nama)
SELECT DISTINCT LEFT(TRIM(supplier), 100)
FROM stg_products
WHERE batch_id = @batch_id
  AND NULLIF(TRIM(supplier), '') IS NOT NULL;

-- 1.4 Metode pembayaran (fallback: 'Tunai')
INSERT IGNORE INTO kasirku.payment_methods (nama)
SELECT DISTINCT LEFT(COALESCE(NULLIF(TRIM(metode_bayar), ''), 'Tunai'), 30)
FROM stg_transactions
WHERE batch_id = @batch_id;

-- 1.5 Resolusi FK produk
UPDATE stg_products s
JOIN kasirku.categories c
  ON c.nama = LEFT(COALESCE(NULLIF(TRIM(s.kategori), ''), 'Umum'), 50)
JOIN kasirku.units u
  ON u.nama = LEFT(COALESCE(NULLIF(TRIM(s.satuan), ''), 'Pcs'), 20)
LEFT JOIN kasirku.suppliers sp
  ON sp.nama = LEFT(TRIM(s.supplier), 100)
 AND NULLIF(TRIM(s.supplier), '') IS NOT NULL
SET s.resolved_category_id = c.id,
    s.resolved_unit_id     = u.id,
    s.resolved_supplier_id = sp.id
WHERE s.batch_id = @batch_id;

-- 1.6 Resolusi FK metode bayar
UPDATE stg_transactions s
JOIN kasirku.payment_methods pm
  ON pm.nama = LEFT(COALESCE(NULLIF(TRIM(s.metode_bayar), ''), 'Tunai'), 30)
SET s.resolved_payment_method_id = pm.id
WHERE s.batch_id = @batch_id;

-- 1.7 Resolusi jenis mutasi stok
UPDATE stg_stock_movements s
JOIN kasirku.movement_types mt
  ON mt.nama = TRIM(s.jenis)
SET s.resolved_movement_type_id = mt.id
WHERE s.batch_id = @batch_id;

-- ============================================================================
-- STEP 2. USERS (opsional)
-- Hash password aplikasi = PBKDF2-SHA256, 100.000 iterasi, salt 16 byte.
-- Hash lama (MD5/SHA1/plaintext) TIDAK bisa dikonversi tanpa password asli.
-- Strategi aman: buat akun dengan password sementara, lalu minta user ganti.
-- ============================================================================

UPDATE stg_users
SET resolved_role = CASE
        WHEN LOWER(TRIM(role_raw)) IN ('manager', 'admin', 'administrator', 'owner') THEN 'Manager'
        WHEN LOWER(TRIM(role_raw)) IN ('kasir', 'cashier', 'operator')               THEN 'Kasir'
        ELSE NULL
    END
WHERE batch_id = @batch_id;

UPDATE stg_users
SET status = 'error', error_msg = 'Role tidak dikenali (harus Kasir/Manager)'
WHERE batch_id = @batch_id AND resolved_role IS NULL;

UPDATE stg_users
SET status = 'error', error_msg = 'Username kosong atau lebih dari 50 karakter'
WHERE batch_id = @batch_id
  AND (TRIM(COALESCE(username, '')) = '' OR CHAR_LENGTH(TRIM(username)) > 50);

-- Username duplikat di staging -> tandai semua sebagai error.
DROP TEMPORARY TABLE IF EXISTS tmp_dup_username;
CREATE TEMPORARY TABLE tmp_dup_username AS
SELECT LOWER(TRIM(username)) AS username
FROM stg_users
WHERE batch_id = @batch_id AND TRIM(COALESCE(username, '')) <> ''
GROUP BY LOWER(TRIM(username))
HAVING COUNT(*) > 1;

UPDATE stg_users s
JOIN tmp_dup_username d ON d.username = LOWER(TRIM(s.username))
SET s.status = 'error', s.error_msg = 'Username duplikat di staging'
WHERE s.batch_id = @batch_id;

UPDATE stg_users
SET status = 'valid'
WHERE batch_id = @batch_id
  AND status = 'pending'
  AND resolved_role IS NOT NULL
  AND TRIM(COALESCE(username, '')) <> ''
  AND CHAR_LENGTH(TRIM(username)) <= 50;

-- Guard: hanya jalan jika hash sementara valid dan bukan placeholder.
UPDATE stg_users
SET status = 'error',
    error_msg = 'Password lama tidak kompatibel; buat akun via aplikasi atau isi @temp_hash/@temp_salt'
WHERE batch_id = @batch_id
  AND status = 'valid'
  AND (@temp_hash NOT REGEXP '^[A-Za-z0-9+/]{43}=$'
       OR @temp_salt NOT REGEXP '^[A-Za-z0-9+/]{22}==$');

INSERT INTO kasirku.users (username, nama_lengkap, role, password_hash, salt, aktif)
SELECT
    LOWER(TRIM(s.username)),
    LEFT(COALESCE(NULLIF(TRIM(s.nama_lengkap), ''), TRIM(s.username)), 100),
    s.resolved_role,
    @temp_hash,
    @temp_salt,
    CASE WHEN LOWER(TRIM(COALESCE(s.aktif_raw, '1')))
              IN ('0', 'false', 'tidak', 'n') THEN FALSE ELSE TRUE END
FROM stg_users s
LEFT JOIN kasirku.users ex ON ex.username = LOWER(TRIM(s.username))
WHERE s.batch_id = @batch_id
  AND s.status = 'valid'
  AND ex.id IS NULL;

INSERT INTO legacy_id_map (entity, old_id, new_id)
SELECT 'user', s.legacy_id, CAST(u.id AS CHAR)
FROM stg_users s
JOIN kasirku.users u ON u.username = LOWER(TRIM(s.username))
WHERE s.batch_id = @batch_id
  AND s.status = 'valid'
  AND NULLIF(TRIM(s.legacy_id), '') IS NOT NULL
ON DUPLICATE KEY UPDATE new_id = VALUES(new_id);

UPDATE stg_users s
JOIN legacy_id_map m ON m.entity = 'user' AND m.old_id = s.legacy_id
SET s.resolved_user_id = CAST(m.new_id AS UNSIGNED)
WHERE s.batch_id = @batch_id;

UPDATE stg_users
SET status = 'imported'
WHERE batch_id = @batch_id AND status = 'valid' AND resolved_user_id IS NOT NULL;

-- ============================================================================
-- STEP 3. PRODUCTS
-- ============================================================================

UPDATE stg_products
SET status = 'error', error_msg = CONCAT_WS('; ',
    IF(TRIM(COALESCE(kode, '')) = '', 'kode kosong', NULL),
    IF(CHAR_LENGTH(TRIM(COALESCE(kode, ''))) > 20, 'kode > 20 karakter', NULL),
    IF(TRIM(COALESCE(nama, '')) = '', 'nama kosong', NULL),
    IF(CHAR_LENGTH(TRIM(COALESCE(barcode, ''))) > 50, 'barcode > 50 karakter', NULL),
    IF(TRIM(COALESCE(harga_beli_raw, '')) NOT REGEXP '^[0-9]+([.,][0-9]+)?$', 'harga_beli bukan angka', NULL),
    IF(TRIM(COALESCE(harga_jual_raw, '')) NOT REGEXP '^[0-9]+([.,][0-9]+)?$', 'harga_jual bukan angka', NULL),
    IF(TRIM(COALESCE(stok_raw, '')) NOT REGEXP '^[0-9]+$', 'stok bukan bilangan bulat', NULL),
    IF(COALESCE(resolved_category_id, 0) = 0, 'kategori gagal dipetakan', NULL),
    IF(COALESCE(resolved_unit_id, 0) = 0, 'satuan gagal dipetakan', NULL),
    IF(TRIM(COALESCE(legacy_id, '')) = '', 'legacy_id kosong', NULL)
)
WHERE batch_id = @batch_id AND status = 'pending';

DROP TEMPORARY TABLE IF EXISTS tmp_dup_kode;
CREATE TEMPORARY TABLE tmp_dup_kode AS
SELECT TRIM(kode) AS kode
FROM stg_products
WHERE batch_id = @batch_id AND TRIM(COALESCE(kode, '')) <> ''
GROUP BY TRIM(kode)
HAVING COUNT(*) > 1;

UPDATE stg_products s
JOIN tmp_dup_kode d ON d.kode = TRIM(s.kode)
SET s.status = 'error', s.error_msg = CONCAT_WS('; ', s.error_msg, 'kode duplikat di staging')
WHERE s.batch_id = @batch_id;

DROP TEMPORARY TABLE IF EXISTS tmp_dup_barcode;
CREATE TEMPORARY TABLE tmp_dup_barcode AS
SELECT TRIM(barcode) AS barcode
FROM stg_products
WHERE batch_id = @batch_id AND TRIM(COALESCE(barcode, '')) <> ''
GROUP BY TRIM(barcode)
HAVING COUNT(*) > 1;

UPDATE stg_products s
JOIN tmp_dup_barcode d ON d.barcode = TRIM(s.barcode)
SET s.status = 'error', s.error_msg = CONCAT_WS('; ', s.error_msg, 'barcode duplikat di staging')
WHERE s.batch_id = @batch_id;

UPDATE stg_products
SET status = 'valid'
WHERE batch_id = @batch_id AND status = 'pending';

INSERT INTO kasirku.products
    (kode, barcode, nama, category_id, unit_id, supplier_id,
     harga_beli, harga_jual, stok, aktif)
SELECT
    TRIM(s.kode),
    NULLIF(TRIM(s.barcode), ''),
    LEFT(TRIM(s.nama), 150),
    s.resolved_category_id,
    s.resolved_unit_id,
    s.resolved_supplier_id,
    CAST(REPLACE(TRIM(s.harga_beli_raw), ',', '.') AS DECIMAL(15,2)),
    CAST(REPLACE(TRIM(s.harga_jual_raw), ',', '.') AS DECIMAL(15,2)),
    CAST(TRIM(s.stok_raw) AS UNSIGNED),
    TRUE
FROM stg_products s
LEFT JOIN kasirku.products ex ON ex.kode = TRIM(s.kode)
WHERE s.batch_id = @batch_id
  AND s.status = 'valid'
  AND ex.id IS NULL;

INSERT INTO legacy_id_map (entity, old_id, new_id)
SELECT 'product', s.legacy_id, CAST(p.id AS CHAR)
FROM stg_products s
JOIN kasirku.products p ON p.kode = TRIM(s.kode)
WHERE s.batch_id = @batch_id
  AND s.status = 'valid'
  AND NULLIF(TRIM(s.legacy_id), '') IS NOT NULL
ON DUPLICATE KEY UPDATE new_id = VALUES(new_id);

UPDATE stg_products s
JOIN legacy_id_map m ON m.entity = 'product' AND m.old_id = s.legacy_id
SET s.resolved_product_id = CAST(m.new_id AS UNSIGNED)
WHERE s.batch_id = @batch_id;

UPDATE stg_products
SET status = 'imported'
WHERE batch_id = @batch_id AND status = 'valid' AND resolved_product_id IS NOT NULL;

-- ============================================================================
-- STEP 4. TRANSACTIONS
-- ============================================================================

-- Isi bayar kosong dengan total (lazim untuk non-tunai di data lama).
UPDATE stg_transactions
SET bayar_raw = total_raw
WHERE batch_id = @batch_id
  AND TRIM(COALESCE(bayar_raw, '')) = ''
  AND TRIM(COALESCE(total_raw, '')) <> '';

-- [SESUAIKAN] Jika tanggal lama masih dd/mm/yyyy, aktifkan konversi berikut
-- dengan urutan hari/bulan yang benar.
-- UPDATE stg_transactions
-- SET tanggal_raw = DATE_FORMAT(
--         STR_TO_DATE(TRIM(tanggal_raw), '%d/%m/%Y %H:%i:%s'),
--         '%Y-%m-%d %H:%i:%s')
-- WHERE batch_id = @batch_id
--   AND TRIM(tanggal_raw) REGEXP '^[0-9]{2}/[0-9]{2}/[0-9]{4}';

-- Resolusi kasir (boleh gagal; user_id di target boleh NULL).
UPDATE stg_transactions s
JOIN kasirku.users u ON u.username = LOWER(TRIM(s.kasir))
SET s.resolved_user_id = u.id
WHERE s.batch_id = @batch_id
  AND NULLIF(TRIM(s.kasir), '') IS NOT NULL;

UPDATE stg_transactions
SET status = 'error', error_msg = CONCAT_WS('; ',
    IF(TRIM(COALESCE(trx_id, '')) = '', 'nomor nota kosong', NULL),
    IF(CHAR_LENGTH(TRIM(COALESCE(trx_id, ''))) > 20, 'nomor nota > 20 karakter', NULL),
    IF(STR_TO_DATE(NULLIF(TRIM(tanggal_raw), ''), '%Y-%m-%d %H:%i:%s') IS NULL,
       'tanggal tidak valid (format harus YYYY-MM-DD HH:MM:SS)', NULL),
    IF(TRIM(COALESCE(total_raw, '')) NOT REGEXP '^[0-9]+([.,][0-9]+)?$',
       'total bukan angka', NULL),
    IF(TRIM(COALESCE(bayar_raw, '')) NOT REGEXP '^[0-9]+([.,][0-9]+)?$',
       'bayar bukan angka', NULL),
    IF(CAST(REPLACE(COALESCE(bayar_raw, '0'), ',', '.') AS DECIMAL(15,2))
       < CAST(REPLACE(COALESCE(total_raw, '0'), ',', '.') AS DECIMAL(15,2)),
       'bayar < total', NULL),
    IF(COALESCE(resolved_payment_method_id, 0) = 0, 'metode bayar gagal dipetakan', NULL),
    IF(TRIM(COALESCE(legacy_id, '')) = '', 'legacy_id kosong', NULL)
)
WHERE batch_id = @batch_id AND status = 'pending';

DROP TEMPORARY TABLE IF EXISTS tmp_dup_trx;
CREATE TEMPORARY TABLE tmp_dup_trx AS
SELECT TRIM(trx_id) AS trx_id
FROM stg_transactions
WHERE batch_id = @batch_id AND TRIM(COALESCE(trx_id, '')) <> ''
GROUP BY TRIM(trx_id)
HAVING COUNT(*) > 1;

UPDATE stg_transactions s
JOIN tmp_dup_trx d ON d.trx_id = TRIM(s.trx_id)
SET s.status = 'error', s.error_msg = CONCAT_WS('; ', s.error_msg, 'nomor nota duplikat di staging')
WHERE s.batch_id = @batch_id;

UPDATE stg_transactions
SET status = 'valid'
WHERE batch_id = @batch_id AND status = 'pending';

INSERT INTO kasirku.transactions
    (id, tanggal, user_id, payment_method_id, total, bayar, catatan)
SELECT
    TRIM(s.trx_id),
    STR_TO_DATE(TRIM(s.tanggal_raw), '%Y-%m-%d %H:%i:%s'),
    s.resolved_user_id,
    s.resolved_payment_method_id,
    CAST(REPLACE(TRIM(s.total_raw), ',', '.') AS DECIMAL(15,2)),
    CAST(REPLACE(TRIM(s.bayar_raw), ',', '.') AS DECIMAL(15,2)),
    NULL
FROM stg_transactions s
LEFT JOIN kasirku.transactions ex ON ex.id = TRIM(s.trx_id)
WHERE s.batch_id = @batch_id
  AND s.status = 'valid'
  AND ex.id IS NULL;

INSERT INTO legacy_id_map (entity, old_id, new_id)
SELECT 'transaction', s.legacy_id, t.id
FROM stg_transactions s
JOIN kasirku.transactions t ON t.id = TRIM(s.trx_id)
WHERE s.batch_id = @batch_id
  AND s.status = 'valid'
  AND NULLIF(TRIM(s.legacy_id), '') IS NOT NULL
ON DUPLICATE KEY UPDATE new_id = VALUES(new_id);

UPDATE stg_transactions s
JOIN legacy_id_map m ON m.entity = 'transaction' AND m.old_id = s.legacy_id
SET s.resolved_transaction_id = m.new_id
WHERE s.batch_id = @batch_id;

UPDATE stg_transactions
SET status = 'imported'
WHERE batch_id = @batch_id AND status = 'valid' AND resolved_transaction_id IS NOT NULL;

-- ============================================================================
-- STEP 5. TRANSACTION ITEMS
-- ============================================================================

-- 5.1 Resolusi header nota (wajib berhasil).
UPDATE stg_transaction_items s
JOIN legacy_id_map m ON m.entity = 'transaction' AND m.old_id = s.legacy_trx_id
SET s.resolved_transaction_id = m.new_id
WHERE s.batch_id = @batch_id
  AND s.resolved_transaction_id IS NULL;

-- 5.2 Resolusi produk: lewat legacy_id dulu, fallback ke kode.
UPDATE stg_transaction_items s
LEFT JOIN legacy_id_map m
       ON m.entity = 'product' AND m.old_id = s.legacy_product_id
LEFT JOIN kasirku.products p
       ON p.kode = TRIM(s.kode)
SET s.resolved_product_id = COALESCE(CAST(m.new_id AS UNSIGNED), p.id)
WHERE s.batch_id = @batch_id
  AND s.resolved_product_id IS NULL;

UPDATE stg_transaction_items
SET status = 'error', error_msg = CONCAT_WS('; ',
    IF(TRIM(COALESCE(legacy_trx_id, '')) = '', 'legacy_trx_id kosong', NULL),
    IF(resolved_transaction_id IS NULL, 'header nota tidak ditemukan', NULL),
    IF(TRIM(COALESCE(harga_raw, '')) NOT REGEXP '^[0-9]+([.,][0-9]+)?$',
       'harga bukan angka', NULL),
    IF(TRIM(COALESCE(harga_beli_raw, '')) <> ''
       AND TRIM(COALESCE(harga_beli_raw, '')) NOT REGEXP '^[0-9]+([.,][0-9]+)?$',
       'harga_beli bukan angka', NULL),
    IF(TRIM(COALESCE(qty_raw, '')) NOT REGEXP '^[0-9]+$', 'qty bukan bilangan bulat', NULL),
    IF(CAST(COALESCE(NULLIF(TRIM(qty_raw), ''), '0') AS UNSIGNED) = 0, 'qty = 0', NULL)
)
WHERE batch_id = @batch_id AND status = 'pending';

UPDATE stg_transaction_items
SET status = 'valid'
WHERE batch_id = @batch_id
  AND status = 'pending'
  AND resolved_transaction_id IS NOT NULL;

-- 5.3 Insert hanya jika nota belum punya item (idempoten per nota).
INSERT INTO kasirku.transaction_items
    (transaction_id, product_id, kode, nama, harga_beli, harga_jual, qty)
SELECT
    s.resolved_transaction_id,
    s.resolved_product_id,
    LEFT(COALESCE(NULLIF(TRIM(s.kode), ''), p.kode, 'UNKNOWN'), 20),
    LEFT(COALESCE(NULLIF(TRIM(s.nama), ''), p.nama, 'Item tanpa nama'), 150),
    CASE
        WHEN TRIM(COALESCE(s.harga_beli_raw, '')) REGEXP '^[0-9]+([.,][0-9]+)?$'
             AND CAST(REPLACE(TRIM(s.harga_beli_raw), ',', '.') AS DECIMAL(15,2)) > 0
        THEN CAST(REPLACE(TRIM(s.harga_beli_raw), ',', '.') AS DECIMAL(15,2))
        ELSE COALESCE(p.harga_beli, 0)
    END,
    COALESCE(
        NULLIF(CAST(REPLACE(TRIM(s.harga_raw), ',', '.') AS DECIMAL(15,2)), 0),
        p.harga_jual,
        0
    ),
    CAST(TRIM(s.qty_raw) AS UNSIGNED)
FROM stg_transaction_items s
LEFT JOIN kasirku.products p ON p.id = s.resolved_product_id
LEFT JOIN (
    SELECT DISTINCT transaction_id
    FROM kasirku.transaction_items
) x ON x.transaction_id = s.resolved_transaction_id
WHERE s.batch_id = @batch_id
  AND s.status = 'valid'
  AND x.transaction_id IS NULL;

UPDATE stg_transaction_items
SET status = 'imported'
WHERE batch_id = @batch_id AND status = 'valid';

-- ============================================================================
-- STEP 6. STOCK MOVEMENTS
-- ============================================================================

-- 6.1 Resolusi produk: legacy_id dulu, fallback kode.
UPDATE stg_stock_movements s
LEFT JOIN legacy_id_map m
       ON m.entity = 'product' AND m.old_id = s.legacy_product_id
LEFT JOIN kasirku.products p
       ON p.kode = TRIM(s.kode)
SET s.resolved_product_id = COALESCE(CAST(m.new_id AS UNSIGNED), p.id)
WHERE s.batch_id = @batch_id
  AND s.resolved_product_id IS NULL;

-- 6.2 Resolusi nota (opsional).
UPDATE stg_stock_movements s
JOIN legacy_id_map m
  ON m.entity = 'transaction' AND m.old_id = s.legacy_trx_id
SET s.resolved_transaction_id = m.new_id
WHERE s.batch_id = @batch_id
  AND NULLIF(TRIM(s.legacy_trx_id), '') IS NOT NULL;

UPDATE stg_stock_movements
SET status = 'error', error_msg = CONCAT_WS('; ',
    IF(TRIM(COALESCE(legacy_product_id, '')) = '', 'legacy_product_id kosong', NULL),
    IF(resolved_product_id IS NULL, 'produk tidak ditemukan', NULL),
    IF(resolved_movement_type_id IS NULL, 'jenis mutasi tidak dikenali (Masuk/Keluar)', NULL),
    IF(STR_TO_DATE(NULLIF(TRIM(tanggal_raw), ''), '%Y-%m-%d %H:%i:%s') IS NULL,
       'tanggal tidak valid (format harus YYYY-MM-DD HH:MM:SS)', NULL),
    IF(TRIM(COALESCE(qty_raw, '')) NOT REGEXP '^[0-9]+$', 'qty bukan bilangan bulat', NULL),
    IF(TRIM(COALESCE(stok_sebelum_raw, '')) NOT REGEXP '^[0-9]+$',
       'stok_sebelum bukan bilangan bulat', NULL),
    IF(TRIM(COALESCE(stok_sesudah_raw, '')) NOT REGEXP '^[0-9]+$',
       'stok_sesudah bukan bilangan bulat', NULL)
)
WHERE batch_id = @batch_id AND status = 'pending';

-- 6.3 Arah mutasi harus cocok dengan jenisnya.
UPDATE stg_stock_movements s
JOIN kasirku.movement_types mt ON mt.id = s.resolved_movement_type_id
SET s.status = 'error',
    s.error_msg = CONCAT_WS('; ', s.error_msg,
        'stok_sesudah - stok_sebelum tidak cocok dengan jenis mutasi')
WHERE s.batch_id = @batch_id
  AND s.status = 'pending'
  AND (CAST(TRIM(s.stok_sesudah_raw) AS SIGNED)
       - CAST(TRIM(s.stok_sebelum_raw) AS SIGNED))
      <> mt.arah * CAST(TRIM(s.qty_raw) AS SIGNED);

UPDATE stg_stock_movements
SET status = 'valid'
WHERE batch_id = @batch_id AND status = 'pending';

INSERT INTO kasirku.stock_movements
    (tanggal, product_id, movement_type_id, qty, stok_sebelum, stok_sesudah,
     keterangan, transaction_id, user_id)
SELECT
    STR_TO_DATE(TRIM(s.tanggal_raw), '%Y-%m-%d %H:%i:%s'),
    s.resolved_product_id,
    s.resolved_movement_type_id,
    CAST(TRIM(s.qty_raw) AS UNSIGNED),
    CAST(TRIM(s.stok_sebelum_raw) AS UNSIGNED),
    CAST(TRIM(s.stok_sesudah_raw) AS UNSIGNED),
    LEFT(COALESCE(s.keterangan, ''), 255),
    s.resolved_transaction_id,
    NULL
FROM stg_stock_movements s
WHERE s.batch_id = @batch_id
  AND s.status = 'valid';

UPDATE stg_stock_movements
SET status = 'imported'
WHERE batch_id = @batch_id AND status = 'valid';

-- ============================================================================
-- STEP 7. FINALISASI
-- ============================================================================

DROP TEMPORARY TABLE IF EXISTS tmp_dup_username;
DROP TEMPORARY TABLE IF EXISTS tmp_dup_kode;
DROP TEMPORARY TABLE IF EXISTS tmp_dup_barcode;
DROP TEMPORARY TABLE IF EXISTS tmp_dup_trx;

UPDATE import_batches
SET selesai = NOW(),
    status = CASE
        WHEN (SELECT COUNT(*) FROM v_stg_error_semua WHERE batch_id = @batch_id) > 0
        THEN 'gagal' ELSE 'selesai'
    END,
    catatan = CONCAT(
        'produk=',  (SELECT COUNT(*) FROM stg_products          WHERE batch_id = @batch_id AND status = 'imported'),
        ', nota=',  (SELECT COUNT(*) FROM stg_transactions      WHERE batch_id = @batch_id AND status = 'imported'),
        ', item=',  (SELECT COUNT(*) FROM stg_transaction_items WHERE batch_id = @batch_id AND status = 'imported'),
        ', mutasi=',(SELECT COUNT(*) FROM stg_stock_movements   WHERE batch_id = @batch_id AND status = 'imported'),
        ', user=',  (SELECT COUNT(*) FROM stg_users             WHERE batch_id = @batch_id AND status = 'imported')
    )
WHERE id = @batch_id;

COMMIT;

-- ============================================================================
-- SETELAH COMMIT
-- 1. Jalankan 03_validation.sql bagian B & C.
-- 2. Jika ada baris error, perbaiki data di staging, lalu jalankan ulang
--    script ini. Baris 'imported' tidak akan diduplikasi.
-- 3. Backup database target:
--    mysqldump -u root -p kasirku > kasirku_setelah_migrasi.sql
-- ============================================================================

SELECT * FROM v_stg_ringkasan;
SELECT * FROM import_batches ORDER BY id DESC LIMIT 1;
