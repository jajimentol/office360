using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Ofis360.Application.Common;
using Ofis360.Application.Identity;
using Ofis360.Application.Services;
using Ofis360.Domain.Common;

namespace Ofis360.Api.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>RequireRole filtresi oturumu HttpContext.Items'a yazar; filtresiz uc noktalarda null olabilir.</summary>
    protected SessionUser Session =>
        HttpContext.Items["ofis360.session"] as SessionUser ?? throw AppErrors.Unauthorized();

    protected AuditActor Actor => AuditActor.FromSession(Session);

    /// <summary>Rezervasyon yonetimi yetkisi: baskalarinin kayit ayrintisini gorebilen roller.</summary>
    protected bool ManagesReservations => Session.Role == Ofis360.Domain.Common.Roles.Admin || Session.IsSecretary;

    protected CancellationToken Ct => HttpContext.RequestAborted;

    /// <summary>Kaynak: Zod parse → 400 VALIDATION_ERROR (ilk mesaj + tum hatalar details'ta).</summary>
    protected static T Validate<T>(IValidator<T> validator, T? input)
    {
        if (input is null) throw AppErrors.InvalidJson();
        var result = validator.Validate(input);
        if (result.IsValid) return input;
        var issues = result.Errors.Select(e => new { path = CamelPath(e.PropertyName), message = e.ErrorMessage }).ToList();
        throw AppErrors.Validation(result.Errors[0].ErrorMessage, issues);
    }

    /// <summary>FluentValidation PascalCase yol adini JSON camelCase'e cevirir (Elements[0].Width → elements[0].width).</summary>
    private static string CamelPath(string path) =>
        string.Join(".", path.Split('.').Select(seg => seg.Length > 0 ? char.ToLowerInvariant(seg[0]) + seg[1..] : seg));

    protected static string DayKeyOr(string? value, string fallback) => value is not null && DayKeys.IsValid(value) ? value : fallback;

    protected static string RequireDayKey(string? value)
    {
        if (value is null || !DayKeys.IsValid(value)) throw AppErrors.Validation("Geçerli bir tarih seçin.");
        return value;
    }

    /// <summary>Istege bagli tarih: bossa null, bozuksa 400.</summary>
    protected static string? OptionalDayKey(string? value) => string.IsNullOrWhiteSpace(value) ? null : RequireDayKey(value);

    protected static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
