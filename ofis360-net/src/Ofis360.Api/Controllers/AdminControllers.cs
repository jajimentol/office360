using Microsoft.AspNetCore.Mvc;
using Ofis360.Api.Contracts;
using Ofis360.Api.Identity;
using Ofis360.Application.Common;
using Ofis360.Application.Services;
using Ofis360.Application.Validation;

namespace Ofis360.Api.Controllers;

[Route("api/admin")]
[RequireAdminOrSecretary]
public sealed class AdminReservationsController(DeskReservationService desks, RoomReservationService rooms) : ApiControllerBase
{
    [HttpGet("desk-reservations")]
    public async Task<IActionResult> Desks([FromQuery] string? userId, [FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? status,
        [FromQuery] string? floorId, [FromQuery] string? search, [FromQuery] int? take, [FromQuery] int? skip)
    {
        var page = await desks.ListAsync(new ListDeskReservationsQuery(Blank(userId), OptionalDayKey(from), OptionalDayKey(to), Blank(status), Blank(floorId), Blank(search), take, skip, Session.Role != Domain.Common.Roles.Admin), Ct);
        return Ok(Dto.Map(page, Dto.Map));
    }

    [HttpGet("room-reservations")]
    public async Task<IActionResult> Rooms([FromQuery] string? date, [FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? roomId,
        [FromQuery] string? userId, [FromQuery] string? status, [FromQuery] string? search, [FromQuery] int? take, [FromQuery] int? skip)
    {
        var page = await rooms.ListAsync(new ListRoomReservationsQuery(OptionalDayKey(date), OptionalDayKey(from), OptionalDayKey(to), Blank(roomId), Blank(userId), Blank(status), Blank(search), take, skip, Session.Role != Domain.Common.Roles.Admin), Ct);
        return Ok(Dto.Map(page, Dto.Map));
    }
}

[Route("api/admin")]
[RequireRole(RoleSets.Admin)]
public sealed class AdminUsersController(UserService users, UserImportService import) : ApiControllerBase
{
    [HttpGet("users")]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] string? role, [FromQuery] string? departmentId,
        [FromQuery] string? status, [FromQuery] int? take, [FromQuery] int? skip, [FromQuery] string? grade)
    {
        var page = await users.ListAsync(new UserListFilters(Blank(search), Blank(role), Blank(departmentId), Blank(status), take, skip, Blank(grade)), Ct);
        return Ok(Dto.Map(page, u => Dto.Map(u.User, u.DeskReservationCount, u.RoomReservationCount)));
    }

    [HttpGet("users/stats")]
    public async Task<IActionResult> Stats() => Ok(await users.GetStatsAsync(Ct));

    [HttpPost("users")]
    public async Task<IActionResult> Create([FromBody] UserInput? input)
    {
        var user = await users.CreateAsync(Validate(new UserValidator(), input), Actor, Ct);
        return Ok(new { user.Id, User = Dto.Map(user) });
    }

    /// <summary>Personel listesi: apply=false onizleme yapar ve hicbir sey yazmaz.</summary>
    [HttpPost("users/import")]
    public async Task<IActionResult> Import([FromBody] UserImportInput? input)
    {
        var payload = Validate(new UserImportValidator(), input);
        return Ok(await import.ImportAsync(payload.Csv, payload.Apply, Actor, Ct));
    }

    [HttpPut("users/{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UserUpdateInput? input)
    {
        var user = await users.UpdateAsync(id, Validate(new UserUpdateValidator(), input), Actor, Ct);
        return Ok(new { user.Id, User = Dto.Map(user) });
    }

    [HttpGet("departments")]
    public async Task<IActionResult> Departments() =>
        Ok((await users.ListDepartmentsAsync(Ct)).Select(d => Dto.Map(d.Department, d.UserCount)));
}

[Route("api/admin/rules")]
[RequireRole(RoleSets.Admin)]
public sealed class AdminRulesController(RuleService rules, IAttendanceFeed attendanceFeed) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List() => Ok((await rules.ListAsync(Ct)).Select(Dto.Map));

    /// <summary>Turnike bagli degilken otomatik masa birakma calismaz; admin ekranlari bunu acikca soyler.</summary>
    /// <summary>Kademe degeri uygulanmamis genel kurallar (admin panosu).</summary>
    [HttpGet("settings")]
    public async Task<IActionResult> GeneralSettings() => Ok(await rules.GetRuleSettingsAsync(Ct));

    [HttpGet("attendance-feed")]
    public IActionResult AttendanceFeed() => Ok(new { configured = attendanceFeed.IsConfigured });

    [HttpGet("matrix")]
    public async Task<IActionResult> Matrix([FromQuery] string? buildingId) => Ok(await rules.GetMatrixAsync(Blank(buildingId), Ct));

    [HttpPut("matrix")]
    public async Task<IActionResult> SaveMatrix([FromBody] RuleMatrixInput? input)
    {
        if (input is null) throw AppErrors.InvalidJson();
        return Ok(await rules.SaveMatrixAsync(Blank(input.BuildingId), input.Cells ?? [], Actor, Ct));
    }

    [HttpPut("{key}")]
    public async Task<IActionResult> Update(string key, [FromBody] RuleUpdateInput? input)
    {
        if (input is null) throw AppErrors.InvalidJson();
        var rule = await rules.UpdateAsync(new UpdateRuleCommand(key, input.Enabled, input.IntValue, input.SecondValue), Actor, Ct);
        return Ok(new { rule.Key, Rule = Dto.Map(rule) });
    }
}

[Route("api/admin/room-priority-gaps")]
[RequireRole(RoleSets.Admin)]
public sealed class AdminRoomPriorityGapsController(RoomPolicyService policies) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List() => Ok(await policies.ListPriorityGapsAsync(Ct));
}

