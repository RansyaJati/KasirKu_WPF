-- ============================================================================
-- KasirKu — 01_schema.sql
-- Skema target ternormalisasi (3NF) untuk aplikasi KasirKu
--
-- Kebutuhan server : MySQL 8.0.16+ (CHECK constraint benar-benar dijalankan)
--                    atau MariaDB 10.6+
-- Default charset  : utf8mb4 / utf8mb4_unicode_ci
-- Timezone         : +07:00 (WIB)
--
-- Script ini TIDAK menghapus data yang sudah ada (semua CREATE IF NOT EXISTS).
-- Untuk reset total saat masih pengembangan / demo TA, jalankan manual:
--
--   DROP DATABASE IF EXISTS kasirku;
--
-- Setelah reset, jalankan ulang file ini.
--
-- Urutan pemakaian lengkap ada di database/README.md
-- ============================================================================

CREATE DATABASE IF NOT EXISTS kasirku
    DEFAULT CHARACTER SET utf8mb4
    DEFAULT COLLATE utf8mb4_unicode_ci;

USE kasirku;

SET NAMES utf8mb4;
SET time_zone = '+07:00';

-- ============================================================================
-- 1. TABEL REFERENSI (hasil normalisasi teks bebas dari JSON lama)
-- ============================================================================

CREATE TABLE IF NOT EXISTS categories (
    id          TINYINT UNSIGNED NOT NULL AUTO_INCREMENT,
    nama        VARCHAR(50)      NOT NULL,
    keterangan  VARCHAR(255)     NULL,
    created_at  TIMESTAMP        NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    UNIQUE KEY uk_categories_nama (nama)
) ENGINE=InnoDB COMMENT='Master kategori produk (pengganti kolom Kategori teks bebas)';

CREATE TABLE IF NOT EXISTS units (
    id          TINYINT UNSIGNED NOT NULL AUTO_INCREMENT,
    nama        VARCHAR(20)      NOT NULL,
    created_at  TIMESTAMP        NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    UNIQUE KEY uk_units_nama (nama)
) ENGINE=InnoDB COMMENT='Master satuan produk (Pcs, Pack, Dus, Kg, ...)';

CREATE TABLE IF NOT EXISTS suppliers (
    id          SMALLINT UNSIGNED NOT NULL AUTO_INCREMENT,
    nama        VARCHAR(100)      NOT NULL,
    telepon     VARCHAR(30)       NULL,
    alamat      VARCHAR(255)      NULL,
    created_at  TIMESTAMP         NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at  TIMESTAMP         NOT NULL DEFAULT CURRENT_TIMESTAMP
                                  ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    UNIQUE KEY uk_suppliers_nama (nama)
) ENGINE=InnoDB COMMENT='Master supplier / distributor';

CREATE TABLE IF NOT EXISTS payment_methods (
    id          TINYINT UNSIGNED NOT NULL AUTO_INCREMENT,
    nama        VARCHAR(30)      NOT NULL,
    created_at  TIMESTAMP        NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    UNIQUE KEY uk_payment_methods_nama (nama)
) ENGINE=InnoDB COMMENT='Master metode pembayaran (Tunai, QRIS, GoPay, ...)';

CREATE TABLE IF NOT EXISTS movement_types (
    id          TINYINT UNSIGNED NOT NULL AUTO_INCREMENT,
    nama        VARCHAR(20)      NOT NULL,
    arah        TINYINT          NOT NULL COMMENT '+1 = stok masuk, -1 = stok keluar',
    created_at  TIMESTAMP        NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    UNIQUE KEY uk_movement_types_nama (nama),
    CONSTRAINT chk_movement_types_arah CHECK (arah IN (-1, 1))
) ENGINE=InnoDB COMMENT='Jenis mutasi stok beserta arah perubahannya';

-- ============================================================================
-- 2. PENGGUNA & PENGATURAN TOKO
-- ============================================================================

