using Microsoft.AspNetCore.Mvc.Filters;
using Ofis360.Api.Middleware;
using Ofis360.Application.Common;
using Ofis360.Application.Identity;
using Ofis360.Domain.Common;

namespace Ofis360.Api.Identity;

/// <summary>
/// Yetki denetimi model baglamadan once calisir: kimligi dogrulanmamis istegin govdesi okunmaz.
/// Yetkilendirme filtresindeki hata AppExceptionFilter'a ulasmadigi icin ayni zarf burada uretilir.
/// </summary>
internal static class AccessGate
{
    public static async Task CheckAsync(AuthorizationFilterContext context, Func<SessionUser, bool> allowed)
    {
        var http = context.HttpContext;
        try
        {
            var identity = http.RequestServices.GetRequiredService<IIdentityProvider>();
            var user = await identity.ResolveAsync(http.RequestAborted) ?? throw AppErrors.Unauthorized();
            if (http.Request.Cookies.TryGetValue(SessionConstants.CookieName, out var token) && !string.IsNullOrEmpty(token))
            {
                http.RequestServices.GetRequiredService<KnownSessions>().Remember(token);
            }
            if (!allowed(user)) throw AppErrors.Forbidden();
        }
        catch (AppException ex)
        {
            context.Result = AppExceptionFilter.ToResult(http, ex, http.RequestServices.GetRequiredService<ILogger<AppExceptionFilter>>());
        }
    }
}

/// <summary>
/// Kaynak: requireUser / requireManager / requireAdmin. Oturum yoksa 401 UNAUTHORIZED, rol uymuyorsa 403 FORBIDDEN.
/// Rol verilmezse oturum yeterlidir.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireRoleAttribute(params string[] roles) : Attribute, IAsyncAuthorizationFilter
{
    public string[] Roles { get; } = roles;

    public Task OnAuthorizationAsync(AuthorizationFilterContext context) =>
        AccessGate.CheckAsync(context, user => Roles.Length == 0 || Roles.Contains(user.Role));
}

/// <summary>Admin rolu veya Sekreter yetkisi gerektirir.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireAdminOrSecretaryAttribute : Attribute, IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context) =>
        AccessGate.CheckAsync(context, user => user.Role == Domain.Common.Roles.Admin || user.IsSecretary);
}

public sealed class RequireUserAttribute() : Attribute, IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context) => new RequireRoleAttribute().OnAuthorizationAsync(context);
}

public static class RoleSets
{
    /// <summary>Yonetici raporlarina hem yonetici hem admin erisebilir.</summary>
    public const string Manager = Domain.Common.Roles.Manager;
    public const string Admin = Domain.Common.Roles.Admin;
}
