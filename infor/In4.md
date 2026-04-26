database infor: 

Authentication
| Thuộc tính            | Giá trị                |
| --------------------- | ---------------------- |
| Authentication Method | Windows Authentication |
| User Name             | DESKTOP-22UAJFI\DELL   |

Connection
| Thuộc tính          | Giá trị         |
| ------------------- | --------------- |
| Database            | KDL_2026_SERVER |
| SPID                | 83              |
| Network Protocol    | <default>       |
| Network Packet Size | 8192            |
| Connection Timeout  | 15              |
| Execution Timeout   | 0               |
| Encrypted           | Yes             |

Product
| Thuộc tính      | Giá trị                                                    |
| --------------- | ---------------------------------------------------------- |
| Product Version | 17.0.1110 RTM                                              |
| Server Name     | DESKTOP-22UAJFI                                            |
| Instance Name   | (trống)                                                    |
| Collation       | Latin1_General_CI_AS                                       |
| Product Name    | Microsoft SQL Server Enterprise Developer Edition (64-bit) |
| Language        | English (United States)                                    |

Server Environment
| Thuộc tính              | Giá trị         |
| ----------------------- | --------------- |
| Computer Name           | DESKTOP-22UAJFI |
| Host Platform           | Windows         |
| Host Distribution       | Windows 10 Pro  |
| Host Release            | 10.0            |
| Host Service Pack Level | (trống)         |
| Processors              | 16              |
| Operating System Memory | 32492           |



| STT | TABLE_NAME          | TABLE_TYPE |
| --- | ------------------- | ---------- |
| 1   | Dim_CuaHang         | BASE TABLE |
| 2   | Dim_KhachHang       | BASE TABLE |
| 3   | Dim_MatHang         | BASE TABLE |
| 4   | Dim_ThoiGian        | BASE TABLE |
| 5   | Dim_VanPhongDaiDien | BASE TABLE |
| 6   | Fact_BanHang        | BASE TABLE |
| 7   | Fact_TonKho         | BASE TABLE |

| STT | FK_Name               | ParentTable   | ParentColumn | ReferencedTable     | ReferencedColumn |
| --- | --------------------- | ------------- | ------------ | ------------------- | ---------------- |
| 1   | FK_CuaHang_VanPhong   | Dim_CuaHang   | MaTP         | Dim_VanPhongDaiDien | MaTP             |
| 2   | FK_KhachHang_VanPhong | Dim_KhachHang | MaTP         | Dim_VanPhongDaiDien | MaTP             |
| 3   | FK_BanHang_KhachHang  | Fact_BanHang  | MaKH         | Dim_KhachHang       | MaKH             |
| 4   | FK_BanHang_MatHang    | Fact_BanHang  | MaMH         | Dim_MatHang         | MaMH             |
| 5   | FK_BanHang_ThoiGian   | Fact_BanHang  | MaTG         | Dim_ThoiGian        | MaTG             |
| 6   | FK_TonKho_CuaHang     | Fact_TonKho   | MaCH         | Dim_CuaHang         | MaCH             |
| 7   | FK_TonKho_MatHang     | Fact_TonKho   | MaMH         | Dim_MatHang         | MaMH             |
| 8   | FK_TonKho_ThoiGian    | Fact_TonKho   | MaTG         | Dim_ThoiGian        | MaTG             |

QUERY:
-- ============================================================
-- Script tái cấu trúc Data Warehouse theo Galaxy Schema
-- Mô hình: 2 Fact tables + 5 Dimension tables
-- Tác giả: Tạo tự động dựa trên mô hình lược đồ dải ngân hà
-- ============================================================

-- ============================================================
-- BƯỚC 1: XÓA CÁC RÀNG BUỘC KHÓA NGOẠI HIỆN TẠI
-- ============================================================
IF OBJECT_ID('FK_TonKho_ThoiGian',    'F') IS NOT NULL ALTER TABLE Fact_TonKho  DROP CONSTRAINT FK_TonKho_ThoiGian;
IF OBJECT_ID('FK_BanHang_ThoiGian',   'F') IS NOT NULL ALTER TABLE Fact_BanHang DROP CONSTRAINT FK_BanHang_ThoiGian;
IF OBJECT_ID('FK_KhachHang_VanPhong', 'F') IS NOT NULL ALTER TABLE Dim_KhachHang DROP CONSTRAINT FK_KhachHang_VanPhong;
IF OBJECT_ID('FK_CuaHang_VanPhong',   'F') IS NOT NULL ALTER TABLE Dim_CuaHang   DROP CONSTRAINT FK_CuaHang_VanPhong;
IF OBJECT_ID('FK_BanHang_KhachHang',  'F') IS NOT NULL ALTER TABLE Fact_BanHang  DROP CONSTRAINT FK_BanHang_KhachHang;
IF OBJECT_ID('FK_TonKho_CuaHang',     'F') IS NOT NULL ALTER TABLE Fact_TonKho   DROP CONSTRAINT FK_TonKho_CuaHang;
IF OBJECT_ID('FK_BanHang_CuaHang',    'F') IS NOT NULL ALTER TABLE Fact_BanHang  DROP CONSTRAINT FK_BanHang_CuaHang;
IF OBJECT_ID('FK_TonKho_MatHang',     'F') IS NOT NULL ALTER TABLE Fact_TonKho   DROP CONSTRAINT FK_TonKho_MatHang;
IF OBJECT_ID('FK_BanHang_MatHang',    'F') IS NOT NULL ALTER TABLE Fact_BanHang  DROP CONSTRAINT FK_BanHang_MatHang;

