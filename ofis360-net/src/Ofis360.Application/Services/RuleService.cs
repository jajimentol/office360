using Microsoft.EntityFrameworkCore;
using Ofis360.Application.Common;
using Ofis360.Application.Persistence;
using Ofis360.Application.Validation;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;
using Ofis360.Domain.Rules;

namespace Ofis360.Application.Services;

/// <summary>Kaynak: updateRule girdisi. Optional: alan gonderilmediyse dokunulmaz, null gonderildiyse NULL'a cekilir.</summary>
public sealed record UpdateRuleCommand(string Key, bool? Enabled = null, Optional<int?> IntValue = default, Optional<int?> SecondValue = default);

public sealed record RuleMatrixRule(string Key, string Name, string Kind, string? Unit, int? MinValue, int? MaxValue, bool BaseEnabled, int? BaseValue, IReadOnlyList<int>? AllowedValues);

public sealed record RuleMatrixBuilding(string Id, string Name);

public sealed record RuleMatrix(string? BuildingId, IReadOnlyList<RuleMatrixRule> Rules, IReadOnlyList<RuleMatrixCell> Cells, IReadOnlyList<RuleMatrixBuilding> Buildings);

public sealed class RuleService(IOfis360Db db, AuditService audit)
{
    private const string KindToggle = "toggle";
    private const string KindNumber = "number";

    /// <summary>Kural ayarlarini veritabanindan okur ve kural motorunun bekledigi bicime cevirir (kademe degerleri uygulanmaz).</summary>
    public async Task<RuleSettings> GetRuleSettingsAsync(CancellationToken ct = default) =>
        RuleDefinitions.MapToSettings(await ReadBaseRowsAsync(ct));

    /// <summary>Kullanicinin kademesine (ve verilirse binaya) gore gecerli kurallar.</summary>
    public async Task<RuleSettings> GetRuleSettingsForUserAsync(string userId, string? buildingId, CancellationToken ct = default)
    {
        var grade = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Grade).FirstOrDefaultAsync(ct);
        var rows = await ReadBaseRowsAsync(ct);
        if (grade is null) return RuleDefinitions.MapToSettings(rows);

