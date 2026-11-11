using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Ofis360.Application.Common;

namespace Ofis360.Api.Middleware;

/// <summary>Servis katmanindan gelen AppException'i kaynakla ayni HTTP zarfina cevirir.</summary>
public sealed class AppExceptionFilter(ILogger<AppExceptionFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not AppException ex)
        {
            return;
        }

        context.Result = ToResult(context.HttpContext, ex, logger);
        context.ExceptionHandled = true;
    }

    public static ObjectResult ToResult(HttpContext http, AppException ex, ILogger logger)
    {
        if (ex.Status == StatusCodes.Status403Forbidden || (ex.Status == StatusCodes.Status401Unauthorized && http.Request.Path.StartsWithSegments("/api/auth/handoff")))
        {
            var agent = http.Request.Headers.UserAgent.ToString();
            logger.LogWarning("Guvenlik olayi: {Status} {Method} {Path} kullanici={UserId} ip={Ip} ajan={UserAgent}",
                ex.Status, http.Request.Method, http.Request.Path.Value, (http.Items["ofis360.session"] as Application.Identity.SessionUser)?.Id,
                http.Connection.RemoteIpAddress?.ToString(), agent.Length > 200 ? agent[..200] : agent);
        }

        return new ObjectResult(new ApiErrorBody(new ApiError(ex.Code, ex.Message, ex.Violations, ex.Details)))
        {
            StatusCode = ex.Status,
        };
    }
}
