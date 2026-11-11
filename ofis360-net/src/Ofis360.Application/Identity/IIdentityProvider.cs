namespace Ofis360.Application.Identity;

/// <summary>
/// Kimlik saglayici seam'i. 1. surum: DemoIdentityProvider (httpOnly cerez `ofis360_session`, 12 saat).
/// Ileride EntraIdIdentityProvider / RayEkspresTicketIdentityProvider ayni arayuzu uygular.
/// </summary>
public interface IIdentityProvider
{
    /// <summary>Gelen istekten oturum kullanicisini cozer; oturum yoksa veya kullanici pasifse null.</summary>
    Task<SessionUser?> ResolveAsync(CancellationToken cancellationToken = default);

    Task SignInAsync(string userId, CancellationToken cancellationToken = default);

    Task SignOutAsync(CancellationToken cancellationToken = default);
}

/// <summary>Istek kapsaminda cozulmus kullanici; servisler yetki kontrolunu bunun uzerinden yapar.</summary>
public interface ICurrentUser
{
    SessionUser? User { get; }
}

public static class SessionConstants
{
    public const string CookieName = "ofis360_session";
}
