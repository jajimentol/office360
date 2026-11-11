namespace Ofis360.Seed;

/// <summary>300 calisanlik kurumsal olcek icin demo veri tanimlari (kaynak: prisma/scale-data.ts). Isimler kurgusaldir.</summary>
public static class ScaleData
{
    public sealed record TargetDepartment(string Code, string Name, string Color, int Headcount);

    public static readonly TargetDepartment[] TargetDepartments =
    [
        new("HAS", "Hasar", "#1f5493", 55),
        new("BT", "Bilgi Teknolojileri", "#0d9488", 45),
        new("SAT", "Satış ve Acenteler", "#7c3aed", 42),
        new("TEK", "Teknik ve Ürün", "#b45309", 35),
        new("OPR", "Operasyon", "#0284c7", 30),
        new("FIN", "Finans", "#dc2626", 22),
        new("IK", "İnsan Kaynakları", "#0f766e", 18),
        new("PZR", "Pazarlama", "#9333ea", 16),
        new("HUK", "Hukuk ve Uyum", "#475569", 14),
        new("AKT", "Aktüerya ve Risk", "#c2410c", 13),
        new("UYD", "Üst Yönetim ve Destek", "#1e293b", 10),
    ];

    /// <summary>Mevcut departman kodlarinin hedef kodlara eslemesi (kayit silinmez, kod/ad guncellenir).</summary>
    public static readonly Dictionary<string, string> DepartmentMigration = new()
    {
        ["TEK"] = "BT", ["SAT"] = "SAT", ["FIN"] = "FIN", ["IK"] = "IK", ["OPS"] = "OPR",
    };

    /// <summary>Departmanlarin ofise birlikte geldigi gunler (1=Pazartesi ... 5=Cuma).</summary>
    public static readonly Dictionary<string, int[]> DepartmentOfficeDays = new()
    {
        ["HAS"] = [2, 4], ["BT"] = [2, 3], ["FIN"] = [3], ["SAT"] = [3, 4], ["IK"] = [2], ["TEK"] = [2, 3],
        ["OPR"] = [3, 4], ["PZR"] = [3], ["HUK"] = [4], ["AKT"] = [3], ["UYD"] = [2, 3],
    };

    /// <summary>Gun bazinda hedef doluluk orani (Pzt %40, Sal %78, Car %88, Per %72, Cum %32).</summary>
    public static readonly Dictionary<int, double> WeekdayAttendance = new() { [1] = 0.4, [2] = 0.78, [3] = 0.88, [4] = 0.72, [5] = 0.32 };

    public static readonly string[] FirstNames =
    [
        "Ayşe", "Fatma", "Emine", "Hatice", "Merve", "Sultan", "Meryem", "Şerife", "Zehra", "Hanife",
        "Özlem", "Yasemin", "Esra", "Büşra", "Melike", "Rabia", "Selin", "Derya", "Gamze", "Pınar",
        "Nurcan", "Sevgi", "Aylin", "Ebru", "Tuğba", "Damla", "Ceren", "İrem", "Nihan", "Bahar",
        "Mehmet", "Mustafa", "Ahmet", "Hüseyin", "Hasan", "İbrahim", "Osman", "Yusuf", "Murat", "Ömer",
        "Ramazan", "Kemal", "Serkan", "Emre", "Onur", "Uğur", "Cem", "Barış", "Tolga", "Kaan",
        "Berk", "Eren", "Furkan", "Oğuz", "Sinan", "Tarık", "Koray", "Ozan", "Erdem", "Alper",
    ];

    public static readonly string[] LastNames =
    [
        "Çelik", "Yıldız", "Yıldırım", "Öztürk", "Aydın", "Özdemir", "Arslan", "Doğan", "Kılıç", "Aslan",
        "Çetin", "Kara", "Koç", "Kurt", "Özkan", "Şimşek", "Polat", "Korkmaz", "Yavuz", "Bulut",
        "Aksoy", "Alkan", "Bilgin", "Ateş", "Güneş", "Tekin", "Sarı", "Turan", "Bozkurt", "Ünal",
        "Acar", "Güler", "Duman", "Karaca", "Avcı", "Işık", "Tunç", "Balcı", "Toprak", "Ergin",
        "Sezer", "Uysal", "Taş", "Keskin", "Sönmez", "Yalçın", "Baran", "Çakır", "Kaplan", "Altun",
        "Gündüz", "Soydan", "Ekinci", "Başaran", "Aydoğan", "Çakmak", "Şen", "Kandemir", "Ünver", "Batur",
    ];

    public static readonly Dictionary<string, string[]> Titles = new()
    {
        ["HAS"] = ["Hasar Eksperi", "Hasar Uzmanı", "Kıdemli Hasar Uzmanı", "Hasar Değerlendirme Uzmanı", "Hasar Takip Sorumlusu"],
        ["BT"] = ["Yazılım Geliştirici", "Kıdemli Yazılım Geliştirici", "Sistem Uzmanı", "Veri Mühendisi", "Test Uzmanı", "DevOps Mühendisi"],
        ["SAT"] = ["Satış Temsilcisi", "Acente Yöneticisi", "Bölge Satış Uzmanı", "Kurumsal Satış Uzmanı", "Kanal Geliştirme Uzmanı"],
        ["TEK"] = ["Ürün Uzmanı", "Teknik Analist", "Underwriter", "Ürün Geliştirme Uzmanı", "Poliçe Teknik Uzmanı"],
        ["OPR"] = ["Operasyon Uzmanı", "Süreç Uzmanı", "Müşteri Hizmetleri Uzmanı", "Operasyon Sorumlusu"],
        ["FIN"] = ["Mali İşler Uzmanı", "Muhasebe Uzmanı", "Bütçe ve Raporlama Uzmanı", "Hazine Uzmanı"],
        ["IK"] = ["İnsan Kaynakları Uzmanı", "İşe Alım Uzmanı", "Eğitim ve Gelişim Uzmanı", "Bordro Uzmanı"],
        ["PZR"] = ["Pazarlama Uzmanı", "Dijital Pazarlama Uzmanı", "Marka İletişimi Uzmanı", "İçerik Uzmanı"],
        ["HUK"] = ["Hukuk Müşaviri", "Uyum Uzmanı", "Sözleşme Uzmanı", "Mevzuat Uzmanı"],
        ["AKT"] = ["Aktüer", "Aktüerya Uzmanı", "Risk Analisti", "Reasürans Uzmanı"],
        ["UYD"] = ["Yönetici Asistanı", "Kurumsal İletişim Uzmanı", "İdari İşler Uzmanı", "Strateji Uzmanı"],
    };