CREATE TABLE IF NOT EXISTS users (
    id             INT UNSIGNED  NOT NULL AUTO_INCREMENT,
    username       VARCHAR(50)   NOT NULL,
    nama_lengkap   VARCHAR(100)  NOT NULL DEFAULT '',
    role           ENUM('Kasir','Manager') NOT NULL DEFAULT 'Kasir',
    password_hash  VARCHAR(128)  NOT NULL COMMENT 'PBKDF2-SHA256, Base64 (100.000 iterasi)',
    salt           VARCHAR(64)   NOT NULL COMMENT 'Salt acak 16 byte, Base64',
    aktif          BOOLEAN       NOT NULL DEFAULT TRUE,
    created_at     TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at     TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP
                                 ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    UNIQUE KEY uk_users_username (username),
    KEY idx_users_role_aktif (role, aktif)
) ENGINE=InnoDB COMMENT='Akun login POS (Kasir) & Manager';

CREATE TABLE IF NOT EXISTS store_settings (
    id                  TINYINT UNSIGNED NOT NULL DEFAULT 1,
    nama_toko           VARCHAR(100)     NOT NULL,
    alamat              VARCHAR(255)     NOT NULL DEFAULT '',
    telepon             VARCHAR(30)      NOT NULL DEFAULT '',
    footer_struk        VARCHAR(255)     NOT NULL DEFAULT '',
    printer_name        VARCHAR(150)     NOT NULL DEFAULT '',
    paper_width_mm      SMALLINT UNSIGNED NOT NULL DEFAULT 80,
    auto_print_receipt  BOOLEAN          NOT NULL DEFAULT TRUE,
    kick_drawer         BOOLEAN          NOT NULL DEFAULT TRUE,
    updated_at          TIMESTAMP        NOT NULL DEFAULT CURRENT_TIMESTAMP
                                         ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    CONSTRAINT chk_store_settings_single_row CHECK (id = 1),
    CONSTRAINT chk_store_settings_paper CHECK (paper_width_mm IN (58, 80))
) ENGINE=InnoDB COMMENT='Profil toko + pengaturan printer (satu baris, id = 1)';

-- ============================================================================
-- 3. PRODUK
-- ============================================================================

