namespace UserManagementAPI.Middleware;

public sealed class TokenAuthenticationMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<TokenAuthenticationMiddleware> logger)
{
    private const string AuthorizationHeaderName = "Authorization";
    private readonly RequestDelegate _next = next;
    private readonly IConfiguration _configuration = configuration;
    private readonly ILogger<TokenAuthenticationMiddleware> _logger = logger;

    public async Task InvokeAsync(HttpContext context)
    {
        // Protect API endpoints while allowing OpenAPI metadata access in development.
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        var configuredToken = _configuration["Auth:ApiToken"];
        if (string.IsNullOrWhiteSpace(configuredToken))
        {
            _logger.LogWarning("Auth:ApiToken is not configured. Falling back to development token.");
            configuredToken = "techhive-dev-token";
        }

        if (!context.Request.Headers.TryGetValue(AuthorizationHeaderName, out var authHeaderValue))
        {
            await WriteUnauthorizedAsync(context);
            return;
        }

        var rawHeader = authHeaderValue.ToString();
        const string bearerPrefix = "Bearer ";

        if (!rawHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await WriteUnauthorizedAsync(context);
            return;
        }

        var incomingToken = rawHeader[bearerPrefix.Length..].Trim();
        if (!string.Equals(incomingToken, configuredToken, StringComparison.Ordinal))
        {
            await WriteUnauthorizedAsync(context);
            return;
        }

        await _next(context);
    }

    private static Task WriteUnauthorizedAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsJsonAsync(new { error = "Unauthorized. Invalid or missing token." });
    }
}
