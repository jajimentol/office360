using Ofis360.Application.Identity;

namespace Ofis360.Api.Identity;

/// <summary>
/// Cerezde rastgele oturum bileti tasiyan kimlik saglayici. Bilet <see cref="SessionService"/> ile sunucuda dogrulanir
/// ve iptal edilir. Demo giris de kurumsal yonlendirme de ayni oturum sozlesmesini kullanir.
/// </summary>
public sealed class CookieSessionIdentityProvider(IHttpContextAccessor accessor, SessionService sessions, SessionPolicy policy, IHostEnvironment environment) : IIdentityProvider
{
    public const string ItemKey = "ofis360.session";

    private HttpContext Context => accessor.HttpContext ?? throw new InvalidOperationException("HttpContext yok.");

    public async Task<SessionUser?> ResolveAsync(CancellationToken cancellationToken = default)
    {
        var context = Context;
        if (context.Items.TryGetValue(ItemKey, out var cached)) return cached as SessionUser;

        context.Request.Cookies.TryGetValue(SessionConstants.CookieName, out var token);
        var session = await sessions.ResolveAsync(token, cancellationToken);
        context.Items[ItemKey] = session;
        return session;
    }

    public async Task SignInAsync(string userId, CancellationToken cancellationToken = default)
    {
        var context = Context;
        var token = await sessions.CreateAsync(userId, cancellationToken);
        context.Response.Cookies.Append(SessionConstants.CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            MaxAge = policy.Absolute,
            Secure = !environment.IsDevelopment(),
        });
        context.Items.Remove(ItemKey);
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        var context = Context;
        context.Request.Cookies.TryGetValue(SessionConstants.CookieName, out var token);
        await sessions.RevokeAsync(token, cancellationToken);
        context.Response.Cookies.Delete(SessionConstants.CookieName, new CookieOptions { Path = "/" });
        context.Items[ItemKey] = null;
    }
}

public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public SessionUser? User => accessor.HttpContext?.Items[CookieSessionIdentityProvider.ItemKey] as SessionUser;
}
