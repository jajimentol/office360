using Microsoft.Extensions.Caching.Memory;

namespace Ofis360.Api.Identity;

/// <summary>
/// Dogrulanmis oturum cerezleri. Istek sinirlayici veritabanina gidemez; rastgele cerezle yeni kova acilamasin diye
/// yalnizca burada kayitli oturumlar kendi kovasini alir, digerleri IP kovasina duser.
/// </summary>
public sealed class KnownSessions(IMemoryCache cache)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    public void Remember(string token) => cache.Set(Key(token), true, new MemoryCacheEntryOptions { SlidingExpiration = Lifetime });

    public bool Contains(string token) => cache.TryGetValue(Key(token), out _);

    private static string Key(string token) => "known-session:" + token;
}
