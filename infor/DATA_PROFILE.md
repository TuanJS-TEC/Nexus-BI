# DATA PROFILE - KDL_WEB

Tai lieu nay tong hop thong tin ket noi du lieu va metadata cube/fact/dim cua du an, duoc trich xuat tu:

- Cau hinh backend: `OlapDemo.Api/appsettings.json`
- Logic metadata backend: `OlapDemo.Api/Services/OlapService.cs`, `OlapDemo.Api/Services/CubeNameRules.cs`
- Relational SQL schema/scripts:
  - `infor/In4.md`
  - `infor/reseed_facts_olap.sql`
  - `infor/Seed_50k_Data.sql`
  - `infor/insert_facts_2018_2020.sql`
  - `infor/insert_facts_2021_2023.sql`
  - `infor/post_insert_validation.sql`
- Du lieu runtime tu API metadata:
  - `GET http://localhost:5000/api/olap/metadata?cube=Cube4BanHang_3D_KH_MH_TG_01`
  - `GET http://localhost:5000/api/olap/metadata?cube=Cube4TonKho_3D_MH_CH_TG_01`

## 1) Thong tin ket noi CSDL/SSAS

- SSAS server (`Data Source`): `DESKTOP-22UAJFI`
- Catalog: `Datawarehouse_01`
- Connection string: `Data Source=DESKTOP-22UAJFI;Catalog=Datawarehouse_01;`
- Default cube backend: `Cube4BanHang_3D_KH_MH_TG_01`
- API backend truy cap SSAS qua `Microsoft.AnalysisServices.AdomdClient`

## 2) Danh sach cube (runtime metadata)

Tong so cube detect duoc: `11`

1. `Cube4BanHang_1D_KH_01`
2. `Cube4BanHang_1D_MH`
3. `Cube4BanHang_1D_TG`
4. `Cube4BanHang_2D_KH_TG_01`
5. `Cube4BanHang_2D_MH_KH_01`
6. `Cube4BanHang_2D_MH_TG_01`
7. `Cube4BanHang_3D_KH_MH_TG_01`
8. `Cube4TonKho_1D_TG_01`
9. `Cube4TonKho_2D_CH_TG_01`
10. `Cube4TonKho_2D_MH_TG_01`
11. `Cube4TonKho_3D_MH_CH_TG_01`

## 3) Bang fact va dim theo cube

Du an co the doi chieu truc tiep relational SQL schema, khong chi suy luan theo ten cube.

Danh sach bang relational xac nhan duoc tu script:

- Dimensions:
  - `Dim_DiaDiem`
  - `Dim_CuaHang`
  - `Dim_MatHang`
  - `Dim_KhachHang`
  - `Dim_ThoiGian`
- Facts:
  - `Fact_BanHang`
  - `Fact_TonKho`

FK chinh:

- `Fact_BanHang.MaMH` -> `Dim_MatHang.MaMH`
- `Fact_BanHang.MaKH` -> `Dim_KhachHang.MaKH`
- `Fact_BanHang.MaTG` -> `Dim_ThoiGian.MaTG`
- `Fact_TonKho.MaCH` -> `Dim_CuaHang.MaCH`
- `Fact_TonKho.MaMH` -> `Dim_MatHang.MaMH`
- `Fact_TonKho.MaTG` -> `Dim_ThoiGian.MaTG`
- `Dim_CuaHang.MaTP` -> `Dim_DiaDiem.MaTP` 
- `Dim_KhachHang.MaTP` -> `Dim_DiaDiem.MaTP`

## 3.1 Nhom Fact_BanHang

### `Cube4BanHang_1D_KH_01`
- Fact: `Fact_BanHang`
- Dimensions: `Khach Hang (KH)`
- Measures: `Tong Tien`, `So Luong Dat`
- Time hierarchy: khong co

### `Cube4BanHang_1D_MH`
- Fact: `Fact_BanHang`
- Dimensions: `Mat Hang (MH)`
- Measures: `Tong Tien`, `So Luong Dat`
- Time hierarchy: khong co