-- ============================================================
-- BƯỚC 2: XÓA CÁC BẢNG CŨ
-- ============================================================
IF OBJECT_ID('Fact_BanHang',         'U') IS NOT NULL DROP TABLE Fact_BanHang;
IF OBJECT_ID('Fact_TonKho',          'U') IS NOT NULL DROP TABLE Fact_TonKho;
IF OBJECT_ID('Dim_KhachHang',        'U') IS NOT NULL DROP TABLE Dim_KhachHang;
IF OBJECT_ID('Dim_CuaHang',          'U') IS NOT NULL DROP TABLE Dim_CuaHang;
IF OBJECT_ID('Dim_MatHang',          'U') IS NOT NULL DROP TABLE Dim_MatHang;
IF OBJECT_ID('Dim_ThoiGian',         'U') IS NOT NULL DROP TABLE Dim_ThoiGian;
IF OBJECT_ID('Dim_VanPhongDaiDien',  'U') IS NOT NULL DROP TABLE Dim_VanPhongDaiDien;
IF OBJECT_ID('Dim_VanPhong',         'U') IS NOT NULL DROP TABLE Dim_VanPhong;

-- ============================================================
-- BƯỚC 3: TẠO CÁC BẢNG DIMENSION
-- ============================================================

-- ------------------------------------------------------------
-- 3.1 Dim_VanPhongDaiDien (Chiều thứ cấp)
--     Dùng chung cho Dim_CuaHang và Dim_KhachHang
-- ------------------------------------------------------------
CREATE TABLE Dim_VanPhongDaiDien (
    MaTP      NVARCHAR(10)  NOT NULL,   -- Khóa chính: Mã thành phố
    TenTP     NVARCHAR(100) NOT NULL,   -- Tên thành phố
    Bang      NVARCHAR(100) NOT NULL,   -- Tên bang
    DiaChiVP  NVARCHAR(255) NOT NULL,   -- Địa chỉ văn phòng đại diện
    CONSTRAINT PK_VanPhongDaiDien PRIMARY KEY (MaTP)
);

-- ------------------------------------------------------------
-- 3.2 Dim_CuaHang
-- ------------------------------------------------------------
CREATE TABLE Dim_CuaHang (
    MaCH        NVARCHAR(10)  NOT NULL,  -- Khóa chính: Mã cửa hàng
    SoDienThoai NVARCHAR(20)  NOT NULL,  -- Số điện thoại cửa hàng
    MaTP        NVARCHAR(10)  NOT NULL,  -- Khóa ngoại → Dim_VanPhongDaiDien
    CONSTRAINT PK_CuaHang PRIMARY KEY (MaCH),
    CONSTRAINT FK_CuaHang_VanPhong FOREIGN KEY (MaTP)
        REFERENCES Dim_VanPhongDaiDien (MaTP)
);

-- ------------------------------------------------------------
-- 3.3 Dim_MatHang
-- ------------------------------------------------------------
CREATE TABLE Dim_MatHang (
    MaMH       NVARCHAR(10)   NOT NULL,  -- Khóa chính: Mã mặt hàng
    MoTa       NVARCHAR(255)  NULL,      -- Mô tả mặt hàng
    KichCo     NVARCHAR(50)   NULL,      -- Kích cỡ
    TrongLuong DECIMAL(10, 2) NULL,      -- Trọng lượng
    Gia        DECIMAL(18, 2) NOT NULL,  -- Giá tiền
    CONSTRAINT PK_MatHang PRIMARY KEY (MaMH)
);

-- ------------------------------------------------------------
-- 3.4 Dim_KhachHang
-- ------------------------------------------------------------
CREATE TABLE Dim_KhachHang (
    MaKH   NVARCHAR(10)  NOT NULL,  -- Khóa chính: Mã khách hàng
    TenKH  NVARCHAR(150) NOT NULL,  -- Tên đầy đủ khách hàng
    LoaiKH NVARCHAR(50)  NULL,      -- Loại khách hàng
    MaTP   NVARCHAR(10)  NOT NULL,  -- Khóa ngoại → Dim_VanPhongDaiDien
    CONSTRAINT PK_KhachHang PRIMARY KEY (MaKH),
    CONSTRAINT FK_KhachHang_VanPhong FOREIGN KEY (MaTP)
        REFERENCES Dim_VanPhongDaiDien (MaTP)
);

