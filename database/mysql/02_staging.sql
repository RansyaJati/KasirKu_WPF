-- ============================================================================
-- KasirKu — 02_staging.sql
-- Schema staging untuk menampung data MENTAH dari database lama klien.
--
-- Tujuan:
--   * Data lama tidak pernah diubah (immutable).
--   * Semua kolom staging bertipe teks supaya data kotor tetap bisa masuk.
--   * Normalisasi dilakukan bertahap: staging -> lookup -> target.
--
-- Tiga schema yang dipakai:
--   kasirku_legacy   : hasil restore dump database lama (JANGAN diedit)
--   kasirku_staging  : file ini (stg_*, legacy_id_map, import_batches)
--   kasirku          : target 3NF (01_schema.sql)
--
-- Jalankan setelah 01_schema.sql.
-- ============================================================================

CREATE DATABASE IF NOT EXISTS kasirku_legacy
    DEFAULT CHARACTER SET utf8mb4
    DEFAULT COLLATE utf8mb4_unicode_ci;

CREATE DATABASE IF NOT EXISTS kasirku_staging
    DEFAULT CHARACTER SET utf8mb4
    DEFAULT COLLATE utf8mb4_unicode_ci;

USE kasirku_staging;

SET NAMES utf8mb4;
SET time_zone = '+07:00';

-- ============================================================================
-- 1. BATCH IMPORT
-- Satu batch = satu file / satu kali tarik data dari database lama.
-- ============================================================================

CREATE TABLE IF NOT EXISTS import_batches (
    id             BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    nama_file      VARCHAR(255)    NOT NULL,
    sumber_db      VARCHAR(100)    NOT NULL DEFAULT '' COMMENT 'Mis. SQL Server 2008 / MySQL 5.5',
    versi_legacy   VARCHAR(50)     NOT NULL DEFAULT '',
    mulai          TIMESTAMP       NOT NULL DEFAULT CURRENT_TIMESTAMP,
    selesai        TIMESTAMP       NULL,
    status         ENUM('proses','selesai','gagal') NOT NULL DEFAULT 'proses',
    catatan        VARCHAR(500)    NOT NULL DEFAULT '',
    PRIMARY KEY (id)
) ENGINE=InnoDB COMMENT='Riwayat eksekusi migrasi per batch';

-- ============================================================================
-- 2. TABEL STAGING
-- Semua kolom sumber bertipe VARCHAR agar menerima tanggal/nominal berformat
-- apa pun. Validasi & konversi tipe dilakukan di 03_validation.sql / ETL.
-- ============================================================================

CREATE TABLE IF NOT EXISTS stg_products (
    row_id                 BIGINT UNSIGNED  NOT NULL AUTO_INCREMENT,
    batch_id               BIGINT UNSIGNED  NULL,
    legacy_id              VARCHAR(64)      NULL COMMENT 'PK tabel produk lama',
    kode                   VARCHAR(50)      NULL,
    barcode                VARCHAR(100)     NULL,
    nama                   VARCHAR(255)     NULL,
    satuan                 VARCHAR(50)      NULL,
    kategori               VARCHAR(50)      NULL,
    supplier               VARCHAR(150)     NULL,
    harga_beli_raw         VARCHAR(50)      NULL,
    harga_jual_raw         VARCHAR(50)      NULL,
    stok_raw               VARCHAR(50)      NULL,
    resolved_category_id   TINYINT UNSIGNED NULL,
    resolved_unit_id       TINYINT UNSIGNED NULL,
    resolved_supplier_id   SMALLINT UNSIGNED NULL,
    resolved_product_id    INT UNSIGNED     NULL,
    status                 ENUM('pending','valid','error','imported') NOT NULL DEFAULT 'pending',
    error_msg              VARCHAR(500)     NULL,
    raw_payload            JSON             NULL,
    created_at             TIMESTAMP        NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at             TIMESTAMP        NOT NULL DEFAULT CURRENT_TIMESTAMP
                                            ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (row_id),
    KEY idx_stg_products_batch (batch_id),
    KEY idx_stg_products_legacy (legacy_id),
    KEY idx_stg_products_kode (kode),
    KEY idx_stg_products_status (status),
    CONSTRAINT fk_stg_products_batch FOREIGN KEY (batch_id)
        REFERENCES import_batches (id) ON DELETE SET NULL
) ENGINE=InnoDB COMMENT='Staging produk dari database lama';