### `Cube4BanHang_1D_TG`
- Fact: `Fact_BanHang`
- Dimensions: `Thoi Gian (TG)`
- Measures: `Tong Tien`, `So Luong Dat`
- Time hierarchy: co (Nam/Quy/Thang)

### `Cube4BanHang_2D_KH_TG_01`
- Fact: `Fact_BanHang`
- Dimensions: `Khach Hang (KH)`, `Thoi Gian (TG)`
- Measures: `Tong Tien`, `So Luong Dat`
- Time hierarchy: co

### `Cube4BanHang_2D_MH_KH_01`
- Fact: `Fact_BanHang`
- Dimensions: `Khach Hang (KH)`, `Mat Hang (MH)`
- Measures: `Tong Tien`, `So Luong Dat`
- Time hierarchy: khong co

### `Cube4BanHang_2D_MH_TG_01`
- Fact: `Fact_BanHang`
- Dimensions: `Mat Hang (MH)`, `Thoi Gian (TG)`
- Measures: `Tong Tien`, `So Luong Dat`
- Time hierarchy: co

### `Cube4BanHang_3D_KH_MH_TG_01`
- Fact: `Fact_BanHang`
- Dimensions: `Khach Hang (KH)`, `Mat Hang (MH)`, `Thoi Gian (TG)`
- Measures: `Tong Tien`, `So Luong Dat`
- Time hierarchy: co

## 3.2 Nhom Fact_TonKho

### `Cube4TonKho_1D_TG_01`
- Fact: `Fact_TonKho`
- Dimensions: `Thoi Gian (TG)`
- Measures: `So Luong Trong Kho`
- Time hierarchy: co

### `Cube4TonKho_2D_CH_TG_01`
- Fact: `Fact_TonKho`
- Dimensions: `Cua Hang (CH)`, `Thoi Gian (TG)`
- Measures: `So Luong Trong Kho`
- Time hierarchy: co

### `Cube4TonKho_2D_MH_TG_01`
- Fact: `Fact_TonKho`
- Dimensions: `Mat Hang (MH)`, `Thoi Gian (TG)`
- Measures: `So Luong Trong Kho`
- Time hierarchy: co

### `Cube4TonKho_3D_MH_CH_TG_01`
- Fact: `Fact_TonKho`
- Dimensions: `Mat Hang (MH)`, `Cua Hang (CH)`, `Thoi Gian (TG)`
- Measures: `So Luong Trong Kho`
- Time hierarchy: co

## 4) Mapping dim/hierarchy duoc backend nhan dien

Backend co logic map hierarchy tu SSAS schema:

- Time: `Dim Thoi Gian` -> level `Nam`, `Quy`, `Thang`.
- Product: `Dim Mat Hang` -> level `Ma MH`
- Customer: `Dim Khach Hang` -> level `Loai KH`, `Ma KH`.
- Store: `Dim Cua Hang` -> level `Ma CH`
- DiaDiem: `Dim Dia Diem` -> level `Bang`, `Ten TP`

## 5) Kha nang OLAP theo cube

Trong code hien tai:

- Cube co `TG` -> cho phep `DrillDown`, `RollUp`, `Slice`, `Dice`, `Pivot`
- Cube khong co `TG` -> cac thao tac tren bi khoa

## 6) Ghi chu quan trong cho cac yeu cau tiep theo

- Metadata `Measures` tra ve theo cube dang query:
  - BanHang: `Tong Tien`, `So Luong Dat`
  - TonKho: `So Luong Trong Kho`
- Danh sach member Product/Customer/Store lay tu schema co the can normalize (backend da co logic normalize/fallback).
- Co the lay chi tiet ten cot SQL, PK/FK truc tiep tu cac script trong thu muc `infor` (dac biet `In4.md`, `reseed_facts_olap.sql`, `Seed_50k_Data.sql`).

