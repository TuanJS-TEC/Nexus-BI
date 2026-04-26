namespace OlapDemo.Api.Models;

/// <summary>Request DTO gửi từ Vue đến API</summary>
public class OlapRequest
{
    public string Cube { get; set; } = "Cube4BanHang_3D_KH_MH_TG_01";
    public string Measure { get; set; } = "Tong Tien";
    public string? ProductKey { get; set; }
    public string? CustomerKey { get; set; }
    public string? StoreKey { get; set; }
    public int? Year { get; set; }
    public int? Quarter { get; set; }
    public string RowLevel { get; set; } = "Nam";
    public string ColLevel { get; set; } = "Ma MH";
    public bool IncludeSoLuong { get; set; } = true;
}
