-- ============================================================================
-- KasirKu — 03_validation.sql
-- Query pemeriksaan kualitas data.
--
-- Aman dijalankan kapan saja: hanya SELECT, tidak mengubah data.
-- Idealnya SEMUA query "harus 0 baris" di bagian A dan B sebelum ETL,
-- lalu dijalankan lagi setelah ETL sebagai bukti migrasi bersih.
--
-- Jalankan setelah 01_schema.sql dan 02_staging.sql.
-- ============================================================================

USE kasirku_staging;

SET NAMES utf8mb4;

-- Ganti dengan batch yang sedang dikerjakan (default: batch terakhir).
SET @batch_id = (SELECT MAX(id) FROM kasirku_staging.import_batches);

SELECT @batch_id AS batch_yang_diperiksa;

-- ============================================================================
-- A. PEMERIKSAAN STAGING (sebelum ETL)
-- ============================================================================

-- A1. Ringkasan jumlah baris per tabel staging.
SELECT * FROM kasirku_staging.v_stg_ringkasan;

-- A2. Semua baris berstatus error.
SELECT * FROM kasirku_staging.v_stg_error_semua
WHERE batch_id = @batch_id;

-- A3. Produk tidak layak (kode/nama kosong, nominal bukan angka, lookup gagal).
SELECT * FROM kasirku_staging.v_stg_products_invalid
WHERE batch_id = @batch_id;

-- A4. Kode produk duplikat di staging  -> HARUS 0 BARIS.
SELECT * FROM kasirku_staging.v_stg_products_duplikat_kode
WHERE batch_id = @batch_id;

-- A5. Barcode duplikat di staging      -> HARUS 0 BARIS.
SELECT * FROM kasirku_staging.v_stg_products_duplikat_barcode
WHERE batch_id = @batch_id;

-- A6. Nota tidak layak.
SELECT * FROM kasirku_staging.v_stg_transactions_invalid
WHERE batch_id = @batch_id;

-- A7. Baris nota tidak layak.
SELECT * FROM kasirku_staging.v_stg_transaction_items_invalid
WHERE batch_id = @batch_id;

-- A8. Mutasi stok tidak layak.
SELECT * FROM kasirku_staging.v_stg_stock_movements_invalid
WHERE batch_id = @batch_id;

-- A9. Pengguna tidak layak.
SELECT * FROM kasirku_staging.v_stg_users_invalid
WHERE batch_id = @batch_id;

-- A10. Item yatim: baris nota tanpa header nota di staging -> HARUS 0 BARIS.
SELECT i.row_id, i.legacy_trx_id, i.kode, i.nama, i.qty_raw
FROM kasirku_staging.stg_transaction_items i
LEFT JOIN kasirku_staging.stg_transactions t
       ON t.legacy_id = i.legacy_trx_id
      AND t.batch_id  = i.batch_id
WHERE i.batch_id = @batch_id
  AND t.row_id IS NULL;

-- A11. Mutasi stok yatim: produknya tidak ada di staging -> HARUS 0 BARIS.
SELECT m.row_id, m.legacy_product_id, m.kode, m.nama
FROM kasirku_staging.stg_stock_movements m
LEFT JOIN kasirku_staging.stg_products p
       ON p.legacy_id = m.legacy_product_id
      AND p.batch_id  = m.batch_id
WHERE m.batch_id = @batch_id
  AND p.row_id IS NULL;

-- A12. Gate akhir: total masalah staging. Target = 0.
SELECT
    (SELECT COUNT(*) FROM kasirku_staging.v_stg_products_invalid         WHERE batch_id = @batch_id) AS produk_invalid,
    (SELECT COUNT(*) FROM kasirku_staging.v_stg_transactions_invalid     WHERE batch_id = @batch_id) AS nota_invalid,
    (SELECT COUNT(*) FROM kasirku_staging.v_stg_transaction_items_invalid WHERE batch_id = @batch_id) AS item_invalid,
    (SELECT COUNT(*) FROM kasirku_staging.v_stg_stock_movements_invalid  WHERE batch_id = @batch_id) AS mutasi_invalid,
    (SELECT COUNT(*) FROM kasirku_staging.v_stg_users_invalid            WHERE batch_id = @batch_id) AS user_invalid;

-- ============================================================================
-- B. PEMERIKSAAN TARGET (setelah ETL)
-- ============================================================================

-- B1. Duplikat kode produk (unique key mencegah, ini bukti tambahan) -> 0 baris.
SELECT kode, COUNT(*) AS jumlah
FROM kasirku.products
GROUP BY kode
HAVING COUNT(*) > 1;

