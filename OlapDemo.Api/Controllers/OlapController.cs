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

    private string DefaultCube => _config["Ssas:DefaultCube"] ?? "Cube4BanHang_1D_TG";

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

    [HttpPost("query")]
    public async Task<IActionResult> Query([FromBody] OlapRequest req)
    {
        try
        {
            string cube = string.IsNullOrWhiteSpace(req.Cube) ? DefaultCube : req.Cube;
            string mdx = MdxBuilder.QueryCurrent(req, cube);
            string level = string.IsNullOrWhiteSpace(req.RowLevel) ? "Nam" : req.RowLevel;
            return Ok(await _olap.ExecuteAsync(mdx, level, "Query"));
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
            var metadata = await _olap.GetMetadataAsync(cube ?? DefaultCube);
            string requestedCube = string.IsNullOrWhiteSpace(cube) ? DefaultCube : cube;
            bool requestTonKho = requestedCube.Contains("TonKho", StringComparison.OrdinalIgnoreCase);
            var exactRequested = metadata.CubeInfos
                .FirstOrDefault(info => info.Name.Equals(requestedCube, StringComparison.OrdinalIgnoreCase));
            var timeAwareFallback = metadata.CubeInfos
                .Where(info => info.Capabilities.HasTime)
                .OrderBy(info => info.DimensionCount)
                .FirstOrDefault(info => requestTonKho
                    ? info.Fact.Contains("TonKho", StringComparison.OrdinalIgnoreCase)
                    : info.Fact.Contains("BanHang", StringComparison.OrdinalIgnoreCase));

            string effectiveCube = exactRequested?.Name
                ?? metadata.Cubes.FirstOrDefault(c => c.Equals(requestedCube, StringComparison.OrdinalIgnoreCase))
                ?? timeAwareFallback?.Name
                ?? metadata.Cubes.FirstOrDefault()
                ?? requestedCube;
            string measureSet = BuildDefaultMeasureSet(effectiveCube);
            string rowSet = BuildDefaultRowSet(effectiveCube);
            string rowLevel = BuildDefaultRowLevel(effectiveCube);
            string mdx = $@"SELECT {measureSet} ON COLUMNS,
  NON EMPTY {rowSet} ON ROWS
FROM [{effectiveCube}]";

            return Ok(await _olap.ExecuteAsync(mdx, rowLevel, "DefaultQuery"));
        }
        catch (Exception ex)
        {
            return BadRequest(new OlapResult { Success = false, Error = ex.Message });
        }
    }

    [HttpGet("cube-mappings")]
    public async Task<IActionResult> GetCubeMappings()
    {
        try
        {
            var meta = await _olap.GetMetadataAsync(DefaultCube);
            var mappings = meta.CubeInfos
                .Select(info =>
                {
                    var tokens = new List<string>();
                    if (info.Capabilities.HasProduct) tokens.Add("MH");
                    if (info.Capabilities.HasTime) tokens.Add("TG");
                    if (info.Capabilities.HasCustomer) tokens.Add("KH");
                    if (info.Capabilities.HasStore) tokens.Add("CH");
                    return new
                    {
                        cube = info.Name,
                        fact = info.Fact.Contains("TonKho", StringComparison.OrdinalIgnoreCase) ? "TonKho" : "BanHang",
                        dimensions = tokens 
                    };
                })
                .OrderBy(x => x.fact)
                .ThenBy(x => x.cube)
                .ToList();

            return Ok(mappings);
        }
        catch (Exception ex)
        {
            return BadRequest(new OlapResult { Success = false, Error = ex.Message });
        }
    }

    private static string BuildDefaultMeasureSet(string cube)
    {
        if (cube.Contains("TonKho", StringComparison.OrdinalIgnoreCase))
        {
            return "{ [Measures].[So Luong Trong Kho] }";
        }

        return "{ [Measures].[Tong Tien], [Measures].[So Luong Dat] }";
    }

    private static string BuildDefaultRowSet(string cube)
    {
        var rule = CubeNameRules.Parse(cube);

        if (rule.DimensionCount >= 3 && rule.HasTime && rule.HasProduct && rule.HasStore)
            return "[Dim Thoi Gian].[Hierarchy].[Nam].Members * [Dim Mat Hang].[Ma MH].Members * [Dim Cua Hang].[Ma CH].Members";
        if (rule.DimensionCount >= 3 && rule.HasTime && rule.HasProduct && rule.HasCustomer)
            return "[Dim Thoi Gian].[Hierarchy].[Nam].Members * [Dim Mat Hang].[Ma MH].Members * [Dim Khach Hang].[Ma KH].Members";

        if (rule.HasTime && rule.HasCustomer && rule.DimensionCount >= 2)
            return "[Dim Thoi Gian].[Hierarchy].[Nam].Members * [Dim Khach Hang].[Ma KH].Members";
        if (rule.HasTime && rule.HasProduct && rule.DimensionCount >= 2)
            return "[Dim Thoi Gian].[Hierarchy].[Nam].Members * [Dim Mat Hang].[Ma MH].Members";
        if (rule.HasTime && rule.HasStore && rule.DimensionCount >= 2)
            return "[Dim Thoi Gian].[Hierarchy].[Nam].Members * [Dim Cua Hang].[Ma CH].Members";
        if (rule.HasProduct && rule.HasCustomer && rule.DimensionCount >= 2)
            return "[Dim Mat Hang].[Ma MH].Members * [Dim Khach Hang].[Ma KH].Members";

        if (rule.HasTime) return "[Dim Thoi Gian].[Hierarchy].[Nam].Members";
        if (rule.HasCustomer) return "[Dim Khach Hang].[Ma KH].Members";
        if (rule.HasProduct) return "[Dim Mat Hang].[Ma MH].Members";
        if (rule.HasStore) return "[Dim Cua Hang].[Ma CH].Members";

        return "[Dim Thoi Gian].[Hierarchy].[Nam].Members";
    }

    private static string BuildDefaultRowLevel(string cube)
    {
        var rule = CubeNameRules.Parse(cube);
        if (rule.HasTime) return "Nam";
        if (rule.HasCustomer) return "Ma KH";
        if (rule.HasProduct) return "Ma MH";
        if (rule.HasStore) return "Ma CH";
        return "Nam";
    }
}

public class SliceDiceRequest : OlapRequest
{
    public bool IsDice { get; set; } = false;
}
