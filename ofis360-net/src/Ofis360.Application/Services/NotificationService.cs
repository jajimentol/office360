using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ofis360.Application.Common;
using Ofis360.Application.Persistence;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;

namespace Ofis360.Application.Services;

public sealed record NotificationPayload(
    string UserId, string Type, string Title, string Body, string? Link = null, string Severity = NotificationSeverities.Info);

/// <summary>
/// Bildirim kanali sozlesmesi. Uygulama ici kanal etkindir; e-posta ve Teams ayni sozlesmeyi uygulayan
/// kanallar olarak eklendiginde servis kodu degismez.
/// </summary>
public interface INotificationChannel
{
    string Key { get; }
    bool Enabled { get; }
    Task SendAsync(NotificationPayload payload, CancellationToken ct);
}

public sealed class InAppNotificationChannel(IOfis360Db db) : INotificationChannel
{
    public string Key => "IN_APP";
    public bool Enabled => true;

    public async Task SendAsync(NotificationPayload p, CancellationToken ct)
    {
        db.Notifications.Add(new Notification
        {
            UserId = p.UserId, Type = p.Type, Title = p.Title, Body = p.Body, Link = p.Link, Severity = p.Severity,
        });
        await db.SaveChangesAsync(ct);
    }
}

/// <summary>Iskele: uretimde SMTP / kurumsal e-posta entegrasyonu. enabled=false oldugu surece yan etkisi yok.</summary>
public sealed class EmailNotificationChannel : INotificationChannel
{
    public string Key => "EMAIL";
    public bool Enabled => false;
    public Task SendAsync(NotificationPayload payload, CancellationToken ct) => Task.CompletedTask;
}

/// <summary>Iskele: uretimde Microsoft Teams webhook entegrasyonu.</summary>
public sealed class TeamsNotificationChannel : INotificationChannel
{
    public string Key => "TEAMS";
    public bool Enabled => false;
    public Task SendAsync(NotificationPayload payload, CancellationToken ct) => Task.CompletedTask;
}

public sealed class NotificationService(IOfis360Db db, IEnumerable<INotificationChannel> channels, IClock clock, ILogger<NotificationService> logger)
{
    private readonly IReadOnlyList<INotificationChannel> _channels = channels.ToList();

    public IReadOnlyList<(string Key, bool Enabled)> ListChannels() => _channels.Select(c => (c.Key, c.Enabled)).ToList();

    /// <summary>Bildirimi etkin tum kanallara gonderir. Bildirim hatasi ana islemi bozmaz.</summary>
    public async Task NotifyAsync(NotificationPayload payload, CancellationToken ct = default)
    {
        var enabled = await db.Users.AsNoTracking()
            .Where(u => u.Id == payload.UserId)
            .Select(u => (bool?)u.NotificationsEnabled)
            .FirstOrDefaultAsync(ct);
        if (enabled != true) return;

        foreach (var channel in _channels)
        {
            if (!channel.Enabled) continue;
            try
            {
                await channel.SendAsync(payload, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Bildirim gönderilemedi ({Channel})", channel.Key);
            }
        }
    }

    public async Task NotifyManyAsync(IEnumerable<NotificationPayload> payloads, CancellationToken ct = default)
    {
        foreach (var payload in payloads) await NotifyAsync(payload, ct);
    }

    public Task<List<Notification>> ListAsync(string userId, int take = 50, CancellationToken ct = default) =>
        db.Notifications.AsNoTracking().Where(n => n.UserId == userId).OrderByDescending(n => n.CreatedAt).Take(take).ToListAsync(ct);

    public Task<int> CountUnreadAsync(string userId, CancellationToken ct = default) =>
        db.Notifications.CountAsync(n => n.UserId == userId && n.ReadAt == null, ct);

    public async Task MarkReadAsync(string userId, string notificationId, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        await db.Notifications
            .Where(n => n.Id == notificationId && n.UserId == userId && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, now), ct);
    }

    public async Task MarkAllReadAsync(string userId, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        await db.Notifications
            .Where(n => n.UserId == userId && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, now), ct);
    }
}
