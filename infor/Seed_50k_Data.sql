/*
    Seed dữ liệu mẫu cho mô hình Galaxy Schema (SQL Server)
    Mục tiêu:
    - Tạo dữ liệu đủ lớn để test, không bắt buộc mọi bảng 50,000 bản ghi
    - Tránh lỗi duplicate attribute key trên Dim_ThoiGian (thuộc tính Thang)
    - Dữ liệu đảm bảo đúng quan hệ khóa ngoại
*/

SET NOCOUNT ON;

DECLARE @TargetRows INT = 20000;  -- Số dòng cho các bảng chính
DECLARE @StartYear  INT = 2018;   -- Năm bắt đầu cho Dim_ThoiGian
DECLARE @EndYear    INT = 2026;   -- Năm kết thúc cho Dim_ThoiGian
DECLARE @TimeRows   INT = ((@EndYear - @StartYear + 1) * 12); -- Mỗi (Nam, Thang) là duy nhất

-- ============================================================
-- 1) Xóa dữ liệu cũ theo thứ tự an toàn với FK
-- ============================================================
DELETE FROM Fact_BanHang;
DELETE FROM Fact_TonKho;
DELETE FROM Dim_KhachHang;
DELETE FROM Dim_CuaHang;
DELETE FROM Dim_MatHang;
DELETE FROM Dim_ThoiGian;
DELETE FROM Dim_DiaDiem;