        var overrides = await db.ReservationRuleOverrides.AsNoTracking()
            .Where(o => o.Grade == grade)
            .Select(o => new RuleOverrideRow(o.RuleKey, o.Grade, o.BuildingId, o.Enabled, o.IntValue))
            .ToListAsync(ct);
        return RuleDefinitions.MapToSettings(RuleDefinitions.ApplyOverrides(rows, overrides, grade, buildingId));
    }

    /// <summary>Islemi Sekreter yapiyorsa rezervasyon penceresi ve calisma gunu kisiti uygulanmaz.</summary>
    public static RuleSettings WithSecretaryExemptions(RuleSettings settings) => settings with
    {
        AdvanceBookingDays = settings.AdvanceBookingDays with { Enabled = false },
        WorkingDaysOnly = new ToggleSetting(false),
    };

    public async Task<WorkCalendar> GetCalendarAsync(CancellationToken ct = default)
    {
        var dates = await db.Holidays.AsNoTracking().Select(h => h.Date).ToListAsync(ct);
        return new WorkCalendar(dates.Select(DayKeys.FromDateOnly));
    }

    private Task<List<RuleRow>> ReadBaseRowsAsync(CancellationToken ct) =>
        db.ReservationRules.AsNoTracking()
            .Select(r => new RuleRow(r.Key, r.Enabled, r.IntValue, r.SecondValue))
            .ToListAsync(ct);

    public async Task<RuleMatrix> GetMatrixAsync(string? buildingId, CancellationToken ct = default)
    {
        var rules = await db.ReservationRules.AsNoTracking()
            .Where(r => RuleDefinitions.GradeScopedKeys.Contains(r.Key))
            .OrderBy(r => r.Order)
            .ToListAsync(ct);
        var cells = await db.ReservationRuleOverrides.AsNoTracking()
            .Where(o => o.BuildingId == buildingId)
            .Select(o => new RuleMatrixCell(o.RuleKey, o.Grade, o.Enabled, o.IntValue))
            .ToListAsync(ct);
        var buildings = await db.Buildings.AsNoTracking()
            .Where(b => b.Active).OrderBy(b => b.Order)
            .Select(b => new RuleMatrixBuilding(b.Id, b.Name))
            .ToListAsync(ct);

        var views = rules.Select(r => new RuleMatrixRule(
            r.Key, r.Name, r.EditableLimit ? KindNumber : KindToggle, r.Unit, r.MinValue, r.MaxValue, r.Enabled, r.IntValue,
            r.Key == RuleKeys.RoomSlotMinutes ? RuleDefinitions.AllowedSlotMinutes : null)).ToList();
        return new RuleMatrix(buildingId, views, cells, buildings);
    }

    /// <summary>Secili kapsamin (tum binalar ya da tek bina) kademe degerlerini verilen listeyle degistirir.</summary>
    public async Task<RuleMatrix> SaveMatrixAsync(string? buildingId, IReadOnlyList<RuleMatrixCell> cells, AuditActor actor, CancellationToken ct = default)
    {
        if (buildingId is not null && !await db.Buildings.AnyAsync(b => b.Id == buildingId, ct))
        {
            throw AppErrors.NotFound("Bina bulunamadı.");
        }

        var rules = await db.ReservationRules.AsNoTracking()
            .Where(r => RuleDefinitions.GradeScopedKeys.Contains(r.Key))
            .ToDictionaryAsync(r => r.Key, ct);

        var seen = new HashSet<(string, string)>();
        foreach (var cell in cells)
        {
            if (!rules.TryGetValue(cell.RuleKey, out var rule)) throw AppErrors.Validation("Bu kural kademe bazlı değiştirilemez.");
            if (!Grades.IsValid(cell.Grade)) throw AppErrors.Validation("Geçersiz kademe.");
            if (!seen.Add((cell.RuleKey, cell.Grade))) throw AppErrors.Validation("Aynı kural ve kademe için birden fazla değer gönderildi.");

            if (rule.EditableLimit)
            {
                if (cell.IntValue is not int value || cell.Enabled is not null) throw AppErrors.Validation($"\"{rule.Name}\" için sayısal değer girilmelidir.");
                ValidateValue(rule, value);
            }
            else if (cell.Enabled is null || cell.IntValue is not null)
            {
                throw AppErrors.Validation($"\"{rule.Name}\" için açık veya kapalı seçilmelidir.");
            }
        }

        var existing = await db.ReservationRuleOverrides.Where(o => o.BuildingId == buildingId).ToListAsync(ct);
        var changes = new List<object>();

        await db.InTransactionAsync(async token =>
        {
            foreach (var row in existing.Where(o => !cells.Any(c => c.RuleKey == o.RuleKey && c.Grade == o.Grade)).ToList())
            {
                changes.Add(new { rule = row.RuleKey, grade = row.Grade, from = (object?)row.IntValue ?? row.Enabled, to = (object?)null });
                db.ReservationRuleOverrides.Remove(row);
            }

            foreach (var cell in cells)
            {
                var row = existing.FirstOrDefault(o => o.RuleKey == cell.RuleKey && o.Grade == cell.Grade);
                if (row is null)
                {
                    db.ReservationRuleOverrides.Add(new ReservationRuleOverride
                    {
                        RuleKey = cell.RuleKey, Grade = cell.Grade, BuildingId = buildingId,
                        Enabled = cell.Enabled, IntValue = cell.IntValue, UpdatedById = actor.Id,
                    });
                    changes.Add(new { rule = cell.RuleKey, grade = cell.Grade, from = (object?)null, to = (object?)cell.IntValue ?? cell.Enabled });
                }
                else if (row.Enabled != cell.Enabled || row.IntValue != cell.IntValue)
                {
                    changes.Add(new { rule = cell.RuleKey, grade = cell.Grade, from = (object?)row.IntValue ?? row.Enabled, to = (object?)cell.IntValue ?? cell.Enabled });
                    row.Enabled = cell.Enabled;
                    row.IntValue = cell.IntValue;
                    row.UpdatedAt = DateTimeOffset.UtcNow;
                    row.UpdatedById = actor.Id;
                }
            }

            await db.SaveChangesAsync(token);
            if (changes.Count > 0)
            {
                await audit.RecordAsync(actor, AuditActions.RuleMatrixUpdate, "ReservationRuleOverride", buildingId,
                    $"Kademe bazlı kurallarda {changes.Count} değer değiştirildi.", new { buildingId, changes }, token);
            }
            return true;
        }, ct);

        return await GetMatrixAsync(buildingId, ct);
    }

    private static void ValidateValue(ReservationRule rule, int value)
    {
        if (rule.MinValue is int min && value < min) throw AppErrors.Validation($"\"{rule.Name}\" en az {min} olmalıdır.");
        if (rule.MaxValue is int max && value > max) throw AppErrors.Validation($"\"{rule.Name}\" en fazla {max} olmalıdır.");
        if (rule.Key == RuleKeys.RoomSlotMinutes && !RuleDefinitions.AllowedSlotMinutes.Contains(value))
        {
            throw AppErrors.Validation("Slot aralığı 15, 30 veya 60 dakika olmalıdır.");
        }
    }

    public async Task<List<ReservationRule>> ListAsync(CancellationToken ct = default)
    {
        var rules = await db.ReservationRules.AsNoTracking().OrderBy(r => r.Order).ToListAsync(ct);
        if (rules.Count > 0) return rules;

        // Kurallar bir sekilde silinmisse varsayilanlari geri yukler (fail-safe).
        foreach (var definition in RuleDefinitions.All)
        {
            db.ReservationRules.Add(ToEntity(definition));
        }
        await db.SaveChangesAsync(ct);
        return await db.ReservationRules.AsNoTracking().OrderBy(r => r.Order).ToListAsync(ct);
    }

    public static ReservationRule ToEntity(RuleDefinition d) => new()
    {
        Key = d.Key, Name = d.Name, Description = d.Description, Category = d.Category, Enabled = d.Enabled,
        EditableLimit = d.EditableLimit, IntValue = d.IntValue, SecondValue = d.SecondValue, Unit = d.Unit,
        MinValue = d.MinValue, MaxValue = d.MaxValue, SystemRule = d.SystemRule, Order = d.Order,
    };

    public async Task<ReservationRule> UpdateAsync(UpdateRuleCommand input, AuditActor actor, CancellationToken ct = default)
    {
        var rule = await db.ReservationRules.FirstOrDefaultAsync(r => r.Key == input.Key, ct)
            ?? throw AppErrors.NotFound("Kural bulunamadı.");

        if (rule.SystemRule && input.Enabled == false)
        {
            throw AppErrors.Validation("Bu kural sistem kuralıdır ve devre dışı bırakılamaz.");
        }

        if (input.IntValue is { HasValue: true, Value: int intValue }) ValidateValue(rule, intValue);

        // Limitli kuralin degeri bosaltilamaz; bir kismi guncelleme de saklanan degerle birlikte tutarli olmali.
        var nextInt = input.IntValue.HasValue ? input.IntValue.Value : rule.IntValue;
        var nextSecond = input.SecondValue.HasValue ? input.SecondValue.Value : rule.SecondValue;
        if (rule.EditableLimit && rule.IntValue is not null && nextInt is null) throw AppErrors.Validation("Bu kuralın değeri boş bırakılamaz.");
        if (rule.EditableLimit && rule.SecondValue is not null && nextSecond is null) throw AppErrors.Validation("Bu kuralın ikinci değeri boş bırakılamaz.");

        if (input.Key == RuleKeys.RoomWorkingHours && nextSecond <= nextInt)
        {
            throw AppErrors.Validation("Mesai bitişi, mesai başlangıcından sonra olmalıdır.");
        }

        if (input.Key == RuleKeys.RoomWorkingHours && nextSecond is > 1440)
        {
            throw AppErrors.Validation("Mesai bitişi en geç 24:00 olabilir.");
        }

        if (input.Key == RuleKeys.NoShowRelease && nextSecond is <= 0 or > 1439)
        {
            throw AppErrors.Validation("Başlangıç saati seçilmelidir; 00:00 olamaz.");
        }

        if (input.Key == RuleKeys.PopularShareLimit && nextSecond >= nextInt)
        {
            throw AppErrors.Validation("Popüler masa üst sınırı, pencere boyutundan küçük olmalıdır.");
        }

        if (input.Enabled is bool enabled) rule.Enabled = enabled;
        if (input.IntValue.HasValue) rule.IntValue = input.IntValue.Value;
        if (input.SecondValue.HasValue) rule.SecondValue = input.SecondValue.Value;
        rule.UpdatedById = actor.Id;
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(actor, AuditActions.RuleUpdate, "ReservationRule", rule.Key,
            $"\"{rule.Name}\" kuralı güncellendi.",
            new { key = rule.Key, enabled = rule.Enabled, intValue = rule.IntValue, secondValue = rule.SecondValue }, ct);

        return rule;
    }
}
