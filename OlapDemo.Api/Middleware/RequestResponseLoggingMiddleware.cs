using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace OlapDemo.Api.Middleware;

/// <summary>
/// Middleware log toàn bộ giao tiếp HTTP giữa FE và BE.
/// Hiển thị: method, path, query, request body, response status, response body, thời gian xử lý.
/// </summary>
public class RequestResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestResponseLoggingMiddleware> _logger;

    // Chỉ log response body với các content-type này
    private static readonly HashSet<string> LoggableContentTypes =
    [
        "application/json",
        "text/plain",
        "text/json"
    ];

    public RequestResponseLoggingMiddleware(RequestDelegate next, ILogger<RequestResponseLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Bỏ qua swagger UI / static files
        string path = context.Request.Path.Value ?? "";
        if (path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/favicon", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var sw = Stopwatch.StartNew();
        string traceId = context.TraceIdentifier;

        // ── LOG REQUEST ────────────────────────────────────────────────────────
        string requestBody = await ReadRequestBodyAsync(context.Request);
        string queryString = context.Request.QueryString.HasValue
            ? context.Request.QueryString.Value ?? ""
            : "";

        _logger.LogInformation(
            "\n┌─── [REQUEST] ─── TraceId={TraceId}\n" +
            "│  {Method} {Path}{Query}\n" +
            "│  From: {RemoteIp}\n" +
            "│  Body: {Body}\n" +
            "└────────────────────────────────────────",
            traceId,
            context.Request.Method,
            path,
            string.IsNullOrEmpty(queryString) ? "" : "?" + queryString.TrimStart('?'),
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            string.IsNullOrWhiteSpace(requestBody) ? "(empty)" : PrettyJson(requestBody));

        // ── BUFFER RESPONSE để đọc body ────────────────────────────────────────
        var originalBodyStream = context.Response.Body;
        using var responseBuffer = new MemoryStream();
        context.Response.Body = responseBuffer;

        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();
            responseBuffer.Seek(0, SeekOrigin.Begin);

            // ── LOG RESPONSE ───────────────────────────────────────────────────
            string responseBody = await ReadResponseBodyAsync(context.Response, responseBuffer);
            int statusCode = context.Response.StatusCode;
            string statusIcon = statusCode >= 500 ? "💥" : statusCode >= 400 ? "⚠️" : "✅";

            _logger.LogInformation(
                "\n└─── [RESPONSE] ─── TraceId={TraceId}\n" +
                "   {Icon} {StatusCode} {Method} {Path} [{ElapsedMs}ms]\n" +
                "   Body: {Body}\n" +
                "────────────────────────────────────────",
                traceId,
                statusIcon,
                statusCode,
                context.Request.Method,
                path,
                sw.ElapsedMilliseconds,
                string.IsNullOrWhiteSpace(responseBody) ? "(empty)" : PrettyJson(responseBody, maxLength: 2000));

            // Copy buffer về response stream gốc
            responseBuffer.Seek(0, SeekOrigin.Begin);
            await responseBuffer.CopyToAsync(originalBodyStream);
            context.Response.Body = originalBodyStream;
        }
    }

    private static async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        if (!request.HasJsonContentType() && request.ContentType?.Contains("json") != true)
        {
            // Với GET / không có body JSON
            return "";
        }

        request.EnableBuffering();
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        string body = await reader.ReadToEndAsync();
        request.Body.Seek(0, SeekOrigin.Begin);
        return body;
    }

    private static async Task<string> ReadResponseBodyAsync(HttpResponse response, MemoryStream buffer)
    {
        string contentType = response.ContentType ?? "";
        bool isLoggable = LoggableContentTypes.Any(t => contentType.Contains(t, StringComparison.OrdinalIgnoreCase));
        if (!isLoggable) return "(binary/non-text)";

        buffer.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(buffer, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    /// <summary>Pretty-print JSON; nếu không phải JSON thì trả về nguyên văn (cắt nếu quá dài).</summary>
    private static string PrettyJson(string raw, int maxLength = 1500)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            string pretty = JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
            if (pretty.Length > maxLength)
                return pretty[..maxLength] + $"\n   ... (truncated, total {pretty.Length} chars)";
            return pretty;
        }
        catch
        {
            // Không phải JSON
            if (raw.Length > maxLength)
                return raw[..maxLength] + $"... (truncated)";
            return raw;
        }
    }
}
