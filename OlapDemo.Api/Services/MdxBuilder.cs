using OlapDemo.Api.Models;

namespace OlapDemo.Api.Services;

/// <summary>
/// Xây MDX query cho 4 phép toán OLAP.
/// Whitelist dimension/measure để tránh MDX injection.
/// </summary>
public static class MdxBuilder
{
    private static readonly HashSet<string> AllowedLevels = new(StringComparer.OrdinalIgnoreCase)
        { "Nam", "Quy", "Thang" };

    private sealed record CubeContext(
        bool HasTime,
        bool HasProduct,
        bool HasCustomer,
        bool HasStore,
        string PivotDimensionMdx);

    private static string ValidLevel(string l) =>
        AllowedLevels.Contains(l) ? l : throw new ArgumentException($"Level không hợp lệ: {l}");

    private static string ValidMeasure(string? measure) =>
        string.IsNullOrWhiteSpace(measure) ? "Tong Tien" : measure.Trim();

    private static string? QuantityMeasureForCube(string? cube)
    {
        if (string.IsNullOrWhiteSpace(cube)) return "So Luong Dat";
        if (cube.Contains("TonKho", StringComparison.OrdinalIgnoreCase)) return "So Luong Trong Kho";
        if (cube.Contains("BanHang", StringComparison.OrdinalIgnoreCase) || cube.Contains("FactBanHang", StringComparison.OrdinalIgnoreCase))
            return "So Luong Dat";
        return null;
    }

    private static CubeContext BuildCubeContext(string cube)
    {
        var rule = CubeNameRules.Parse(cube);
        bool hasProduct = rule.HasProduct;
        bool hasCustomer = rule.HasCustomer;
        bool hasStore = rule.HasStore;
        bool hasTime = rule.HasTime;

        string pivotDimensionMdx = hasProduct
            ? "[Dim Mat Hang].[Ma MH].Members"
            : hasCustomer
                ? "[Dim Khach Hang].[Ma KH].Members"
                : hasStore
                    ? "[Dim Cua Hang].[Ma CH].Members"
                    : "[Dim Thoi Gian].[Hierarchy].[Nam].Members";

        return new CubeContext(hasTime, hasProduct, hasCustomer, hasStore, pivotDimensionMdx);
    }

    private static string MeasureSet(OlapRequest req, string cube)
    {
        string selectedMeasure = ValidMeasure(req.Measure);
        var parts = new List<string> { $"[Measures].[{selectedMeasure}]" };

        string? quantityMeasure = QuantityMeasureForCube(cube);
        if (req.IncludeSoLuong
            && !string.IsNullOrWhiteSpace(quantityMeasure)
            && !selectedMeasure.Equals(quantityMeasure, StringComparison.OrdinalIgnoreCase))
        {
            parts.Add($"[Measures].[{quantityMeasure}]");
        }

        return "{ " + string.Join(", ", parts) + " }";
    }

