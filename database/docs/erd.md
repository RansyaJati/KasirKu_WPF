# ERD KasirKu — Skema Target

Diagram di bawah bisa dirender di GitHub, GitLab, VS Code (ekstensi Mermaid),
atau <https://mermaid.live>.

```mermaid
erDiagram
    CATEGORIES {
        tinyint_unsigned id PK
        varchar nama UK
    }
    UNITS {
        tinyint_unsigned id PK
        varchar nama UK
    }
    SUPPLIERS {
        smallint_unsigned id PK
        varchar nama UK
        varchar telepon
        varchar alamat
    }
    PAYMENT_METHODS {
        tinyint_unsigned id PK
        varchar nama UK
    }
    MOVEMENT_TYPES {
        tinyint_unsigned id PK
        varchar nama UK
        tinyint arah
    }
    USERS {
        int_unsigned id PK
        varchar username UK
        varchar nama_lengkap
        enum role
        varchar password_hash
        varchar salt
        boolean aktif
    }
    STORE_SETTINGS {
        tinyint_unsigned id PK
        varchar nama_toko
        varchar alamat
        varchar telepon
        varchar footer_struk
        varchar printer_name
        smallint_unsigned paper_width_mm
        boolean auto_print_receipt
        boolean kick_drawer
    }
    PRODUCTS {
        int_unsigned id PK
        varchar kode UK
        varchar barcode UK
        varchar nama
        tinyint_unsigned category_id FK
        tinyint_unsigned unit_id FK
        smallint_unsigned supplier_id FK
        decimal harga_beli
        decimal harga_jual
        int stok
        boolean aktif
    }
    TRANSACTIONS {
        varchar id PK
        datetime tanggal
        int_unsigned user_id FK
        tinyint_unsigned payment_method_id FK
        decimal total
        decimal bayar
        decimal kembalian
        varchar catatan
    }
    TRANSACTION_ITEMS {
        bigint_unsigned id PK
        varchar transaction_id FK
        int_unsigned product_id FK
        varchar kode
        varchar nama
        decimal harga_beli
        decimal harga_jual
        int qty
        decimal subtotal
    }
    STOCK_MOVEMENTS {
        bigint_unsigned id PK
        datetime tanggal
        int_unsigned product_id FK
        tinyint_unsigned movement_type_id FK
        int qty
        int stok_sebelum
        int stok_sesudah
        int perubahan
        varchar keterangan
        varchar transaction_id FK
        int_unsigned user_id FK
    }

    CATEGORIES   ||--o{ PRODUCTS          : "mengelompokkan"
    UNITS        ||--o{ PRODUCTS          : "satuan"
    SUPPLIERS    ||--o{ PRODUCTS          : "memasok"
    USERS        ||--o{ TRANSACTIONS      : "melayani"
    PAYMENT_METHODS ||--o{ TRANSACTIONS   : "dibayar dengan"
    TRANSACTIONS ||--o{ TRANSACTION_ITEMS : "berisi"
    PRODUCTS     ||--o{ TRANSACTION_ITEMS : "dijual sebagai"
    PRODUCTS     ||--o{ STOCK_MOVEMENTS   : "dimutasi"
    MOVEMENT_TYPES ||--o{ STOCK_MOVEMENTS : "jenis"
    TRANSACTIONS ||--o{ STOCK_MOVEMENTS   : "menyebabkan (opsional)"
    USERS        ||--o{ STOCK_MOVEMENTS   : "mencatat (opsional)"
```

## Kardinalitas

| Relasi | Kardinalitas | Catatan |
|---|---|---|
| `categories` → `products` | 1 : N | Satu kategori dipakai banyak produk |
| `units` → `products` | 1 : N | Satuan wajib |
| `suppliers` → `products` | 1 : N | Supplier boleh `NULL` |
| `payment_methods` → `transactions` | 1 : N | Metode wajib |
| `users` → `transactions` | 1 : N | Kasir boleh `NULL` untuk data lama |
| `transactions` → `transaction_items` | 1 : N | Minimal 1 item per nota |
| `products` → `transaction_items` | 1 : N | `product_id` boleh `NULL` (snapshot tetap ada) |
| `products` → `stock_movements` | 1 : N | Kartu stok per produk |
| `movement_types` → `stock_movements` | 1 : N | Masuk (+1) / Keluar (−1) |
| `transactions` → `stock_movements` | 1 : N | Opsional, penanda mutasi dari penjualan |
| `users` → `stock_movements` | 1 : N | Opsional, pencatat mutasi |

## Tabel staging (bukan bagian aplikasi)

```mermaid
erDiagram
    IMPORT_BATCHES ||--o{ STG_PRODUCTS : "batch"
    IMPORT_BATCHES ||--o{ STG_TRANSACTIONS : "batch"
    IMPORT_BATCHES ||--o{ STG_TRANSACTION_ITEMS : "batch"
    IMPORT_BATCHES ||--o{ STG_STOCK_MOVEMENTS : "batch"
    IMPORT_BATCHES ||--o{ STG_USERS : "batch"

    IMPORT_BATCHES {
        bigint_unsigned id PK
        varchar nama_file
        varchar sumber_db
        enum status
    }
    STG_PRODUCTS {
        bigint_unsigned row_id PK
        bigint_unsigned batch_id FK
        varchar legacy_id
        varchar kode
        varchar kategori
        varchar satuan
        varchar supplier
        varchar harga_beli_raw
        varchar harga_jual_raw
        varchar stok_raw
        enum status
    }
    STG_TRANSACTIONS {
        bigint_unsigned row_id PK
        bigint_unsigned batch_id FK
        varchar legacy_id
        varchar trx_id
        varchar tanggal_raw
        varchar metode_bayar
        varchar total_raw
        varchar bayar_raw
        enum status
    }
    STG_TRANSACTION_ITEMS {
        bigint_unsigned row_id PK
        bigint_unsigned batch_id FK
        varchar legacy_trx_id
        varchar legacy_product_id
        varchar harga_raw
        varchar qty_raw
        enum status
    }
    STG_STOCK_MOVEMENTS {
        bigint_unsigned row_id PK
        bigint_unsigned batch_id FK
        varchar legacy_product_id
        varchar jenis
        varchar qty_raw
        varchar stok_sebelum_raw
        varchar stok_sesudah_raw
        enum status
    }
    STG_USERS {
        bigint_unsigned row_id PK
        bigint_unsigned batch_id FK
        varchar username
        varchar role_raw
        enum resolved_role
        enum status
    }
    LEGACY_ID_MAP {
        enum entity PK
        varchar old_id PK
        varchar new_id
    }
```

`legacy_id_map` tidak punya FK ke staging/target karena berfungsi sebagai
jembatan antar-schema.
