using Ofis360.Application.Services;
using Ofis360.Application.Tests.Fixtures;

namespace Ofis360.Application.Tests.Integration;

/// <summary>
/// K8: metin aramalari Turkce karakterlerde de buyuk/kucuk harf duyarsizdir. Kaynakta (SQLite LIKE) "yılmaz" araması
/// "Elif Yılmaz"ı buluyordu; Oracle'da .NET ToUpperInvariant ile Oracle UPPER() i/ı/İ harflerini farkli katladigi icin
/// bu bozulmustu. Bu testler i-ailesinin her iki tarafta ayni katlandigini dogrular.
/// </summary>
[Collection(OracleCollection.Name)]
public class TurkishSearchTests(WorldFixture fx) : IClassFixture<WorldFixture>
{
    [Theory]
    [InlineData("yıldırım")]
    [InlineData("Yıldırım")]
    [InlineData("YILDIRIM")]
    [InlineData("ilhan")]
    [InlineData("İlhan")]
    [InlineData("İLHAN")]
    [InlineData("ILHAN")]
    [InlineData("şükrü")]
    [InlineData("ŞÜKRÜ")]
    [InlineData("rü yıl")]
    public async Task Kullanici_aramasi_Turkce_harflerde_buyuk_kucuk_harf_duyarsizdir(string search)
    {
        var name = "Şükrü Yıldırım İlhan";
        await EnsureUserAsync(name);

        var page = await fx.Svc<UserService>().ListAsync(new UserListFilters(Search: search, Take: 50));

        Assert.Contains(page.Items, item => item.User.FullName == name);
    }

    [Fact]
    public async Task Ilgisiz_arama_eslesmez()
    {
        await EnsureUserAsync("Şükrü Yıldırım İlhan");
        var page = await fx.Svc<UserService>().ListAsync(new UserListFilters(Search: "yıldızlı", Take: 50));
        Assert.DoesNotContain(page.Items, item => item.User.FullName == "Şükrü Yıldırım İlhan");
    }

    [Theory]
    [InlineData("_")]
    [InlineData("%")]
    [InlineData("Şükrü_")]
    public async Task Joker_karakterler_duz_harf_olarak_aranir(string search)
    {
        // Kaynak (Prisma contains) % ve _ karakterlerini kacislar; "_" aramasi herkesi dondurmemeli.
        await EnsureUserAsync("Şükrü Yıldırım İlhan");
        var page = await fx.Svc<UserService>().ListAsync(new UserListFilters(Search: search, Take: 50));
        Assert.DoesNotContain(page.Items, item => item.User.FullName == "Şükrü Yıldırım İlhan");
    }

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static bool _created;

    private async Task EnsureUserAsync(string name)
    {
        await Gate.WaitAsync();
        try
        {
            if (_created) return;
            await fx.CreateTestUserAsync(fullName: name);
            _created = true;
        }
        finally
        {
            Gate.Release();
        }
    }
}