    public static (string mdx, string newLevel) DrillDown(OlapRequest req, string cube)
    {
        var ctx = BuildCubeContext(cube);
        if (!ctx.HasTime)
            throw new InvalidOperationException($"Cube [{cube}] không hỗ trợ hierarchy thời gian để drill down.");

        string cur = ValidLevel(req.RowLevel);
        string next = cur switch
        {
            "Nam" => "Quy",
            "Quy" => "Thang",
            _ => throw new InvalidOperationException("Đã ở mức thấp nhất (Thang)")
        };

        string mdx;
        if (cur == "Nam" && req.Year.HasValue)
        {
            mdx = $"SELECT {MeasureSet(req, cube)} ON COLUMNS,\n  NON EMPTY DESCENDANTS(\n    [Dim Thoi Gian].[Hierarchy].[Nam].&[{req.Year}],\n    [Dim Thoi Gian].[Hierarchy].[Quy]\n  ) ON ROWS\nFROM [{cube}]\n{BuildWhereClause(req, cube, skipTime: true)}".Trim();
        }
        else if (cur == "Quy" && req.Year.HasValue && req.Quarter.HasValue)
        {
            mdx = $"SELECT {MeasureSet(req, cube)} ON COLUMNS,\n  NON EMPTY DESCENDANTS(\n    [Dim Thoi Gian].[Hierarchy].[Quy].&[{req.Year}]&[{req.Quarter}],\n    [Dim Thoi Gian].[Hierarchy].[Thang]\n  ) ON ROWS\nFROM [{cube}]\n{BuildWhereClause(req, cube, skipTime: true)}".Trim();
        }
        else if (cur == "Quy" && req.Year.HasValue)
        {
            // Fallback an toàn: nếu chưa chọn Quý, vẫn giới hạn theo Năm rồi trả toàn bộ Tháng của năm đó.
            mdx = $"SELECT {MeasureSet(req, cube)} ON COLUMNS,\n  NON EMPTY DESCENDANTS(\n    [Dim Thoi Gian].[Hierarchy].[Nam].&[{req.Year}],\n    [Dim Thoi Gian].[Hierarchy].[Thang]\n  ) ON ROWS\nFROM [{cube}]\n{BuildWhereClause(req, cube, skipTime: true)}".Trim();
        }
        else
        {
            mdx = $"SELECT {MeasureSet(req, cube)} ON COLUMNS,\n  NON EMPTY [Dim Thoi Gian].[Hierarchy].[{next}].Members ON ROWS\nFROM [{cube}]\n{BuildWhereClause(req, cube, skipTime: true)}".Trim();
        }

        return (mdx, next);
    }

    public static (string mdx, string newLevel) RollUp(OlapRequest req, string cube)
    {
        var ctx = BuildCubeContext(cube);
        if (!ctx.HasTime)
            throw new InvalidOperationException($"Cube [{cube}] không hỗ trợ hierarchy thời gian để roll up.");

        string cur = ValidLevel(req.RowLevel);
        string parent = cur switch
        {
            "Thang" => "Quy",
            "Quy" => "Nam",
            _ => throw new InvalidOperationException("Đã ở mức cao nhất (Nam)")
        };

        string where = BuildWhereClause(req, cube, skipTime: true);
        string mdx;

        if (parent == "Nam")
        {
            mdx = $"SELECT {MeasureSet(req, cube)} ON COLUMNS,\n  NON EMPTY [Dim Thoi Gian].[Hierarchy].[Nam].Members ON ROWS\nFROM [{cube}]\n{where}".Trim();
        }
        else
        {
            string timeFilter = req.Year.HasValue
                ? $"DESCENDANTS([Dim Thoi Gian].[Hierarchy].[Nam].&[{req.Year}], [Dim Thoi Gian].[Hierarchy].[{parent}])"
                : $"[Dim Thoi Gian].[Hierarchy].[{parent}].Members";

            mdx = $"SELECT {MeasureSet(req, cube)} ON COLUMNS,\n  NON EMPTY {timeFilter} ON ROWS\nFROM [{cube}]\n{where}".Trim();
        }

        return (mdx, parent);
    }

    public static string Slice(OlapRequest req, string cube)
    {
        var ctx = BuildCubeContext(cube);
        if (!ctx.HasTime)
            throw new InvalidOperationException($"Cube [{cube}] không hỗ trợ chiều thời gian để slice.");

        if (!req.Year.HasValue) throw new ArgumentException("Slice cần Year");
        string level = ValidLevel(req.RowLevel);
        string where = BuildDimensionWhere(req, ctx);
        return $"SELECT {MeasureSet(req, cube)} ON COLUMNS,\n  NON EMPTY DESCENDANTS(\n    [Dim Thoi Gian].[Hierarchy].[Nam].&[{req.Year}],\n    [Dim Thoi Gian].[Hierarchy].[{level}]\n  ) ON ROWS\nFROM [{cube}]\n{where}".Trim();
    }