-- ============================================================
-- 2) Tạo tập số 1..50,000 dùng cho toàn bộ thao tác insert
-- ============================================================
;WITH CitySeed AS (
    SELECT *
    FROM (VALUES
        (N'Tokyo', N'Tokyo Prefecture', N'Japan'),
        (N'Osaka', N'Osaka Prefecture', N'Japan'),
        (N'Seoul', N'Seoul', N'South Korea'),
        (N'Busan', N'Busan', N'South Korea'),
        (N'Beijing', N'Beijing', N'China'),
        (N'Shanghai', N'Shanghai', N'China'),
        (N'Guangzhou', N'Guangdong', N'China'),
        (N'Shenzhen', N'Guangdong', N'China'),
        (N'Hong Kong', N'Hong Kong', N'China'),
        (N'Singapore', N'Central Region', N'Singapore'),
        (N'Bangkok', N'Bangkok', N'Thailand'),
        (N'Chiang Mai', N'Chiang Mai', N'Thailand'),
        (N'Hanoi', N'Ha Noi', N'Vietnam'),
        (N'Ho Chi Minh City', N'Ho Chi Minh', N'Vietnam'),
        (N'Da Nang', N'Da Nang', N'Vietnam'),
        (N'Kuala Lumpur', N'Kuala Lumpur', N'Malaysia'),
        (N'Johor Bahru', N'Johor', N'Malaysia'),
        (N'Jakarta', N'Jakarta', N'Indonesia'),
        (N'Surabaya', N'East Java', N'Indonesia'),
        (N'Manila', N'Metro Manila', N'Philippines'),
        (N'Cebu City', N'Cebu', N'Philippines'),
        (N'New Delhi', N'Delhi', N'India'),
        (N'Mumbai', N'Maharashtra', N'India'),
        (N'Bengaluru', N'Karnataka', N'India'),
        (N'Chennai', N'Tamil Nadu', N'India'),
        (N'Dubai', N'Dubai', N'United Arab Emirates'),
        (N'Abu Dhabi', N'Abu Dhabi', N'United Arab Emirates'),
        (N'Doha', N'Doha', N'Qatar'),
        (N'Istanbul', N'Istanbul', N'Turkiye'),
        (N'Ankara', N'Ankara', N'Turkiye'),
        (N'Riyadh', N'Riyadh', N'Saudi Arabia'),
        (N'Jeddah', N'Makkah', N'Saudi Arabia'),
        (N'Cairo', N'Cairo', N'Egypt'),
        (N'Cape Town', N'Western Cape', N'South Africa'),
        (N'Johannesburg', N'Gauteng', N'South Africa'),
        (N'Nairobi', N'Nairobi County', N'Kenya'),
        (N'Lagos', N'Lagos', N'Nigeria'),
        (N'Casablanca', N'Casablanca-Settat', N'Morocco'),
        (N'Paris', N'Ile-de-France', N'France'),
        (N'Lyon', N'Auvergne-Rhone-Alpes', N'France'),
        (N'Berlin', N'Berlin', N'Germany'),
        (N'Munich', N'Bavaria', N'Germany'),
        (N'Frankfurt', N'Hesse', N'Germany'),
        (N'Madrid', N'Community of Madrid', N'Spain'),
        (N'Barcelona', N'Catalonia', N'Spain'),
        (N'Rome', N'Lazio', N'Italy'),
        (N'Milan', N'Lombardy', N'Italy'),
        (N'Lisbon', N'Lisbon', N'Portugal'),
        (N'Amsterdam', N'North Holland', N'Netherlands'),
        (N'Brussels', N'Brussels-Capital Region', N'Belgium'),
        (N'Zurich', N'Zurich', N'Switzerland'),
        (N'Vienna', N'Vienna', N'Austria'),
        (N'Prague', N'Prague', N'Czech Republic'),
        (N'Warsaw', N'Masovian', N'Poland'),
        (N'Stockholm', N'Stockholm County', N'Sweden'),
        (N'Oslo', N'Oslo', N'Norway'),
        (N'Copenhagen', N'Capital Region', N'Denmark'),
        (N'Helsinki', N'Uusimaa', N'Finland'),
        (N'Dublin', N'Leinster', N'Ireland'),
        (N'London', N'England', N'United Kingdom'),
        (N'Manchester', N'England', N'United Kingdom'),
        (N'Edinburgh', N'Scotland', N'United Kingdom'),
        (N'Glasgow', N'Scotland', N'United Kingdom'),
        (N'Moscow', N'Moscow', N'Russia'),
        (N'Saint Petersburg', N'Saint Petersburg', N'Russia'),
        (N'Kyiv', N'Kyiv', N'Ukraine'),
        (N'Bucharest', N'Bucharest', N'Romania'),
        (N'Athens', N'Attica', N'Greece'),
        (N'Budapest', N'Budapest', N'Hungary'),
        (N'Belgrade', N'Belgrade', N'Serbia'),
        (N'New York City', N'New York', N'United States'),
        (N'Los Angeles', N'California', N'United States'),
        (N'Chicago', N'Illinois', N'United States'),
        (N'Houston', N'Texas', N'United States'),
        (N'Phoenix', N'Arizona', N'United States'),
        (N'Philadelphia', N'Pennsylvania', N'United States'),
        (N'San Antonio', N'Texas', N'United States'),
        (N'San Diego', N'California', N'United States'),
        (N'Dallas', N'Texas', N'United States'),
        (N'San Jose', N'California', N'United States'),
        (N'Austin', N'Texas', N'United States'),
        (N'Jacksonville', N'Florida', N'United States'),
        (N'San Francisco', N'California', N'United States'),
        (N'Columbus', N'Ohio', N'United States'),
        (N'Charlotte', N'North Carolina', N'United States'),
        (N'Indianapolis', N'Indiana', N'United States'),
        (N'Seattle', N'Washington', N'United States'),
        (N'Denver', N'Colorado', N'United States'),
        (N'Washington', N'District of Columbia', N'United States'),
        (N'Boston', N'Massachusetts', N'United States'),
        (N'El Paso', N'Texas', N'United States'),
        (N'Nashville', N'Tennessee', N'United States'),
        (N'Detroit', N'Michigan', N'United States'),
        (N'Portland', N'Oregon', N'United States'),
        (N'Oklahoma City', N'Oklahoma', N'United States'),
        (N'Las Vegas', N'Nevada', N'United States'),
        (N'Memphis', N'Tennessee', N'United States'),
        (N'Louisville', N'Kentucky', N'United States'),
        (N'Baltimore', N'Maryland', N'United States'),
        (N'Milwaukee', N'Wisconsin', N'United States'),
        (N'Albuquerque', N'New Mexico', N'United States'),
        (N'Tucson', N'Arizona', N'United States'),
        (N'Fresno', N'California', N'United States'),
        (N'Sacramento', N'California', N'United States'),
        (N'Kansas City', N'Missouri', N'United States'),
        (N'Atlanta', N'Georgia', N'United States'),
        (N'Miami', N'Florida', N'United States'),
        (N'Toronto', N'Ontario', N'Canada'),
        (N'Vancouver', N'British Columbia', N'Canada'),
        (N'Montreal', N'Quebec', N'Canada'),
        (N'Calgary', N'Alberta', N'Canada'),
        (N'Ottawa', N'Ontario', N'Canada'),
        (N'Mexico City', N'Ciudad de Mexico', N'Mexico'),
        (N'Guadalajara', N'Jalisco', N'Mexico'),
        (N'Monterrey', N'Nuevo Leon', N'Mexico'),
        (N'Sao Paulo', N'Sao Paulo', N'Brazil'),
        (N'Rio de Janeiro', N'Rio de Janeiro', N'Brazil'),
        (N'Brasilia', N'Distrito Federal', N'Brazil'),
        (N'Buenos Aires', N'Buenos Aires', N'Argentina'),
        (N'Santiago', N'Santiago Metropolitan', N'Chile'),
        (N'Lima', N'Lima', N'Peru'),
        (N'Bogota', N'Bogota Capital District', N'Colombia'),
        (N'Medellin', N'Antioquia', N'Colombia'),
        (N'Sydney', N'New South Wales', N'Australia'),
        (N'Melbourne', N'Victoria', N'Australia'),
        (N'Brisbane', N'Queensland', N'Australia'),
        (N'Perth', N'Western Australia', N'Australia'),
        (N'Auckland', N'Auckland', N'New Zealand'),
        (N'Wellington', N'Wellington', N'New Zealand')
    ) AS v(TenTP, Bang, QuocGia)
),
CityRank AS (
    SELECT ROW_NUMBER() OVER (ORDER BY TenTP, Bang, QuocGia) AS CityId, TenTP, Bang, QuocGia
    FROM CitySeed
),
CityCount AS (
    SELECT COUNT(1) AS TotalCities FROM CityRank
),
N AS (
    SELECT TOP (@TargetRows)
        ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.all_objects a
    CROSS JOIN sys.all_objects b
)
-- ------------------------------------------------------------
-- 2.1 Dim_DiaDiem - city/state mapping hop le
-- ------------------------------------------------------------
INSERT INTO Dim_DiaDiem (MaTP, TenTP, Bang, DiaChiVP)
SELECT
    CONCAT('TP', RIGHT('00000' + CAST(N.n AS VARCHAR(5)), 5)) AS MaTP,
    CONCAT(CR.TenTP, N' (', CR.QuocGia, N')') AS TenTP,
    CR.Bang AS Bang,
    CONCAT(CAST(((N.n - 1) % 300 + 1) AS NVARCHAR(10)), N' Global Business Ave, Zone ',
           CAST(((N.n - 1) % 25 + 1) AS NVARCHAR(10)), N', ', CR.TenTP) AS DiaChiVP