CREATE TABLE IF NOT EXISTS stg_transactions (
    row_id                     BIGINT UNSIGNED  NOT NULL AUTO_INCREMENT,
    batch_id                   BIGINT UNSIGNED  NULL,
    legacy_id                  VARCHAR(64)      NULL COMMENT 'PK nota lama',
    trx_id                     VARCHAR(64)      NULL COMMENT 'Nomor nota lama, mis. TRX-20240101-001',
    tanggal_raw                VARCHAR(64)      NULL,
    metode_bayar               VARCHAR(50)      NULL,
    total_raw                  VARCHAR(50)      NULL,
    bayar_raw                  VARCHAR(50)      NULL,
    kasir                      VARCHAR(100)     NULL COMMENT 'Username/nama kasir lama',
    resolved_payment_method_id TINYINT UNSIGNED NULL,
    resolved_user_id           INT UNSIGNED     NULL,
    resolved_transaction_id    VARCHAR(20)      NULL,
    status                     ENUM('pending','valid','error','imported') NOT NULL DEFAULT 'pending',
    error_msg                  VARCHAR(500)     NULL,
    raw_payload                JSON             NULL,
    created_at                 TIMESTAMP        NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at                 TIMESTAMP        NOT NULL DEFAULT CURRENT_TIMESTAMP
                                                ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (row_id),
    KEY idx_stg_transactions_batch (batch_id),
    KEY idx_stg_transactions_legacy (legacy_id),
    KEY idx_stg_transactions_trx (trx_id),
    KEY idx_stg_transactions_status (status),
    CONSTRAINT fk_stg_transactions_batch FOREIGN KEY (batch_id)
        REFERENCES import_batches (id) ON DELETE SET NULL
) ENGINE=InnoDB COMMENT='Staging header nota dari database lama';

CREATE TABLE IF NOT EXISTS stg_transaction_items (
    row_id                    BIGINT UNSIGNED  NOT NULL AUTO_INCREMENT,
    batch_id                  BIGINT UNSIGNED  NULL,
    legacy_id                 VARCHAR(64)      NULL,
    legacy_trx_id             VARCHAR(64)      NULL COMMENT 'FK nota di database lama',
    legacy_product_id         VARCHAR(64)      NULL COMMENT 'FK produk di database lama',
    kode                      VARCHAR(50)      NULL,
    nama                      VARCHAR(255)     NULL,
    harga_raw                 VARCHAR(50)      NULL COMMENT 'Harga jual saat transaksi',
    harga_beli_raw            VARCHAR(50)      NULL COMMENT 'Modal saat transaksi (boleh kosong)',
    qty_raw                   VARCHAR(50)      NULL,
    resolved_transaction_id   VARCHAR(20)      NULL,
    resolved_product_id       INT UNSIGNED     NULL,
    status                    ENUM('pending','valid','error','imported') NOT NULL DEFAULT 'pending',
    error_msg                 VARCHAR(500)     NULL,
    raw_payload               JSON             NULL,
    created_at                TIMESTAMP        NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at                TIMESTAMP        NOT NULL DEFAULT CURRENT_TIMESTAMP
                                               ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (row_id),
    KEY idx_stg_items_batch (batch_id),
    KEY idx_stg_items_legacy_trx (legacy_trx_id),
    KEY idx_stg_items_legacy_product (legacy_product_id),
    KEY idx_stg_items_status (status),
    CONSTRAINT fk_stg_items_batch FOREIGN KEY (batch_id)
        REFERENCES import_batches (id) ON DELETE SET NULL
) ENGINE=InnoDB COMMENT='Staging baris nota dari database lama';

