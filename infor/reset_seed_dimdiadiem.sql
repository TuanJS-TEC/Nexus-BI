SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRAN;

    /* 1) Drop old constraints if they exist */
    IF OBJECT_ID('FK_TonKho_ThoiGian', 'F') IS NOT NULL ALTER TABLE Fact_TonKho DROP CONSTRAINT FK_TonKho_ThoiGian;
    IF OBJECT_ID('FK_TonKho_MatHang', 'F') IS NOT NULL ALTER TABLE Fact_TonKho DROP CONSTRAINT FK_TonKho_MatHang;
    IF OBJECT_ID('FK_TonKho_CuaHang', 'F') IS NOT NULL ALTER TABLE Fact_TonKho DROP CONSTRAINT FK_TonKho_CuaHang;
    IF OBJECT_ID('FK_BanHang_ThoiGian', 'F') IS NOT NULL ALTER TABLE Fact_BanHang DROP CONSTRAINT FK_BanHang_ThoiGian;
    IF OBJECT_ID('FK_BanHang_MatHang', 'F') IS NOT NULL ALTER TABLE Fact_BanHang DROP CONSTRAINT FK_BanHang_MatHang;
    IF OBJECT_ID('FK_BanHang_KhachHang', 'F') IS NOT NULL ALTER TABLE Fact_BanHang DROP CONSTRAINT FK_BanHang_KhachHang;
    IF OBJECT_ID('FK_KhachHang_DiaDiem', 'F') IS NOT NULL ALTER TABLE Dim_KhachHang DROP CONSTRAINT FK_KhachHang_DiaDiem;
    IF OBJECT_ID('FK_KhachHang_VanPhong', 'F') IS NOT NULL ALTER TABLE Dim_KhachHang DROP CONSTRAINT FK_KhachHang_VanPhong;
    IF OBJECT_ID('FK_CuaHang_DiaDiem', 'F') IS NOT NULL ALTER TABLE Dim_CuaHang DROP CONSTRAINT FK_CuaHang_DiaDiem;
    IF OBJECT_ID('FK_CuaHang_VanPhong', 'F') IS NOT NULL ALTER TABLE Dim_CuaHang DROP CONSTRAINT FK_CuaHang_VanPhong;

    /* 2) Drop old tables (facts first) */
    IF OBJECT_ID('Fact_BanHang', 'U') IS NOT NULL DROP TABLE Fact_BanHang;
    IF OBJECT_ID('Fact_TonKho', 'U') IS NOT NULL DROP TABLE Fact_TonKho;
    IF OBJECT_ID('Dim_KhachHang', 'U') IS NOT NULL DROP TABLE Dim_KhachHang;
    IF OBJECT_ID('Dim_CuaHang', 'U') IS NOT NULL DROP TABLE Dim_CuaHang;
    IF OBJECT_ID('Dim_MatHang', 'U') IS NOT NULL DROP TABLE Dim_MatHang;
    IF OBJECT_ID('Dim_ThoiGian', 'U') IS NOT NULL DROP TABLE Dim_ThoiGian;
    IF OBJECT_ID('Dim_DiaDiem', 'U') IS NOT NULL DROP TABLE Dim_DiaDiem;
    IF OBJECT_ID('Dim_VanPhongDaiDien', 'U') IS NOT NULL DROP TABLE Dim_VanPhongDaiDien;
    IF OBJECT_ID('Dim_VanPhong', 'U') IS NOT NULL DROP TABLE Dim_VanPhong;

    /* 3) Create dimensions with unified geography name: Dim_DiaDiem */
    CREATE TABLE Dim_DiaDiem (
        MaTP      NVARCHAR(10)  NOT NULL,
        TenTP     NVARCHAR(100) NOT NULL,
        Bang      NVARCHAR(100) NOT NULL,
        DiaChiVP  NVARCHAR(255) NOT NULL,
        CONSTRAINT PK_Dim_DiaDiem PRIMARY KEY (MaTP)
    );

    CREATE TABLE Dim_CuaHang (
        MaCH        NVARCHAR(10) NOT NULL,
        SoDienThoai NVARCHAR(20) NOT NULL,
        MaTP        NVARCHAR(10) NOT NULL,
        CONSTRAINT PK_Dim_CuaHang PRIMARY KEY (MaCH),
        CONSTRAINT FK_CuaHang_DiaDiem FOREIGN KEY (MaTP) REFERENCES Dim_DiaDiem (MaTP)
    );

    CREATE TABLE Dim_MatHang (
        MaMH       NVARCHAR(10)   NOT NULL,
        MoTa       NVARCHAR(255)  NULL,
        KichCo     NVARCHAR(50)   NULL,
        TrongLuong DECIMAL(10, 2) NULL,
        Gia        DECIMAL(18, 2) NOT NULL,
        CONSTRAINT PK_Dim_MatHang PRIMARY KEY (MaMH)
    );

    CREATE TABLE Dim_KhachHang (
        MaKH   NVARCHAR(10)  NOT NULL,
        TenKH  NVARCHAR(150) NOT NULL,
        LoaiKH NVARCHAR(50)  NULL,
        MaTP   NVARCHAR(10)  NOT NULL,
        CONSTRAINT PK_Dim_KhachHang PRIMARY KEY (MaKH),
        CONSTRAINT FK_KhachHang_DiaDiem FOREIGN KEY (MaTP) REFERENCES Dim_DiaDiem (MaTP)
    );

    CREATE TABLE Dim_ThoiGian (
        MaTG   NVARCHAR(10) NOT NULL, /* TGYYYYMM */
        Nam    INT          NOT NULL,
        Quy    INT          NOT NULL,
        Thang  INT          NOT NULL, /* 1..12 */
        CONSTRAINT PK_Dim_ThoiGian PRIMARY KEY (MaTG)
    );

    /* 4) Create facts */
    CREATE TABLE Fact_BanHang (
        MaMH       NVARCHAR(10)   NOT NULL,
        MaKH       NVARCHAR(10)   NOT NULL,
        MaTG       NVARCHAR(10)   NOT NULL,
        SoLuongDat INT            NOT NULL,
        TongTien   DECIMAL(18, 2) NOT NULL,
        CONSTRAINT PK_Fact_BanHang PRIMARY KEY (MaMH, MaKH, MaTG),
        CONSTRAINT FK_BanHang_MatHang   FOREIGN KEY (MaMH) REFERENCES Dim_MatHang (MaMH),
        CONSTRAINT FK_BanHang_KhachHang FOREIGN KEY (MaKH) REFERENCES Dim_KhachHang (MaKH),
        CONSTRAINT FK_BanHang_ThoiGian  FOREIGN KEY (MaTG) REFERENCES Dim_ThoiGian (MaTG)
    );

    CREATE TABLE Fact_TonKho (
        MaCH            NVARCHAR(10) NOT NULL,
        MaMH            NVARCHAR(10) NOT NULL,
        MaTG            NVARCHAR(10) NOT NULL,
        SoLuongTrongKho INT          NOT NULL,
        CONSTRAINT PK_Fact_TonKho PRIMARY KEY (MaCH, MaMH, MaTG),
        CONSTRAINT FK_TonKho_CuaHang  FOREIGN KEY (MaCH) REFERENCES Dim_CuaHang (MaCH),
        CONSTRAINT FK_TonKho_MatHang  FOREIGN KEY (MaMH) REFERENCES Dim_MatHang (MaMH),
        CONSTRAINT FK_TonKho_ThoiGian FOREIGN KEY (MaTG) REFERENCES Dim_ThoiGian (MaTG)
    );

    /* 5) Seed dimensions */
    INSERT INTO Dim_DiaDiem (MaTP, TenTP, Bang, DiaChiVP)
    VALUES
        ('TP01', N'Ha Noi', N'Mien Bac', N'12 Ba Trieu'),
        ('TP02', N'Hai Phong', N'Mien Bac', N'18 Tran Phu'),
        ('TP03', N'Quang Ninh', N'Mien Bac', N'33 Le Thanh Tong'),
        ('TP04', N'Da Nang', N'Mien Trung', N'71 Bach Dang'),
        ('TP05', N'Hue', N'Mien Trung', N'21 Hung Vuong'),
        ('TP06', N'Khanh Hoa', N'Mien Trung', N'10 Tran Phu'),
        ('TP07', N'TP HCM', N'Mien Nam', N'101 Nguyen Hue'),
        ('TP08', N'Can Tho', N'Mien Nam', N'15 Hoa Binh'),
        ('TP09', N'Dong Nai', N'Mien Nam', N'88 Vo Thi Sau'),
        ('TP10', N'Binh Duong', N'Mien Nam', N'50 Cach Mang Thang 8');

    ;WITH n AS (
        SELECT TOP (20) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS i
        FROM sys.all_objects
    )
    INSERT INTO Dim_CuaHang (MaCH, SoDienThoai, MaTP)
    SELECT
        'CH' + RIGHT('000' + CAST(i AS VARCHAR(3)), 3),
        '09' + RIGHT('00000000' + CAST(10000000 + i * 177 AS VARCHAR(8)), 8),
        'TP' + RIGHT('00' + CAST(((i - 1) % 10) + 1 AS VARCHAR(2)), 2)
    FROM n;

    ;WITH n AS (
        SELECT TOP (200) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS i
        FROM sys.all_objects
    )
    INSERT INTO Dim_KhachHang (MaKH, TenKH, LoaiKH, MaTP)
    SELECT
        'KH' + RIGHT('00000' + CAST(i AS VARCHAR(5)), 5),
        N'KhachHang ' + CAST(i AS NVARCHAR(10)),
        CASE WHEN i % 3 = 0 THEN N'VIP' WHEN i % 3 = 1 THEN N'Retail' ELSE N'Wholesale' END,
        'TP' + RIGHT('00' + CAST(((i - 1) % 10) + 1 AS VARCHAR(2)), 2)
    FROM n;

    ;WITH n AS (
        SELECT TOP (300) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS i
        FROM sys.all_objects
    )
    INSERT INTO Dim_MatHang (MaMH, MoTa, KichCo, TrongLuong, Gia)
    SELECT
        'MH' + RIGHT('00000' + CAST(i AS VARCHAR(5)), 5),
        N'Mat hang ' + CAST(i AS NVARCHAR(10)),
        CASE WHEN i % 4 = 0 THEN N'XL' WHEN i % 4 = 1 THEN N'S' WHEN i % 4 = 2 THEN N'M' ELSE N'L' END,
        CAST(0.2 + (i % 30) * 0.15 AS DECIMAL(10,2)),
        CAST(45000 + (i % 50) * 8500 AS DECIMAL(18,2))
    FROM n;

    ;WITH y AS (
        SELECT 2024 AS Nam UNION ALL SELECT 2025 UNION ALL SELECT 2026
    ),
    m AS (
        SELECT TOP (12) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS Thang
        FROM sys.all_objects
    )
    INSERT INTO Dim_ThoiGian (MaTG, Nam, Quy, Thang)
    SELECT
        'TG' + CAST(y.Nam AS VARCHAR(4)) + RIGHT('00' + CAST(m.Thang AS VARCHAR(2)), 2),
        y.Nam,
        ((m.Thang - 1) / 3) + 1,
        m.Thang
    FROM y CROSS JOIN m;

    /* 6) Seed Fact_BanHang with guaranteed monthly coverage per product */
    ;WITH prod AS (
        SELECT MaMH, CAST(SUBSTRING(MaMH, 3, 5) AS INT) AS mh_idx, Gia
        FROM Dim_MatHang
    ),
    tg AS (
        SELECT MaTG, Nam, Thang
        FROM Dim_ThoiGian
    ),
    base AS (
        SELECT
            p.MaMH,
            p.mh_idx,
            p.Gia,
            t.MaTG,
            t.Nam,
            t.Thang,
            'KH' + RIGHT('00000' + CAST(((p.mh_idx + t.Thang + t.Nam) % 200) + 1 AS VARCHAR(5)), 5) AS BaseKH
        FROM prod p
        CROSS JOIN tg t
    )
    INSERT INTO Fact_BanHang (MaMH, MaKH, MaTG, SoLuongDat, TongTien)
    SELECT
        b.MaMH,
        b.BaseKH,
        b.MaTG,
        (ABS(CHECKSUM(b.MaMH, b.MaTG, 'B')) % 25) + 5 +
            CASE WHEN b.Thang IN (11,12,1) THEN 5 ELSE 0 END AS SoLuongDat,
        CAST(
            (
                (ABS(CHECKSUM(b.MaMH, b.MaTG, 'B')) % 25) + 5 +
                CASE WHEN b.Thang IN (11,12,1) THEN 5 ELSE 0 END
            ) * b.Gia * (1 + ((ABS(CHECKSUM(b.mh_idx, b.Thang)) % 11) - 5) / 100.0)
            AS DECIMAL(18,2)
        ) AS TongTien
    FROM base b;

    ;WITH prod AS (
        SELECT MaMH, CAST(SUBSTRING(MaMH, 3, 5) AS INT) AS mh_idx, Gia
        FROM Dim_MatHang
    ),
    tg AS (
        SELECT MaTG, Nam, Thang
        FROM Dim_ThoiGian
    ),
    kh AS (
        SELECT MaKH, CAST(SUBSTRING(MaKH, 3, 5) AS INT) AS kh_idx
        FROM Dim_KhachHang
    ),
    base AS (
        SELECT
            p.MaMH,
            p.mh_idx,
            p.Gia,
            t.MaTG,
            t.Nam,
            t.Thang,
            'KH' + RIGHT('00000' + CAST(((p.mh_idx + t.Thang + t.Nam) % 200) + 1 AS VARCHAR(5)), 5) AS BaseKH
        FROM prod p
        CROSS JOIN tg t
    )
    INSERT INTO Fact_BanHang (MaMH, MaKH, MaTG, SoLuongDat, TongTien)
    SELECT
        b.MaMH,
        k.MaKH,
        b.MaTG,
        (ABS(CHECKSUM(b.MaMH, k.MaKH, b.MaTG, 'E')) % 20) + 1 +
            CASE WHEN b.Thang IN (11,12,1) THEN 4 ELSE 0 END AS SoLuongDat,
        CAST(
            (
                (ABS(CHECKSUM(b.MaMH, k.MaKH, b.MaTG, 'E')) % 20) + 1 +
                CASE WHEN b.Thang IN (11,12,1) THEN 4 ELSE 0 END
            ) * b.Gia * (1 + ((ABS(CHECKSUM(k.kh_idx, b.mh_idx, b.Thang)) % 21) - 10) / 100.0)
            AS DECIMAL(18,2)
        ) AS TongTien
    FROM base b
    JOIN kh k
        ON ABS(CHECKSUM(b.MaMH, b.MaTG, k.MaKH)) % 100 < 15
       AND k.MaKH <> b.BaseKH;

    /* 7) Seed Fact_TonKho */
    ;WITH ch AS (
        SELECT MaCH, CAST(SUBSTRING(MaCH, 3, 3) AS INT) AS ch_idx
        FROM Dim_CuaHang
    ),
    mh AS (
        SELECT MaMH, CAST(SUBSTRING(MaMH, 3, 5) AS INT) AS mh_idx
        FROM Dim_MatHang
    ),
    tg AS (
        SELECT MaTG, Thang
        FROM Dim_ThoiGian
    )
    INSERT INTO Fact_TonKho (MaCH, MaMH, MaTG, SoLuongTrongKho)
    SELECT
        c.MaCH,
        m.MaMH,
        t.MaTG,
        50 + (ABS(CHECKSUM(c.MaCH, m.MaMH, t.MaTG)) % 450) +
            CASE WHEN t.Thang IN (1,2) THEN 40 WHEN t.Thang IN (11,12) THEN 25 ELSE 0 END AS SoLuongTrongKho
    FROM ch c
    CROSS JOIN mh m
    CROSS JOIN tg t
    WHERE ABS(CHECKSUM(c.MaCH, m.MaMH, t.MaTG, 'TK')) % 100 < 35;

    COMMIT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    THROW;
