// Ofis360.Seed — deterministik demo verisi CLI (kaynak: prisma/*.ts).
//   seed                      DESTRUCTIVE: tum tablolari temizleyip demo verisini yazar (onay ister; --yes ile atlanir)
//   palladium [--redraw-sketch]   Palladium Tower 36. Kat (additive, idempotent; konumlari ezmez)
//   palladium-form            Ek Bina Faz 1 formunu kurulu veritabanina uygular (idempotent; konumlari ezmez, kayit silmez)
//   ek-bina-rules             Formdaki kural degerleri ve sabit resmi tatiller (idempotent; admin degisikliklerini ezmez)
//   ek-bina-rooms             Formdaki oda kurallari (idempotent; kurali olan odaya dokunmaz)
//   demo-room-roles           Yalniz gelistirme: ornek Sekreter ve bolge muduru hesaplari
//   demo-grades               Yalniz gelistirme: kademesi bos ornek kullanicilara kademe atar (idempotent)
//   demo-cast                 Yalniz gelistirme: demoyu kademe basina bir kisiye indirir (silmez, pasife alir)
//   scale                     300 calisanlik kurumsal kapasite (additive)
//   activity [--building=PT] [--departments=SAT,PZR] [--history=60] [--seed=N] [--only=desks|rooms]
//   buildings                 Demo Bina'yi (pasif, isDemo) olusturur; binasiz kat kalmaz (Oracle semasinda zorunlu alan)
// Baglanti: OFIS360_CONNECTION ortam degiskeni, yoksa yerel gelistirme semasi (ConnectionStrings.LocalDev).

using Microsoft.EntityFrameworkCore;
using Ofis360.Application.Common;
using Ofis360.Domain.Entities;
using Ofis360.Infrastructure.Persistence;
using Ofis360.Seed;

var command = args.Length > 0 ? args[0] : "help";
string? Arg(string name) => args.Select(a => a.StartsWith($"--{name}=") ? a[(name.Length + 3)..] : null).FirstOrDefault(v => v is not null);
bool Flag(string name) => args.Contains($"--{name}");

var connection = Environment.GetEnvironmentVariable("OFIS360_CONNECTION") ?? ConnectionStrings.LocalDev;
var today = new SystemClock().TodayKey();

switch (command)
{
    case "seed":
    {
        if (!Flag("yes"))
        {
            Console.Write("Bu komut veritabanindaki TUM kayitlari siler ve demo verisini yeniden yazar. Devam? (evet/hayir): ");
            var answer = Console.ReadLine();
            if (!string.Equals(answer?.Trim(), "evet", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Iptal edildi.");
                return 1;
            }
        }
        await using var db = SeedSupport.CreateContext(connection);
        await new DemoSeeder(db, today, Console.Out).RunAsync();
        return 0;
    }
    case "palladium":
    {
        Console.WriteLine("Palladium Tower 36. Kat kuruluyor (additive, idempotent).");
        await using var db = SeedSupport.CreateContext(connection);
        var result = await PalladiumSeeder.SeedAsync(db, Flag("redraw-sketch"));
        PalladiumSeeder.PrintReport(Console.Out, result);
        return 0;
    }
    case "palladium-form":
    {
        await using var db = SeedSupport.CreateContext(connection);
        var report = await PalladiumFormUpdater.ApplyAsync(db, today);
        PalladiumFormUpdater.PrintReport(Console.Out, report);
        return 0;
    }
    case "ek-bina-rules":
    {
        await using var db = SeedSupport.CreateContext(connection);
        EkBinaRuleSeeder.PrintReport(Console.Out, await EkBinaRuleSeeder.ApplyAsync(db));
        return 0;
    }
    case "ek-bina-rooms":
    {
        await using var db = SeedSupport.CreateContext(connection);
        Console.WriteLine($"Kural yazilan oda: {await EkBinaRoomSeeder.ApplyAsync(db)}");
        return 0;
    }
    case "demo-room-roles":
    {
        await using var db = SeedSupport.CreateContext(connection);
        await EkBinaRoomSeeder.AssignDemoRolesAsync(db, Console.Out);
        return 0;
    }
    case "demo-grades":
    {
        await using var db = SeedSupport.CreateContext(connection);
        await DemoGradeSeeder.AssignAsync(db, Console.Out);
        return 0;
    }
    case "demo-cast":
    {
        await using var db = SeedSupport.CreateContext(connection);
        var castDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul")).DateTime);
        await DemoCastSeeder.ApplyAsync(db, castDay, Console.Out);
        return 0;
    }
    case "scale":
    {
        await using var db = SeedSupport.CreateContext(connection);
        await new ScaleSeeder(db, Console.Out).RunAsync();
        return 0;
    }
    case "activity":
    {
        var departments = Arg("departments")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var options = new ActivityOptions(
            BuildingCode: Arg("building"),
            DepartmentCodes: departments,
            HistoryDays: int.TryParse(Arg("history"), out var h) && h > 0 ? h : 365,
            Seed: int.TryParse(Arg("seed"), out var s) && s != 0 ? s : null,
            Only: Arg("only"));
        await using var db = SeedSupport.CreateContext(connection);
        await new ActivitySeeder(db, today, options, Console.Out).RunAsync();
        return 0;
    }
    case "buildings":
    {
        // Kaynak: migrate-buildings.ts. Oracle semasinda FLOOR.BUILDING_ID zorunlu oldugundan binasiz kat olusamaz;
        // komut Demo Bina kaydini garanti eder ve ozeti basar.
        await using var db = SeedSupport.CreateContext(connection);
        var demo = await db.Buildings.FirstOrDefaultAsync(b => b.Code == "DEMO");
        if (demo is null)
        {
            demo = new Building { Code = "DEMO", Name = "Demo Bina", Address = "Kurgusal gösterim binası (canlıya çıkışta temizlenecek)", Order = 90, Active = false, IsDemo = true };
            db.Buildings.Add(demo);
            await db.SaveChangesAsync();
        }
        Console.WriteLine($"Binasiz kat: 0 -> \"{demo.Name}\" binasina baglandi.");
        foreach (var b in await db.Buildings.OrderBy(b => b.Order).Select(b => new { b.Code, b.Name, b.Active, b.IsDemo, FloorCount = b.Floors.Count }).ToListAsync())
        {
            Console.WriteLine($"  {b.Code,-6} {b.Name,-18} kat={b.FloorCount} active={b.Active.ToString().ToLowerInvariant()} demo={b.IsDemo.ToString().ToLowerInvariant()}");
        }
        return 0;
    }
    default:
        Console.WriteLine("Kullanım: dotnet run --project src/Ofis360.Seed -- <seed|palladium|palladium-form|ek-bina-rules|ek-bina-rooms|demo-room-roles|demo-grades|demo-cast|scale|activity|buildings> [seçenekler]");
        return command == "help" ? 0 : 1;
}
