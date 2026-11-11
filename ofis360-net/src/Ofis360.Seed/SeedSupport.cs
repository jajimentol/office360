using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Ofis360.Domain.Common;
using Ofis360.Infrastructure.Persistence;

namespace Ofis360.Seed;

/// <summary>Seed betiklerinin ortak yardimcilari (JS semantigi korunur).</summary>
public static partial class SeedSupport
{
    /// <summary>JS Math.floor(random() * n) ile ayni; JS `pick` = items[floor(r*len) % len].</summary>
    public static T Pick<T>(this SeededRandom random, IReadOnlyList<T> items) =>
        items[(int)Math.Floor(random.Next() * items.Count) % items.Count];

    public static int RandomInt(this SeededRandom random, int min, int max) =>
        min + (int)Math.Floor(random.Next() * (max - min + 1));

    /// <summary>JS Math.round (yarim yukari).</summary>
    public static int JsRound(double value) => (int)Math.Floor(value + 0.5);

    /// <summary>Gun anahtari + saat (UTC) → DateTimeOffset. Kaynak: new Date(date.getTime() + h*3_600_000).</summary>
    public static DateTimeOffset At(string dayKey, double hours) =>
        new(DayKeys.ToUtcDate(dayKey).AddHours(hours), TimeSpan.Zero);

    public static DateTimeOffset AtMinutes(string dayKey, int minutes) =>
        new(DayKeys.WithMinutes(dayKey, minutes), TimeSpan.Zero);

    private static readonly Dictionary<char, char> EmailMap = new()
    {
        ['ç'] = 'c', ['Ç'] = 'c', ['ğ'] = 'g', ['Ğ'] = 'g', ['ı'] = 'i', ['İ'] = 'i', ['ö'] = 'o', ['Ö'] = 'o',
        ['ş'] = 's', ['Ş'] = 's', ['ü'] = 'u', ['Ü'] = 'u', ['â'] = 'a', ['î'] = 'i', ['û'] = 'u',
    };

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"[^a-z.]")]
    private static partial Regex NonSlug();

    /// <summary>Kaynak: seed.ts toEmailSlug — harf eslemesi, kucuk harf, bosluk → nokta, [a-z.] disi atilir.</summary>
    public static string EmailSlug(string fullName)
    {
        var mapped = new StringBuilder();
        foreach (var ch in fullName) mapped.Append(EmailMap.TryGetValue(ch, out var m) ? m : ch);
        var lower = mapped.ToString().ToLowerInvariant();
        return NonSlug().Replace(Whitespace().Replace(lower, "."), "");
    }

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonAlnum();

    /// <summary>Kaynak: seed-scale.ts toEmailSlug — tr-TR kucuk harf, Turkce harf sadelestirme, [^a-z0-9]+ → '.'.</summary>
    public static string ScaleEmailSlug(string fullName)
    {
        var lower = fullName.ToLower(CultureInfo.GetCultureInfo("tr-TR"))
            .Replace('ı', 'i').Replace('ğ', 'g').Replace('ü', 'u').Replace('ş', 's').Replace('ö', 'o').Replace('ç', 'c');
        return NonAlnum().Replace(lower, ".").Trim('.');
    }

    /// <summary>Prisma createMany karsiligi: 1000'lik partilerle, takipsiz toplu INSERT.</summary>
    public static async Task BulkInsertAsync<T>(Ofis360DbContext db, IReadOnlyList<T> rows, int batchSize = 1000) where T : class
    {
        db.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            for (var i = 0; i < rows.Count; i += batchSize)
            {
                db.Set<T>().AddRange(rows.Skip(i).Take(batchSize));
                await db.SaveChangesAsync();
                db.ChangeTracker.Clear();
            }
        }
        finally
        {
            db.ChangeTracker.AutoDetectChangesEnabled = true;
        }
    }

    public static Ofis360DbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<Ofis360DbContext>().UseOracle(connectionString, OracleOptions.Configure).Options);
}
