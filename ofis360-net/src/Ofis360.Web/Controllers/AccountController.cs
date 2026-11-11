using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ofis360.Web.Api;

namespace Ofis360.Web.Controllers;

public sealed record DemoLoginRequest(string? UserId);

/// <summary>
/// Giris ve cikis. Demo modunda API'nin demo hesaplari listelenir; secilen hesapla API'de oturum acilir ve donen bilet
/// MVC'nin sifreli kimlik cerezine konur. Tarayici API biletini hic gormez.
/// </summary>
public sealed class AccountController(ApiClient api) : WebControllerBase(api)
{
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true) return LocalRedirect(SafeReturnUrl(returnUrl));
        ViewData["ReturnUrl"] = SafeReturnUrl(returnUrl);
        return View();
    }

    /// <summary>Giris ekrani verisi: giris yontemi, demo hesaplar ve tanitim sayaclari.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Options()
    {
        try
        {
            var options = await Api.GetAsync("api/auth/options", Ct);
            if (!options.Ok) return await ForwardAsync(options);
            var mode = options.Json.GetProperty("mode").GetString();
            var status = options.Json.GetProperty("status").GetString();
            var accounts = status == "DEMO" ? await Api.GetAsync("api/auth/demo-accounts", Ct) : null;
            var intro = await Api.GetAsync("api/auth/intro", Ct);
            return Json(new
            {
                mode,
                status,
                accounts = accounts is { Ok: true } ? accounts.Json : (JsonElement?)null,
                intro = intro.Ok ? intro.Json : (JsonElement?)null,
            });
        }
        catch (HttpRequestException)
        {
            return ApiUnavailable();
        }
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] DemoLoginRequest? request, string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(request?.UserId)) return BadRequest(new { error = new { code = "VALIDATION_ERROR", message = "Bir hesap seçin." } });
        try
        {
            var login = await Api.PostAsync("api/auth/login", new { userId = request.UserId }, Ct);
            if (!login.Ok) return await ForwardAsync(login);
            var token = ApiClient.ExtractSessionToken(login.SetCookies);
            if (token is null) return ApiUnavailable();

            var me = await Api.SendAsync(HttpMethod.Get, "api/auth/me", null, Ct, token);
            if (!me.Ok) return await ForwardAsync(me);
            var user = me.Json.GetProperty("user");

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.GetProperty("id").GetString()!),
                new(ClaimTypes.Name, user.GetProperty("fullName").GetString()!),
                new(ClaimTypes.Role, user.GetProperty("role").GetString()!),
                new("ofis360:title", user.TryGetProperty("title", out var title) ? title.GetString() ?? "" : ""),
                new("ofis360:department", user.TryGetProperty("departmentName", out var dep) ? dep.GetString() ?? "" : ""),
                new("ofis360:app_name", me.Json.TryGetProperty("appName", out var appName) ? appName.GetString() ?? "Ofis360" : "Ofis360"),
                new("ofis360:secretary", user.TryGetProperty("isSecretary", out var sec) && sec.ValueKind == JsonValueKind.True ? "true" : "false"),
                new(ApiClient.TokenClaim, token),
            };
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
            return Json(new { redirect = SafeReturnUrl(returnUrl) });
        }
        catch (HttpRequestException)
        {
            return ApiUnavailable();
        }
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        try
        {
            await Api.PostAsync("api/auth/logout", null, Ct);
        }
        catch (HttpRequestException)
        {
            // API'ye ulasilamasa da MVC oturumu kapatilir; API bileti suresi dolunca gecersizlesir.
        }
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Error() => View();

    private string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) && !returnUrl.StartsWith("/Account", StringComparison.OrdinalIgnoreCase) ? returnUrl : "/Home";
}