    public static readonly Dictionary<string, string> ManagerTitles = new()
    {
        ["HAS"] = "Hasar Direktörü", ["BT"] = "Bilgi Teknolojileri Direktörü", ["SAT"] = "Satış Direktörü", ["TEK"] = "Teknik ve Ürün Direktörü",
        ["OPR"] = "Operasyon Direktörü", ["FIN"] = "Finans Direktörü", ["IK"] = "İnsan Kaynakları Direktörü", ["PZR"] = "Pazarlama Direktörü",
        ["HUK"] = "Hukuk ve Uyum Direktörü", ["AKT"] = "Aktüerya ve Risk Direktörü", ["UYD"] = "Genel Müdür Yardımcısı",
    };

    public sealed record NewDeskSpec(int FloorNumber, string ZoneCode, string Prefix, int StartIndex, int Count, double PopularRatio, string[][] FeatureSets, int AccessibleCount, int InactiveCount);

    public static readonly NewDeskSpec[] NewDesks =
    [
        new(1, "A", "F1-A", 9, 8, 0.6, [["WINDOW", "DUAL_MONITOR"], ["WINDOW", "ERGONOMIC"], ["WINDOW", "DOCK", "DUAL_MONITOR"], ["WINDOW", "SINGLE_MONITOR"]], 1, 0),
        new(1, "B", "F1-B", 5, 8, 0, [["DUAL_MONITOR", "DOCK"], ["SINGLE_MONITOR"], ["ERGONOMIC", "DOCK"], ["STANDING", "DUAL_MONITOR"]], 1, 1),
        new(1, "C", "F1-C", 5, 6, 0, [["QUIET", "SINGLE_MONITOR"], ["QUIET", "ERGONOMIC"], ["QUIET", "DOCK"]], 1, 0),
        new(2, "D", "F2-D", 9, 12, 0.5, [["WINDOW", "DUAL_MONITOR"], ["WINDOW", "ERGONOMIC", "DOCK"], ["WINDOW", "SINGLE_MONITOR"], ["WINDOW", "STANDING"]], 1, 1),
        new(2, "E", "F2-E", 7, 14, 0, [["DUAL_MONITOR", "DOCK"], ["SINGLE_MONITOR"], ["ERGONOMIC"], ["STANDING", "DOCK"], ["DUAL_MONITOR"]], 2, 1),
        new(2, "F", "F2-F", 7, 12, 0.5, [["WINDOW", "DUAL_MONITOR", "DOCK"], ["WINDOW", "ERGONOMIC"], ["WINDOW", "SINGLE_MONITOR"]], 1, 0),
        new(3, "H", "F3-H", 7, 12, 0.5, [["WINDOW", "DUAL_MONITOR"], ["WINDOW", "DOCK"], ["WINDOW", "ERGONOMIC", "DUAL_MONITOR"]], 1, 1),
        new(3, "I", "F3-I", 7, 12, 0, [["DUAL_MONITOR", "DOCK"], ["SINGLE_MONITOR"], ["STANDING"], ["ERGONOMIC", "DUAL_MONITOR"]], 1, 1),
        new(3, "J", "F3-J", 5, 12, 0, [["QUIET", "SINGLE_MONITOR"], ["QUIET", "ERGONOMIC"], ["QUIET", "DOCK", "DUAL_MONITOR"]], 1, 0),
    ];

    public sealed record NewRoomSpec(int FloorNumber, string Code, string Name, int Capacity, int Width, int Height, string[] Features);

    public static readonly NewRoomSpec[] NewRooms =
    [
        new(1, "T105", "Nemrut", 10, 190, 130, ["SCREEN", "VIDEO", "WHITEBOARD"]),
        new(1, "T106", "Sümela", 4, 150, 105, ["SCREEN", "PHONE"]),
        new(2, "T203", "Kaçkar", 12, 200, 140, ["SCREEN", "VIDEO", "WHITEBOARD", "PHONE"]),
        new(2, "T204", "Palandöken", 8, 175, 120, ["SCREEN", "WHITEBOARD"]),
        new(2, "T205", "Ilgaz", 4, 150, 105, ["PHONE"]),
        new(2, "T206", "Bolkar", 6, 165, 115, ["SCREEN", "VIDEO"]),
        new(3, "T302", "Toroslar", 14, 210, 145, ["SCREEN", "VIDEO", "WHITEBOARD", "DAYLIGHT"]),
        new(3, "T303", "Kızılırmak", 8, 175, 120, ["SCREEN", "WHITEBOARD"]),
        new(3, "T304", "Munzur", 4, 150, 105, ["PHONE"]),
        new(3, "T305", "Nallıhan", 6, 165, 115, ["SCREEN", "VIDEO"]),
    ];

    public const string ScaleMarkerKey = "demo.scale.enterprise300";
}
