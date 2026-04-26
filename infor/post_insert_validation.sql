SET NOCOUNT ON;

PRINT '==== POST INSERT VALIDATION START ====';

/* ----------------------------------------------------------
   1) Row counts
-----------------------------------------------------------*/
SELECT 'Dim_DiaDiem' AS TableName, COUNT(*) AS TotalRows FROM Dim_DiaDiem
UNION ALL SELECT 'Dim_CuaHang', COUNT(*) FROM Dim_CuaHang
UNION ALL SELECT 'Dim_KhachHang', COUNT(*) FROM Dim_KhachHang
UNION ALL SELECT 'Dim_MatHang', COUNT(*) FROM Dim_MatHang
UNION ALL SELECT 'Dim_ThoiGian', COUNT(*) FROM Dim_ThoiGian
UNION ALL SELECT 'Fact_BanHang', COUNT(*) FROM Fact_BanHang
UNION ALL SELECT 'Fact_TonKho', COUNT(*) FROM Fact_TonKho;

/* ----------------------------------------------------------
   2) Duplicate PK checks
-----------------------------------------------------------*/
SELECT 'DUP_PK_Fact_BanHang' AS CheckName, COUNT(*) AS Violations
FROM (
    SELECT MaMH, MaKH, MaTG
    FROM Fact_BanHang
    GROUP BY MaMH, MaKH, MaTG
    HAVING COUNT(*) > 1
) d;

SELECT 'DUP_PK_Fact_TonKho' AS CheckName, COUNT(*) AS Violations
FROM (
    SELECT MaCH, MaMH, MaTG
    FROM Fact_TonKho
    GROUP BY MaCH, MaMH, MaTG
    HAVING COUNT(*) > 1
) d;

/* ----------------------------------------------------------
   3) Orphan FK checks
-----------------------------------------------------------*/
SELECT 'ORPHAN_FK_Fact_BanHang' AS CheckName, COUNT(*) AS Violations
FROM Fact_BanHang fb
LEFT JOIN Dim_MatHang mh ON mh.MaMH = fb.MaMH
LEFT JOIN Dim_KhachHang kh ON kh.MaKH = fb.MaKH
LEFT JOIN Dim_ThoiGian tg ON tg.MaTG = fb.MaTG
WHERE mh.MaMH IS NULL OR kh.MaKH IS NULL OR tg.MaTG IS NULL;

SELECT 'ORPHAN_FK_Fact_TonKho' AS CheckName, COUNT(*) AS Violations
FROM Fact_TonKho tk
LEFT JOIN Dim_CuaHang ch ON ch.MaCH = tk.MaCH
LEFT JOIN Dim_MatHang mh ON mh.MaMH = tk.MaMH
LEFT JOIN Dim_ThoiGian tg ON tg.MaTG = tk.MaTG
WHERE ch.MaCH IS NULL OR mh.MaMH IS NULL OR tg.MaTG IS NULL;

/* ----------------------------------------------------------
   4) Domain and consistency checks
-----------------------------------------------------------*/
SELECT 'NEGATIVE_OR_ZERO_FACT_BANHANG' AS CheckName, COUNT(*) AS Violations
FROM Fact_BanHang
WHERE SoLuongDat <= 0 OR TongTien < 0;

SELECT 'NEGATIVE_FACT_TONKHO' AS CheckName, COUNT(*) AS Violations
FROM Fact_TonKho
WHERE SoLuongTrongKho < 0;

SELECT 'INVALID_DIM_THOIGIAN_MONTH' AS CheckName, COUNT(*) AS Violations
FROM Dim_ThoiGian
WHERE Thang < 1 OR Thang > 12;

SELECT 'INVALID_DIM_THOIGIAN_QUARTER' AS CheckName, COUNT(*) AS Violations
FROM Dim_ThoiGian
WHERE Quy < 1 OR Quy > 4;

SELECT 'MISMATCH_QUARTER_FROM_MONTH' AS CheckName, COUNT(*) AS Violations
FROM Dim_ThoiGian
WHERE Quy <> ((Thang - 1) / 3) + 1;

/* ----------------------------------------------------------
   5) Coverage checks for OLAP demo (year 2025)
-----------------------------------------------------------*/
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

SELECT
    tg.Thang,
    COUNT(DISTINCT fb.MaMH) AS so_mat_hang,
    COUNT(*) AS so_dong_fact
FROM Fact_BanHang fb
JOIN Dim_ThoiGian tg ON tg.MaTG = fb.MaTG
WHERE tg.Nam = 2025
GROUP BY tg.Thang
ORDER BY tg.Thang;

/* ----------------------------------------------------------
   6) Sample rows for quick inspection
-----------------------------------------------------------*/
SELECT TOP 20 * FROM Dim_DiaDiem ORDER BY MaTP;
SELECT TOP 20 * FROM Dim_ThoiGian ORDER BY Nam, Thang;
SELECT TOP 20 * FROM Fact_BanHang ORDER BY MaTG, MaMH, MaKH;
SELECT TOP 20 * FROM Fact_TonKho ORDER BY MaTG, MaCH, MaMH;

PRINT '==== POST INSERT VALIDATION END ====';
