SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRAN;

    /* Add Fact_BanHang rows for 2021-2023 only (idempotent) */
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
        WHERE Nam BETWEEN 2021 AND 2023
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
    ),
    base_insert AS (
        SELECT
            b.MaMH,
            b.BaseKH AS MaKH,
            b.MaTG,
            (ABS(CHECKSUM(b.MaMH, b.MaTG, 'B2021')) % 28) + 4
                + CASE WHEN b.Thang IN (11, 12) THEN 8 WHEN b.Thang = 1 THEN 5 ELSE 0 END AS SoLuongDat,
            CAST(
                (
                    (ABS(CHECKSUM(b.MaMH, b.MaTG, 'B2021')) % 28) + 4
                    + CASE WHEN b.Thang IN (11, 12) THEN 8 WHEN b.Thang = 1 THEN 5 ELSE 0 END
                ) * b.Gia * (1 + ((ABS(CHECKSUM(b.p_idx, b.t_idx)) % 13) - 6) / 100.0)
                AS DECIMAL(18, 2)
            ) AS TongTien
        FROM base b
    )
    INSERT INTO Fact_BanHang (MaMH, MaKH, MaTG, SoLuongDat, TongTien)
    SELECT bi.MaMH, bi.MaKH, bi.MaTG, bi.SoLuongDat, bi.TongTien
    FROM base_insert bi
    WHERE NOT EXISTS (
        SELECT 1
        FROM Fact_BanHang f
        WHERE f.MaMH = bi.MaMH
          AND f.MaKH = bi.MaKH
          AND f.MaTG = bi.MaTG
    );

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
        WHERE Nam BETWEEN 2021 AND 2023
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
    ),
    extra_insert AS (
        SELECT
            b.MaMH,
            k.MaKH,
            b.MaTG,
            (ABS(CHECKSUM(b.MaMH, k.MaKH, b.MaTG, 'E2021')) % 24) + 1
                + CASE WHEN b.Thang IN (11, 12) THEN 6 WHEN b.Thang = 1 THEN 4 ELSE 0 END AS SoLuongDat,
            CAST(
                (
                    (ABS(CHECKSUM(b.MaMH, k.MaKH, b.MaTG, 'E2021')) % 24) + 1
                    + CASE WHEN b.Thang IN (11, 12) THEN 6 WHEN b.Thang = 1 THEN 4 ELSE 0 END
                ) * b.Gia * (1 + ((ABS(CHECKSUM(k.k_idx, b.p_idx, b.t_idx)) % 21) - 10) / 100.0)
                AS DECIMAL(18, 2)
            ) AS TongTien
        FROM base b
        JOIN k
          ON ABS(CHECKSUM(b.MaMH, b.MaTG, k.MaKH)) % 100 < 8
         AND k.MaKH <> b.BaseKH
    )
    INSERT INTO Fact_BanHang (MaMH, MaKH, MaTG, SoLuongDat, TongTien)
    SELECT ei.MaMH, ei.MaKH, ei.MaTG, ei.SoLuongDat, ei.TongTien
    FROM extra_insert ei
    WHERE NOT EXISTS (
        SELECT 1
        FROM Fact_BanHang f
        WHERE f.MaMH = ei.MaMH
          AND f.MaKH = ei.MaKH
          AND f.MaTG = ei.MaTG
    );

    /* Add Fact_TonKho rows for 2021-2023 only (idempotent) */
    ;WITH c AS (
        SELECT TOP (40)
            MaCH
        FROM Dim_CuaHang
        ORDER BY MaCH
    ),
    p AS (
        SELECT TOP (300)
            MaMH
        FROM Dim_MatHang
        ORDER BY MaMH
    ),
    t AS (
        SELECT
            MaTG,
            Thang
        FROM Dim_ThoiGian
        WHERE Nam BETWEEN 2021 AND 2023
    ),
    tonkho_insert AS (
        SELECT
            c.MaCH,
            p.MaMH,
            t.MaTG,
            80 + (ABS(CHECKSUM(c.MaCH, p.MaMH, t.MaTG, 'TK2021')) % 520)
                + CASE WHEN t.Thang IN (1, 2) THEN 50 WHEN t.Thang IN (11, 12) THEN 30 ELSE 0 END AS SoLuongTrongKho
        FROM c
        CROSS JOIN p
        CROSS JOIN t
        WHERE ABS(CHECKSUM(c.MaCH, p.MaMH, t.MaTG, 'TK_FILTER_2021')) % 100 < 42
    )
    INSERT INTO Fact_TonKho (MaCH, MaMH, MaTG, SoLuongTrongKho)
    SELECT ti.MaCH, ti.MaMH, ti.MaTG, ti.SoLuongTrongKho
    FROM tonkho_insert ti
    WHERE NOT EXISTS (
        SELECT 1
        FROM Fact_TonKho f
        WHERE f.MaCH = ti.MaCH
          AND f.MaMH = ti.MaMH
          AND f.MaTG = ti.MaTG
    );

    COMMIT;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    THROW;
END CATCH;

/* Quick verification */
SELECT tg.Nam, COUNT(*) AS BanHangRows
FROM Fact_BanHang fb
JOIN Dim_ThoiGian tg ON tg.MaTG = fb.MaTG
GROUP BY tg.Nam
ORDER BY tg.Nam;

SELECT tg.Nam, COUNT(*) AS TonKhoRows
FROM Fact_TonKho tk
JOIN Dim_ThoiGian tg ON tg.MaTG = tk.MaTG
GROUP BY tg.Nam
ORDER BY tg.Nam;
