using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Ofis360.Web.Api;

namespace Ofis360.Web.Controllers;

/// <summary>
/// Ortak MVC tabani. JSON aksiyonlari API cevabini AngularJS'e iletir; API oturumu dustuyse (401) MVC oturumu da kapatilir.
/// Sayfa (View) donen aksiyonlarda kabuk icin kullanici ve menu bilgisi ViewData'ya yazilir.
/// </summary>
public abstract class WebControllerBase(ApiClient api) : Controller
{
    protected ApiClient Api { get; } = api;

    protected CancellationToken Ct => HttpContext.RequestAborted;

    protected string? CurrentRole => User.FindFirstValue(ClaimTypes.Role);

    protected async Task<IActionResult> ForwardAsync(Task<ApiResult> call)
    {
        ApiResult result;
        try
        {
            result = await call;
        }
        catch (HttpRequestException)
        {
            return ApiUnavailable();
        }
        catch (TaskCanceledException) when (!Ct.IsCancellationRequested)
        {
            return ApiUnavailable();
        }
        return await ForwardAsync(result);
    }

    protected async Task<IActionResult> ForwardAsync(ApiResult result)
    {
        if (result.Status == HttpStatusCode.Unauthorized)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
        if (result.Status == HttpStatusCode.NoContent || string.IsNullOrWhiteSpace(result.Body))
        {
            return StatusCode((int)result.Status);
        }
        return new ContentResult { Content = result.Body, ContentType = "application/json; charset=utf-8", StatusCode = (int)result.Status };
    }

    protected IActionResult ApiUnavailable() =>
        StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = new { code = "API_UNAVAILABLE", message = "Sunucuya şu anda ulaşılamıyor. Lütfen biraz sonra yeniden deneyin." } });

    protected static string Q(string? value) => Uri.EscapeDataString(value ?? string.Empty);

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();
        if (executed.Result is ViewResult view && User.Identity?.IsAuthenticated == true)
        {
            view.ViewData["UserName"] = User.FindFirstValue(ClaimTypes.Name);
            view.ViewData["UserTitle"] = User.FindFirstValue("ofis360:title");
            view.ViewData["UserRole"] = CurrentRole;
            view.ViewData["AppName"] = User.FindFirstValue("ofis360:app_name") ?? "Ofis360";
            view.ViewData["PresenceAllowed"] = await PresenceAllowedAsync();
        }
    }

    private async Task<bool> PresenceAllowedAsync()
    {
        try
        {
            var access = await Api.GetAsync("api/presence/access", Ct);
            return access.Ok && access.Json.TryGetProperty("allowed", out var allowed) && allowed.GetBoolean();
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }
}
