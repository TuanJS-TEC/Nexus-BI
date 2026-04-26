# INSERT Rules for Data Warehouse Team

This document defines the mandatory rules for inserting data into the warehouse so OLAP cubes remain correct and demo-ready.

## 1) Scope and naming standard

- Use one geography dimension name only: `Dim_DiaDiem`.
- Use consistent ID formats:
  - `MaTP`: `TPxxxxx`
  - `MaCH`: `CHxxxxx`
  - `MaKH`: `KHxxxxx`
  - `MaMH`: `MHxxxxx`
  - `MaTG`: `TGYYYYMM` (example: `TG202501`)
- Do not mix multiple ID formats in the same table.

## 2) Insert order (strict)

1. `Dim_DiaDiem`
2. `Dim_CuaHang`, `Dim_KhachHang`, `Dim_MatHang`, `Dim_ThoiGian`
3. `Fact_BanHang`, `Fact_TonKho`

Never insert fact tables before dimensions.

## 3) Table-level data rules

## `Dim_DiaDiem`

- `MaTP` must be unique and not null.
- `TenTP`, `Bang`, `DiaChiVP` must not be null/empty.

## `Dim_CuaHang`

- `MaCH` must be unique and not null.
- `MaTP` must exist in `Dim_DiaDiem`.
- `SoDienThoai` should be normalized and non-empty.

## `Dim_KhachHang`

- `MaKH` must be unique and not null.
- `MaTP` must exist in `Dim_DiaDiem`.
- `TenKH` must not be null/empty.

## `Dim_MatHang`

- `MaMH` must be unique and not null.
- `Gia` must be `> 0`.
- `TrongLuong` must be `>= 0` when provided.

## `Dim_ThoiGian`

- `MaTG` must be unique and not null.
- `Thang` must be in `[1..12]`.
- `Quy` must be in `[1..4]`.
- Quarter must match month: `Quy = ((Thang - 1) / 3) + 1`.
- One `MaTG` maps to exactly one `(Nam, Quy, Thang)`.

## `Fact_BanHang`

- PK `(MaMH, MaKH, MaTG)` must be unique.
- `SoLuongDat > 0`.
- `TongTien >= 0`.
- All FK keys must exist in dimensions.
- Data should be time-series friendly:
  - Each product should appear in many months (target 12/12 in demo year).
  - Avoid sparse product-month coverage.

## `Fact_TonKho`

- PK `(MaCH, MaMH, MaTG)` must be unique.
- `SoLuongTrongKho >= 0`.
- All FK keys must exist in dimensions.
- Ensure enough coverage across store-product-time for pivot/drill.

## 4) Data quality rules for OLAP demo

- Do not generate purely linear/robotic patterns for all products.
- Add controlled variation by time/product/customer/store.
- Add mild seasonality (for example higher demand in months 11-12).
- Avoid extreme outliers unless intentionally modeled.

## 5) Transaction and deployment rules

- Run insert scripts inside a transaction:
  - `BEGIN TRAN`
  - `TRY...CATCH`
  - `ROLLBACK` on error
  - `COMMIT` on success
- Use idempotent scripts when possible for repeated test runs.
- Version control every insert/reseed script.

## 6) Mandatory post-insert validation

- Run `post_insert_validation.sql` after every insert/reseed.
- Do not process cubes until validation passes.
- After validation passes:
  1. Process SSAS database/cubes.
  2. Run smoke MDX queries for drill/roll/slice-dice/pivot.

## 7) OLAP readiness acceptance criteria

- In demo year (for example 2025), each product has data in at least 8 months (target 12).
- No duplicate PK in facts.
- No orphan foreign keys.
- Time hierarchy data is consistent.
- Core demo queries return non-empty and meaningful result sets.