FROM N
CROSS JOIN CityCount CC
JOIN CityRank CR ON CR.CityId = ((N.n - 1) % CC.TotalCities) + 1
OPTION (MAXDOP 1);

;WITH N AS (
    SELECT TOP (@TargetRows)
        ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.all_objects a
    CROSS JOIN sys.all_objects b
)
-- ------------------------------------------------------------
-- 2.2 Dim_CuaHang - tham chiếu Dim_DiaDiem
-- ------------------------------------------------------------
INSERT INTO Dim_CuaHang (MaCH, SoDienThoai, MaTP)
SELECT
    CONCAT('CH', RIGHT('00000' + CAST(n AS VARCHAR(5)), 5)) AS MaCH,
    CONCAT('09', RIGHT('00000000' + CAST(((n * 17) % 100000000) AS VARCHAR(8)), 8)) AS SoDienThoai,
    CONCAT('TP', RIGHT('00000' + CAST((((n - 1) % @TargetRows) + 1) AS VARCHAR(5)), 5)) AS MaTP
FROM N
OPTION (MAXDOP 1);

;WITH N AS (
    SELECT TOP (@TargetRows)
        ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.all_objects a
    CROSS JOIN sys.all_objects b
)
-- ------------------------------------------------------------
-- 2.3 Dim_MatHang (50,000)
-- ------------------------------------------------------------
INSERT INTO Dim_MatHang (MaMH, MoTa, KichCo, TrongLuong, Gia)
SELECT
    CONCAT('MH', RIGHT('00000' + CAST(n AS VARCHAR(5)), 5)) AS MaMH,
    CONCAT(N'Mặt hàng số ', n, N' - nhóm ', ((n - 1) % 50) + 1) AS MoTa,
    CASE ((n - 1) % 5)
        WHEN 0 THEN N'S'
        WHEN 1 THEN N'M'
        WHEN 2 THEN N'L'
        WHEN 3 THEN N'XL'
        ELSE N'XXL'
    END AS KichCo,
    CAST(0.25 + ((n - 1) % 500) * 0.05 AS DECIMAL(10, 2)) AS TrongLuong,
    CAST(10000 + ((n - 1) % 2000) * 250 AS DECIMAL(18, 2)) AS Gia
FROM N
OPTION (MAXDOP 1);

;WITH N AS (
    SELECT TOP (@TargetRows)
        ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.all_objects a
    CROSS JOIN sys.all_objects b
)
-- ------------------------------------------------------------
-- 2.4 Dim_KhachHang - tham chiếu Dim_DiaDiem
-- ------------------------------------------------------------
INSERT INTO Dim_KhachHang (MaKH, TenKH, LoaiKH, MaTP)
SELECT
    CONCAT('KH', RIGHT('00000' + CAST(n AS VARCHAR(5)), 5)) AS MaKH,
    CONCAT(N'Khach hang ', n) AS TenKH,
    CASE ((n - 1) % 3)
        WHEN 0 THEN N'Khách hàng du lịch'
        WHEN 1 THEN N'Khách hàng bưu điện'
        ELSE N'Khách hàng thuộc cả 2 loại'
    END AS LoaiKH,
    CONCAT('TP', RIGHT('00000' + CAST((((n * 7) - 1) % @TargetRows + 1) AS VARCHAR(5)), 5)) AS MaTP
