using Microsoft.AspNetCore.Mvc;
using Ofis360.Api.Contracts;
using Ofis360.Api.Identity;
using Ofis360.Application.Common;
using Ofis360.Application.Services;
using Ofis360.Application.Validation;

namespace Ofis360.Api.Controllers;

[Route("api/admin/buildings")]
[RequireRole(RoleSets.Admin)]
public sealed class AdminBuildingsController(SpaceService space) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool includeInactive = false) =>
        Ok((await space.ListBuildingsAsync(includeInactive, Ct)).Select(b => Dto.Map(b.Building, b.FloorCount)));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] BuildingInput? input)
    {
        var building = await space.CreateBuildingAsync(Validate(new BuildingValidator(), input), Actor, Ct);
        return Ok(new { building.Id, Building = Dto.Map(building) });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] BuildingUpdateInput? input)
    {
        if (input is null) throw AppErrors.InvalidJson();
        if (input.Code is not null || input.Name is not null || input.Address is not null || input.Order is not null)
        {
            // Zod partial: verilen alanlar tam sema sinirlariyla dogrulanir.
            var probe = new BuildingInput(input.Code ?? "XX", input.Name ?? "XX", input.Address ?? "", input.Order ?? 0, input.Active ?? true);
            Validate(new BuildingValidator(), probe);
        }
        var building = await space.UpdateBuildingAsync(id, input, Actor, Ct);
        return Ok(new { building.Id, Building = Dto.Map(building) });
    }
}

[Route("api/admin")]
[RequireRole(RoleSets.Admin)]
public sealed class AdminFloorsController(SpaceService space, FloorPlanService plans) : ApiControllerBase
{
    [HttpGet("floors")]
    public async Task<IActionResult> List([FromQuery] string? buildingId, [FromQuery] bool onlyActiveBuildings = false) =>
        Ok((await space.ListFloorsAsync(Blank(buildingId), onlyActiveBuildings, Ct)).Select(f => Dto.Map(f.Floor, f.DeskCount, f.RoomCount)));

    [HttpGet("floors/capacity")]
    public async Task<IActionResult> Capacity() => Ok(await space.GetFloorCapacityStatsAsync(Ct));

    [HttpGet("floors/{id}")]
    public async Task<IActionResult> Detail(string id)
    {
        var floor = await space.GetFloorDetailAsync(id, Ct);
        return Ok(new
        {
            Floor = Dto.Map(floor),
            Desks = floor.Desks.Select(d => Dto.Map(d)),
            Rooms = floor.Rooms.Select(r => Dto.Map(r)),
        });
    }

    [HttpPost("floors")]
    public async Task<IActionResult> Create([FromBody] FloorInput? input)
    {
        var floor = await space.CreateFloorAsync(Validate(new FloorValidator(), input), Actor, Ct);
        return Ok(new { floor.Id, Floor = Dto.Map(floor) });
    }

