using Microsoft.AspNetCore.Mvc;
using Ofis360.Web.Api;

namespace Ofis360.Web.Controllers;

public sealed record PreferencesRequest(string? PreferredFloorId, bool? PrefersQuietZone, bool? NotificationsEnabled, bool ClearPreferredFloor = false);

/// <summary>Calisan ana sayfasi (eski /panel): bugunku masa, yaklasan kayitlar, adil kullanim hakki, duyuru, kat dolulugu.</summary>
public sealed class HomeController(ApiClient api) : WebControllerBase(api)
{
    [HttpGet]
    public IActionResult Index()
    {
        ViewData["Title"] = "Ana sayfa";
        ViewData["NgController"] = "HomeCtrl as vm";
        ViewData["Page"] = "page-home";
        return View();
    }

    [HttpGet]
    public Task<IActionResult> Data() => ForwardAsync(Api.GetAsync("api/panel/home", Ct));
}

/// <summary>Toplanti odasi rezervasyonu (eski /panel/toplanti): takvim ve liste gorunumu, saat/kapasite/kat/donanim filtreleri.</summary>
public sealed class RoomsController(ApiClient api) : WebControllerBase(api)
{
    [HttpGet]
    public IActionResult Index()
    {
        ViewData["Title"] = "Toplantı odası rezervasyonu";
        ViewData["NgController"] = "RoomsCtrl as vm";
        ViewData["Page"] = "page-rooms";
        return View();
    }

    [HttpGet]
    public Task<IActionResult> Availability(string? date, int? start, int? end, int? minCapacity, string? floorId, string? features)
    {
        var query = new List<string> { "date=" + Q(date) };
        if (start is not null && end is not null) { query.Add($"start={start}"); query.Add($"end={end}"); }
        if (minCapacity is > 0) query.Add($"minCapacity={minCapacity}");
        if (!string.IsNullOrWhiteSpace(floorId)) query.Add("floorId=" + Q(floorId));
        if (!string.IsNullOrWhiteSpace(features)) query.Add("features=" + Q(features));
        return ForwardAsync(Api.GetAsync("api/rooms/availability?" + string.Join('&', query), Ct));
    }
}

/// <summary>Kisisel sayfalar: kullanim ozetim, bildirim merkezi, profil ve tercihler.</summary>
public sealed class MeController(ApiClient api) : WebControllerBase(api)
{
    [HttpGet] public IActionResult Usage() => Screen("Kullanım özetim", "UsageCtrl");
    [HttpGet] public IActionResult Notifications() => Screen("Bildirim merkezi", "NotificationsCtrl");
    [HttpGet] public IActionResult Profile() => Screen("Profil ve tercihler", "ProfileCtrl");

    [HttpGet] public Task<IActionResult> UsageData() => ForwardAsync(Api.GetAsync("api/me/usage", Ct));
    [HttpGet] public Task<IActionResult> NotificationList(int? take) => ForwardAsync(Api.GetAsync($"api/me/notifications?take={take ?? 50}", Ct));
    [HttpGet] public Task<IActionResult> UnreadCount() => ForwardAsync(Api.GetAsync("api/me/notifications/unread-count", Ct));
    [HttpPost] public Task<IActionResult> MarkRead(string id) => ForwardAsync(Api.PostAsync($"api/me/notifications/{Q(id)}/read", null, Ct));
    [HttpPost] public Task<IActionResult> MarkAllRead() => ForwardAsync(Api.PostAsync("api/me/notifications/read-all", null, Ct));
    [HttpGet] public Task<IActionResult> ProfileData() => ForwardAsync(Api.GetAsync("api/auth/me", Ct));
    [HttpGet] public Task<IActionResult> Context() => ForwardAsync(Api.GetAsync("api/me/context", Ct));

    /// <summary>Tercihler: tercih edilen kat (bos = temizle), sessiz alan, bildirimler.</summary>
    [HttpPost]
    public Task<IActionResult> Preferences([FromBody] PreferencesRequest request)
    {
        var body = new Dictionary<string, object?>();
        if (request.ClearPreferredFloor) body["preferredFloorId"] = null;
        else if (request.PreferredFloorId is not null) body["preferredFloorId"] = request.PreferredFloorId;
        if (request.PrefersQuietZone is not null) body["prefersQuietZone"] = request.PrefersQuietZone;
        if (request.NotificationsEnabled is not null) body["notificationsEnabled"] = request.NotificationsEnabled;
        return ForwardAsync(Api.PutAsync("api/me/preferences", body, Ct));
    }

    private ViewResult Screen(string title, string ngController)
    {
        ViewData["Title"] = title;
        ViewData["NgController"] = ngController + " as vm";
        ViewData["Page"] = "page-me";
        return View(ControllerContext.ActionDescriptor.ActionName);
    }
}