FROM N
OPTION (MAXDOP 1);

;WITH N AS (
    SELECT TOP (@TimeRows)
        ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.all_objects a
    CROSS JOIN sys.all_objects b
)
-- ------------------------------------------------------------
-- 2.5 Dim_ThoiGian (nhiều năm)
--     Mỗi cặp (Nam, Thang) chỉ xuất hiện đúng 1 lần để tránh duplicate key
-- ------------------------------------------------------------
INSERT INTO Dim_ThoiGian (MaTG, Nam, Quy, Thang)
SELECT
    CONCAT('TG', RIGHT('00000' + CAST(n AS VARCHAR(5)), 5)) AS MaTG,
    @StartYear + ((n - 1) / 12) AS Nam,
    ((((n - 1) % 12) / 3) + 1) AS Quy,
    (((n - 1) % 12) + 1) AS Thang
FROM N
OPTION (MAXDOP 1);

;WITH N AS (
    SELECT TOP (@TargetRows)
        ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.all_objects a
    CROSS JOIN sys.all_objects b
)
-- ------------------------------------------------------------
-- 2.6 Fact_BanHang (50,000)
--     PK tổng hợp (MaMH, MaKH, MaTG) là duy nhất theo n
-- ------------------------------------------------------------
INSERT INTO Fact_BanHang (MaMH, MaKH, MaTG, SoLuongDat, TongTien)
SELECT
    MH.MaMH,
    KH.MaKH,
    TG.MaTG,
    ((N.n - 1) % 20) + 1 AS SoLuongDat,
    CAST((((N.n - 1) % 20) + 1) * MH.Gia AS DECIMAL(18, 2)) AS TongTien
FROM N
JOIN Dim_MatHang   MH ON MH.MaMH = CONCAT('MH', RIGHT('00000' + CAST(N.n AS VARCHAR(5)), 5))
JOIN Dim_KhachHang KH ON KH.MaKH = CONCAT('KH', RIGHT('00000' + CAST(N.n AS VARCHAR(5)), 5))
JOIN Dim_ThoiGian  TG ON TG.MaTG = CONCAT('TG', RIGHT('00000' + CAST((((N.n - 1) % @TimeRows) + 1) AS VARCHAR(5)), 5))
OPTION (MAXDOP 1);

;WITH N AS (
    SELECT TOP (@TargetRows)
        ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n
    FROM sys.all_objects a
    CROSS JOIN sys.all_objects b
)
-- ------------------------------------------------------------
-- 2.7 Fact_TonKho (50,000)
--     PK tổng hợp (MaCH, MaMH, MaTG) là duy nhất theo n
-- ------------------------------------------------------------
INSERT INTO Fact_TonKho (MaCH, MaMH, MaTG, SoLuongTrongKho)
SELECT
    CH.MaCH,
    MH.MaMH,
    TG.MaTG,
    ((N.n - 1) % 500) + 10 AS SoLuongTrongKho
FROM N
JOIN Dim_CuaHang  CH ON CH.MaCH = CONCAT('CH', RIGHT('00000' + CAST(N.n AS VARCHAR(5)), 5))
JOIN Dim_MatHang  MH ON MH.MaMH = CONCAT('MH', RIGHT('00000' + CAST(N.n AS VARCHAR(5)), 5))
JOIN Dim_ThoiGian TG ON TG.MaTG = CONCAT('TG', RIGHT('00000' + CAST((((N.n - 1) % @TimeRows) + 1) AS VARCHAR(5)), 5))
OPTION (MAXDOP 1);

-- ============================================================
-- 3) Kiểm tra số lượng bản ghi
-- ============================================================
SELECT 'Dim_DiaDiem' AS [TableName], COUNT(*) AS [TotalRows] FROM Dim_DiaDiem
UNION ALL
SELECT 'Dim_CuaHang', COUNT(*) FROM Dim_CuaHang
UNION ALL
SELECT 'Dim_MatHang', COUNT(*) FROM Dim_MatHang
UNION ALL
SELECT 'Dim_KhachHang', COUNT(*) FROM Dim_KhachHang
UNION ALL
SELECT 'Dim_ThoiGian', COUNT(*) FROM Dim_ThoiGian
UNION ALL
SELECT 'Fact_BanHang', COUNT(*) FROM Fact_BanHang
UNION ALL
SELECT 'Fact_TonKho', COUNT(*) FROM Fact_TonKho;
