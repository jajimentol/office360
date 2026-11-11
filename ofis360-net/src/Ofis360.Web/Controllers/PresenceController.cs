using Microsoft.AspNetCore.Mvc;
using Ofis360.Web.Api;

namespace Ofis360.Web.Controllers;

public sealed record PresenceVisibilityRequest(bool ManagersOnly);

/// <summary>"Ofiste kim var": secilen gun masa rezervasyonu olanlar. Kimin gorecegi admin ayariyla belirlenir.</summary>
public sealed class PresenceController(ApiClient api) : WebControllerBase(api)
{
    /// <summary>Sayfa yalnizca ayarin izin verdigi rollere acilir; digerleri kat planina doner.</summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        try
        {
            var access = await Api.GetAsync("api/presence/access", Ct);
            if (!access.Ok) return access.Status == System.Net.HttpStatusCode.Unauthorized ? await ForwardAsync(access) : RedirectToAction("Index", "Plan");
            if (!access.Json.GetProperty("allowed").GetBoolean()) return RedirectToAction("Index", "Plan");
        }
        catch (HttpRequestException)
        {
            return ApiUnavailable();
        }
        return View();
    }

    [HttpGet]
    public Task<IActionResult> Data(string? date, string? floorId, string? departmentId, string? q) =>
        ForwardAsync(Api.GetAsync($"api/presence?date={Q(date)}&floorId={Q(floorId)}&departmentId={Q(departmentId)}&q={Q(q)}", Ct));

    /// <summary>Admin: sayfayi herkese ya da yalnizca yoneticilere ac. Yetki denetimi API'dedir (admin rolu).</summary>
    [HttpPost]
    public Task<IActionResult> Visibility([FromBody] PresenceVisibilityRequest request) =>
        ForwardAsync(Api.PutAsync("api/admin/settings/presence.managers_only", new { value = request.ManagersOnly ? "true" : "false" }, Ct));
}
