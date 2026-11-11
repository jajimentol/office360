using Microsoft.EntityFrameworkCore;
using Ofis360.Application.Common;
using Ofis360.Application.Identity;
using Ofis360.Application.Services;
using Ofis360.Application.Tests.Fixtures;
using Ofis360.Application.Validation;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;
using static Ofis360.Application.Tests.Fixtures.TestHelpers;

namespace Ofis360.Application.Tests.Integration;

public sealed class UserGradeWorld : IAsyncLifetime
{
    public WorldFixture Fx { get; } = new();
    public SessionUser Admin { get; private set; } = null!;
    public SessionUser Employee { get; private set; } = null!;
    public SessionUser Manager { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Fx.InitializeAsync();
        Admin = await Fx.CreateTestUserAsync(Roles.Admin);
        Employee = await Fx.CreateTestUserAsync();
        Manager = await Fx.CreateTestUserAsync(Roles.Manager);
    }

    public Task DisposeAsync() => Fx.DisposeAsync();
}

[Collection(OracleCollection.Name)]
public class UserGradeTests(UserGradeWorld w) : IClassFixture<UserGradeWorld>
{
    private WorldFixture Fx => w.Fx;
    private UserImportService Import => Fx.Svc<UserImportService>();

    private async Task<AppUser> Load(string email)
    {
        await using var db = Fx.Db();
        return await db.Users.AsNoTracking().Include(u => u.Department).SingleAsync(u => u.Email == email);
    }

    private string Dept => Fx.World.DepartmentId;

    private async Task<string> DeptName()
    {
        await using var db = Fx.Db();
        return (await db.Departments.SingleAsync(d => d.Id == Dept)).Name;
    }

    [Fact]
    public async Task Kademe_ve_sekreter_kaydedilir_ve_kademe_temizlenebilir()
    {
        var users = Fx.Svc<UserService>();
        var user = await users.CreateAsync(new UserInput("Sekreter Kişi", "grade1@ray.test", "Asistan", Roles.Employee, Dept, Grade: Grades.Specialist, IsSecretary: true), Actor(w.Admin));
        Assert.Equal(Grades.Specialist, user.Grade);
        Assert.True(user.IsSecretary);

        await users.UpdateAsync(user.Id, new UserUpdateInput(Grade: Grades.Director, IsSecretary: false), Actor(w.Admin));
        var updated = await Load("grade1@ray.test");
        Assert.Equal(Grades.Director, updated.Grade);
        Assert.False(updated.IsSecretary);

        // Alan gonderilmezse degismez.
        await users.UpdateAsync(user.Id, new UserUpdateInput(Title: "Yeni Unvan"), Actor(w.Admin));
        Assert.Equal(Grades.Director, (await Load("grade1@ray.test")).Grade);

        await users.UpdateAsync(user.Id, new UserUpdateInput(Grade: Optional<string?>.Of(null)), Actor(w.Admin));
        Assert.Null((await Load("grade1@ray.test")).Grade);
    }

    [Fact]
    public async Task Kademe_rol_yetkisini_degistirmez()
    {
        var users = Fx.Svc<UserService>();
        await users.UpdateAsync(w.Employee.Id, new UserUpdateInput(Grade: Grades.Gm), Actor(w.Admin));
        await using var db = Fx.Db();
        Assert.Equal(Roles.Employee, (await db.Users.SingleAsync(u => u.Id == w.Employee.Id)).Role);
    }

    [Fact]
    public void Gecersiz_kademe_dogrulamadan_gecmez()
    {
        Assert.False(new UserValidator().Validate(new UserInput("Ali Veli", "a@b.co", "Uzman", Roles.Employee, "d", Grade: "STAJYER")).IsValid);
        Assert.False(new UserUpdateValidator().Validate(new UserUpdateInput(Grade: "STAJYER")).IsValid);
        Assert.True(new UserUpdateValidator().Validate(new UserUpdateInput(Grade: Optional<string?>.Of(null))).IsValid);
        Assert.True(new UserUpdateValidator().Validate(new UserUpdateInput()).IsValid);
    }

