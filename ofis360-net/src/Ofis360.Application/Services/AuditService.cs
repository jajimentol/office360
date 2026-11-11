using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ofis360.Application.Common;
using Ofis360.Application.Identity;
using Ofis360.Application.Persistence;
using Ofis360.Domain.Entities;

namespace Ofis360.Application.Services;

public sealed record AuditActor(string? Id, string FullName, string Role)
{
    public static AuditActor FromSession(SessionUser user) => new(user.Id, user.FullName, user.Role);
}

public sealed record AuditQuery(
    string? Action = null, string? EntityType = null, string? ActorId = null, string? Search = null, int? Take = null, int? Skip = null);

/// <summary>
/// Sistem hareketlerini kaydeder (kaynak: audit.service.ts). Audit kaydi asla ana islemi bozmamalidir;
/// hatalar yutulur ve loglanir (K10).
/// </summary>
public sealed class AuditService(IOfis360Db db, ILogger<AuditService> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task RecordAsync(AuditActor actor, string action, string entityType, string? entityId, string summary,
        object? metadata = null, CancellationToken ct = default)
    {
        var entry = new AuditLog
        {
            ActorId = actor.Id,
            ActorName = actor.FullName,
            ActorRole = actor.Role,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Summary = summary,
            Metadata = metadata is null ? null : JsonSerializer.Serialize(metadata, Json),
        };
        try
        {
            db.AuditLogs.Add(entry);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Denetim kaydı oluşturulamadı: {Action} {EntityType}", action, entityType);
            db.AuditLogs.Entry(entry).State = EntityState.Detached;
        }
    }

    public async Task<PagedResult<AuditLog>> ListAsync(AuditQuery query, CancellationToken ct = default)
    {
        var take = Paging.Take(query.Take, 50);
        var skip = Paging.Skip(query.Skip);
        var q = db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(query.Action)) q = q.Where(x => x.Action == query.Action);
        if (!string.IsNullOrEmpty(query.EntityType)) q = q.Where(x => x.EntityType == query.EntityType);
        if (!string.IsNullOrEmpty(query.ActorId)) q = q.Where(x => x.ActorId == query.ActorId);
        if (!string.IsNullOrEmpty(query.Search))
        {
            var pattern = Search.Pattern(query.Search);
            q = q.Where(x => EF.Functions.Like(x.Summary.ToUpper().Replace("İ", "I"), pattern, Search.Escape));
        }

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(x => x.CreatedAt).Skip(skip).Take(take).ToListAsync(ct);
        return new PagedResult<AuditLog>(items, total, take, skip);
    }
}

/// <summary>
/// K8: metin aramalari Turkce karakterlerde de buyuk/kucuk harf duyarsizdir.
/// Oracle UPPER() 'ı'→'I' yapar, 'İ'yi korur; .NET ToUpperInvariant ise 'ı'yi korur. Iki taraf da i-ailesini (i, ı, I, İ)
/// tek harfe ('I') katlar: desen burada, sutun tarafi sorguda <c>col.ToUpper().Replace("İ", "I")</c> ile.
/// </summary>
public static class Search
{
    /// <summary>LIKE kacis karakteri. Kaynak (Prisma contains) % ve _ karakterlerini duz harf olarak arar; burada da oyle.</summary>
    public const string Escape = "\\";

    public static string Fold(string value) => value.ToUpperInvariant().Replace('İ', 'I').Replace('ı', 'I');

    public static string Pattern(string search) =>
        "%" + Fold(search.Trim()).Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
}
