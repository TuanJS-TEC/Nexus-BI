namespace OlapDemo.Api.Models;

/// <summary>Request DTO gửi từ Vue đến API</summary>
public class OlapRequest
{
    public string Cube { get; set; } = "Cube4BanHang_3D_KH_MH_TG_01";
    public string Measure { get; set; } = "Tong Tien";
    public string ActiveDimension { get; set; } = "ThoiGian";
    public string? ProductKey { get; set; }
    public string? CustomerKey { get; set; }
    public string? CustomerType { get; set; }
    public string? CustomerName { get; set; }
    public string? StoreKey { get; set; }
    public string? State { get; set; }
    public string? City { get; set; }
    public int? Year { get; set; }
    public int? Quarter { get; set; }
    public int? Month { get; set; }
    public string RowLevel { get; set; } = "Nam";
    public string ColLevel { get; set; } = "Ma MH";
    public bool IncludeSoLuong { get; set; } = true;
    public List<string>? ActiveDimensions { get; set; }
    public Dictionary<string, string>? DimensionLevels { get; set; }
}