CREATE TABLE IF NOT EXISTS stg_stock_movements (
    row_id                  BIGINT UNSIGNED  NOT NULL AUTO_INCREMENT,
    batch_id                BIGINT UNSIGNED  NULL,
    legacy_id               VARCHAR(64)      NULL,
    legacy_product_id       VARCHAR(64)      NULL,
    legacy_trx_id           VARCHAR(64)      NULL,
    kode                    VARCHAR(50)      NULL,
    nama                    VARCHAR(255)     NULL,
    tanggal_raw             VARCHAR(64)      NULL,
    jenis                   VARCHAR(20)      NULL COMMENT 'Masuk / Keluar',
    qty_raw                 VARCHAR(50)      NULL,
    stok_sebelum_raw        VARCHAR(50)      NULL,
    stok_sesudah_raw        VARCHAR(50)      NULL,
    keterangan              VARCHAR(255)     NULL,
    resolved_product_id     INT UNSIGNED     NULL,
    resolved_movement_type_id TINYINT UNSIGNED NULL,
    resolved_transaction_id VARCHAR(20)      NULL,
    status                  ENUM('pending','valid','error','imported') NOT NULL DEFAULT 'pending',
    error_msg               VARCHAR(500)     NULL,
    raw_payload             JSON             NULL,
    created_at              TIMESTAMP        NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at              TIMESTAMP        NOT NULL DEFAULT CURRENT_TIMESTAMP
                                             ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (row_id),
    KEY idx_stg_movements_batch (batch_id),
    KEY idx_stg_movements_legacy (legacy_id),
    KEY idx_stg_movements_product (legacy_product_id),
    KEY idx_stg_movements_status (status),
    CONSTRAINT fk_stg_movements_batch FOREIGN KEY (batch_id)
        REFERENCES import_batches (id) ON DELETE SET NULL
) ENGINE=InnoDB COMMENT='Staging mutasi stok dari database lama';

CREATE TABLE IF NOT EXISTS stg_users (
    row_id            BIGINT UNSIGNED  NOT NULL AUTO_INCREMENT,
    batch_id          BIGINT UNSIGNED  NULL,
    legacy_id         VARCHAR(64)      NULL,
    username          VARCHAR(100)     NULL,
    nama_lengkap      VARCHAR(150)     NULL,
    role_raw          VARCHAR(50)      NULL COMMENT 'Kasir / Manager / admin / dll',
    password_hash_raw VARCHAR(255)     NULL COMMENT 'Hash lama; TIDAK bisa dipakai langsung',
    salt_raw          VARCHAR(255)     NULL,
    aktif_raw         VARCHAR(20)      NULL,
    resolved_role     ENUM('Kasir','Manager') NULL COMMENT 'Hasil normalisasi role_raw',
    resolved_user_id  INT UNSIGNED     NULL,
    status            ENUM('pending','valid','error','imported') NOT NULL DEFAULT 'pending',
    error_msg         VARCHAR(500)     NULL,
    raw_payload       JSON             NULL,
    created_at        TIMESTAMP        NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at        TIMESTAMP        NOT NULL DEFAULT CURRENT_TIMESTAMP
                                       ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (row_id),
    KEY idx_stg_users_batch (batch_id),
    KEY idx_stg_users_legacy (legacy_id),
    KEY idx_stg_users_username (username),
    KEY idx_stg_users_status (status),
    CONSTRAINT fk_stg_users_batch FOREIGN KEY (batch_id)
        REFERENCES import_batches (id) ON DELETE SET NULL
) ENGINE=InnoDB COMMENT='Staging akun pengguna dari database lama';

-- ============================================================================
-- 3. PETA ID LAMA -> ID BARU
-- Wajib diisi setiap kali sebuah baris staging berhasil dimasukkan ke target.
-- Dibuat unik per (entity, old_id) supaya ETL bisa dijalankan ulang.
-- ============================================================================

