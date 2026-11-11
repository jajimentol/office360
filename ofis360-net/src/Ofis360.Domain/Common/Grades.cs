using System.Globalization;
using System.Text;

namespace Ofis360.Domain.Common;

/// <summary>Turkce metinleri karsilastirma anahtarina cevirir: buyuk/kucuk harf, Turkce karakter ve noktalama farklari yok sayilir.</summary>
public static class TurkishText
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static string Key(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var builder = new StringBuilder(text.Length);
        foreach (var c in text.Trim().ToLower(Turkish))
        {
            var mapped = c switch { 'ç' => 'c', 'ğ' => 'g', 'ı' => 'i', 'ö' => 'o', 'ş' => 's', 'ü' => 'u', 'â' => 'a', 'î' => 'i', 'û' => 'u', _ => c };
            if (char.IsLetterOrDigit(mapped)) builder.Append(mapped);
        }
        return builder.ToString();
    }
}

/// <summary>
/// Kurumsal kademe. Uygulama yetkisini belirleyen <see cref="Roles"/>'ten ayridir: Yonetici rolu analitik ve CSV
/// yetkisi de verdigi icin kademe bu role baglanmaz. Rezervasyon kurallari kademeye gore degisir.
/// Kademesi henuz bilinmeyen kullanicida deger null'dur.
/// </summary>
public static class Grades
{
    public const string AssistantSpecialist = "ASSISTANT_SPECIALIST";
    public const string Specialist = "SPECIALIST";
    public const string SeniorSpecialist = "SENIOR_SPECIALIST";
    public const string AssistantManager = "ASSISTANT_MANAGER";
    public const string Manager = "MANAGER";
    public const string SeniorManager = "SENIOR_MANAGER";
    public const string Director = "DIRECTOR";
    public const string DeputyGm = "DEPUTY_GM";
    public const string Gm = "GM";

    /// <summary>Kidem sirasina gore, en alttan en uste.</summary>
    public static readonly IReadOnlyList<string> All =
        [AssistantSpecialist, Specialist, SeniorSpecialist, AssistantManager, Manager, SeniorManager, Director, DeputyGm, Gm];

    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        [AssistantSpecialist] = "Uzman Yardımcısı",
        [Specialist] = "Uzman",
        [SeniorSpecialist] = "Kıdemli Uzman",
        [AssistantManager] = "Müdür Yardımcısı",
        [Manager] = "Müdür",
        [SeniorManager] = "Kıdemli Müdür",
        [Director] = "Direktör",
        [DeputyGm] = "Genel Müdür Yardımcısı",
        [Gm] = "Genel Müdür",
    };

    private static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>
    {
        ["uzmanyardimcisi"] = AssistantSpecialist,
        ["uzmanyrd"] = AssistantSpecialist,
        ["uzyrd"] = AssistantSpecialist,
        ["uzman"] = Specialist,
        ["kidemliuzman"] = SeniorSpecialist,
        ["kduzman"] = SeniorSpecialist,
        ["muduryardimcisi"] = AssistantManager,
        ["muduryrd"] = AssistantManager,
        ["mudyrd"] = AssistantManager,
        ["mudur"] = Manager,
        ["kidemlimudur"] = SeniorManager,
        ["kdmudur"] = SeniorManager,
        ["direktor"] = Director,
        ["genelmuduryardimcisi"] = DeputyGm,
        ["genelmuduryrd"] = DeputyGm,
        ["gmy"] = DeputyGm,
        ["genelmudur"] = Gm,
        ["gm"] = Gm,
    };

    public static bool IsValid(string? grade) => grade is not null && All.Contains(grade);

    public static string? Label(string? grade) => grade is null ? null : Labels.GetValueOrDefault(grade, grade);

    /// <summary>Kidem sirasi; bilinmeyen veya atanmamis kademe en alttadir (-1).</summary>
    public static int Rank(string? grade) => grade is null ? -1 : All.ToList().IndexOf(grade);

    /// <summary>Dosyadan gelen metni kademeye cevirir ("Uzman Yrd.", "GMY", "KIDEMLİ MÜDÜR" gibi); taninmazsa null.</summary>
    public static string? Parse(string? text)
    {
        var key = TurkishText.Key(text);
        return key.Length == 0 ? null : Aliases.GetValueOrDefault(key);
    }
}
