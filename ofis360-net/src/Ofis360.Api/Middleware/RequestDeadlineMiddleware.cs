using System.Text.Json;
using Ofis360.Application.Common;

namespace Ofis360.Api.Middleware;

/// <summary>
/// Veritabani donarsa Oracle surucusu zaman asimlarini uygulayamiyor (2026-11-08'de olculdu). Sure dolunca istemciye hemen 503 doner.
/// Asili kalan is bitene kadar burada beklenir: HttpContext erken serbest kalip baska istekle karismaz.
/// </summary>
public sealed class RequestDeadlineMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<RequestDeadlineMiddleware> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly TimeSpan _deadline = TimeSpan.FromSeconds(configuration.GetValue("Ofis360:RequestDeadlineSeconds", 40));

    public async Task InvokeAsync(HttpContext context)
    {
        // Saglik ucunun kendi 5 sn siniri var.
        if (!context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/api/health"))
        {
            await next(context);
            return;
        }

        using var timer = new CancellationTokenSource();
        var work = next(context);
        var finished = await Task.WhenAny(work, Task.Delay(_deadline, timer.Token));
        if (finished == work)
        {
            await timer.CancelAsync();
            await work;
            return;
        }

        logger.LogError("Istek suresi doldu: {Method} {Path}", context.Request.Method, context.Request.Path.Value);
        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.Headers.Connection = "close";
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsync(JsonSerializer.Serialize(
                new ApiErrorBody(new ApiError("SERVICE_UNAVAILABLE", "Hizmet şu anda yanıt vermiyor. Lütfen biraz sonra yeniden deneyin.")), Json));
            await context.Response.CompleteAsync();
        }

        try
        {
            await work;
        }
        catch (Exception ex)
        {
            // Yanit zaten gonderildi; asili isin gec hatasi yalnizca kaydedilir.
            logger.LogWarning(ex, "Suresi dolan istek gec sonlandi.");
        }
    }
}
