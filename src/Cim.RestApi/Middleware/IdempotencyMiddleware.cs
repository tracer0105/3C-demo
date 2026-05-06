using System.Collections.Concurrent;

namespace Cim.RestApi.Middleware;

/// <summary>
/// Idempotency middleware: caches responses keyed on the <c>Idempotency-Key</c> header.
/// Subsequent requests with the same key receive the cached response without re-executing handlers.
/// Cache entries expire after <see cref="CacheTtl"/> to prevent unbounded memory growth.
/// </summary>
public class IdempotencyMiddleware
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);
    private static readonly ConcurrentDictionary<string, CachedResponse> _cache = new(StringComparer.OrdinalIgnoreCase);

    private readonly RequestDelegate _next;
    private readonly ILogger<IdempotencyMiddleware> _logger;

    public IdempotencyMiddleware(RequestDelegate next, ILogger<IdempotencyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only apply to mutating methods
        if (context.Request.Method is "GET" or "HEAD" or "OPTIONS")
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue("Idempotency-Key", out var keyValues)
            || string.IsNullOrWhiteSpace(keyValues.FirstOrDefault()))
        {
            await _next(context);
            return;
        }

        var key = keyValues.First()!.Trim();

        if (_cache.TryGetValue(key, out var cached) && !cached.IsExpired)
        {
            _logger.LogDebug("Idempotency cache hit for key {Key}", key);
            context.Response.StatusCode = cached.StatusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(cached.Body);
            return;
        }

        // Capture the response
        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        await _next(context);

        buffer.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(buffer).ReadToEndAsync();

        _cache[key] = new CachedResponse(context.Response.StatusCode, body, DateTimeOffset.UtcNow.Add(CacheTtl));

        buffer.Seek(0, SeekOrigin.Begin);
        await buffer.CopyToAsync(originalBody);
        context.Response.Body = originalBody;
    }

    private record CachedResponse(int StatusCode, string Body, DateTimeOffset ExpiresAt)
    {
        public bool IsExpired => DateTimeOffset.UtcNow > ExpiresAt;
    }
}