    public static string Dice(OlapRequest req, string cube)
    {
        var ctx = BuildCubeContext(cube);
        if (!ctx.HasTime)
            throw new InvalidOperationException($"Cube [{cube}] không hỗ trợ chiều thời gian để dice.");

        string level = ValidLevel(req.RowLevel);
        var whereParts = BuildWhereParts(req, ctx, skipTime: true);

        string whereClause = whereParts.Count > 0
            ? "WHERE (\n  " + string.Join(",\n  ", whereParts) + "\n)"
            : "";

        if (req.Year.HasValue)
        {
            return $"SELECT {MeasureSet(req, cube)} ON COLUMNS,\n  NON EMPTY [Dim Thoi Gian].[Hierarchy].[{level}].Members ON ROWS\nFROM (\n  SELECT ( [Dim Thoi Gian].[Hierarchy].[Nam].&[{req.Year}] ) ON 0\n  FROM [{cube}]\n)\n{whereClause}".Trim();
        }
        return $"SELECT {MeasureSet(req, cube)} ON COLUMNS,\n  NON EMPTY [Dim Thoi Gian].[Hierarchy].[{level}].Members ON ROWS\nFROM [{cube}]\n{whereClause}".Trim();
    }

    public static string Pivot(OlapRequest req, string cube)
    {
        var ctx = BuildCubeContext(cube);
        if (!ctx.HasTime)
            throw new InvalidOperationException($"Cube [{cube}] không hỗ trợ chiều thời gian để pivot.");

        string level = ValidLevel(req.RowLevel);
        string sideDimension = ctx.PivotDimensionMdx;

        if (req.Year.HasValue)
        {
            if (req.ColLevel is "Ma MH" or "Ma KH" or "Ma CH")
                return $"SELECT\n  NON EMPTY {sideDimension} ON COLUMNS,\n  NON EMPTY DESCENDANTS(\n    [Dim Thoi Gian].[Hierarchy].[Nam].&[{req.Year}],\n    [Dim Thoi Gian].[Hierarchy].[{level}]\n  ) ON ROWS\nFROM [{cube}]";
            else
                return $"SELECT\n  NON EMPTY DESCENDANTS(\n    [Dim Thoi Gian].[Hierarchy].[Nam].&[{req.Year}],\n    [Dim Thoi Gian].[Hierarchy].[{level}]\n  ) ON COLUMNS,\n  NON EMPTY {sideDimension} ON ROWS\nFROM [{cube}]";
        }
        if (req.ColLevel is "Ma MH" or "Ma KH" or "Ma CH")
            return $"SELECT\n  NON EMPTY {sideDimension} ON COLUMNS,\n  NON EMPTY [Dim Thoi Gian].[Hierarchy].[{level}].Members ON ROWS\nFROM [{cube}]";
        return $"SELECT\n  NON EMPTY [Dim Thoi Gian].[Hierarchy].[{level}].Members ON COLUMNS,\n  NON EMPTY {sideDimension} ON ROWS\nFROM [{cube}]";
    }

    private static string BuildWhereClause(OlapRequest req, string cube, bool skipTime = false)
    {
        var parts = BuildWhereParts(req, BuildCubeContext(cube), skipTime);
        return parts.Count > 0 ? "WHERE (\n  " + string.Join(",\n  ", parts) + "\n)" : "";
    }

    private static string BuildDimensionWhere(OlapRequest req, CubeContext ctx)
    {
        var parts = BuildWhereParts(req, ctx, skipTime: true);
        return parts.Count > 0 ? "WHERE (\n  " + string.Join(",\n  ", parts) + "\n)" : "";
    }

    private static List<string> BuildWhereParts(OlapRequest req, CubeContext ctx, bool skipTime)
    {
        var parts = new List<string>();
        if (!skipTime && ctx.HasTime && req.Year.HasValue)
            parts.Add($"[Dim Thoi Gian].[Hierarchy].[Nam].&[{req.Year}]");
        if (ctx.HasProduct && !string.IsNullOrWhiteSpace(req.ProductKey))
            parts.Add($"[Dim Mat Hang].[Ma MH].&[{req.ProductKey}]");
        if (ctx.HasCustomer && !string.IsNullOrWhiteSpace(req.CustomerKey))
            parts.Add($"[Dim Khach Hang].[Ma KH].&[{req.CustomerKey}]");
        if (ctx.HasStore && !string.IsNullOrWhiteSpace(req.StoreKey))
            parts.Add($"[Dim Cua Hang].[Ma CH].&[{req.StoreKey}]");
        return parts;
    }
}
