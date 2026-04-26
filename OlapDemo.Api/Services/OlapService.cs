using Microsoft.AnalysisServices.AdomdClient;
using OlapDemo.Api.Models;
using System.Text.RegularExpressions;

namespace OlapDemo.Api.Services;

/// <summary>
/// Kết nối SSAS qua Microsoft.AnalysisServices.AdomdClient (.NET Standard 19.113.7).
/// Dùng AdomdDataReader với query DRILLTHROUGH hoặc MDX SELECT FLAT.
/// </summary>
public class OlapService
{
    private readonly string _connectionString;
    private readonly ILogger<OlapService> _logger;

    public OlapService(IConfiguration config, ILogger<OlapService> logger)
    {
        _connectionString = config["Ssas:ConnectionString"]
            ?? "Data Source=DESKTOP-22UAJFI;Catalog=Datawarehouse_01;";
        _logger = logger;
    }

    // ─── Execute MDX dạng SELECT ... ON COLUMNS / ROWS ───────────────────────
    // AdomdDataReader yêu cầu MDX dạng FLAT hoặc dùng ExecuteReader với SELECT thông thường
    public async Task<OlapResult> ExecuteAsync(string mdx, string currentLevel = "Nam", string opType = "")
    {
        return await Task.Run(() =>
        {
            var result = new OlapResult
            {
                Mdx = mdx,
                CurrentLevel = currentLevel,
                OperationType = opType
            };

            try
            {
                using var conn = new AdomdConnection(_connectionString);
                conn.Open();
                string effectiveMdx = RewriteMdxWithDiscoveredSchema(conn, mdx);
                result.Mdx = effectiveMdx;
                using var cmd = new AdomdCommand(effectiveMdx, conn);

                // AdomdDataReader hoạt động với SELECT multidim nhưng trả về flat table
                using var reader = cmd.ExecuteReader();

                int fieldCount = reader.FieldCount;
                if (fieldCount == 0)
                {
                    result.Success = false;
                    result.Error = "Không có cột dữ liệu";
                    return result;
                }

                // Build columns
                var columns = new List<string>();
                for (int i = 0; i < fieldCount; i++)
                    columns.Add(reader.GetName(i));
                result.Columns = columns;

                // Build rows
                while (reader.Read())
                {
                    var row = new Dictionary<string, object?>();
                    for (int i = 0; i < fieldCount; i++)
                        row[columns[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    result.Rows.Add(row);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi AdomdClient Execute: {Mdx}", result.Mdx ?? mdx);
                result.Success = false;
                result.Error = ex.Message;
            }

            return result;
        });
    }

    // ─── Metadata ─────────────────────────────────────────────────────────────
    public async Task<MetadataResult> GetMetadataAsync(string cube)
    {
        var meta = new MetadataResult
        {
            Cubes = []
        };

        try
        {
            using var conn = new AdomdConnection(_connectionString);
            conn.Open();
            _logger.LogInformation("Đã kết nối SSAS thành công");

            meta.Cubes = await GetCubesViaSchemaAsync(conn);
            if (meta.Cubes.Count == 0)
                meta.Cubes = GetFallbackCubeNames();
            meta.CubeInfos = meta.Cubes.Select(BuildCubeInfo).ToList();

            // Dùng AdomdSchemaDataSet để lấy members
            // Hoặc dùng MDX với AdomdDataReader

            var hierarchies = await DiscoverHierarchyMapAsync(conn, cube);

            if (!string.IsNullOrWhiteSpace(hierarchies.TimeHierarchyUniqueName))
                meta.Years = await GetMembersByHierarchyAsync(conn, cube, hierarchies.TimeHierarchyUniqueName!, "Nam");

            if (!string.IsNullOrWhiteSpace(hierarchies.ProductHierarchyUniqueName))
                meta.Products = await GetMembersByHierarchyAsync(conn, cube, hierarchies.ProductHierarchyUniqueName!, "Ma MH");

            if (!string.IsNullOrWhiteSpace(hierarchies.CustomerHierarchyUniqueName))
                meta.Customers = await GetMembersByHierarchyAsync(conn, cube, hierarchies.CustomerHierarchyUniqueName!, "Ma KH");

            if (!string.IsNullOrWhiteSpace(hierarchies.StoreHierarchyUniqueName))
                meta.Stores = await GetMembersByHierarchyAsync(conn, cube, hierarchies.StoreHierarchyUniqueName!, "Ma CH");

            // Measures
            meta.Measures = await GetMeasuresViaSchemaAsync(conn, cube);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể lấy metadata, dùng data mẫu");
            meta.Cubes = GetFallbackCubeNames();
            meta.CubeInfos = meta.Cubes.Select(BuildCubeInfo).ToList();
            meta.Years = Enumerable.Range(2022, 4)
                .Select(y => new MemberInfo { Key = y.ToString(), Name = y.ToString() })
                .ToList();
            meta.Measures = GetFallbackMeasuresForCube(cube);
        }

        return meta;
    }

    private static Task<List<MemberInfo>> GetMembersViaSchemaAsync(
        AdomdConnection conn, string cube, string dimensionName, string hierarchyName)
    {
        return Task.Run(() =>
        {
            var list = new List<MemberInfo>();
            try
            {
                // Strategy 1: restrictions chặt theo hierarchy.
                var strictRestrictions = new AdomdRestrictionCollection
                {
                    new AdomdRestriction("CATALOG_NAME", conn.Database),
                    new AdomdRestriction("CUBE_NAME", cube),
                    new AdomdRestriction("DIMENSION_UNIQUE_NAME", $"[{dimensionName}]"),
                    new AdomdRestriction("HIERARCHY_UNIQUE_NAME", $"[{dimensionName}].[{hierarchyName}]"),
                };
                var strictDs = conn.GetSchemaDataSet("MDSCHEMA_MEMBERS", strictRestrictions);
                AppendMembersFromSchemaRows(list, strictDs, hierarchyName);

                // Strategy 2: nới lỏng nếu strategy 1 không có dữ liệu (hữu ích khi unique-name khác giữa cube versions).
                if (list.Count == 0)
                {
                    var relaxedRestrictions = new AdomdRestrictionCollection
                    {
                        new AdomdRestriction("CATALOG_NAME", conn.Database),
                        new AdomdRestriction("CUBE_NAME", cube),
                        new AdomdRestriction("DIMENSION_UNIQUE_NAME", $"[{dimensionName}]"),
                    };
                    var relaxedDs = conn.GetSchemaDataSet("MDSCHEMA_MEMBERS", relaxedRestrictions);
                    AppendMembersFromSchemaRows(list, relaxedDs, expectedLevelName: string.Empty);
                }

                // Strategy 3: fallback MDX CellSet để lấy member captions trực tiếp từ cube.
                if (list.Count == 0)
                {
                    list.AddRange(GetMembersViaMdx(conn, cube, dimensionName, hierarchyName));
                }
            }
            catch (Exception)
            {
                // ignore per-hierarchy errors, caller handles fallback
            }
            list = NormalizeMembersByHierarchy(list, hierarchyName);
            return list
                .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        });
    }

    private static List<MemberInfo> GetMembersViaMdx(
        AdomdConnection conn,
        string cube,
        string dimensionName,
        string hierarchyName)
    {
        var result = new List<MemberInfo>();
        string setExpr = GetMemberSetExpression(dimensionName, hierarchyName);
        if (string.IsNullOrWhiteSpace(setExpr)) return result;

        try
        {
            string mdx = $@"
WITH MEMBER [Measures].[__MetaDummy] AS 1
SELECT
  {{ [Measures].[__MetaDummy] }} ON COLUMNS,
  NON EMPTY {setExpr} ON ROWS
FROM [{cube}]";

            using var cmd = new AdomdCommand(mdx, conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string candidate = "";
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    if (reader.IsDBNull(i)) continue;
                    object value = reader.GetValue(i);
                    if (value is string s && !string.IsNullOrWhiteSpace(s) && s != "1")
                    {
                        candidate = s.Trim();
                        break;
                    }
                }

                if (string.IsNullOrWhiteSpace(candidate)) continue;
                if (candidate.Equals("All", StringComparison.OrdinalIgnoreCase)
                    || candidate.StartsWith("(All", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                result.Add(new MemberInfo { Key = candidate, Name = candidate });
            }
        }
        catch (Exception)
        {
            // ignore; caller has outer fallback
        }

        return result;
    }

    private static string GetMemberSetExpression(string dimensionName, string hierarchyName)
    {
        if (dimensionName.Equals("Dim Thoi Gian", StringComparison.OrdinalIgnoreCase))
            return "[Dim Thoi Gian].[Hierarchy].[Nam].Members";
        if (dimensionName.Equals("Dim Mat Hang", StringComparison.OrdinalIgnoreCase))
            return "[Dim Mat Hang].[Ma MH].Members";
        if (dimensionName.Equals("Dim Khach Hang", StringComparison.OrdinalIgnoreCase))
            return "[Dim Khach Hang].[Ma KH].Members";
        if (dimensionName.Equals("Dim Cua Hang", StringComparison.OrdinalIgnoreCase))
            return "[Dim Cua Hang].[Ma CH].Members";

        // fallback generic expression if future dimensions are added
        return $"[{dimensionName}].[{hierarchyName}].Members";
    }

    private static void AppendMembersFromSchemaRows(
        List<MemberInfo> target,
        System.Data.DataSet? schemaDs,
        string expectedLevelName)
    {
        if (schemaDs?.Tables.Count is not > 0) return;

        foreach (System.Data.DataRow row in schemaDs.Tables[0].Rows)
        {
            string levelCaption = row["LEVEL_CAPTION"]?.ToString() ?? row["LEVEL_NAME"]?.ToString() ?? "";
            string levelUnique = row["LEVEL_UNIQUE_NAME"]?.ToString() ?? "";
            string? key = row["MEMBER_UNIQUE_NAME"]?.ToString();
            string? name = row["MEMBER_CAPTION"]?.ToString();
            if (string.IsNullOrWhiteSpace(name)) continue;

            // Bỏ các member hệ thống/all.
            if (name.Equals("All", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("(All", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(expectedLevelName))
            {
                bool matchesLevel =
                    levelCaption.Equals(expectedLevelName, StringComparison.OrdinalIgnoreCase)
                    || levelUnique.Contains($".[{expectedLevelName}]", StringComparison.OrdinalIgnoreCase);

                if (!matchesLevel && !LooksLikeExpectedCaption(name, expectedLevelName))
                    continue;
            }

            target.Add(new MemberInfo { Key = key ?? name, Name = name });
        }
    }

    private static bool LooksLikeExpectedCaption(string caption, string expectedLevelName)
    {
        if (expectedLevelName.Equals("Nam", StringComparison.OrdinalIgnoreCase))
            return Regex.IsMatch(caption, @"^\d{4}$");
        if (expectedLevelName.Equals("Ma MH", StringComparison.OrdinalIgnoreCase))
            return caption.StartsWith("MH", StringComparison.OrdinalIgnoreCase);
        if (expectedLevelName.Equals("Ma KH", StringComparison.OrdinalIgnoreCase))
            return caption.StartsWith("KH", StringComparison.OrdinalIgnoreCase);
        if (expectedLevelName.Equals("Ma CH", StringComparison.OrdinalIgnoreCase))
            return caption.StartsWith("CH", StringComparison.OrdinalIgnoreCase);

        return false;
    }

    private static List<MemberInfo> NormalizeMembersByHierarchy(List<MemberInfo> source, string hierarchyName)
    {
        var normalized = new List<MemberInfo>();
        var relaxedCodes = new List<MemberInfo>();
        foreach (var m in source)
        {
            string key = m.Key ?? "";
            string name = m.Name ?? "";
            string extracted = ExtractMemberKey(key);

            if (hierarchyName.Equals("Nam", StringComparison.OrdinalIgnoreCase))
            {
                string year = ExtractYear(name);
                if (string.IsNullOrWhiteSpace(year)) year = ExtractYear(extracted);
                if (string.IsNullOrWhiteSpace(year)) continue;

                normalized.Add(new MemberInfo { Key = year, Name = year });
                continue;
            }

            if (hierarchyName is "Ma MH" or "Ma KH" or "Ma CH")
            {
                string code = !string.IsNullOrWhiteSpace(extracted) ? extracted : name;
                if (string.IsNullOrWhiteSpace(code)) continue;
                relaxedCodes.Add(new MemberInfo { Key = code, Name = code });

                bool strictMatch =
                    (hierarchyName == "Ma MH" && code.StartsWith("MH", StringComparison.OrdinalIgnoreCase))
                    || (hierarchyName == "Ma KH" && code.StartsWith("KH", StringComparison.OrdinalIgnoreCase))
                    || (hierarchyName == "Ma CH" && code.StartsWith("CH", StringComparison.OrdinalIgnoreCase));
                if (strictMatch)
                    normalized.Add(new MemberInfo { Key = code, Name = code });
                continue;
            }

            if (!string.IsNullOrWhiteSpace(name))
                normalized.Add(new MemberInfo { Key = key, Name = name });
        }

        if (normalized.Count == 0 && hierarchyName is "Ma MH" or "Ma KH" or "Ma CH")
            return relaxedCodes;

        return normalized;
    }

    private static string ExtractMemberKey(string uniqueName)
    {
        var match = Regex.Match(uniqueName ?? "", @"&\[([^\]]+)\]");
        return match.Success ? match.Groups[1].Value : "";
    }

    private static string ExtractYear(string text)
    {
        var match = Regex.Match(text ?? "", @"\b(19|20)\d{2}\b");
        return match.Success ? match.Value : "";
    }

    private static Task<List<string>> GetCubesViaSchemaAsync(AdomdConnection conn)
    {
        return Task.Run(() =>
        {
            var cubes = new List<string>();
            try
            {
                var restrictions = new AdomdRestrictionCollection
                {
                    new AdomdRestriction("CATALOG_NAME", conn.Database),
                };

                var schemaDs = conn.GetSchemaDataSet("MDSCHEMA_CUBES", restrictions);
                if (schemaDs?.Tables.Count > 0)
                {
                    foreach (System.Data.DataRow row in schemaDs.Tables[0].Rows)
                    {
                        string? cubeName = row["CUBE_NAME"]?.ToString();
                        string? cubeSource = row["CUBE_SOURCE"]?.ToString();
                        if (string.IsNullOrWhiteSpace(cubeName))
                            continue;

                        // CUBE_SOURCE=1: cube; ignore perspectives/other artifacts.
                        if (cubeSource == "1")
                            cubes.Add(cubeName);
                    }
                }
            }
            catch (Exception)
            {
                // fallback below
            }

            return cubes
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToList();
        });
    }

    private sealed class HierarchyMap
    {
        public string? TimeHierarchyUniqueName { get; set; }
        public string? ProductHierarchyUniqueName { get; set; }
        public string? CustomerHierarchyUniqueName { get; set; }
        public string? StoreHierarchyUniqueName { get; set; }
    }

    private static Task<HierarchyMap> DiscoverHierarchyMapAsync(AdomdConnection conn, string cube)
    {
        return Task.Run(() =>
        {
            var map = new HierarchyMap();
            try
            {
                var restrictions = new AdomdRestrictionCollection
                {
                    new AdomdRestriction("CATALOG_NAME", conn.Database),
                    new AdomdRestriction("CUBE_NAME", cube),
                };
                var schemaDs = conn.GetSchemaDataSet("MDSCHEMA_HIERARCHIES", restrictions);
                if (schemaDs?.Tables.Count is not > 0) return map;

                foreach (System.Data.DataRow row in schemaDs.Tables[0].Rows)
                {
                    string hierarchyUnique = row["HIERARCHY_UNIQUE_NAME"]?.ToString() ?? "";
                    string hierarchyName = row["HIERARCHY_NAME"]?.ToString() ?? "";
                    string dimensionUnique = row["DIMENSION_UNIQUE_NAME"]?.ToString() ?? "";
                    string signature = $"{hierarchyUnique}|{hierarchyName}|{dimensionUnique}".ToUpperInvariant();

                    if (string.IsNullOrWhiteSpace(map.TimeHierarchyUniqueName)
                        && (signature.Contains("TG") || signature.Contains("THOI") || signature.Contains("TIME") || signature.Contains("DATE")))
                        map.TimeHierarchyUniqueName = hierarchyUnique;

                    if (string.IsNullOrWhiteSpace(map.ProductHierarchyUniqueName)
                        && (signature.Contains("MH") || signature.Contains("MAT") || signature.Contains("PRODUCT")))
                        map.ProductHierarchyUniqueName = hierarchyUnique;

                    if (string.IsNullOrWhiteSpace(map.CustomerHierarchyUniqueName)
                        && (signature.Contains("KH") || signature.Contains("KHACH") || signature.Contains("CUSTOMER")))
                        map.CustomerHierarchyUniqueName = hierarchyUnique;

                    if (string.IsNullOrWhiteSpace(map.StoreHierarchyUniqueName)
                        && (signature.Contains("CH") || signature.Contains("CUA") || signature.Contains("STORE") || signature.Contains("SHOP")))
                        map.StoreHierarchyUniqueName = hierarchyUnique;
                }
            }
            catch (Exception)
            {
                // caller handles fallback
            }
            return map;
        });
    }

    private static string RewriteMdxWithDiscoveredSchema(AdomdConnection conn, string mdx)
    {
        try
        {
            string? cube = ExtractCubeNameFromMdx(mdx);
            if (string.IsNullOrWhiteSpace(cube)) return mdx;

            var map = DiscoverHierarchyMapAsync(conn, cube).GetAwaiter().GetResult();
            string rewritten = mdx;

            if (!string.IsNullOrWhiteSpace(map.TimeHierarchyUniqueName))
                rewritten = rewritten.Replace("[Dim Thoi Gian].[Hierarchy]", map.TimeHierarchyUniqueName, StringComparison.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(map.ProductHierarchyUniqueName))
                rewritten = rewritten.Replace("[Dim Mat Hang].[Ma MH]", map.ProductHierarchyUniqueName, StringComparison.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(map.CustomerHierarchyUniqueName))
            {
                rewritten = rewritten.Replace("[Dim Khach Hang].[Ma KH]", map.CustomerHierarchyUniqueName, StringComparison.OrdinalIgnoreCase);
                rewritten = rewritten.Replace("[Dim Khach Hang].[Hierarchy]", map.CustomerHierarchyUniqueName, StringComparison.OrdinalIgnoreCase);
            }
            if (!string.IsNullOrWhiteSpace(map.StoreHierarchyUniqueName))
            {
                rewritten = rewritten.Replace("[Dim Cua Hang].[Ma CH]", map.StoreHierarchyUniqueName, StringComparison.OrdinalIgnoreCase);
                rewritten = rewritten.Replace("[Dim Cua Hang].[Hierarchy]", map.StoreHierarchyUniqueName, StringComparison.OrdinalIgnoreCase);
                rewritten = rewritten.Replace("[Dim Dia Diem].[Hierarchy]", map.StoreHierarchyUniqueName, StringComparison.OrdinalIgnoreCase);
            }

            return rewritten;
        }
        catch (Exception)
        {
            return mdx;
        }
    }

    private static string? ExtractCubeNameFromMdx(string mdx)
    {
        var match = Regex.Match(mdx ?? string.Empty, @"FROM\s+\[([^\]]+)\]", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static Task<List<MemberInfo>> GetMembersByHierarchyAsync(
        AdomdConnection conn,
        string cube,
        string hierarchyUniqueName,
        string hierarchyKind)
    {
        return Task.Run(() =>
        {
            var list = new List<MemberInfo>();
            try
            {
                string mdx = $@"
WITH MEMBER [Measures].[__MetaDummy] AS 1
SELECT
  {{ [Measures].[__MetaDummy] }} ON COLUMNS,
  NON EMPTY {hierarchyUniqueName}.Members ON ROWS
FROM [{cube}]";

                using var cmd = new AdomdCommand(mdx, conn);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string candidate = "";
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        if (reader.IsDBNull(i)) continue;
                        object value = reader.GetValue(i);
                        if (value is string s && !string.IsNullOrWhiteSpace(s) && s != "1")
                        {
                            candidate = s.Trim();
                            break;
                        }
                    }

                    if (string.IsNullOrWhiteSpace(candidate)) continue;
                    list.Add(new MemberInfo { Key = candidate, Name = candidate });
                }
            }
            catch (Exception)
            {
                // ignore and fallback by empty result
            }

            list = NormalizeMembersByHierarchy(list, hierarchyKind);
            return list
                .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        });
    }

    private static Task<List<string>> GetMeasuresViaSchemaAsync(AdomdConnection conn, string cube)
    {
        return Task.Run(() =>
        {
            var measures = new List<string>();
            try
            {
                var restrictions = new AdomdRestrictionCollection
                {
                    new AdomdRestriction("CATALOG_NAME", conn.Database),
                    new AdomdRestriction("CUBE_NAME", cube),
                };

                var schemaDs = conn.GetSchemaDataSet("MDSCHEMA_MEASURES", restrictions);
                if (schemaDs?.Tables.Count > 0)
                {
                    foreach (System.Data.DataRow row in schemaDs.Tables[0].Rows)
                    {
                        string? name = row["MEASURE_NAME"]?.ToString();
                        if (!string.IsNullOrWhiteSpace(name) && !name.StartsWith("_", StringComparison.Ordinal))
                            measures.Add(name);
                    }
                }
            }
            catch (Exception)
            {
                // fallback below
            }

            if (measures.Count == 0)
                measures.AddRange(GetFallbackMeasuresForCube(cube));

            return measures.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        });
    }

    private static List<string> GetFallbackMeasuresForCube(string cube)
    {
        if (cube.Contains("TonKho", StringComparison.OrdinalIgnoreCase))
            return ["So Luong Trong Kho"];
        return ["Tong Tien", "So Luong Dat"];
    }

    private static List<string> GetFallbackCubeNames() =>
    [
        "Cube4FactBanHang_3D_KH_MH_TG",
        "Cube4BanHang_3D_KH_MH_TG_01",
        "Cube4BanHang_2D_KH_TG",
        "Cube4BanHang_2D_KH_TG_01",
        "Cube4BanHang_2D_MH_TG",
        "Cube4BanHang_2D_MH_TG_01",
        "Cube4BanHang_2D_MH_KH",
        "Cube4BanHang_1D_MH",
        "Cube4BanHang_1D_MH_01",
        "Cube4BanHang_1D_KH",
        "Cube4BanHang_1D_KH_01",
        "Cube4BanHang_1D_TG",
        "Cube4BanHang_1D_TG_01",
        "Cube4TonKho_3D_MH_CH_TG",
        "Cube4TonKho_3D_MH_CH_TG_01",
        "Cube4TonKho_2D_CH_TG",
        "Cube4TonKho_2D_CH_TG_01",
        "Cube4TonKho_2D_MH_TG",
        "Cube4TonKho_2D_MH_TG_01",
        "Cube4TonKho_1D_TG",
        "Cube4TonKho_1D_TG_01"
    ];

    private static OlapDemo.Api.Models.CubeInfo BuildCubeInfo(string cubeName)
    {
        var rule = CubeNameRules.Parse(cubeName);
        return new OlapDemo.Api.Models.CubeInfo
        {
            Name = rule.Name,
            Fact = rule.Fact,
            DimensionCount = rule.DimensionCount,
            Dimensions = rule.Dimensions.ToList(),
            Measures = rule.Measures.ToList(),
            Description = rule.Description,
            Capabilities = new CubeCapabilities
            {
                HasTime = rule.HasTime,
                HasProduct = rule.HasProduct,
                HasCustomer = rule.HasCustomer,
                HasStore = rule.HasStore,
                AllowDrillDown = rule.HasTime,
                AllowRollUp = rule.HasTime,
                AllowSlice = rule.HasTime,
                AllowDice = rule.HasTime,
                AllowPivot = rule.HasTime
            }
        };
    }
}
