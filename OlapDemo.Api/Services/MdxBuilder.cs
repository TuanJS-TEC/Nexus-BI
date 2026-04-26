using OlapDemo.Api.Models;

namespace OlapDemo.Api.Services;

/// <summary>
/// Xây MDX query cho 4 phép toán OLAP.
/// Whitelist dimension/measure để tránh MDX injection.
/// </summary>
public static class MdxBuilder
{
    private static readonly Dictionary<string, string[]> HierarchyByDimension = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ThoiGian"] = ["Nam", "Quy", "Thang"],
        ["KhachHang"] = ["LoaiKH", "TenKH"],
        ["DiaDiem"] = ["Bang", "TenTP"]
    };

    private sealed record CubeContext(
        bool HasTime,
        bool HasProduct,
        bool HasCustomer,
        bool HasStore,
        string PivotDimensionMdx);

    private static string ValidDimension(string? d)
    {
        string value = string.IsNullOrWhiteSpace(d) ? "ThoiGian" : d.Trim();
        return HierarchyByDimension.ContainsKey(value)
            ? value
            : throw new ArgumentException($"Dimension không hợp lệ: {d}");
    }

    private static string ValidLevel(string dimension, string l)
    {
        string normalized = NormalizeLevelAlias(dimension, l);
        var allowed = HierarchyByDimension[dimension];
        return allowed.Contains(normalized, StringComparer.OrdinalIgnoreCase)
            ? normalized
            : throw new ArgumentException($"Level không hợp lệ cho {dimension}: {l}");
    }

    private static string ValidMeasure(string? measure) =>
        string.IsNullOrWhiteSpace(measure) ? "Tong Tien" : measure.Trim();

    private static int ValidYear(int? year) =>
        year is >= 2000 and <= 2100
            ? year.Value
            : throw new ArgumentException($"Year không hợp lệ: {year}");

    private static int ValidQuarter(int? quarter) =>
        quarter is >= 1 and <= 4
            ? quarter.Value
            : throw new ArgumentException($"Quarter không hợp lệ: {quarter}");

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
        string dimension = ValidDimension(req.ActiveDimension);
        string cur = ValidLevel(dimension, req.RowLevel);
        string next = NextLevel(dimension, cur);

        string mdx = dimension switch
        {
            "ThoiGian" => BuildTimeDrillDownMdx(req, cube, cur, next, ctx),
            "KhachHang" => BuildCustomerDrillDownMdx(req, cube, cur, next, ctx),
            "DiaDiem" => BuildLocationDrillDownMdx(req, cube, cur, next, ctx),
            _ => throw new InvalidOperationException($"Không hỗ trợ drill down cho chiều {dimension}.")
        };

        return (mdx, next);
    }

    public static (string mdx, string newLevel) RollUp(OlapRequest req, string cube)
    {
        var ctx = BuildCubeContext(cube);
        string dimension = ValidDimension(req.ActiveDimension);
        string cur = ValidLevel(dimension, req.RowLevel);
        string parent = ParentLevel(dimension, cur);

        string mdx = dimension switch
        {
            "ThoiGian" => BuildTimeRollUpMdx(req, cube, parent, ctx),
            "KhachHang" => BuildCustomerRollUpMdx(req, cube, parent, ctx),
            "DiaDiem" => BuildLocationRollUpMdx(req, cube, parent, ctx),
            _ => throw new InvalidOperationException($"Không hỗ trợ roll up cho chiều {dimension}.")
        };

        return (mdx, parent);
    }

    public static string QueryCurrent(OlapRequest req, string cube)
    {
        var ctx = BuildCubeContext(cube);
        var queryDimensions = ResolveQueryDimensions(req, ctx);
        string rowSet = BuildRowSetForQuery(req, queryDimensions);

        bool skipTime = queryDimensions.Contains("ThoiGian", StringComparer.OrdinalIgnoreCase);
        bool skipCustomerHierarchy = queryDimensions.Contains("KhachHang", StringComparer.OrdinalIgnoreCase);
        bool skipLocationHierarchy = queryDimensions.Contains("DiaDiem", StringComparer.OrdinalIgnoreCase);

        string where = BuildWhereClause(req, cube, skipTime, skipCustomerHierarchy, skipLocationHierarchy);
        return $"SELECT {MeasureSet(req, cube)} ON COLUMNS,\n  NON EMPTY {rowSet} ON ROWS\nFROM [{cube}]\n{where}".Trim();
    }

    private static List<string> ResolveQueryDimensions(OlapRequest req, CubeContext ctx)
    {
        var requested = req.ActiveDimensions?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToList()
            ?? [];

        if (!requested.Any())
            requested.Add(ValidDimension(req.ActiveDimension));

        var filtered = new List<string>();
        foreach (var dim in requested)
        {
            if (dim.Equals("ThoiGian", StringComparison.OrdinalIgnoreCase) && ctx.HasTime) filtered.Add("ThoiGian");
            else if (dim.Equals("KhachHang", StringComparison.OrdinalIgnoreCase) && ctx.HasCustomer) filtered.Add("KhachHang");
            else if (dim.Equals("DiaDiem", StringComparison.OrdinalIgnoreCase) && ctx.HasStore) filtered.Add("DiaDiem");
            else if (dim.Equals("MatHang", StringComparison.OrdinalIgnoreCase) && ctx.HasProduct) filtered.Add("MatHang");
        }

        if (!filtered.Any())
            filtered.Add(ValidDimension(req.ActiveDimension));

        return filtered.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static string Slice(OlapRequest req, string cube)
    {
        var ctx = BuildCubeContext(cube);
        if (!ctx.HasTime)
            throw new InvalidOperationException($"Cube [{cube}] không hỗ trợ chiều thời gian để slice.");

        if (!req.Year.HasValue) throw new ArgumentException("Slice cần Year");
        string level = ValidLevel("ThoiGian", req.RowLevel);
        string where = BuildDimensionWhere(req, ctx);
        return $"SELECT {MeasureSet(req, cube)} ON COLUMNS,\n  NON EMPTY DESCENDANTS(\n    [Dim Thoi Gian].[Hierarchy].[Nam].&[{req.Year}],\n    [Dim Thoi Gian].[Hierarchy].[{level}]\n  ) ON ROWS\nFROM [{cube}]\n{where}".Trim();
    }

    public static string Dice(OlapRequest req, string cube)
    {
        var ctx = BuildCubeContext(cube);
        if (!ctx.HasTime)
            throw new InvalidOperationException($"Cube [{cube}] không hỗ trợ chiều thời gian để dice.");

        string level = ValidLevel("ThoiGian", req.RowLevel);
        var whereParts = BuildWhereParts(req, ctx, skipTime: true, skipCustomerHierarchy: false, skipLocationHierarchy: false);

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

        string level = ValidLevel("ThoiGian", req.RowLevel);
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

    private static string BuildWhereClause(
        OlapRequest req,
        string cube,
        bool skipTime = false,
        bool skipCustomerHierarchy = false,
        bool skipLocationHierarchy = false)
    {
        var parts = BuildWhereParts(req, BuildCubeContext(cube), skipTime, skipCustomerHierarchy, skipLocationHierarchy);
        return parts.Count > 0 ? "WHERE (\n  " + string.Join(",\n  ", parts) + "\n)" : "";
    }

    private static string BuildDimensionWhere(OlapRequest req, CubeContext ctx)
    {
        var parts = BuildWhereParts(req, ctx, skipTime: true, skipCustomerHierarchy: false, skipLocationHierarchy: false);
        return parts.Count > 0 ? "WHERE (\n  " + string.Join(",\n  ", parts) + "\n)" : "";
    }

    private static List<string> BuildWhereParts(
        OlapRequest req,
        CubeContext ctx,
        bool skipTime,
        bool skipCustomerHierarchy,
        bool skipLocationHierarchy)
    {
        var parts = new List<string>();
        if (!skipTime && ctx.HasTime && req.Year.HasValue)
            parts.Add($"[Dim Thoi Gian].[Hierarchy].[Nam].&[{req.Year}]");
        if (!skipTime && ctx.HasTime && req.Year.HasValue && req.Quarter.HasValue)
            parts.Add($"[Dim Thoi Gian].[Hierarchy].[Quy].&[{req.Year}]&[{req.Quarter}]");
        if (!skipTime && ctx.HasTime && req.Year.HasValue && req.Quarter.HasValue && req.Month.HasValue)
            parts.Add($"[Dim Thoi Gian].[Hierarchy].[Thang].&[{req.Year}]&[{req.Quarter}]&[{req.Month}]");
        if (ctx.HasProduct && !string.IsNullOrWhiteSpace(req.ProductKey))
            parts.Add($"[Dim Mat Hang].[Ma MH].&[{req.ProductKey}]");
        if (!skipCustomerHierarchy && ctx.HasCustomer && !string.IsNullOrWhiteSpace(req.CustomerKey))
            parts.Add($"[Dim Khach Hang].[Ma KH].&[{req.CustomerKey}]");
        if (!skipCustomerHierarchy && ctx.HasCustomer && !string.IsNullOrWhiteSpace(req.CustomerType))
            parts.Add($"[Dim Khach Hang].[Hierarchy].[Loai KH].&[{req.CustomerType}]");
        if (!skipCustomerHierarchy && ctx.HasCustomer && !string.IsNullOrWhiteSpace(req.CustomerName))
            parts.Add($"[Dim Khach Hang].[Hierarchy].[Ten KH].&[{req.CustomerName}]");
        if (ctx.HasStore && !string.IsNullOrWhiteSpace(req.StoreKey))
            parts.Add($"[Dim Cua Hang].[Ma CH].&[{req.StoreKey}]");
        if (!skipLocationHierarchy && ctx.HasStore && !string.IsNullOrWhiteSpace(req.State))
            parts.Add($"[Dim Cua Hang].[Hierarchy].[Bang].&[{req.State}]");
        if (!skipLocationHierarchy && ctx.HasStore && !string.IsNullOrWhiteSpace(req.City))
            parts.Add($"[Dim Cua Hang].[Hierarchy].[Ten TP].&[{req.City}]");
        return parts;
    }

    private static string NextLevel(string dimension, string currentLevel)
    {
        var levels = HierarchyByDimension[dimension];
        int idx = Array.FindIndex(levels, l => l.Equals(currentLevel, StringComparison.OrdinalIgnoreCase));
        if (idx < 0 || idx + 1 >= levels.Length)
            throw new InvalidOperationException($"Đã ở mức thấp nhất ({currentLevel}).");
        return levels[idx + 1];
    }

    private static string ParentLevel(string dimension, string currentLevel)
    {
        var levels = HierarchyByDimension[dimension];
        int idx = Array.FindIndex(levels, l => l.Equals(currentLevel, StringComparison.OrdinalIgnoreCase));
        if (idx <= 0)
            throw new InvalidOperationException($"Đã ở mức cao nhất ({currentLevel}).");
        return levels[idx - 1];
    }

    private static string BuildTimeDrillDownMdx(OlapRequest req, string cube, string currentLevel, string nextLevel, CubeContext ctx)
    {
        if (!ctx.HasTime)
            throw new InvalidOperationException($"Cube [{cube}] không hỗ trợ hierarchy thời gian để drill down.");
        string nonTimeWhere = BuildNonTimeWhereClause(req, cube);
        if (currentLevel == "Nam" && req.Year.HasValue)
        {
            int year = ValidYear(req.Year);
            return $"SELECT {MeasureSet(req, cube)} ON COLUMNS,\n  NON EMPTY DESCENDANTS(\n    [Dim Thoi Gian].[Hierarchy].[Nam].&[{year}],\n    [Dim Thoi Gian].[Hierarchy].[Quy]\n  ) ON ROWS\nFROM [{cube}]\n{nonTimeWhere}".Trim();
        }
        if (currentLevel == "Quy" && req.Year.HasValue && req.Quarter.HasValue)
        {
            int year = ValidYear(req.Year);
            int quarter = ValidQuarter(req.Quarter);
            return $"SELECT {MeasureSet(req, cube)} ON COLUMNS,\n  NON EMPTY DESCENDANTS(\n    [Dim Thoi Gian].[Hierarchy].[Quy].&[{year}]&[{quarter}],\n    [Dim Thoi Gian].[Hierarchy].[Thang]\n  ) ON ROWS\nFROM [{cube}]\n{nonTimeWhere}".Trim();
        }
        throw new ArgumentException($"DrillDown từ {currentLevel} cần chọn member hợp lệ trước.");
    }

    private static string BuildRowSetForQuery(OlapRequest req, List<string> dimensions)
    {
        var levelMap = req.DimensionLevels ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var sets = new List<string>();

        foreach (var dimension in dimensions)
        {
            string level = levelMap.TryGetValue(dimension, out var configured) && !string.IsNullOrWhiteSpace(configured)
                ? configured
                : (dimension.Equals(req.ActiveDimension, StringComparison.OrdinalIgnoreCase)
                    ? req.RowLevel
                    : DefaultLevelForDimension(dimension));

            sets.Add(BuildSingleDimensionSet(req, dimension, level));
        }

        if (sets.Count == 1) return sets[0];
        return string.Join(" * ", sets.Select(s => $"({s})"));
    }

    private static string BuildSingleDimensionSet(OlapRequest req, string dimension, string level)
    {
        if (dimension == "ThoiGian")
        {
            string normalized = ValidLevel("ThoiGian", level);
            if (normalized == "Thang" && req.Year.HasValue && req.Quarter.HasValue)
                return $"DESCENDANTS([Dim Thoi Gian].[Hierarchy].[Quy].&[{req.Year}]&[{req.Quarter}], [Dim Thoi Gian].[Hierarchy].[Thang])";
            if (normalized == "Quy" && req.Year.HasValue)
                return $"DESCENDANTS([Dim Thoi Gian].[Hierarchy].[Nam].&[{req.Year}], [Dim Thoi Gian].[Hierarchy].[Quy])";
            return $"[Dim Thoi Gian].[Hierarchy].[{normalized}].Members";
        }

        if (dimension == "KhachHang")
        {
            string normalized = ValidLevel("KhachHang", level);
            string mdxLevel = MapLevelToMdxLabel("KhachHang", normalized);
            if (normalized.Equals("TenKH", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(req.CustomerType))
                return $"DESCENDANTS([Dim Khach Hang].[Hierarchy].[Loai KH].&[{req.CustomerType}], [Dim Khach Hang].[Hierarchy].[{mdxLevel}])";
            return $"[Dim Khach Hang].[Hierarchy].[{mdxLevel}].Members";
        }

        if (dimension == "MatHang")
            return "[Dim Mat Hang].[Ma MH].Members";

        string normalizedLocation = ValidLevel("DiaDiem", level);
        string locationLevel = MapLevelToMdxLabel("DiaDiem", normalizedLocation);
        if (normalizedLocation.Equals("TenTP", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(req.State))
            return $"DESCENDANTS([Dim Cua Hang].[Hierarchy].[Bang].&[{req.State}], [Dim Cua Hang].[Hierarchy].[{locationLevel}])";
        return $"[Dim Cua Hang].[Hierarchy].[{locationLevel}].Members";
    }

    private static string DefaultLevelForDimension(string dimension)
    {
        if (dimension.Equals("ThoiGian", StringComparison.OrdinalIgnoreCase)) return "Nam";
        if (dimension.Equals("KhachHang", StringComparison.OrdinalIgnoreCase)) return "LoaiKH";
        if (dimension.Equals("DiaDiem", StringComparison.OrdinalIgnoreCase)) return "Bang";
        if (dimension.Equals("MatHang", StringComparison.OrdinalIgnoreCase)) return "MaMH";
        return "Nam";
    }

    private static string BuildTimeRollUpMdx(OlapRequest req, string cube, string parentLevel, CubeContext ctx)
    {
        if (!ctx.HasTime)
            throw new InvalidOperationException($"Cube [{cube}] không hỗ trợ hierarchy thời gian để roll up.");
        string where = BuildNonTimeWhereClause(req, cube);
        if (parentLevel == "Nam")
            return $"SELECT {MeasureSet(req, cube)} ON COLUMNS,\n  NON EMPTY [Dim Thoi Gian].[Hierarchy].[Nam].Members ON ROWS\nFROM [{cube}]\n{where}".Trim();
        string timeFilter = req.Year.HasValue
            ? $"DESCENDANTS([Dim Thoi Gian].[Hierarchy].[Nam].&[{req.Year}], [Dim Thoi Gian].[Hierarchy].[{parentLevel}])"
            : $"[Dim Thoi Gian].[Hierarchy].[{parentLevel}].Members";
        return $"SELECT {MeasureSet(req, cube)} ON COLUMNS,\n  NON EMPTY {timeFilter} ON ROWS\nFROM [{cube}]\n{where}".Trim();
    }

    private static string BuildNonTimeWhereClause(OlapRequest req, string cube)
    {
        var nonTimeReq = new OlapRequest
        {
            Cube = req.Cube,
            Measure = req.Measure,
            ActiveDimension = req.ActiveDimension,
            ProductKey = req.ProductKey,
            CustomerKey = req.CustomerKey,
            CustomerType = req.CustomerType,
            CustomerName = req.CustomerName,
            StoreKey = req.StoreKey,
            State = req.State,
            City = req.City,
            RowLevel = req.RowLevel,
            ColLevel = req.ColLevel,
            IncludeSoLuong = req.IncludeSoLuong,
            Year = null,
            Quarter = null,
            Month = null,
        };
        return BuildWhereClause(nonTimeReq, cube, skipTime: true);
    }

    private static string BuildCustomerDrillDownMdx(OlapRequest req, string cube, string currentLevel, string nextLevel, CubeContext ctx)
    {
        if (!ctx.HasCustomer)
            throw new InvalidOperationException($"Cube [{cube}] không hỗ trợ hierarchy khách hàng để drill down.");
        if (currentLevel == "LoaiKH" && !string.IsNullOrWhiteSpace(req.CustomerType))
            return $"SELECT {MeasureSet(req, cube)} ON COLUMNS,\n  NON EMPTY DESCENDANTS(\n    [Dim Khach Hang].[Hierarchy].[Loai KH].&[{req.CustomerType}],\n    [Dim Khach Hang].[Hierarchy].[{MapLevelToMdxLabel("KhachHang", nextLevel)}]\n  ) ON ROWS\nFROM [{cube}]\n{BuildWhereClause(req, cube, skipTime: false, skipCustomerHierarchy: true)}".Trim();
        throw new ArgumentException("DrillDown khách hàng yêu cầu chọn Loại KH trước.");
    }

    private static string BuildCustomerRollUpMdx(OlapRequest req, string cube, string parentLevel, CubeContext ctx)
    {
        if (!ctx.HasCustomer)
            throw new InvalidOperationException($"Cube [{cube}] không hỗ trợ hierarchy khách hàng để roll up.");
        return $"SELECT {MeasureSet(req, cube)} ON COLUMNS,\n  NON EMPTY [Dim Khach Hang].[Hierarchy].[{MapLevelToMdxLabel("KhachHang", parentLevel)}].Members ON ROWS\nFROM [{cube}]\n{BuildWhereClause(req, cube, skipTime: false, skipCustomerHierarchy: true)}".Trim();
    }

    private static string BuildLocationDrillDownMdx(OlapRequest req, string cube, string currentLevel, string nextLevel, CubeContext ctx)
    {
        if (!ctx.HasStore)
            throw new InvalidOperationException($"Cube [{cube}] không hỗ trợ hierarchy địa điểm để drill down.");
        if (currentLevel == "Bang" && !string.IsNullOrWhiteSpace(req.State))
            return $"SELECT {MeasureSet(req, cube)} ON COLUMNS,\n  NON EMPTY DESCENDANTS(\n    [Dim Cua Hang].[Hierarchy].[Bang].&[{req.State}],\n    [Dim Cua Hang].[Hierarchy].[{MapLevelToMdxLabel("DiaDiem", nextLevel)}]\n  ) ON ROWS\nFROM [{cube}]\n{BuildWhereClause(req, cube, skipTime: false)}".Trim();
        throw new ArgumentException("DrillDown địa điểm yêu cầu chọn Bang trước.");
    }

    private static string BuildLocationRollUpMdx(OlapRequest req, string cube, string parentLevel, CubeContext ctx)
    {
        if (!ctx.HasStore)
            throw new InvalidOperationException($"Cube [{cube}] không hỗ trợ hierarchy địa điểm để roll up.");
        return $"SELECT {MeasureSet(req, cube)} ON COLUMNS,\n  NON EMPTY [Dim Cua Hang].[Hierarchy].[{MapLevelToMdxLabel("DiaDiem", parentLevel)}].Members ON ROWS\nFROM [{cube}]\n{BuildWhereClause(req, cube, skipTime: false)}".Trim();
    }

    private static string NormalizeLevelAlias(string dimension, string level)
    {
        string value = string.IsNullOrWhiteSpace(level) ? string.Empty : level.Trim();
        if (dimension.Equals("KhachHang", StringComparison.OrdinalIgnoreCase))
        {
            if (value.Equals("MaKH", StringComparison.OrdinalIgnoreCase)) return "TenKH";
            if (value.Equals("Ma KH", StringComparison.OrdinalIgnoreCase)) return "TenKH";
            if (value.Equals("Ten KH", StringComparison.OrdinalIgnoreCase)) return "TenKH";
            if (value.Equals("Loai KH", StringComparison.OrdinalIgnoreCase)) return "LoaiKH";
        }
        if (dimension.Equals("DiaDiem", StringComparison.OrdinalIgnoreCase))
        {
            if (value.Equals("ThanhPho", StringComparison.OrdinalIgnoreCase)) return "TenTP";
            if (value.Equals("Thanh Pho", StringComparison.OrdinalIgnoreCase)) return "TenTP";
            if (value.Equals("Ten TP", StringComparison.OrdinalIgnoreCase)) return "TenTP";
        }
        return value;
    }

    private static string MapLevelToMdxLabel(string dimension, string level)
    {
        string normalized = NormalizeLevelAlias(dimension, level);
        if (dimension.Equals("KhachHang", StringComparison.OrdinalIgnoreCase))
        {
            if (normalized.Equals("LoaiKH", StringComparison.OrdinalIgnoreCase)) return "Loai KH";
            if (normalized.Equals("TenKH", StringComparison.OrdinalIgnoreCase)) return "Ten KH";
        }
        if (dimension.Equals("DiaDiem", StringComparison.OrdinalIgnoreCase))
        {
            if (normalized.Equals("Bang", StringComparison.OrdinalIgnoreCase)) return "Bang";
            if (normalized.Equals("TenTP", StringComparison.OrdinalIgnoreCase)) return "Ten TP";
        }
        return normalized;
    }
}
