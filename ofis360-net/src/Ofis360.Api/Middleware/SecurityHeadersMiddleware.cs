namespace Ofis360.Api.Middleware;

/// <summary>API yanitlari icin guvenlik basliklari. Kisisel veri iceren yanitlar onbellege alinmaz.</summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
            if (context.Request.Path.StartsWithSegments("/api")) headers.CacheControl = "no-store";
            return Task.CompletedTask;
        });
        return next(context);
    }
}