CREATE TABLE IF NOT EXISTS legacy_id_map (
    entity      ENUM('category','unit','supplier','payment_method',
                     'product','transaction','transaction_item','stock_movement','user') NOT NULL,
    old_id      VARCHAR(64)  NOT NULL,
    new_id      VARCHAR(64)  NULL,
    catatan     VARCHAR(255) NOT NULL DEFAULT '',
    created_at  TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at  TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP
                             ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (entity, old_id),
    KEY idx_legacy_id_map_new (entity, new_id)
) ENGINE=InnoDB COMMENT='Pemetaan primary key database lama ke primary key KasirKu';

-- ============================================================================
-- 4. VIEW PEMBANTU VALIDASI
-- Semua view hanya membaca; tidak mengubah data.
-- ============================================================================

-- Baris produk yang tidak layak masuk target.
CREATE OR REPLACE VIEW v_stg_products_invalid AS
SELECT
    row_id,
    batch_id,
    legacy_id,
    kode,
    nama,
    harga_beli_raw,
    harga_jual_raw,
    stok_raw,
    status,
    error_msg
FROM stg_products
WHERE status = 'error'
   OR TRIM(COALESCE(kode, '')) = ''
   OR TRIM(COALESCE(nama, '')) = ''
   OR TRIM(COALESCE(harga_beli_raw, '')) NOT REGEXP '^[0-9]+([.,][0-9]+)?$'
   OR TRIM(COALESCE(harga_jual_raw, '')) NOT REGEXP '^[0-9]+([.,][0-9]+)?$'
   OR TRIM(COALESCE(stok_raw, '')) NOT REGEXP '^[0-9]+$';

-- Kode produk duplikat di dalam satu batch.
CREATE OR REPLACE VIEW v_stg_products_duplikat_kode AS
SELECT
    batch_id,
    TRIM(kode) AS kode,
    COUNT(*)   AS jumlah
FROM stg_products
WHERE TRIM(COALESCE(kode, '')) <> ''
GROUP BY batch_id, TRIM(kode)
HAVING COUNT(*) > 1;

-- Barcode duplikat (barcode kosong/NULL diabaikan).
CREATE OR REPLACE VIEW v_stg_products_duplikat_barcode AS
SELECT
    batch_id,
    TRIM(barcode) AS barcode,
    COUNT(*)      AS jumlah
FROM stg_products
WHERE TRIM(COALESCE(barcode, '')) <> ''
GROUP BY batch_id, TRIM(barcode)
HAVING COUNT(*) > 1;

-- Nota yang tidak layak (nomor kosong, nominal/tanggal tidak valid).
CREATE OR REPLACE VIEW v_stg_transactions_invalid AS
SELECT
    row_id,
    batch_id,
    legacy_id,
    trx_id,
    tanggal_raw,
    total_raw,
    bayar_raw,
    status,
    error_msg
FROM stg_transactions
WHERE status = 'error'
   OR TRIM(COALESCE(trx_id, '')) = ''
   OR STR_TO_DATE(NULLIF(TRIM(tanggal_raw), ''), '%Y-%m-%d %H:%i:%s') IS NULL
   OR TRIM(COALESCE(total_raw, '')) NOT REGEXP '^[0-9]+([.,][0-9]+)?$'
   OR TRIM(COALESCE(bayar_raw, '')) NOT REGEXP '^[0-9]+([.,][0-9]+)?$';

-- Baris nota tanpa header / qty / harga tidak valid.
CREATE OR REPLACE VIEW v_stg_transaction_items_invalid AS
SELECT
    row_id,
    batch_id,
    legacy_id,
    legacy_trx_id,
    legacy_product_id,
    kode,
    nama,
    harga_raw,
    qty_raw,
    status,
    error_msg
