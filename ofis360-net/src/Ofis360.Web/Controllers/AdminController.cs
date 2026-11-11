using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ofis360.Web.Api;

namespace Ofis360.Web.Controllers;

/// <summary>
/// Admin ekranlari. Her ekran bir Razor gorunumu + AngularJS controller'idir. Veri icin AngularJS yalnizca
/// <c>/Admin/Api/...</c> aksiyonunu cagirir; bu aksiyon istegi API'nin <c>/api/admin/...</c> uclarina iletir.
/// Yetki API'de de denetlenir (admin rolu; rezervasyon listesi icin admin veya Sekreter).
/// </summary>
[Authorize(Roles = "ADMIN")]
public sealed class AdminController(ApiClient api) : WebControllerBase(api)
{
    [HttpGet] public IActionResult Index() => Screen("Admin panosu", "AdminHomeCtrl");
    [HttpGet] public IActionResult Floors() => Screen("Kat ve alan yönetimi", "AdminFloorsCtrl");
    [HttpGet] public IActionResult PlanEditor() => Screen("Kat planı düzenleyici", "PlanEditorCtrl");
    [HttpGet] public IActionResult Desks() => Screen("Masa yönetimi", "AdminDesksCtrl");
    [HttpGet] public IActionResult Rooms() => Screen("Toplantı odaları", "AdminRoomsCtrl");
    [HttpGet] public IActionResult Users() => Screen("Kullanıcı ve roller", "AdminUsersCtrl");
    [HttpGet] public IActionResult Rules() => Screen("Rezervasyon kuralları", "AdminRulesCtrl");
    [HttpGet] public IActionResult Closures() => Screen("Bakım ve kapatmalar", "AdminClosuresCtrl");
    [HttpGet] public IActionResult Attendance() => Screen("Katılım raporu", "AdminAttendanceCtrl");
    [HttpGet] public IActionResult Audit() => Screen("Denetim kayıtları", "AdminAuditCtrl");
    [HttpGet] public IActionResult Settings() => Screen("Sistem ayarları", "AdminSettingsCtrl");

    /// <summary>Rezervasyon yonetimi: admin ve Sekreter yetkili kullanicilar.</summary>
    [HttpGet]
    [AllowAnonymous]
    public IActionResult Reservations()
    {
        if (User.Identity?.IsAuthenticated != true) return Challenge();
        if (!IsAdmin && !IsSecretary) return RedirectToAction("Index", "Plan");
        return Screen("Rezervasyon yönetimi", "AdminReservationsCtrl");
    }

    /// <summary>Genel iletim: /Admin/Api/{yol} → /api/admin/{yol}. Rezervasyon listeleri Sekreter icin de acik.</summary>
    [AcceptVerbs("GET", "POST", "PUT", "DELETE")]
    [Route("Admin/Api/{**path}")]
    [AllowAnonymous]
    public Task<IActionResult> Proxy(string? path)
    {
        if (User.Identity?.IsAuthenticated != true) return Task.FromResult<IActionResult>(Unauthorized());
        var reservations = path is "desk-reservations" or "room-reservations" && HttpMethods.IsGet(Request.Method);
        if (!IsAdmin && !(reservations && IsSecretary)) return Task.FromResult<IActionResult>(Forbid());
        return ProxyAsync("api/admin", path);
    }

    /// <summary>Kisi arama (rezervasyonu baskasi adina yapma, kullanici secimi): /api/people/search.</summary>
    [HttpGet]
    [AllowAnonymous]
    public Task<IActionResult> People()
    {
        if (User.Identity?.IsAuthenticated != true) return Task.FromResult<IActionResult>(Unauthorized());
        if (!IsAdmin && !IsSecretary) return Task.FromResult<IActionResult>(Forbid());
        return ProxyAsync("api/people/search", null);
    }

    /// <summary>Panodaki kat ozetleri (calisan ucu, salt okunur).</summary>
    [HttpGet]
    [AllowAnonymous]
    public Task<IActionResult> FloorSummaries(string? date)
    {
        if (User.Identity?.IsAuthenticated != true) return Task.FromResult<IActionResult>(Unauthorized());
        return ForwardAsync(Api.GetAsync($"api/floors/summaries?date={Q(date)}", Ct));
    }

    /// <summary>Duzenleyicide kroki onizlemesi icin calisan plan gorunumu.</summary>
    [HttpGet]
    public Task<IActionResult> PlanView(string floorId, string? date) => ForwardAsync(Api.GetAsync($"api/floors/{Q(floorId)}/plan?date={Q(date)}", Ct));

    private ViewResult Screen(string title, string ngController)
    {
        ViewData["Title"] = title;
        ViewData["NgController"] = ngController + " as vm";
        ViewData["Page"] = "page-admin";
        return View(ControllerContext.ActionDescriptor.ActionName);
    }
}

/// <summary>Yonetici analitigi. /Manager/Api/{yol} → /api/analytics/{yol}.</summary>
[Authorize(Roles = "MANAGER,ADMIN")]
public sealed class ManagerController(ApiClient api) : WebControllerBase(api)
{
    [HttpGet]
    public IActionResult Index()
    {
        ViewData["Title"] = "Ofis analitiği";
        ViewData["NgController"] = "AnalyticsCtrl as vm";
        ViewData["Page"] = "page-analytics";
        return View();
    }

    [AcceptVerbs("GET")]
    [Route("Manager/Api/{**path}")]
    public Task<IActionResult> Proxy(string? path) => ProxyAsync("api/analytics", path);
}
