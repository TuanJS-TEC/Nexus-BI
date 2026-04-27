# Nexus BI - Multidimensional Data Analytics Platform

A high-performance Business Intelligence solution designed for sophisticated multi-dimensional data analysis. Built with a robust **.NET Core** backend and a reactive **Vue 3** frontend, this platform leverages **SQL Server Analysis Services (SSAS)** and **MDX** to deliver deep, interactive insights through dynamic visualizations and seamless data exploration.

## Project Overview
This project demonstrates the implementation of core OLAP operations (Drill-down, Roll-up, Slice, Dice, and Pivot) on SSAS cubes, providing a modern web interface for enterprise-grade data analysis.

---

## Kiến Trúc
```
Vue UI (port 5173) → ASP.NET API (port 5000) → SSAS (DESKTOP-22UAJFI) → ASP.NET → Vue
```

## Khởi Động

### 1. Backend (ASP.NET Core)
```powershell
cd E:\PTIT_Document\KDL_WEB\OlapDemo.Api
dotnet run
# API chạy tại: http://localhost:5000
# Swagger UI: http://localhost:5000/swagger
```

### 2. Frontend (Vue 3)
```powershell
cd E:\PTIT_Document\KDL_WEB\olap-demo-ui
npm run dev
# UI chạy tại: http://localhost:5173/dashboard
```

---

## API Endpoints

| Method | Endpoint | Mô tả |
|--------|----------|-------|
| GET | `/api/olap/metadata?cube=...` | Lấy danh sách Năm, Mã MH, Mã KH |
| POST | `/api/olap/drill` | Drill down theo cấp thời gian |
| POST | `/api/olap/rollup` | Roll up lên cấp cao hơn |
| POST | `/api/olap/slice-dice` | Slice hoặc Dice dữ liệu |
| POST | `/api/olap/pivot` | Pivot trục hàng/cột |

---

## Cấu Hình SSAS
`OlapDemo.Api/appsettings.json`:
```json
{
  "Ssas": {
    "ConnectionString": "Data Source=DESKTOP-22UAJFI;Catalog=Datawarehouse_01;",
    "DefaultCube": "Cube4BanHang_1D_TG_01"
  }
}
```

## Danh Sách Cube
| Cube | Dimensions |
|------|-----------|
| `Cube4BanHang_3D_KH_MH_TG_01` | KH × MH × TG (3D) |
| `Cube4BanHang_2D_KH_TG_01` | KH × TG |
| `Cube4BanHang_2D_MH_KH_01` | MH × KH |
| `Cube4BanHang_2D_MH_TG_01` | MH × TG |
| `Cube4BanHang_1D_MH_01` | MH only |
| `Cube4BanHang_1D_KH_01` | KH only |
| `Cube4BanHang_1D_TG_01` | TG only |
| `Cube4TonKho_3D_MH_CH_TG_01` | MH × CH × TG |
| `Cube4TonKho_2D_CH_TG_01` | CH × TG |
| `Cube4TonKho_2D_MH_TG_01` | MH × TG |
| `Cube4TonKho_1D_TG_01` | TG only |

## Lưu Ý Quan Trọng
1. **Không đặt cùng hierarchy ở ROWS và WHERE** → dùng subselect cho Dice
2. **Dùng NON EMPTY** → tránh bảng đầy NULL
3. **Whitelist measure/level** → tránh MDX injection
4. **CORS đã bật** → Vue (5173) gọi được API (5000)
