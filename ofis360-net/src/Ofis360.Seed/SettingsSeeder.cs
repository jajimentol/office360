using Microsoft.EntityFrameworkCore;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;
using Ofis360.Infrastructure.Persistence;

namespace Ofis360.Seed;

/// <summary>Sonradan eklenen sistem ayarlarini eksikse ekler; mevcut degerlere dokunmaz. Tekrar calistirmak guvenlidir.</summary>
public static class SettingsSeeder
{
    public static IReadOnlyList<SystemSetting> Defaults() =>
    [
        new SystemSetting
        {
            Key = SettingKeys.PresenceManagersOnly, Value = "false", Label = "“Ofiste kim var” yalnızca yöneticilere açık",
            Description = "Kapalıyken tüm çalışanlar görür; açıkken yalnızca yönetici ve admin görür.", Type = SettingTypes.Boolean, Group = "OFFICE", Order = 10,
        },
    ];

    public static async Task<int> ApplyAsync(Ofis360DbContext db)
    {
        var existing = await db.SystemSettings.Select(s => s.Key).ToListAsync();
        var added = 0;
        foreach (var setting in Defaults().Where(s => !existing.Contains(s.Key)))
        {
            db.SystemSettings.Add(setting);
            added++;
        }
        await db.SaveChangesAsync();
        return added;
    }
}
