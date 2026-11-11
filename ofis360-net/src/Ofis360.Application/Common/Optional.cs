namespace Ofis360.Application.Common;

/// <summary>
/// JS "undefined" ile "null" ayrimi: guncelleme girdilerinde alan hic gonderilmemis olabilir (HasValue=false)
/// ya da acikca null'a cekilmek istenebilir (HasValue=true, Value=null). Kaynaktaki Prisma `data: { ...rest }`
/// semantigi icin kullanilir.
/// </summary>
public readonly record struct Optional<T>(bool HasValue, T? Value)
{
    public static Optional<T> None => default;
    public static Optional<T> Of(T? value) => new(true, value);
    public static implicit operator Optional<T>(T? value) => Of(value);
}