    [HttpPut("floors/{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] FloorUpdateInput? input)
    {
        var floor = await space.UpdateFloorAsync(id, Validate(new FloorUpdateValidator(), input), Actor, Ct);
        return Ok(new { floor.Id, Floor = Dto.Map(floor) });
    }

    [HttpPut("zones/{id}")]
    public async Task<IActionResult> UpdateZone(string id, [FromBody] ZoneUpdateInput? input)
    {
        var zone = await space.UpdateZoneAsync(id, Validate(new ZoneUpdateValidator(), input), Actor, Ct);
        return Ok(new { zone.Id, Zone = Dto.Map(zone) });
    }

    // --- Kroki (kaynak: floor-plan.actions.ts) ---

    [HttpGet("floors/{id}/plan-snapshot")]
    public async Task<IActionResult> Snapshot(string id) => Ok(await plans.GetSnapshotAsync(id, Ct));

    [HttpPut("floors/{id}/plan")]
    public async Task<IActionResult> SavePlan(string id, [FromBody] SavePlanInput? input)
    {
        var payload = Validate(new SavePlanValidator(), input);
        if (payload.FloorId != id) throw AppErrors.Validation("Kat kimliği uyuşmuyor.");
        return Ok(await plans.SaveAsync(payload, Actor, Ct));
    }

    [HttpPost("floors/{id}/plan/auto-place")]
    public async Task<IActionResult> AutoPlace(string id) => Ok(await plans.AutoPlacePoolItemsAsync(id, Actor, Ct));

    [HttpGet("floors/{id}/plan/copy-from/{sourceId}")]
    public async Task<IActionResult> CopyFrom(string id, string sourceId) =>
        Ok(new { elements = await plans.CopyPlanFromFloorAsync(sourceId, Ct) });

    [HttpPost("floors/{id}/plan/desks")]
    public async Task<IActionResult> QuickDesk(string id, [FromBody] QuickDeskInput? input)
    {
        var payload = Validate(new QuickDeskValidator(), input);
        if (payload.FloorId != id) throw AppErrors.Validation("Kat kimliği uyuşmuyor.");
        return Ok(await plans.CreateDeskFromPlanAsync(payload, Actor, Ct));
    }

    [HttpPost("floors/{id}/plan/rooms")]
    public async Task<IActionResult> QuickRoom(string id, [FromBody] QuickRoomInput? input)
    {
        var payload = Validate(new QuickRoomValidator(), input);
        if (payload.FloorId != id) throw AppErrors.Validation("Kat kimliği uyuşmuyor.");
        return Ok(await plans.CreateRoomFromPlanAsync(payload, Actor, Ct));
    }
}

[Route("api/admin")]
[RequireRole(RoleSets.Admin)]
public sealed class AdminDesksController(SpaceService space) : ApiControllerBase
{
    [HttpGet("desks")]
    public async Task<IActionResult> List([FromQuery] string? floorId, [FromQuery] string? search, [FromQuery] string? placement,
        [FromQuery] bool onlyInactive = false, [FromQuery] int? take = null, [FromQuery] int? skip = null)
    {
        var page = await space.ListDesksAsync(new DeskListFilters(Blank(floorId), Blank(search), onlyInactive, Blank(placement), take, skip), Ct);
        return Ok(Dto.Map(page, d => Dto.Map(d.Desk, d.ReservationCount)));
    }

    [HttpPost("desks")]
    public async Task<IActionResult> Create([FromBody] DeskInput? input)
    {
        var desk = await space.CreateDeskAsync(Validate(new DeskValidator(), input), Actor, Ct);
        return Ok(new { desk.Id, Desk = Dto.Map(desk) });
    }

    [HttpPut("desks/{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] DeskUpdateInput? input)
    {
        var desk = await space.UpdateDeskAsync(id, Validate(new DeskUpdateValidator(), input), Actor, Ct);
        return Ok(new { desk.Id, Desk = Dto.Map(desk) });
    }

    [HttpDelete("desks/{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        await space.DeleteDeskAsync(id, Actor, Ct);
        return Ok(new { deleted = true });
    }

    [HttpGet("desk-features")]
    public async Task<IActionResult> Features() => Ok((await space.ListDeskFeaturesAsync(Ct)).Select(Dto.Map));
}

[Route("api/admin")]
[RequireRole(RoleSets.Admin)]
public sealed class AdminRoomsController(SpaceService space) : ApiControllerBase
{
    [HttpGet("rooms")]
    public async Task<IActionResult> List([FromQuery] string? floorId, [FromQuery] string? search) =>
        Ok((await space.ListRoomsAsync(Blank(floorId), Blank(search), Ct)).Select(r => Dto.Map(r.Room, r.ReservationCount)));

    [HttpPost("rooms")]
    public async Task<IActionResult> Create([FromBody] RoomInput? input)
    {
        var room = await space.CreateRoomAsync(Validate(new RoomValidator(), input), Actor, Ct);
        return Ok(new { room.Id, Room = Dto.Map(room) });
    }

    [HttpPut("rooms/{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] RoomUpdateInput? input)
    {
        var room = await space.UpdateRoomAsync(id, Validate(new RoomUpdateValidator(), input), Actor, Ct);
        return Ok(new { room.Id, Room = Dto.Map(room) });
    }

    [HttpDelete("rooms/{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        await space.DeleteRoomAsync(id, Actor, Ct);
        return Ok(new { deleted = true });
    }

    [HttpGet("room-features")]
    public async Task<IActionResult> Features() => Ok((await space.ListRoomFeaturesAsync(Ct)).Select(Dto.Map));
}