-- B2. Duplikat barcode -> 0 baris.
SELECT barcode, COUNT(*) AS jumlah
FROM kasirku.products
WHERE barcode IS NOT NULL AND barcode <> ''
GROUP BY barcode
HAVING COUNT(*) > 1;

-- B3. Stok negatif -> 0 baris (CHECK constraint mencegah).
SELECT id, kode, nama, stok
FROM kasirku.products
WHERE stok < 0;

-- B4. Total nota != jumlah subtotal item -> 0 baris.
SELECT
    t.id,
    t.tanggal,
    t.total,
    COALESCE(SUM(i.subtotal), 0)                     AS total_item,
    t.total - COALESCE(SUM(i.subtotal), 0)           AS selisih
FROM kasirku.transactions t
LEFT JOIN kasirku.transaction_items i ON i.transaction_id = t.id
GROUP BY t.id, t.tanggal, t.total
HAVING selisih <> 0;

-- B5. Item yatim (harus 0 baris; FK sudah menjaga).
SELECT i.id, i.transaction_id, i.kode
FROM kasirku.transaction_items i
LEFT JOIN kasirku.transactions t ON t.id = i.transaction_id
WHERE t.id IS NULL;

-- B6. Qty item tidak wajar -> 0 baris.
SELECT id, transaction_id, kode, qty
FROM kasirku.transaction_items
WHERE qty <= 0;

-- B7. Arah mutasi stok tidak cocok dengan jenisnya -> 0 baris.
SELECT
    m.id,
    m.product_id,
    mt.nama  AS jenis,
    m.qty,
    m.stok_sebelum,
    m.stok_sesudah,
    m.perubahan,
    mt.arah * m.qty AS perubahan_harusnya
FROM kasirku.stock_movements m
JOIN kasirku.movement_types mt ON mt.id = m.movement_type_id
WHERE m.perubahan <> mt.arah * m.qty;

-- B8. Stok produk != stok_sesudah pada mutasi terakhir -> 0 baris.
SELECT
    p.id,
    p.kode,
    p.nama,
    p.stok,
    m.stok_sesudah,
    m.tanggal AS mutasi_terakhir
FROM kasirku.products p
JOIN kasirku.stock_movements m
  ON m.id = (
        SELECT m2.id
        FROM kasirku.stock_movements m2
        WHERE m2.product_id = p.id
        ORDER BY m2.tanggal DESC, m2.id DESC
        LIMIT 1
     )
WHERE m.stok_sesudah <> p.stok;

-- B9. Rantai stok tidak nyambung (stok_sebelum != stok_sesudah sebelumnya).
SELECT id, product_id, tanggal, stok_sebelum, stok_sebelum_harusnya
FROM (
    SELECT
        m.id,
        m.product_id,
        m.tanggal,
        m.stok_sebelum,
        LAG(m.stok_sesudah) OVER (
            PARTITION BY m.product_id
            ORDER BY m.tanggal, m.id
        ) AS stok_sebelum_harusnya
    FROM kasirku.stock_movements m
) x
WHERE stok_sebelum_harusnya IS NOT NULL
  AND stok_sebelum <> stok_sebelum_harusnya;

-- B10. Nota dengan total 0 (curiga data rusak/bon kosong) -> 0 baris.
SELECT id, tanggal, total, bayar
FROM kasirku.transactions
WHERE total = 0;

-- B11. Nota bertanggal jauh di masa depan -> 0 baris.
SELECT id, tanggal
FROM kasirku.transactions
WHERE tanggal > (NOW() + INTERVAL 1 DAY);

-- B12. Item dengan snapshot harga 0 (data lama bisa 0; lihat mapping-legacy.md).
SELECT
    COUNT(*)                                   AS jumlah_item_harga_0,
    COUNT(DISTINCT transaction_id)             AS jumlah_nota_terdampak
FROM kasirku.transaction_items
WHERE harga_jual = 0;

-- B13. Item tanpa relasi produk (wajar jika produk sudah dihapus, cek jumlahnya).
SELECT
    i.kode,
    i.nama,
    COUNT(*) AS jumlah
FROM kasirku.transaction_items i
WHERE i.product_id IS NULL
GROUP BY i.kode, i.nama;

-- B14. User dengan role di luar Kasir/Manager (ENUM mencegah, bukti tambahan).
SELECT id, username, role
FROM kasirku.users
WHERE role NOT IN ('Kasir', 'Manager');

-- ============================================================================
-- C. REKONSILIASI STAGING vs TARGET
-- ============================================================================

