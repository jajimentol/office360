using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ofis360.Infrastructure.Persistence;

namespace Ofis360.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController(Ofis360DbContext db) : ControllerBase
{
    /// <summary>Oracle baglantisi (SELECT 1 FROM DUAL) + uygulanmis son migration adi.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        // Veritabani donarsa yoklama beklemez; yuk dengeleyici dugumu saglıksiz isaretleyebilir.
        try
        {
            return await CheckAsync(ct).WaitAsync(TimeSpan.FromSeconds(5), ct);
        }
        catch (TimeoutException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "timeout", database = "oracle", connected = false });
        }
    }

    private async Task<IActionResult> CheckAsync(CancellationToken ct)
    {
        var canConnect = await db.Database.CanConnectAsync(ct);
        if (!canConnect)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                status = "unavailable",
                database = "oracle",
                connected = false,
            });
        }

        var one = await db.Database.SqlQueryRaw<int>("SELECT 1 AS \"Value\" FROM DUAL").SingleAsync(ct);
        var applied = (await db.Database.GetAppliedMigrationsAsync(ct)).ToList();
        var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();

        return Ok(new
        {
            status = pending.Count == 0 ? "ok" : "migrations-pending",
            database = "oracle",
            connected = one == 1,
            schemaVersion = applied.LastOrDefault(),
            appliedMigrations = applied.Count,
            pendingMigrations = pending,
            serverTimeUtc = DateTimeOffset.UtcNow,
        });
    }
}
