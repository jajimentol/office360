using Microsoft.AspNetCore.Mvc;
using Ofis360.Web.Api;

namespace Ofis360.Web.Controllers;

/// <summary>Rezervasyonlarim: yaklasan ve gecmis masa / oda kayitlari. Iptal ve check-in PlanController uzerinden.</summary>
public sealed class BookingsController(ApiClient api) : WebControllerBase(api)
{
    [HttpGet]
    public IActionResult Index() => View();

    [HttpGet]
    public Task<IActionResult> List() => ForwardAsync(Api.GetAsync("api/me/reservations", Ct));
}
