using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Ofis360.Application.Common;
using Ofis360.Application.Persistence;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;

namespace Ofis360.Application.Services;

/// <summary>Sistem ayarlari (admin). Ayar degisiklikleri denetim kaydina yazilir.</summary>
public sealed class SettingService(IOfis360Db db, AuditService audit)
{
    public Task<List<SystemSetting>> ListAsync(CancellationToken ct = default) =>
        db.SystemSettings.AsNoTracking().OrderBy(s => s.Group).ThenBy(s => s.Order).ToListAsync(ct);

    public async Task<Dictionary<string, string>> GetMapAsync(CancellationToken ct = default) =>
        await db.SystemSettings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => s.Value, ct);

    public async Task<SystemSetting> UpdateAsync(string key, string value, AuditActor actor, CancellationToken ct = default)
    {
        var setting = await db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key, ct)
            ?? throw AppErrors.NotFound("Ayar bulunamadı.");

        if (setting.Type == SettingTypes.Number && !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            throw AppErrors.Validation("Bu ayar sayısal bir değer olmalıdır.");
        }
        if (setting.Type == SettingTypes.Boolean && value is not ("true" or "false"))
        {
            throw AppErrors.Validation("Bu ayar açık veya kapalı olmalıdır.");
        }
        if (setting.Type == SettingTypes.Time && !DayKeys.IsValidTime(value))
        {
            throw AppErrors.Validation("Saat değeri SS:DD biçiminde olmalıdır (örneğin 08:30).");
        }
        if (value.Trim().Length == 0)
        {
            throw AppErrors.Validation("Değer boş bırakılamaz.");
        }

        setting.Value = value;
        await db.SaveChangesAsync(ct);

        // Mesai saatleri ayari, toplanti odasi kuralini da gunceller: tek kaynak korunur.
        if (key is SettingKeys.WorkdayStart or SettingKeys.WorkdayEnd)
        {
            var map = await GetMapAsync(ct);
            var start = DayKeys.TimeToMinutes(map.GetValueOrDefault(SettingKeys.WorkdayStart, "08:00"));
            var end = DayKeys.TimeToMinutes(map.GetValueOrDefault(SettingKeys.WorkdayEnd, "19:00"));
            if (end > start)
            {
                var rule = await db.ReservationRules.FirstOrDefaultAsync(r => r.Key == RuleKeys.RoomWorkingHours, ct);
                if (rule is not null)
                {
                    rule.IntValue = start;
                    rule.SecondValue = end;
                    await db.SaveChangesAsync(ct);
                }
            }
        }

        await audit.RecordAsync(actor, AuditActions.SettingUpdate, "SystemSetting", key,
            $"\"{setting.Label}\" ayarı güncellendi.", new { key, value }, ct);

        return setting;
    }
}
