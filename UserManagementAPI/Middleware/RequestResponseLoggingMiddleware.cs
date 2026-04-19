using System.Diagnostics;

namespace UserManagementAPI.Middleware;

public sealed class RequestResponseLoggingMiddleware(RequestDelegate next, ILogger<RequestResponseLoggingMiddleware> logger)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<RequestResponseLoggingMiddleware> _logger = logger;

    public async Task InvokeAsync(HttpContext context)
    {
        var timer = Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            timer.Stop();
            var statusCode = context.Response.StatusCode;
            var logLevel = statusCode >= 500 ? LogLevel.Error
                         : statusCode >= 400 ? LogLevel.Warning
                         : LogLevel.Information;

            _logger.Log(logLevel,
                "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms",
                context.Request.Method,
                context.Request.Path,
                statusCode,
                timer.ElapsedMilliseconds);
        }
    }
}