-- ------------------------------------------------------------
-- 3.5 Dim_ThoiGian
-- ------------------------------------------------------------
CREATE TABLE Dim_ThoiGian (
    MaTG   NVARCHAR(10) NOT NULL,  -- Khóa chính: Mã thời gian (phạm vi 1 tháng)
    Nam    INT          NOT NULL,  -- Năm
    Quy    INT          NOT NULL,  -- Quý trong năm (1-4)
    Thang  INT          NOT NULL,  -- Tháng trong quý (1-3)
    CONSTRAINT PK_ThoiGian PRIMARY KEY (MaTG)
);

-- ============================================================
-- BƯỚC 4: TẠO CÁC BẢNG FACT
-- ============================================================

-- ------------------------------------------------------------
-- 4.1 Fact_BanHang
--     Khóa chính tổng hợp: MaMH + MaKH + MaTG
--     Quan hệ: Dim_MatHang, Dim_KhachHang, Dim_ThoiGian
--     (KHÔNG có quan hệ trực tiếp với Dim_CuaHang)
-- ------------------------------------------------------------
CREATE TABLE Fact_BanHang (
    MaMH          NVARCHAR(10)   NOT NULL,  -- Khóa ngoại → Dim_MatHang
    MaKH          NVARCHAR(10)   NOT NULL,  -- Khóa ngoại → Dim_KhachHang
    MaTG          NVARCHAR(10)   NOT NULL,  -- Khóa ngoại → Dim_ThoiGian
    SoLuongDat    INT            NOT NULL,  -- Độ đo: Tổng số lượng đặt
    TongTien      DECIMAL(18, 2) NOT NULL,  -- Độ đo: Tổng tiền bán được
    CONSTRAINT PK_BanHang PRIMARY KEY (MaMH, MaKH, MaTG),
    CONSTRAINT FK_BanHang_MatHang  FOREIGN KEY (MaMH) REFERENCES Dim_MatHang  (MaMH),
    CONSTRAINT FK_BanHang_KhachHang FOREIGN KEY (MaKH) REFERENCES Dim_KhachHang (MaKH),
    CONSTRAINT FK_BanHang_ThoiGian FOREIGN KEY (MaTG) REFERENCES Dim_ThoiGian  (MaTG)
);

-- ------------------------------------------------------------
-- 4.2 Fact_TonKho
--     Khóa chính tổng hợp: MaCH + MaMH + MaTG
--     Quan hệ: Dim_CuaHang, Dim_MatHang, Dim_ThoiGian
-- ------------------------------------------------------------
CREATE TABLE Fact_TonKho (
    MaCH             NVARCHAR(10) NOT NULL,  -- Khóa ngoại → Dim_CuaHang
    MaMH             NVARCHAR(10) NOT NULL,  -- Khóa ngoại → Dim_MatHang
    MaTG             NVARCHAR(10) NOT NULL,  -- Khóa ngoại → Dim_ThoiGian
    SoLuongTrongKho  INT          NOT NULL,  -- Độ đo: Số lượng tồn kho
    CONSTRAINT PK_TonKho PRIMARY KEY (MaCH, MaMH, MaTG),
    CONSTRAINT FK_TonKho_CuaHang  FOREIGN KEY (MaCH) REFERENCES Dim_CuaHang  (MaCH),
    CONSTRAINT FK_TonKho_MatHang  FOREIGN KEY (MaMH) REFERENCES Dim_MatHang  (MaMH),
    CONSTRAINT FK_TonKho_ThoiGian FOREIGN KEY (MaTG) REFERENCES Dim_ThoiGian (MaTG)
);

-- ============================================================
-- BƯỚC 5: KIỂM TRA LẠI CẤU TRÚC SAU KHI TẠO
-- ============================================================

-- Kiểm tra danh sách bảng
SELECT 
    TABLE_NAME,
    TABLE_TYPE
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_TYPE = 'BASE TABLE'
  AND TABLE_NAME IN (
      'Dim_VanPhongDaiDien', 'Dim_CuaHang', 'Dim_MatHang',
      'Dim_KhachHang', 'Dim_ThoiGian', 'Fact_BanHang', 'Fact_TonKho'
  )
ORDER BY TABLE_TYPE DESC, TABLE_NAME;

-- Kiểm tra toàn bộ khóa ngoại
SELECT 
    fk.name                AS FK_Name,
    tp.name                AS ParentTable,
    cp.name                AS ParentColumn,
    tr.name                AS ReferencedTable,
    cr.name                AS ReferencedColumn
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
JOIN sys.tables  tp  ON fkc.parent_object_id     = tp.object_id
JOIN sys.columns cp  ON fkc.parent_object_id     = cp.object_id AND fkc.parent_column_id     = cp.column_id
JOIN sys.tables  tr  ON fkc.referenced_object_id = tr.object_id
JOIN sys.columns cr  ON fkc.referenced_object_id = cr.object_id AND fkc.referenced_column_id = cr.column_id
ORDER BY tp.name, fk.name;