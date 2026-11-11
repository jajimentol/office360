using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Ofis360.Api.Contracts;
using Ofis360.Api.Identity;
using Ofis360.Application.Common;
using Ofis360.Application.Identity;
using Ofis360.Application.Persistence;
using Ofis360.Application.Services;
using Ofis360.Application.Validation;
using Ofis360.Domain.Common;

namespace Ofis360.Api.Controllers;

public sealed record AuthSettings(string Mode)
{
    public bool DemoEnabled => Mode == AuthModes.Demo;
}

public sealed record HandoffInput(string? Ticket);

/// <summary>Oturum acma / kapatma. Demo uclari yalnizca Demo modunda vardir.</summary>
[Route("api/auth")]
public sealed class AuthController(IIdentityProvider identity, IOfis360Db db, UserService users, AuditService audit, SettingService settings,
    AuthSettings authSettings, SsoSignInService sso) : ApiControllerBase
{
    private void RequireDemo()
    {
        if (!authSettings.DemoEnabled) throw AppErrors.NotFound("Kaynak bulunamadı.");
    }

    /// <summary>Giris ekraninin hangi yontemi gosterecegi. Durum: DEMO, READY veya CONFIG_ERROR.</summary>
    [HttpGet("options")]
    public IActionResult Options() => Ok(new
    {
        mode = authSettings.Mode,
        status = authSettings.DemoEnabled ? "DEMO" : sso.IsConfigured ? "READY" : "CONFIG_ERROR",
    });

    /// <summary>Kurumsal yonlendirme: bilet dogrulanir, kisiye oturum acilir.</summary>
    [HttpPost("handoff")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Handoff([FromBody] HandoffInput? input)
    {
        if (authSettings.DemoEnabled) throw AppErrors.NotFound("Kaynak bulunamadı.");
        if (string.IsNullOrWhiteSpace(input?.Ticket)) throw AppErrors.Unauthorized();

        var user = await sso.ResolveUserAsync(input.Ticket, Ct);
        await identity.SignInAsync(user.Id, Ct);
        await audit.RecordAsync(new AuditActor(user.Id, user.FullName, user.Role), AuditActions.Login, "User", user.Id,
            $"{user.FullName} kurumsal girişle oturum açtı.", null, Ct);
        return Ok(new { role = user.Role, defaultRoute = DefaultRouteForRole(user.Role) });
    }

    [HttpGet("demo-accounts")]
    public async Task<IActionResult> DemoAccounts()
    {
        RequireDemo();
        return Ok((await users.ListDemoAccountsAsync(Ct)).Select(u => Dto.Map(u)));
    }

    /// <summary>Kaynak giris/page.tsx: tanitim panelindeki sayaclar (rezervasyona acik masa, toplanti odasi, kat).</summary>
    [HttpGet("intro")]
    public async Task<IActionResult> Intro()
    {
        var deskCount = await db.Desks.AsNoTracking().CountAsync(d => d.Active && d.Placed && d.Zone.Reservable && d.Floor.Reservable && d.Floor.Active && d.Floor.Building.Active, Ct);
        var roomCount = await db.MeetingRooms.AsNoTracking().CountAsync(r => r.Active && r.Placed && r.Floor.Active && r.Floor.Building.Active, Ct);
        var floorCount = await db.Floors.AsNoTracking().CountAsync(f => f.Active && f.Building.Active, Ct);
        return Ok(new { deskCount, roomCount, floorCount });
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginInput? input)
    {
        RequireDemo();
        var payload = Validate(new LoginValidator(), input);
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == payload.UserId && u.Active, Ct)
            ?? throw AppErrors.NotFound("Kullanıcı bulunamadı veya pasif durumda.");

        await identity.SignInAsync(user.Id, Ct);
        await audit.RecordAsync(new AuditActor(user.Id, user.FullName, user.Role), AuditActions.Login, "User", user.Id,
            $"{user.FullName} demo hesabıyla oturum açtı.", null, Ct);

        return Ok(new { role = user.Role, defaultRoute = DefaultRouteForRole(user.Role) });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var user = await identity.ResolveAsync(Ct);
        if (user is not null)
        {
            await audit.RecordAsync(new AuditActor(user.Id, user.FullName, user.Role), AuditActions.Logout, "User", user.Id,
                $"{user.FullName} oturumu kapattı.", null, Ct);
        }
        await identity.SignOutAsync(Ct);
        return NoContent();
    }

    /// <summary>Kaynak (app)/layout.tsx: oturum + kabuk icin uygulama adi (app.name ayari) birlikte doner.</summary>
    [HttpGet("me")]
    [RequireRole]
    public async Task<IActionResult> Me()
    {
        var map = await settings.GetMapAsync(Ct);
        var appName = map.GetValueOrDefault(SettingKeys.AppName);
        return Ok(new { user = Session, demoMode = authSettings.DemoEnabled, defaultRoute = DefaultRouteForRole(Session.Role), appName = string.IsNullOrWhiteSpace(appName) ? AppConfig.Name : appName });
    }

    /// <summary>Rolune gore kullanicinin varsayilan baslangic sayfasi.</summary>
    public static string DefaultRouteForRole(string role) => role switch
    {
        Roles.Admin => "/admin",
        Roles.Manager => "/yonetim",
        _ => "/panel",
    };
}
