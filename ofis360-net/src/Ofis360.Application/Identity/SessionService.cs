using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Ofis360.Application.Common;
using Ofis360.Application.Persistence;
using Ofis360.Domain.Entities;

namespace Ofis360.Application.Identity;

/// <summary>Boşta kalma ve mutlak oturum süreleri sunucuda uygulanır.</summary>
public sealed record SessionPolicy(TimeSpan Idle, TimeSpan Absolute)
{
    public static readonly SessionPolicy Default = new(TimeSpan.FromHours(2), TimeSpan.FromHours(12));
}

/// <summary>
/// Sunucu tarafli oturum. Cerezde tahmin edilemeyen rastgele bilet tasinir, veritabaninda yalnizca ozeti durur;
/// veritabani sizsa bile bilet elde edilemez. Cikis ve suresi dolan oturum sunucuda gecersiz olur.
/// </summary>
public sealed class SessionService(IOfis360Db db, IClock clock, SessionPolicy policy)
{
    private static readonly TimeSpan TouchInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan RetainExpired = TimeSpan.FromDays(7);

    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>Yeni oturum acar ve yalnizca cereze yazilacak bileti dondurur.</summary>
    public async Task<string> CreateAsync(string userId, CancellationToken ct = default)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var now = clock.UtcNow;

        var threshold = now - RetainExpired;
        await db.AppSessions.Where(s => s.ExpiresAt < threshold).ExecuteDeleteAsync(ct);

        db.AppSessions.Add(new AppSession
        {
            TokenHash = Hash(token), UserId = userId, CreatedAt = now, LastSeenAt = now, ExpiresAt = now + policy.Absolute,
        });
        await db.SaveChangesAsync(ct);
        return token;
    }

    /// <summary>Gecerli bilet icin oturum kullanicisini doner; suresi dolmus, iptal edilmis veya pasif kullanici icin null.</summary>
    public async Task<SessionUser?> ResolveAsync(string? token, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(token)) return null;
        var hash = Hash(token);

        var row = await db.AppSessions.AsNoTracking()
            .Where(s => s.TokenHash == hash && s.User.Active)
            .Select(s => new
            {
                s.ExpiresAt, s.LastSeenAt, s.RevokedAt,
                User = new SessionUser(s.User.Id, s.User.Email, s.User.FullName, s.User.Title, s.User.Role, s.User.DepartmentId, s.User.Department.Name,
                    s.User.AccessibilityApproved, s.User.PreferredFloorId, s.User.PrefersQuietZone, s.User.NotificationsEnabled, s.User.Grade, s.User.IsSecretary),
            })
            .FirstOrDefaultAsync(ct);

        var now = clock.UtcNow;
        if (row is null || row.RevokedAt is not null || row.ExpiresAt <= now || now - row.LastSeenAt >= policy.Idle) return null;

        // Her istekte yazmamak icin dakikada bir guncellenir.
        if (now - row.LastSeenAt >= TouchInterval)
        {
            await db.AppSessions.Where(s => s.TokenHash == hash).ExecuteUpdateAsync(u => u.SetProperty(s => s.LastSeenAt, now), ct);
        }
        return row.User;
    }

    public async Task RevokeAsync(string? token, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(token)) return;
        var hash = Hash(token);
        var now = clock.UtcNow;
        await db.AppSessions.Where(s => s.TokenHash == hash && s.RevokedAt == null)
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.RevokedAt, now), ct);
    }
}

/// <summary>Kurumsal yonlendirmeyle gelen bileti dogrulayan seam. RayExpress yontemi netlesince uygulanir.</summary>
public interface ISsoTicketValidator
{
    bool IsConfigured { get; }

    /// <summary>Gecerli bilet icin kisinin kurumsal e-postasi; degilse null.</summary>
    Task<string?> ValidateAsync(string ticket, CancellationToken ct = default);
}

/// <summary>Varsayilan: kapali. Kimse giris yapamaz; asla demo moduna dusmez.</summary>
public sealed class NotConfiguredSsoTicketValidator : ISsoTicketValidator
{
    public bool IsConfigured => false;

    public Task<string?> ValidateAsync(string ticket, CancellationToken ct = default) => Task.FromResult<string?>(null);
}

public sealed class SsoSignInService(IOfis360Db db, ISsoTicketValidator validator)
{
    public bool IsConfigured => validator.IsConfigured;

    /// <summary>Bileti dogrular ve aktif kullaniciyi bulur.</summary>
    public async Task<AppUser> ResolveUserAsync(string ticket, CancellationToken ct = default)
    {
        if (!validator.IsConfigured)
        {
            throw new AppException(503, "SSO_NOT_CONFIGURED", "Kurumsal giriş henüz yapılandırılmadı.");
        }

        var email = await validator.ValidateAsync(ticket, ct);
        if (string.IsNullOrWhiteSpace(email)) throw AppErrors.Unauthorized();

        var normalized = email.Trim().ToLowerInvariant();
        return await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email.ToLower() == normalized && u.Active, ct)
            ?? throw AppErrors.Forbidden("Bu hesap için Ofis360 erişimi tanımlı değil.");
    }
}
