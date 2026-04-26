using Microsoft.AspNetCore.Mvc;
using OlapDemo.Api.Models;
using OlapDemo.Api.Services;

namespace OlapDemo.Api.Controllers;

[ApiController]
[Route("api/olap")]
public class OlapController : ControllerBase
{
    private readonly OlapService _olap;
    private readonly IConfiguration _config;

    public OlapController(OlapService olap, IConfiguration config)
    {
        _olap = olap;
        _config = config;
    }

    private string DefaultCube => _config["Ssas:DefaultCube"] ?? "Cube4BanHang_3D_KH_MH_TG_01";

    [HttpGet("metadata")]
    public async Task<IActionResult> GetMetadata([FromQuery] string? cube)
    {
        var result = await _olap.GetMetadataAsync(cube ?? DefaultCube);
        return Ok(result);
    }

    [HttpPost("drill")]
    public async Task<IActionResult> DrillDown([FromBody] OlapRequest req)
    {
        try
        {
            string cube = string.IsNullOrWhiteSpace(req.Cube) ? DefaultCube : req.Cube;
            var (mdx, newLevel) = MdxBuilder.DrillDown(req, cube);
            return Ok(await _olap.ExecuteAsync(mdx, newLevel, "DrillDown"));
        }
        catch (Exception ex)
        {
            return BadRequest(new OlapResult { Success = false, Error = ex.Message });
        }
    }

    [HttpPost("rollup")]
    public async Task<IActionResult> RollUp([FromBody] OlapRequest req)
    {
        try
        {
            string cube = string.IsNullOrWhiteSpace(req.Cube) ? DefaultCube : req.Cube;
            var (mdx, newLevel) = MdxBuilder.RollUp(req, cube);
            return Ok(await _olap.ExecuteAsync(mdx, newLevel, "RollUp"));
        }
        catch (Exception ex)
        {
            return BadRequest(new OlapResult { Success = false, Error = ex.Message });
        }
    }

    [HttpPost("slice-dice")]
    public async Task<IActionResult> SliceDice([FromBody] SliceDiceRequest req)
    {
        try
        {
            string cube = string.IsNullOrWhiteSpace(req.Cube) ? DefaultCube : req.Cube;
            string mdx = req.IsDice ? MdxBuilder.Dice(req, cube) : MdxBuilder.Slice(req, cube);
            string opType = req.IsDice ? "Dice" : "Slice";
            return Ok(await _olap.ExecuteAsync(mdx, req.RowLevel, opType));
        }
        catch (Exception ex)
        {
            return BadRequest(new OlapResult { Success = false, Error = ex.Message });
        }
    }

    [HttpPost("pivot")]
    public async Task<IActionResult> Pivot([FromBody] OlapRequest req)
    {
        try
        {
            string cube = string.IsNullOrWhiteSpace(req.Cube) ? DefaultCube : req.Cube;
            string mdx = MdxBuilder.Pivot(req, cube);
            return Ok(await _olap.ExecuteAsync(mdx, req.RowLevel, "Pivot"));
        }
        catch (Exception ex)
        {
            return BadRequest(new OlapResult { Success = false, Error = ex.Message });
        }
    }

    [HttpGet("default-query")]
    public async Task<IActionResult> DefaultQuery([FromQuery] string? cube)
    {
        try
        {
            string effectiveCube = string.IsNullOrWhiteSpace(cube) ? DefaultCube : cube;
            string mdx = $@"SELECT {{ [Measures].[Tong Tien], [Measures].[So Luong Dat] }} ON COLUMNS,
  NON EMPTY [Dim_Thoi_Gian].[Hierarchy].[Nam].Members ON ROWS
FROM [{effectiveCube}]";

            return Ok(await _olap.ExecuteAsync(mdx, "Nam", "DefaultQuery"));
        }
        catch (Exception ex)
        {
            return BadRequest(new OlapResult { Success = false, Error = ex.Message });
        }
    }
}

public class SliceDiceRequest : OlapRequest
{
    public bool IsDice { get; set; } = false;
}