-- C1. Jumlah baris per entitas.
SELECT 'products'          AS entitas,
       (SELECT COUNT(*) FROM kasirku_staging.stg_products          WHERE batch_id = @batch_id AND status = 'imported') AS dari_staging,
       (SELECT COUNT(*) FROM kasirku.products)                                                                           AS di_target
UNION ALL
SELECT 'transactions',
       (SELECT COUNT(*) FROM kasirku_staging.stg_transactions      WHERE batch_id = @batch_id AND status = 'imported'),
       (SELECT COUNT(*) FROM kasirku.transactions)
UNION ALL
SELECT 'transaction_items',
       (SELECT COUNT(*) FROM kasirku_staging.stg_transaction_items WHERE batch_id = @batch_id AND status = 'imported'),
       (SELECT COUNT(*) FROM kasirku.transaction_items)
UNION ALL
SELECT 'stock_movements',
       (SELECT COUNT(*) FROM kasirku_staging.stg_stock_movements   WHERE batch_id = @batch_id AND status = 'imported'),
       (SELECT COUNT(*) FROM kasirku.stock_movements)
UNION ALL
SELECT 'users',
       (SELECT COUNT(*) FROM kasirku_staging.stg_users             WHERE batch_id = @batch_id AND status = 'imported'),
       (SELECT COUNT(*) FROM kasirku.users);

-- C2. Baris staging 'imported' yang tidak punya entri di legacy_id_map -> 0 baris.
SELECT 'product' AS entitas, s.legacy_id
FROM kasirku_staging.stg_products s
LEFT JOIN kasirku_staging.legacy_id_map m
       ON m.entity = 'product' AND m.old_id = s.legacy_id
WHERE s.batch_id = @batch_id AND s.status = 'imported' AND m.new_id IS NULL
UNION ALL
SELECT 'transaction', s.legacy_id
FROM kasirku_staging.stg_transactions s
LEFT JOIN kasirku_staging.legacy_id_map m
       ON m.entity = 'transaction' AND m.old_id = s.legacy_id
WHERE s.batch_id = @batch_id AND s.status = 'imported' AND m.new_id IS NULL
UNION ALL
SELECT 'user', s.legacy_id
FROM kasirku_staging.stg_users s
LEFT JOIN kasirku_staging.legacy_id_map m
       ON m.entity = 'user' AND m.old_id = s.legacy_id
WHERE s.batch_id = @batch_id AND s.status = 'imported' AND m.new_id IS NULL;

-- C3. Total omzet staging vs target (selisih harus 0).
SELECT
    (SELECT COALESCE(SUM(CAST(REPLACE(total_raw, ',', '.') AS DECIMAL(15,2))), 0)
       FROM kasirku_staging.stg_transactions
      WHERE batch_id = @batch_id AND status = 'imported') AS omzet_staging,
    (SELECT COALESCE(SUM(total), 0) FROM kasirku.transactions) AS omzet_target;

-- C4. Jumlah item terjual staging vs target.
SELECT
    (SELECT COALESCE(SUM(CAST(qty_raw AS UNSIGNED)), 0)
       FROM kasirku_staging.stg_transaction_items
      WHERE batch_id = @batch_id AND status = 'imported') AS qty_staging,
    (SELECT COALESCE(SUM(qty), 0) FROM kasirku.transaction_items) AS qty_target;

-- ============================================================================
-- D. TEMPLATE PEMERIKSAAN DATABASE LAMA
-- Nama tabel/kolom lama berbeda per klien, jadi bagian ini sengaja
-- dikomentari. Sesuaikan setelah struktur DB lama diketahui.
-- ============================================================================

-- D1. Cek duplikat kode produk di DB lama:
-- SELECT kode_produk, COUNT(*)
-- FROM kasirku_legacy.nama_tabel_produk
-- GROUP BY kode_produk
-- HAVING COUNT(*) > 1;

-- D2. Cek nota tanpa item:
-- SELECT h.nomor_nota
-- FROM kasirku_legacy.nama_tabel_nota h
-- LEFT JOIN kasirku_legacy.nama_tabel_item d ON d.nomor_nota = h.nomor_nota
-- WHERE d.nomor_nota IS NULL;

-- D3. Cek item dengan qty <= 0 atau harga negatif:
-- SELECT *
-- FROM kasirku_legacy.nama_tabel_item
-- WHERE qty <= 0 OR harga < 0;

-- D4. Cek tanggal tidak valid (contoh SQL Server):
-- SELECT * FROM nama_tabel_nota WHERE ISDATE(tanggal) = 0;

-- ============================================================================
-- SELESAI.
-- ============================================================================
