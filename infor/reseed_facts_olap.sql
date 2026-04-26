SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRAN;

    /* Clear fact data only */
    DELETE FROM Fact_BanHang;
    DELETE FROM Fact_TonKho;

    /* Scope for demo-friendly OLAP dataset */
    ;WITH p AS (
        SELECT TOP (300)
            MaMH,
            Gia,
            ROW_NUMBER() OVER (ORDER BY MaMH) AS p_idx
        FROM Dim_MatHang
        ORDER BY MaMH
    ),
    k AS (
        SELECT TOP (300)
            MaKH,
            ROW_NUMBER() OVER (ORDER BY MaKH) AS k_idx
        FROM Dim_KhachHang
        ORDER BY MaKH
    ),
    t AS (
        SELECT
            MaTG,
            Nam,
            Quy,
            Thang,
            ROW_NUMBER() OVER (ORDER BY Nam, Thang) AS t_idx
        FROM Dim_ThoiGian
        WHERE Nam BETWEEN 2024 AND 2026
    ),
    base AS (
        SELECT
            p.MaMH,
            p.p_idx,
            p.Gia,
            t.MaTG,
            t.Nam,
            t.Quy,
            t.Thang,
            t.t_idx,
            'KH' + RIGHT('00000' + CAST(((p.p_idx + t.t_idx) % 300) + 1 AS VARCHAR(5)), 5) AS BaseKH
        FROM p
        CROSS JOIN t
    )
    INSERT INTO Fact_BanHang (MaMH, MaKH, MaTG, SoLuongDat, TongTien)
    SELECT
        b.MaMH,
        b.BaseKH,
        b.MaTG,
        (ABS(CHECKSUM(b.MaMH, b.MaTG, 'B')) % 28) + 4
            + CASE WHEN b.Thang IN (11, 12) THEN 8 WHEN b.Thang = 1 THEN 5 ELSE 0 END AS SoLuongDat,
        CAST(
            (
                (ABS(CHECKSUM(b.MaMH, b.MaTG, 'B')) % 28) + 4
                + CASE WHEN b.Thang IN (11, 12) THEN 8 WHEN b.Thang = 1 THEN 5 ELSE 0 END
            ) * b.Gia * (1 + ((ABS(CHECKSUM(b.p_idx, b.t_idx)) % 13) - 6) / 100.0)
            AS DECIMAL(18, 2)
        ) AS TongTien
    FROM base b;

    ;WITH p AS (
        SELECT TOP (300)
            MaMH,
            Gia,
            ROW_NUMBER() OVER (ORDER BY MaMH) AS p_idx
        FROM Dim_MatHang
        ORDER BY MaMH
    ),
    k AS (
        SELECT TOP (300)
            MaKH,
            ROW_NUMBER() OVER (ORDER BY MaKH) AS k_idx
        FROM Dim_KhachHang
        ORDER BY MaKH
    ),
    t AS (
        SELECT
            MaTG,
            Nam,
            Thang,
            ROW_NUMBER() OVER (ORDER BY Nam, Thang) AS t_idx
        FROM Dim_ThoiGian
        WHERE Nam BETWEEN 2024 AND 2026
    ),
    base AS (
        SELECT
            p.MaMH,
            p.p_idx,
            p.Gia,
            t.MaTG,
            t.Nam,
            t.Thang,
            t.t_idx,
            'KH' + RIGHT('00000' + CAST(((p.p_idx + t.t_idx) % 300) + 1 AS VARCHAR(5)), 5) AS BaseKH
        FROM p
        CROSS JOIN t
    )
    INSERT INTO Fact_BanHang (MaMH, MaKH, MaTG, SoLuongDat, TongTien)
    SELECT
        b.MaMH,
        k.MaKH,
        b.MaTG,
        (ABS(CHECKSUM(b.MaMH, k.MaKH, b.MaTG, 'E')) % 24) + 1
            + CASE WHEN b.Thang IN (11, 12) THEN 6 WHEN b.Thang = 1 THEN 4 ELSE 0 END AS SoLuongDat,
        CAST(
            (
                (ABS(CHECKSUM(b.MaMH, k.MaKH, b.MaTG, 'E')) % 24) + 1
                + CASE WHEN b.Thang IN (11, 12) THEN 6 WHEN b.Thang = 1 THEN 4 ELSE 0 END
            ) * b.Gia * (1 + ((ABS(CHECKSUM(k.k_idx, b.p_idx, b.t_idx)) % 21) - 10) / 100.0)
            AS DECIMAL(18, 2)
        ) AS TongTien
    FROM base b
    JOIN k
      ON ABS(CHECKSUM(b.MaMH, b.MaTG, k.MaKH)) % 100 < 8
     AND k.MaKH <> b.BaseKH;

    ;WITH c AS (
        SELECT TOP (40)
            MaCH,
            ROW_NUMBER() OVER (ORDER BY MaCH) AS c_idx
        FROM Dim_CuaHang
        ORDER BY MaCH
    ),
    p AS (
        SELECT TOP (300)
            MaMH,
            ROW_NUMBER() OVER (ORDER BY MaMH) AS p_idx
        FROM Dim_MatHang
        ORDER BY MaMH
    ),
    t AS (
        SELECT
            MaTG,
            Thang,
            ROW_NUMBER() OVER (ORDER BY Nam, Thang) AS t_idx
        FROM Dim_ThoiGian
        WHERE Nam BETWEEN 2024 AND 2026
    )
    INSERT INTO Fact_TonKho (MaCH, MaMH, MaTG, SoLuongTrongKho)
    SELECT
        c.MaCH,
        p.MaMH,
        t.MaTG,
        80 + (ABS(CHECKSUM(c.MaCH, p.MaMH, t.MaTG)) % 520)
            + CASE WHEN t.Thang IN (1, 2) THEN 50 WHEN t.Thang IN (11, 12) THEN 30 ELSE 0 END AS SoLuongTrongKho
    FROM c
    CROSS JOIN p
    CROSS JOIN t
    WHERE ABS(CHECKSUM(c.MaCH, p.MaMH, t.MaTG, 'TK')) % 100 < 42;

    COMMIT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    THROW;
END CATCH;

/* Verification */
SELECT
    (SELECT COUNT(*) FROM Fact_BanHang) AS fact_banhang_rows,
    (SELECT COUNT(*) FROM Fact_TonKho) AS fact_tonkho_rows;

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
