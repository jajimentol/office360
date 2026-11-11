using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ofis360.Api.Identity;
using Ofis360.Application.Common;
using Ofis360.Application.Persistence;
using Ofis360.Application.Services;
using Ofis360.Domain.Common;

namespace Ofis360.Api.Controllers;

/// <summary>Yonetici raporlari (MANAGER, ADMIN). Kaynak: yonetim/page.tsx + api/raporlar/disa-aktar.</summary>
[Route("api/analytics")]
[RequireRole(RoleSets.Manager, RoleSets.Admin)]
public sealed class AnalyticsController(AnalyticsService analytics, AuditService audit, IOfis360Db db) : ApiControllerBase
{
    private AnalyticsFilters BuildFilters(string? from, string? to, string? floor, string? department, string? resource)
    {
        var (defaultFrom, defaultTo) = analytics.DefaultRange();
        var fromDayKey = DayKeyOr(from, defaultFrom);
        var toDayKey = DayKeyOr(to, defaultTo);
        if (!AnalyticsService.ValidateRange(fromDayKey, toDayKey)) throw AppErrors.Validation("Geçersiz tarih aralığı.");
        var resourceType = resource is "DESK" or "ROOM" ? resource : "ALL";
        return new AnalyticsFilters(fromDayKey, toDayKey, Blank(floor), Blank(department), resourceType);
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? floor, [FromQuery] string? department, [FromQuery] string? resource) =>
        Ok(await analytics.GetAnalyticsAsync(BuildFilters(from, to, floor, department, resource), Ct));

    /// <summary>
    /// Kaynak yonetim/page.tsx'in sunucuda dogrudan Prisma ile cektigi filtre secenekleri ve isi haritasi icin kat krokileri
    /// (yalnizca aktif binalarin rezervasyona acik aktif katlari, aktif departmanlar).
    /// </summary>
    [HttpGet("context")]
    public async Task<IActionResult> Context()
    {
        var floors = await db.Floors.AsNoTracking()
            .Where(f => f.Active && f.Reservable && f.Building.Active)
            .OrderBy(f => f.Building.Order).ThenBy(f => f.Number)
            .Select(f => new { f.Id, f.Name, f.PlanWidth, f.PlanHeight })
            .ToListAsync(Ct);
        var floorIds = floors.Select(f => f.Id).ToList();
        var elements = await db.FloorPlanElements.AsNoTracking()
            .Where(e => floorIds.Contains(e.FloorId))
            .OrderBy(e => e.LayerOrder).ThenBy(e => e.CreatedAt)
            .ToListAsync(Ct);
        var desks = await db.Desks.AsNoTracking()
            .Where(d => floorIds.Contains(d.FloorId))
            .OrderBy(d => d.Code)
            .Select(d => new { d.Id, d.FloorId, d.Code, d.X, d.Y, d.Width, d.Height, d.Rotation })
            .ToListAsync(Ct);
        var departments = await db.Departments.AsNoTracking()
            .Where(d => d.Active).OrderBy(d => d.Name)
            .Select(d => new { d.Id, d.Name })
            .ToListAsync(Ct);

        return Ok(new
        {
            Floors = floors.Select(f => new { f.Id, f.Name }),
            Departments = departments,
            PlanFloors = floors.Select(f => new
            {
                f.Id, f.Name, f.PlanWidth, f.PlanHeight,
                Elements = elements.Where(e => e.FloorId == f.Id).Select(AvailabilityService.ToPlanElement),
                Desks = desks.Where(d => d.FloorId == f.Id).Select(d => new { d.Id, d.Code, d.X, d.Y, d.Width, d.Height, d.Rotation }),
            }),
        });
    }

    [HttpGet("export")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("export")]
    public async Task<IActionResult> Export([FromQuery] string? type, [FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? floor, [FromQuery] string? department, [FromQuery] string? resource)
    {
        var reportType = string.IsNullOrWhiteSpace(type) ? "ozet" : type;
        if (!ReportCsvBuilder.KnownTypes.Contains(reportType)) throw AppErrors.Validation("Geçersiz rapor türü.");
        var filters = BuildFilters(from, to, floor, department, resource);
        var result = await analytics.GetAnalyticsAsync(filters, Ct);
        var report = ReportCsvBuilder.Build(reportType, result, filters.FromDayKey, filters.ToDayKey);

        await audit.RecordAsync(Actor, AuditActions.ReportExport, "Report", reportType,
            $"\"{reportType}\" raporu CSV olarak dışa aktarıldı ({filters.FromDayKey} - {filters.ToDayKey}).",
            new { type = reportType, fromDayKey = filters.FromDayKey, toDayKey = filters.ToDayKey }, Ct);

        Response.Headers.CacheControl = "no-store";
        // BOM metnin icinde (TextUtils.ToCsv); encoder ikinci bir BOM eklemez.
        return File(new UTF8Encoding(false).GetBytes(report.Csv), "text/csv; charset=utf-8", report.FileName);
    }
}