    [Fact]
    public async Task Veritabani_gecersiz_kademeyi_reddeder()
    {
        await using var db = Fx.Db();
        db.Users.Add(new AppUser { Email = "ck@ray.test", FullName = "Check Kişi", Title = "Uzman", DepartmentId = Dept, Grade = "STAJYER" });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Filtreler_ve_ozet_kademeye_gore_calisir()
    {
        var users = Fx.Svc<UserService>();
        await users.CreateAsync(new UserInput("Filtre Direktör", "filtre1@ray.test", "Direktör", Roles.Employee, Dept, Grade: Grades.Director), Actor(w.Admin));
        await users.CreateAsync(new UserInput("Filtre Sekreter", "filtre2@ray.test", "Sekreter", Roles.Employee, Dept, IsSecretary: true), Actor(w.Admin));

        var directors = await users.ListAsync(new UserListFilters(Search: "Filtre", Grade: Grades.Director));
        Assert.Equal("filtre1@ray.test", Assert.Single(directors.Items).User.Email);
        var secretaries = await users.ListAsync(new UserListFilters(Search: "Filtre", Grade: UserService.GradeFilterSecretary));
        Assert.Equal("filtre2@ray.test", Assert.Single(secretaries.Items).User.Email);
        var none = await users.ListAsync(new UserListFilters(Search: "Filtre", Grade: UserService.GradeFilterNone));
        Assert.Equal("filtre2@ray.test", Assert.Single(none.Items).User.Email);

        var stats = await users.GetStatsAsync();
        Assert.True(stats.WithoutGrade >= 1);
        Assert.True(stats.Secretaries >= 1);
    }

    [Fact]
    public async Task Onizleme_hicbir_sey_yazmaz()
    {
        var csv = "e-posta;ad soyad;kademe;departman\nonizleme@ray.test;Önizleme Kişi;Uzman;Hiç Olmayan Birim\n";
        var result = await Import.ImportAsync(csv, apply: false, Actor(w.Admin));

        Assert.False(result.Applied);
        Assert.Equal(1, result.Creates);
        Assert.Equal(["Hiç Olmayan Birim"], result.NewDepartments);

        await using var db = Fx.Db();
        Assert.False(await db.Users.AnyAsync(u => u.Email == "onizleme@ray.test"));
        Assert.False(await db.Departments.AnyAsync(d => d.Name == "Hiç Olmayan Birim"));
    }

    [Fact]
    public async Task Yukleme_olusturur_gunceller_ve_denetim_kaydi_yazar()
    {
        var name = await DeptName();
        var csv = $"e-posta;ad soyad;unvan;kademe;departman;sekreter\n"
            + $"yeni1@ray.test;Yeni Bir;;Kıdemli Uzman;Anadolu Bölge 1;Hayır\n"
            + $"yeni2@ray.test;Yeni İki;Sekreter;;ANADOLU BÖLGE 1;Evet\n"
            + $"{w.Manager.Email};;;GMY;{name};\n";
        var result = await Import.ImportAsync(csv, apply: true, Actor(w.Admin));

        Assert.True(result.Applied);
        Assert.Equal(2, result.Creates);
        Assert.Equal(1, result.Updates);
        Assert.Equal(0, result.Errors);
        Assert.Equal(["Anadolu Bölge 1"], result.NewDepartments);

        var one = await Load("yeni1@ray.test");
        Assert.Equal(Grades.SeniorSpecialist, one.Grade);
        Assert.Equal("Kıdemli Uzman", one.Title);
        Assert.Equal(Roles.Employee, one.Role);
        Assert.True(one.Active);
        Assert.Equal("Anadolu Bölge 1", one.Department.Name);

        var two = await Load("yeni2@ray.test");
        Assert.Null(two.Grade);
        Assert.True(two.IsSecretary);
        Assert.Equal(one.DepartmentId, two.DepartmentId);

        var manager = await Load(w.Manager.Email);
        Assert.Equal(Grades.DeputyGm, manager.Grade);
        Assert.Equal(Roles.Manager, manager.Role);
        Assert.Equal(w.Manager.FullName, manager.FullName); // bos hucre korur

        await using var db = Fx.Db();
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == AuditActions.UserImport));

        var again = await Import.ImportAsync(csv, apply: true, Actor(w.Admin));
        Assert.False(again.Applied);
        Assert.Equal(3, again.Unchanged);
        Assert.Empty(again.NewDepartments);
    }

    [Fact]
    public async Task Tek_hatali_satir_tum_yuklemeyi_durdurur()
    {
        var csv = "e-posta;ad soyad;kademe;departman;sekreter\n"
            + "dogru@ray.test;Doğru Kişi;Uzman;Hasar X;\n"
            + "bozuk;Bozuk Kişi;Uzman;Hasar X;\n"
            + "kademe@ray.test;Kademe Kişi;Stajyer;Hasar X;\n"
            + "sek@ray.test;Sek Kişi;Uzman;Hasar X;Belki\n"
            + "eksik@ray.test;;Uzman;;\n"
            + "dogru@ray.test;Tekrar Kişi;Uzman;Hasar X;\n";
        var result = await Import.ImportAsync(csv, apply: true, Actor(w.Admin));

        Assert.False(result.Applied);
        Assert.Equal(5, result.Errors);
        Assert.Contains("Bilinmeyen kademe", result.Rows.Single(r => r.Line == 4).Errors[0]);

        await using var db = Fx.Db();
        Assert.False(await db.Users.AnyAsync(u => u.Email == "dogru@ray.test"));
        Assert.False(await db.Departments.AnyAsync(d => d.Name == "Hasar X"));
    }

    [Fact]
    public async Task Dosyada_olmayan_kullanici_ve_pasif_durum_degismez()
    {
        await using (var db = Fx.Db())
        {
            db.Users.Add(new AppUser { Email = "pasif@ray.test", FullName = "Pasif Kişi", Title = "Uzman", DepartmentId = Dept, Active = false });
            await db.SaveChangesAsync();
        }
        var before = await Load(w.Admin.Email);

        var result = await Import.ImportAsync("e-posta;kademe\npasif@ray.test;Müdür\n", apply: true, Actor(w.Admin));
        Assert.True(result.Applied);

        var passive = await Load("pasif@ray.test");
        Assert.False(passive.Active);
        Assert.Equal(Grades.Manager, passive.Grade);
        Assert.Equal(before.Grade, (await Load(w.Admin.Email)).Grade);
        Assert.True((await Load(w.Admin.Email)).Active);
    }

    [Fact]
    public async Task Bozuk_dosya_hata_doner()
    {
        var result = await Import.ImportAsync("ad;soyad\nAli;Veli\n", apply: true, Actor(w.Admin));
        Assert.False(result.Applied);
        Assert.NotNull(result.FileError);
    }
}
