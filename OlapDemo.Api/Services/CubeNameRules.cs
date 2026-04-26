namespace OlapDemo.Api.Services;

public sealed record CubeRuleResult(
    string Name,
    string Fact,
    int DimensionCount,
    bool HasTime,
    bool HasProduct,
    bool HasCustomer,
    bool HasStore,
    IReadOnlyList<string> Dimensions,
    IReadOnlyList<string> Measures,
    string Description);

public static class CubeNameRules
{
    public static CubeRuleResult Parse(string cubeName)
    {
        string normalizedCubeName = cubeName ?? string.Empty;
        var tokens = normalizedCubeName
            .Split('_', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        bool hasProduct = tokens.Any(t => t.Equals("MH", StringComparison.OrdinalIgnoreCase));
        bool hasCustomer = tokens.Any(t => t.Equals("KH", StringComparison.OrdinalIgnoreCase));
        bool hasStore = tokens.Any(t => t.Equals("CH", StringComparison.OrdinalIgnoreCase));
        bool hasTime = tokens.Any(t => t.Equals("TG", StringComparison.OrdinalIgnoreCase));

        int dimensionCount = ParseDimensionCount(tokens);
        string fact = ParseFactName(tokens);

        var dimensions = new List<string>();
        if (hasCustomer) dimensions.Add("Khach Hang (KH)");
        if (hasProduct) dimensions.Add("Mat Hang (MH)");
        if (hasStore) dimensions.Add("Cua Hang (CH)");
        if (hasTime) dimensions.Add("Thoi Gian (TG)");

        var measures = fact.Contains("TonKho", StringComparison.OrdinalIgnoreCase)
            ? new List<string> { "So Luong Trong Kho" }
            : new List<string> { "Tong Tien", "So Luong Dat" };

        string description = BuildDescription(fact, dimensionCount, dimensions, hasTime, measures);

        return new CubeRuleResult(
            normalizedCubeName,
            fact,
            dimensionCount,
            hasTime,
            hasProduct,
            hasCustomer,
            hasStore,
            dimensions,
            measures,
            description);
    }

    private static int ParseDimensionCount(IEnumerable<string> tokens)
    {
        foreach (string token in tokens)
        {
            if (token.EndsWith("D", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(token[..^1], out int n))
            {
                return n;
            }
        }
        return 0;
    }

    private static string ParseFactName(IReadOnlyList<string> tokens)
    {
        string compact = string.Concat(tokens);
        if (compact.Contains("TonKho", StringComparison.OrdinalIgnoreCase)) return "Fact_TonKho";
        if (compact.Contains("BanHang", StringComparison.OrdinalIgnoreCase)) return "Fact_BanHang";
        return "Unknown";
    }

    private static string BuildDescription(
        string fact,
        int dimensionCount,
        IReadOnlyList<string> dimensions,
        bool hasTime,
        IReadOnlyList<string> measures)
    {
        string dimText = dimensions.Count > 0 ? string.Join(", ", dimensions) : "khong xac dinh";
        string measureText = string.Join(", ", measures);
        string timeText = hasTime ? "Nam/Quy/Thang" : "khong co chieu thoi gian";
        return $"{fact} {dimensionCount}D, dimensions: {dimText}, measures: {measureText}, muc thoi gian: {timeText}.";
    }
}
