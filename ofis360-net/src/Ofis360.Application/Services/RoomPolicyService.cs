using Microsoft.EntityFrameworkCore;
using Ofis360.Application.Common;
using Ofis360.Application.Persistence;
using Ofis360.Application.Validation;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;
using Ofis360.Domain.Rules;

namespace Ofis360.Application.Services;

public sealed record RoomPriorityPerson(string Id, string FullName, string Title);

public sealed record RoomPolicyView(
    string RoomId, string RoomName, int Capacity, bool SecretaryOnly, int? PriorityOpenDays,
    IReadOnlyList<RoomGradeRuleInput> GradeRules, IReadOnlyList<RoomPriorityPerson> PriorityUsers);

/// <summary>Odaya ozel kurallarin admin yonetimi.</summary>
public sealed record RoomPriorityGap(string RoomId, string Code, string Name);

public sealed class RoomPolicyService(IOfis360Db db, AuditService audit)
{
    public const int MaxPriorityOpenDays = 90;

    /// <summary>Oncelik suresi tanimli ama aktif oncelikli kisisi olmayan odalar: bu odalarda oncelik fiilen calismaz.</summary>
    public Task<List<RoomPriorityGap>> ListPriorityGapsAsync(CancellationToken ct = default) =>
        db.MeetingRooms.AsNoTracking()
            .Where(r => r.Active && r.Floor.Active && r.Floor.Building.Active && !r.SecretaryOnly && r.PriorityOpenDays != null && !r.PriorityUsers.Any(p => p.User.Active))
            .OrderBy(r => r.Code)
            .Select(r => new RoomPriorityGap(r.Id, r.Code, r.Name))
            .ToListAsync(ct);

    public static RoomPolicy ToPolicy(MeetingRoom room) => new(
        room.SecretaryOnly,
        room.AccessRules.Select(r => new RoomGradeRule(r.Grade, r.MinAttendees, r.BypassPriority)).ToList(),
        room.PriorityUsers.Select(p => p.UserId).ToList(),
        room.PriorityOpenDays);

    public async Task<RoomPolicyView> GetAsync(string roomId, CancellationToken ct = default)
    {
        var room = await db.MeetingRooms.AsNoTracking()
            .Include(r => r.AccessRules)
            .Include(r => r.PriorityUsers).ThenInclude(p => p.User)
            .FirstOrDefaultAsync(r => r.Id == roomId, ct)
            ?? throw AppErrors.NotFound("Toplantı odası bulunamadı.");

        return new RoomPolicyView(room.Id, room.Name, room.Capacity, room.SecretaryOnly, room.PriorityOpenDays,
            room.AccessRules.OrderBy(r => Grades.Rank(r.Grade)).Select(r => new RoomGradeRuleInput(r.Grade, r.MinAttendees, r.BypassPriority)).ToList(),
            room.PriorityUsers.OrderBy(p => p.User.FullName).Select(p => new RoomPriorityPerson(p.UserId, p.User.FullName, p.User.Title)).ToList());
    }

    public async Task<RoomPolicyView> SaveAsync(string roomId, RoomPolicyInput input, AuditActor actor, CancellationToken ct = default)
    {
        var room = await db.MeetingRooms
            .Include(r => r.AccessRules)
            .Include(r => r.PriorityUsers)
            .FirstOrDefaultAsync(r => r.Id == roomId, ct)
            ?? throw AppErrors.NotFound("Toplantı odası bulunamadı.");

        var rules = input.GradeRules ?? [];
        var userIds = (input.PriorityUserIds ?? []).Distinct().ToList();

        foreach (var rule in rules)
        {
            if (!Grades.IsValid(rule.Grade)) throw AppErrors.Validation("Geçersiz kademe.");
            if (rule.MinAttendees < 1 || rule.MinAttendees > room.Capacity)
            {
                throw AppErrors.Validation($"En az katılımcı 1 ile oda kapasitesi ({room.Capacity}) arasında olmalıdır.");
            }
        }
        if (rules.Select(r => r.Grade).Distinct().Count() != rules.Count) throw AppErrors.Validation("Aynı kademe birden fazla kez seçilemez.");

        if (input.PriorityOpenDays is < 0 or > MaxPriorityOpenDays)
        {
            throw AppErrors.Validation($"Herkese açılma süresi 0 ile {MaxPriorityOpenDays} iş günü arasında olmalıdır.");
        }
        if (userIds.Count > 0 && input.PriorityOpenDays is null)
        {
            throw AppErrors.Validation("Öncelikli kişi seçildiğinde herkese açılma süresi girilmelidir.");
        }
        if (userIds.Count > 0)
        {
            var found = await db.Users.CountAsync(u => userIds.Contains(u.Id) && u.Active, ct);
            if (found != userIds.Count) throw AppErrors.Validation("Öncelikli kişilerden biri bulunamadı veya aktif değil.");
        }

        await db.InTransactionAsync(async token =>
        {
            room.SecretaryOnly = input.SecretaryOnly;
            room.PriorityOpenDays = input.PriorityOpenDays;

            db.RoomAccessRules.RemoveRange(room.AccessRules.Where(existing => rules.All(r => r.Grade != existing.Grade)).ToList());
            foreach (var rule in rules)
            {
                var existing = room.AccessRules.FirstOrDefault(r => r.Grade == rule.Grade);
                if (existing is not null)
                {
                    existing.MinAttendees = rule.MinAttendees;
                    existing.BypassPriority = rule.BypassPriority;
                }
                else
                {
                    db.RoomAccessRules.Add(new RoomAccessRule { RoomId = room.Id, Grade = rule.Grade, MinAttendees = rule.MinAttendees, BypassPriority = rule.BypassPriority });
                }
            }

            db.RoomPriorityUsers.RemoveRange(room.PriorityUsers.Where(p => !userIds.Contains(p.UserId)).ToList());
            foreach (var userId in userIds.Where(id => room.PriorityUsers.All(p => p.UserId != id)))
            {
                db.RoomPriorityUsers.Add(new RoomPriorityUser { RoomId = room.Id, UserId = userId });
            }

            await db.SaveChangesAsync(token);
            await audit.RecordAsync(actor, AuditActions.RoomPolicyUpdate, "MeetingRoom", room.Id,
                $"{room.Name} odasının kuralları güncellendi.",
                new { input.SecretaryOnly, input.PriorityOpenDays, gradeRules = rules, priorityUserIds = userIds }, token);
            return true;
        }, ct);

        return await GetAsync(roomId, ct);
    }
}
