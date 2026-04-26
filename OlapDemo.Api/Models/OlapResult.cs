namespace OlapDemo.Api.Models;

public class OlapResult
{
    public bool Success { get; set; } = true;
    public string? Error { get; set; }
    public List<string> Columns { get; set; } = [];
    public List<Dictionary<string, object?>> Rows { get; set; } = [];
    public string? Mdx { get; set; }
    public string CurrentLevel { get; set; } = "Nam";
    public string OperationType { get; set; } = "";
}

public class MemberInfo
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
}

public class MetadataResult
{
    public List<MemberInfo> Years { get; set; } = [];
    public List<MemberInfo> Products { get; set; } = [];
    public List<MemberInfo> Customers { get; set; } = [];
    public List<MemberInfo> Stores { get; set; } = [];
    public List<string> Measures { get; set; } = [];
    public List<string> Cubes { get; set; } = [];
    public List<CubeInfo> CubeInfos { get; set; } = [];
}

public class CubeInfo
{
    public string Name { get; set; } = "";
    public string Fact { get; set; } = "";
    public int DimensionCount { get; set; }
    public List<string> Dimensions { get; set; } = [];
    public List<string> Measures { get; set; } = [];
    public string Description { get; set; } = "";
    public CubeCapabilities Capabilities { get; set; } = new();
}

public class CubeCapabilities
{
    public bool HasTime { get; set; }
    public bool HasProduct { get; set; }
    public bool HasCustomer { get; set; }
    public bool HasStore { get; set; }
    public bool AllowDrillDown { get; set; }
    public bool AllowRollUp { get; set; }
    public bool AllowSlice { get; set; }
    public bool AllowDice { get; set; }
    public bool AllowPivot { get; set; }
}
