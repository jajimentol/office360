namespace Ofis360.Application.Common;

/// <summary>Liste uclari icin sayfalama sinirlari; istemci degeri ne olursa olsun sorgu sinirli kalir.</summary>
public static class Paging
{
    public const int MaxTake = 500;

    public static int Take(int? take, int defaultTake = 50) => Math.Clamp(take ?? defaultTake, 1, MaxTake);

    public static int Skip(int? skip) => Math.Max(0, skip ?? 0);
}
