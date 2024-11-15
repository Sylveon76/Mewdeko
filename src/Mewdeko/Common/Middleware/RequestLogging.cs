using Microsoft.AspNetCore.Http;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Microsoft.IO;

/// <summary>
///
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;
    private readonly RecyclableMemoryStreamManager _streamManager;

    /// <summary>
    ///
    /// </summary>
    /// <param name="next"></param>
    /// <param name="logger"></param>
    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _streamManager = new RecyclableMemoryStreamManager();
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="context"></param>
    public async Task InvokeAsync(HttpContext context)
    {
        context.Request.EnableBuffering();

        await using var requestStream = _streamManager.GetStream();
        await context.Request.Body.CopyToAsync(requestStream);

        var body = await ReadStreamInChunks(requestStream);
        _logger.LogInformation(
            "Request: {Method} {Path}\nBody: {Body}",
            context.Request.Method,
            context.Request.Path,
            body);

        context.Request.Body.Position = 0;

        await _next(context);
    }

    private static async Task<string> ReadStreamInChunks(Stream stream)
    {
        stream.Position = 0;
        using var textWriter = new StringWriter();
        using var reader = new StreamReader(stream);
        var readChunk = new char[4096];
        int readChunkLength;

        do
        {
            readChunkLength = await reader.ReadBlockAsync(readChunk);
            await textWriter.WriteAsync(readChunk, 0, readChunkLength);
        } while (readChunkLength > 0);

        return textWriter.ToString();
    }
}

// Extension method to make registration cleaner
/// <summary>
///
/// </summary>
public static class RequestLoggingMiddlewareExtensions
{
    /// <summary>
    ///
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestLoggingMiddleware>();
    }
}