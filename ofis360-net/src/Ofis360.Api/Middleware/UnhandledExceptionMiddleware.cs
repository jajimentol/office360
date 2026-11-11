using System.Text.Json;
using Ofis360.Application.Common;

namespace Ofis360.Api.Middleware;

/// <summary>
/// Controller disinda (middleware, model baglama vb.) yakalanmayan hatalar icin 500 INTERNAL_ERROR zarfi.
/// Kaynak: server/api.ts apiError son dali.
/// </summary>
public sealed class UnhandledExceptionMiddleware(RequestDelegate next, ILogger<UnhandledExceptionMiddleware> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (AppException ex) when (!context.Response.HasStarted)
        {
            context.Response.StatusCode = ex.Status;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsync(JsonSerializer.Serialize(
                new ApiErrorBody(new ApiError(ex.Code, ex.Message, ex.Violations, ex.Details)), Json));
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            logger.LogError(ex, "Beklenmeyen sunucu hatası");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsync(JsonSerializer.Serialize(
                new ApiErrorBody(new ApiError(AppErrors.InternalCode, AppErrors.InternalMessage)), Json));
        }
    }
}
