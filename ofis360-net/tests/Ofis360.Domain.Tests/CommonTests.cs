using Ofis360.Domain.Common;

namespace Ofis360.Domain.Tests;

public class CommonTests
{
    [Fact]
    public void Ids_New_produces_32_char_hex_unique_values()
    {
        var a = Ids.New();
        var b = Ids.New();

        Assert.Equal(Ids.Length, a.Length);
        Assert.Matches("^[0-9a-f]{32}$", a);
        Assert.NotEqual(a, b);
    }

    [Theory]
    [InlineData("EMPLOYEE", true)]
    [InlineData("MANAGER", true)]
    [InlineData("ADMIN", true)]
    [InlineData("employee", false)]
    [InlineData("", false)]
    public void Roles_IsValid_matches_source_role_list(string role, bool expected)
    {
        Assert.Equal(expected, Roles.IsValid(role));
    }

    [Fact]
    public void Role_labels_match_source_texts()
    {
        Assert.Equal("Çalışan", Roles.Labels["EMPLOYEE"]);
        Assert.Equal("Yönetici", Roles.Labels["MANAGER"]);
        Assert.Equal("Yönetim (Admin)", Roles.Labels["ADMIN"]);
    }
}
