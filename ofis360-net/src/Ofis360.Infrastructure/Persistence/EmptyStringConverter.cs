using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Ofis360.Infrastructure.Persistence;

/// <summary>
/// Oracle bos dizgiyi NULL sayar (ORA-01400 "NOT NULL sutuna NULL eklenemez"). Kaynakta bos dizgi gecerli olan
/// metin alanlari (bina adresi, kat aciklamasi, ayar degeri...) Oracle'da nullable tutulur ve
/// "" ↔ NULL donusumu burada yapilir; uygulama tarafi her zaman bos dizgi gorur.
/// </summary>
/// <remarks>
/// NULL'in donusturucuden gecmesi icin <c>convertsNulls: true</c> gerekir; bu parametre EF Core'da
/// "internal API" olarak isaretli (EF1001) ama EF 6'dan beri ayni imzayla duruyor. Surum yukseltmede
/// <see cref="EmptyStringConverterTests"/> bu davranisi dogrular.
/// </remarks>
public sealed class EmptyStringConverter : ValueConverter<string, string?>
{
#pragma warning disable EF1001 // convertsNulls parametresi: bkz. remarks
    public EmptyStringConverter()
        : base(v => v.Length == 0 ? null : v, v => v ?? string.Empty, convertsNulls: true)
    {
    }
#pragma warning restore EF1001
}
