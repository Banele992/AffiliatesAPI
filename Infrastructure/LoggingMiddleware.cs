using System.Diagnostics;

namespace AffiliatesAPI.Infrastructure;

/// <summary>
/// Request/response logging middleware.
/// Logs method, path, status code, elapsed time, and a correlation ID on every request.
/// </summary>
public class LoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<LoggingMiddleware> _logger;

    public LoggingMiddleware(RequestDelegate next, ILogger<LoggingMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
                            ?? Guid.NewGuid().ToString("N");

        // Surface the correlation ID on the response so clients can trace it
        context.Response.Headers["X-Correlation-ID"] = correlationId;

        var sw = Stopwatch.StartNew();

        _logger.LogInformation(
            "→ {Method} {Path} | CorrelationId={CorrelationId}",
            context.Request.Method,
            context.Request.Path,
            correlationId);

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unhandled exception | CorrelationId={CorrelationId}", correlationId);
            throw;
        }
        finally
        {
            sw.Stop();
            _logger.LogInformation(
                "← {Method} {Path} | Status={StatusCode} | Elapsed={ElapsedMs}ms | CorrelationId={CorrelationId}",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                sw.ElapsedMilliseconds,
                correlationId);
        }
    }
}