END CATCH;

/* 8) Verification summary */
SELECT 'Dim_DiaDiem' AS TableName, COUNT(*) AS [TotalRows] FROM Dim_DiaDiem
UNION ALL SELECT 'Dim_CuaHang', COUNT(*) FROM Dim_CuaHang
UNION ALL SELECT 'Dim_KhachHang', COUNT(*) FROM Dim_KhachHang
UNION ALL SELECT 'Dim_MatHang', COUNT(*) FROM Dim_MatHang
UNION ALL SELECT 'Dim_ThoiGian', COUNT(*) FROM Dim_ThoiGian
UNION ALL SELECT 'Fact_BanHang', COUNT(*) FROM Fact_BanHang
UNION ALL SELECT 'Fact_TonKho', COUNT(*) FROM Fact_TonKho;

SELECT
    MIN(x.so_thang) AS min_so_thang_2025,
    MAX(x.so_thang) AS max_so_thang_2025,
    AVG(CAST(x.so_thang AS FLOAT)) AS avg_so_thang_2025
FROM (
    SELECT fb.MaMH, COUNT(DISTINCT tg.MaTG) AS so_thang
    FROM Fact_BanHang fb
    JOIN Dim_ThoiGian tg ON tg.MaTG = fb.MaTG
    WHERE tg.Nam = 2025
    GROUP BY fb.MaMH
) x;
