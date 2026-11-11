using Microsoft.AspNetCore.Mvc;
using Ofis360.Web.Api;

namespace Ofis360.Web.Controllers;

public sealed record DeskReserveRequest(string? DeskId, string? Date, string? Note, string? TargetUserId = null);
public sealed record CancelRequest(string? Reason);
public sealed record RoomReserveRequest(string? RoomId, string? Date, int StartMinutes, int EndMinutes, string? Title, string? Purpose, int AttendeeCount, string? TargetUserId = null);

/// <summary>Kat plani ekrani: tarih seridi, kroki, masa ve toplanti odasi rezervasyonu.</summary>
public sealed class PlanController(ApiClient api) : WebControllerBase(api)
{
    [HttpGet]
    public IActionResult Index() => View();

    /// <summary>Ekran baglami: katlar, son rezerve edilebilir gun, kapali gunler, kurallar.</summary>
    [HttpGet]
    public Task<IActionResult> Context() => ForwardAsync(Api.GetAsync("api/me/context", Ct));

    /// <summary>Secilen kat ve gun icin kroki; masa durumlari ve odalarin gunluk dolulugu (FREE / PARTIAL / FULL).</summary>
    [HttpGet]
    public Task<IActionResult> Data(string floorId, string? date, string? asUser) =>
        ForwardAsync(Api.GetAsync($"api/floors/{Q(floorId)}/plan?date={Q(date)}&asUser={Q(asUser)}", Ct));

    /// <summary>Kattaki odalarin o gunku dolu/bos dilimleri (mesai saatleri, kural tanimli dilim adimi).</summary>
    [HttpGet]
    public Task<IActionResult> Rooms(string floorId, string? date) =>
        ForwardAsync(Api.GetAsync($"api/rooms/availability?floorId={Q(floorId)}&date={Q(date)}", Ct));

    [HttpPost]
    public Task<IActionResult> ReserveDesk([FromBody] DeskReserveRequest request) =>
        ForwardAsync(Api.PostAsync("api/desk-reservations", new { deskId = request.DeskId, date = request.Date, note = request.Note, targetUserId = request.TargetUserId }, Ct));

    [HttpPost]
    public Task<IActionResult> CancelDesk(string id, [FromBody] CancelRequest? request) =>
        ForwardAsync(Api.PostAsync($"api/desk-reservations/{Q(id)}/cancel", new { action = "cancel", reason = request?.Reason }, Ct));

    [HttpPost]
    public Task<IActionResult> CheckIn(string id) =>
        ForwardAsync(Api.PostAsync($"api/desk-reservations/{Q(id)}/check-in", null, Ct));

    [HttpPost]
    public Task<IActionResult> ReserveRoom([FromBody] RoomReserveRequest request) =>
        ForwardAsync(Api.PostAsync("api/room-reservations", new
        {
            roomId = request.RoomId, date = request.Date, startMinutes = request.StartMinutes, endMinutes = request.EndMinutes,
            title = request.Title, purpose = request.Purpose, attendeeCount = request.AttendeeCount, targetUserId = request.TargetUserId,
        }, Ct));

    [HttpPost]
    public Task<IActionResult> CancelRoom(string id, [FromBody] CancelRequest? request) =>
        ForwardAsync(Api.PostAsync($"api/room-reservations/{Q(id)}/cancel", new { action = "cancel", reason = request?.Reason }, Ct));

    [HttpPost]
    public Task<IActionResult> StartMeeting(string id) =>
        ForwardAsync(Api.PostAsync($"api/room-reservations/{Q(id)}/start", null, Ct));
}
