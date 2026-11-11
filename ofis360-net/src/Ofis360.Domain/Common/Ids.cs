namespace Ofis360.Domain.Common;

/// <summary>
/// Kaynak projede kimlikler cuid dizgisiydi. Hedefte uygulama tarafinda uretilen 32 karakterlik
/// Guid ("N") kullanilir; API sozlesmesinde kimlikler string kalir.
/// </summary>
public static class Ids
{
    public const int Length = 32;

    public static string New() => Guid.NewGuid().ToString("N");
}