[Route("api/admin/rooms/{roomId}/policy")]
[RequireRole(RoleSets.Admin)]
public sealed class AdminRoomPolicyController(RoomPolicyService policies) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(string roomId) => Ok(await policies.GetAsync(roomId, Ct));

    [HttpPut]
    public async Task<IActionResult> Save(string roomId, [FromBody] RoomPolicyInput? input)
    {
        if (input is null) throw AppErrors.InvalidJson();
        return Ok(await policies.SaveAsync(roomId, input, Actor, Ct));
    }
}

[Route("api/people")]
[RequireAdminOrSecretary]
public sealed class PeopleController(UserService users) : ApiControllerBase
{
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? q) => Ok(await users.SearchPeopleAsync(Blank(q), Ct));
}

[Route("api/admin/attendance-report")]
[RequireRole(RoleSets.Admin)]
public sealed class AdminAttendanceController(AttendanceReportService reports, AuditService audit) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? departmentId) =>
        Ok(await reports.BuildAsync(Blank(from), Blank(to), Blank(departmentId), Ct));

    [HttpGet("export")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("export")]
    public async Task<IActionResult> Export([FromQuery] string? type, [FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? departmentId)
    {
        var report = await reports.BuildAsync(Blank(from), Blank(to), Blank(departmentId), Ct);
        var csv = AttendanceReportService.BuildCsv(report, Blank(type) ?? "departman");
        await audit.RecordAsync(Actor, Domain.Common.AuditActions.ReportExport, "Report", $"katilim-{type}",
            $"Katılım raporu dışa aktarıldı: {type} ({report.From} - {report.To}).", new { type, report.From, report.To, departmentId }, Ct);
        return File(new System.Text.UTF8Encoding(false).GetBytes(csv.Csv), "text/csv; charset=utf-8", csv.FileName);
    }
}

[Route("api/admin/holidays")]
[RequireRole(RoleSets.Admin)]
public sealed class AdminHolidaysController(HolidayService holidays) : ApiControllerBase
{
    private static object Map(Domain.Entities.Holiday h) => new { h.Id, h.DayKey, h.Name };

    [HttpGet]
    public async Task<IActionResult> List() => Ok((await holidays.ListAsync(Ct)).Select(Map));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] HolidayInput? input)
    {
        var payload = Validate(new HolidayValidator(), input);
        return Ok(Map(await holidays.CreateAsync(payload.Date, payload.Name.Trim(), Actor, Ct)));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        await holidays.DeleteAsync(id, Actor, Ct);
        return Ok(new { id });
    }
}

[Route("api/admin/closures")]
[RequireRole(RoleSets.Admin)]
public sealed class AdminClosuresController(ClosureService closures) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool? active, [FromQuery] string? scope) =>
        Ok((await closures.ListAsync(active, Blank(scope), Ct)).Select(Dto.Map));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ClosureInput? input)
    {
        var result = await closures.CreateAsync(Validate(new ClosureValidator(), input), Actor, Ct);
        return Ok(new { result.Closure.Id, result.CancelledReservations, Closure = Dto.Map(result.Closure) });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] ClosureUpdateInput? input)
    {
        var closure = await closures.UpdateAsync(id, Validate(new ClosureUpdateValidator(), input), Actor, Ct);
        return Ok(new { closure.Id, Closure = Dto.Map(closure) });
    }
}

[Route("api/admin/settings")]
[RequireRole(RoleSets.Admin)]
public sealed class AdminSettingsController(SettingService settings) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List() => Ok((await settings.ListAsync(Ct)).Select(Dto.Map));

    [HttpPut("{key}")]
    public async Task<IActionResult> Update(string key, [FromBody] SettingUpdateInput? input)
    {
        var payload = Validate(new SettingUpdateValidator(), input);
        var setting = await settings.UpdateAsync(key, payload.Value, Actor, Ct);
        return Ok(new { setting.Key, Setting = Dto.Map(setting) });
    }
}

[Route("api/admin/audit")]
[RequireRole(RoleSets.Admin)]
public sealed class AdminAuditController(AuditService audit) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? action, [FromQuery] string? entityType, [FromQuery] string? actorId,
        [FromQuery] string? search, [FromQuery] int? take, [FromQuery] int? skip)
    {
        var page = await audit.ListAsync(new AuditQuery(Blank(action), Blank(entityType), Blank(actorId), Blank(search), take, skip), Ct);
        return Ok(Dto.Map(page, Dto.Map));
    }
}