FROM stg_transaction_items
WHERE status = 'error'
   OR TRIM(COALESCE(legacy_trx_id, '')) = ''
   OR TRIM(COALESCE(harga_raw, '')) NOT REGEXP '^[0-9]+([.,][0-9]+)?$'
   OR (TRIM(COALESCE(harga_beli_raw, '')) <> ''
       AND TRIM(COALESCE(harga_beli_raw, '')) NOT REGEXP '^[0-9]+([.,][0-9]+)?$')
   OR TRIM(COALESCE(qty_raw, '')) NOT REGEXP '^[0-9]+$'
   OR CAST(NULLIF(TRIM(qty_raw), '') AS UNSIGNED) = 0;

-- Mutasi stok yang tidak layak.
CREATE OR REPLACE VIEW v_stg_stock_movements_invalid AS
SELECT
    row_id,
    batch_id,
    legacy_id,
    legacy_product_id,
    tanggal_raw,
    jenis,
    qty_raw,
    stok_sebelum_raw,
    stok_sesudah_raw,
    status,
    error_msg
FROM stg_stock_movements
WHERE status = 'error'
   OR TRIM(COALESCE(legacy_product_id, '')) = ''
   OR TRIM(COALESCE(jenis, '')) = ''
   OR TRIM(COALESCE(qty_raw, '')) NOT REGEXP '^[0-9]+$'
   OR TRIM(COALESCE(stok_sebelum_raw, '')) NOT REGEXP '^[0-9]+$'
   OR TRIM(COALESCE(stok_sesudah_raw, '')) NOT REGEXP '^[0-9]+$';

-- Pengguna yang tidak layak.
CREATE OR REPLACE VIEW v_stg_users_invalid AS
SELECT
    row_id,
    batch_id,
    legacy_id,
    username,
    role_raw,
    status,
    error_msg
FROM stg_users
WHERE status = 'error'
   OR TRIM(COALESCE(username, '')) = ''
   OR TRIM(COALESCE(role_raw, '')) = '';

-- Ringkasan jumlah baris per tabel & status.
CREATE OR REPLACE VIEW v_stg_ringkasan AS
SELECT 'stg_products'           AS tabel, status, COUNT(*) AS jumlah FROM stg_products           GROUP BY status
UNION ALL
SELECT 'stg_transactions'       AS tabel, status, COUNT(*) AS jumlah FROM stg_transactions       GROUP BY status
UNION ALL
SELECT 'stg_transaction_items'  AS tabel, status, COUNT(*) AS jumlah FROM stg_transaction_items  GROUP BY status
UNION ALL
SELECT 'stg_stock_movements'    AS tabel, status, COUNT(*) AS jumlah FROM stg_stock_movements    GROUP BY status
UNION ALL
SELECT 'stg_users'              AS tabel, status, COUNT(*) AS jumlah FROM stg_users              GROUP BY status;

-- Semua baris bermasalah dalam satu tampilan.
CREATE OR REPLACE VIEW v_stg_error_semua AS
SELECT 'stg_products'          AS tabel, row_id, batch_id, legacy_id, status, error_msg FROM stg_products          WHERE status = 'error'
UNION ALL
SELECT 'stg_transactions'      AS tabel, row_id, batch_id, legacy_id, status, error_msg FROM stg_transactions      WHERE status = 'error'
UNION ALL
SELECT 'stg_transaction_items' AS tabel, row_id, batch_id, legacy_id, status, error_msg FROM stg_transaction_items WHERE status = 'error'
UNION ALL
SELECT 'stg_stock_movements'   AS tabel, row_id, batch_id, legacy_id, status, error_msg FROM stg_stock_movements   WHERE status = 'error'
UNION ALL
SELECT 'stg_users'             AS tabel, row_id, batch_id, legacy_id, status, error_msg FROM stg_users             WHERE status = 'error';

-- ============================================================================
-- SELESAI.
-- Cek:
--   SHOW TABLES;
--   SELECT * FROM import_batches;
--   SELECT * FROM v_stg_ringkasan;
-- ============================================================================