CREATE TABLE IF NOT EXISTS products (
    id           INT UNSIGNED      NOT NULL AUTO_INCREMENT,
    kode         VARCHAR(20)       NOT NULL COMMENT 'Kode internal toko, mis. BRG001',
    barcode      VARCHAR(50)       NULL     COMMENT 'Barcode scanner, boleh kosong',
    nama         VARCHAR(150)      NOT NULL,
    category_id  TINYINT UNSIGNED  NOT NULL,
    unit_id      TINYINT UNSIGNED  NOT NULL,
    supplier_id  SMALLINT UNSIGNED NULL,
    harga_beli   DECIMAL(15,2)     NOT NULL DEFAULT 0.00,
    harga_jual   DECIMAL(15,2)     NOT NULL DEFAULT 0.00,
    stok         INT               NOT NULL DEFAULT 0,
    aktif        BOOLEAN           NOT NULL DEFAULT TRUE,
    created_at   TIMESTAMP         NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at   TIMESTAMP         NOT NULL DEFAULT CURRENT_TIMESTAMP
                                   ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    UNIQUE KEY uk_products_kode (kode),
    UNIQUE KEY uk_products_barcode (barcode),
    KEY idx_products_nama (nama),
    KEY idx_products_category (category_id),
    KEY idx_products_unit (unit_id),
    KEY idx_products_supplier (supplier_id),
    CONSTRAINT chk_products_harga_beli CHECK (harga_beli >= 0),
    CONSTRAINT chk_products_harga_jual CHECK (harga_jual >= 0),
    CONSTRAINT chk_products_stok CHECK (stok >= 0),
    CONSTRAINT fk_products_category FOREIGN KEY (category_id)
        REFERENCES categories (id) ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT fk_products_unit FOREIGN KEY (unit_id)
        REFERENCES units (id) ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT fk_products_supplier FOREIGN KEY (supplier_id)
        REFERENCES suppliers (id) ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB COMMENT='Master produk (stok = stok terkini, histori di stock_movements)';

-- ============================================================================
-- 4. TRANSAKSI PENJUALAN
-- ============================================================================

CREATE TABLE IF NOT EXISTS transactions (
    id                VARCHAR(20)      NOT NULL COMMENT 'Format TRX-yyyyMMdd-###',
    tanggal           DATETIME         NOT NULL DEFAULT CURRENT_TIMESTAMP,
    user_id           INT UNSIGNED     NULL COMMENT 'Kasir yang melayani',
    payment_method_id TINYINT UNSIGNED NOT NULL,
    total             DECIMAL(15,2)    NOT NULL DEFAULT 0.00,
    bayar             DECIMAL(15,2)    NOT NULL DEFAULT 0.00,
    kembalian         DECIMAL(15,2)    GENERATED ALWAYS AS (bayar - total) STORED,
    catatan           VARCHAR(255)     NULL,
    created_at        TIMESTAMP        NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    KEY idx_transactions_tanggal (tanggal),
    KEY idx_transactions_user (user_id),
    KEY idx_transactions_payment (payment_method_id),
    CONSTRAINT chk_transactions_total CHECK (total >= 0),
    CONSTRAINT chk_transactions_bayar CHECK (bayar >= 0),
    CONSTRAINT chk_transactions_bayar_cukup CHECK (bayar >= total),
    CONSTRAINT fk_transactions_user FOREIGN KEY (user_id)
        REFERENCES users (id) ON UPDATE CASCADE ON DELETE SET NULL,
    CONSTRAINT fk_transactions_payment FOREIGN KEY (payment_method_id)
        REFERENCES payment_methods (id) ON UPDATE CASCADE ON DELETE RESTRICT
) ENGINE=InnoDB COMMENT='Header nota penjualan';

CREATE TABLE IF NOT EXISTS transaction_items (
    id                BIGINT UNSIGNED  NOT NULL AUTO_INCREMENT,
    transaction_id    VARCHAR(20)      NOT NULL,
    product_id        INT UNSIGNED     NULL COMMENT 'Boleh NULL jika produk sudah dihapus',
    kode              VARCHAR(20)      NOT NULL COMMENT 'Snapshot kode saat transaksi',
    nama              VARCHAR(150)     NOT NULL COMMENT 'Snapshot nama saat transaksi',
    harga_beli        DECIMAL(15,2)    NOT NULL DEFAULT 0.00 COMMENT 'Snapshot modal saat transaksi',
    harga_jual        DECIMAL(15,2)    NOT NULL DEFAULT 0.00 COMMENT 'Snapshot harga jual saat transaksi',
    qty               INT              NOT NULL,
    subtotal          DECIMAL(18,2)    GENERATED ALWAYS AS (harga_jual * qty) STORED,
    PRIMARY KEY (id),
    KEY idx_transaction_items_trx (transaction_id),
    KEY idx_transaction_items_product (product_id),
    CONSTRAINT chk_transaction_items_harga_beli CHECK (harga_beli >= 0),
    CONSTRAINT chk_transaction_items_harga_jual CHECK (harga_jual >= 0),
    CONSTRAINT chk_transaction_items_qty CHECK (qty > 0),
    CONSTRAINT fk_transaction_items_trx FOREIGN KEY (transaction_id)
        REFERENCES transactions (id) ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT fk_transaction_items_product FOREIGN KEY (product_id)
        REFERENCES products (id) ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB COMMENT='Baris nota; menyimpan snapshot harga historis';

-- ============================================================================
-- 5. MUTASI STOK
-- ============================================================================

CREATE TABLE IF NOT EXISTS stock_movements (
    id                 BIGINT UNSIGNED  NOT NULL AUTO_INCREMENT,
    tanggal            DATETIME         NOT NULL DEFAULT CURRENT_TIMESTAMP,
    product_id         INT UNSIGNED     NOT NULL,
    movement_type_id   TINYINT UNSIGNED NOT NULL,
    qty                INT              NOT NULL COMMENT 'Selalu positif; arah dari movement_types.arah',
    stok_sebelum       INT              NOT NULL,
    stok_sesudah       INT              NOT NULL,
    perubahan          INT              GENERATED ALWAYS AS (stok_sesudah - stok_sebelum) STORED,
    keterangan         VARCHAR(255)     NOT NULL DEFAULT '',
    transaction_id     VARCHAR(20)      NULL COMMENT 'Diisi jika mutasi berasal dari penjualan',
    user_id            INT UNSIGNED     NULL,
    created_at         TIMESTAMP        NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (id),
    KEY idx_stock_movements_produk_tanggal (product_id, tanggal),
    KEY idx_stock_movements_tanggal (tanggal),
    KEY idx_stock_movements_trx (transaction_id),
    KEY idx_stock_movements_user (user_id),
    CONSTRAINT chk_stock_movements_qty CHECK (qty > 0),
    CONSTRAINT chk_stock_movements_stok_sebelum CHECK (stok_sebelum >= 0),
    CONSTRAINT chk_stock_movements_stok_sesudah CHECK (stok_sesudah >= 0),
    CONSTRAINT fk_stock_movements_product FOREIGN KEY (product_id)
        REFERENCES products (id) ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT fk_stock_movements_type FOREIGN KEY (movement_type_id)
        REFERENCES movement_types (id) ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT fk_stock_movements_trx FOREIGN KEY (transaction_id)
        REFERENCES transactions (id) ON UPDATE CASCADE ON DELETE SET NULL,
    CONSTRAINT fk_stock_movements_user FOREIGN KEY (user_id)
        REFERENCES users (id) ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE=InnoDB COMMENT='Kartu stok / log mutasi stok';

-- ============================================================================
-- 6. VERSI SKEMA
-- ============================================================================

CREATE TABLE IF NOT EXISTS schema_migrations (
    versi            VARCHAR(20)  NOT NULL,
    keterangan       VARCHAR(255) NOT NULL,
    diterapkan_pada  TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (versi)
) ENGINE=InnoDB COMMENT='Riwayat versi skema database';

INSERT IGNORE INTO schema_migrations (versi, keterangan)
VALUES ('1.0.0', 'Skema target KasirKu 3NF + tabel referensi');

-- ============================================================================
-- 7. SEED DATA REFERENSI
-- Nilai awal mengikuti pilihan ComboBox di aplikasi KasirKu.
-- ============================================================================

INSERT IGNORE INTO categories (nama) VALUES
    ('Makanan'), ('Minuman'), ('Snack'), ('Frozen'), ('ATK'), ('Lainnya'), ('Umum');

INSERT IGNORE INTO units (nama) VALUES
    ('Pcs'), ('Pack'), ('Dus'), ('Karton'), ('Kg'), ('Gram'), ('Liter'), ('Botol'), ('Lusin');

INSERT IGNORE INTO payment_methods (nama) VALUES
    ('Tunai'), ('QRIS'), ('GoPay'), ('OVO'), ('DANA'), ('ShopeePay'), ('Transfer Bank');

INSERT IGNORE INTO movement_types (id, nama, arah) VALUES
    (1, 'Masuk', 1),
    (2, 'Keluar', -1);

INSERT INTO store_settings (id, nama_toko, alamat, telepon, footer_struk)
VALUES (1, 'BANYU FROZEN FOOD', 'Jl. Contoh No. 123', '0812-3456-7890', 'Terima kasih & sampai jumpa!')
ON DUPLICATE KEY UPDATE id = id;

-- ============================================================================
-- SELESAI. Cek hasil:
--   SHOW TABLES;
--   SELECT * FROM payment_methods;
--   SELECT * FROM schema_migrations;
-- ============================================================================
