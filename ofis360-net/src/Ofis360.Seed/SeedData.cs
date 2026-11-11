namespace Ofis360.Seed;

// Kaynak: prisma/seed-data.ts — kat planlari, masa yerlesimleri, departmanlar ve personel listesi (birebir).

public sealed record DeskGridSpec(int Cols, int Rows, string Prefix, string[][] FeatureSets, int[]? PopularRows = null, int[]? AccessibleIndexes = null);

public sealed record ZoneSpec(
    string Code, string Name, string Kind, int X, int Y, int Width, int Height,
    bool WindowSide = false, bool Quiet = false, bool Reservable = true, DeskGridSpec? Desks = null);

public sealed record RoomSpec(string Code, string Name, int Capacity, int X, int Y, int Width, int Height, string[] Features, string? Notes = null);

public sealed record FloorSpec(int Number, string Name, string Description, bool Reservable, int PlanWidth, int PlanHeight, ZoneSpec[] Zones, RoomSpec[] Rooms);

public sealed record DeskFeatureSpec(string Key, string Name, string Description, string Icon);

public sealed record RoomFeatureSpec(string Key, string Name, string Icon);

public sealed record DepartmentSpec(string Code, string Name, string Color);

public sealed record EmployeeSpec(string FullName, string Title, string DepartmentCode, string Role, double Attendance, bool AccessibilityApproved = false, bool IsDemoAccount = false);

public sealed record AnnouncementSpec(string Title, string Body);

public static class SeedData
{
    public static readonly DeskFeatureSpec[] DeskFeatures =
    [
        new("WINDOW", "Pencere yanı", "Doğal ışık alan, pencereye komşu masa.", "sun"),
        new("DUAL_MONITOR", "Çift monitör", "İki adet 27 inç monitör bulunur.", "monitor"),
        new("SINGLE_MONITOR", "Tek monitör", "Tek adet 24 inç monitör bulunur.", "monitor-dot"),
        new("QUIET", "Sessiz alan", "Odaklanma gerektiren çalışmalar için sessiz bölge.", "volume-off"),
        new("ERGONOMIC", "Ergonomik koltuk", "Bel destekli, yüksekliği ayarlanabilir koltuk.", "armchair"),
        new("STANDING", "Yüksekliği ayarlı masa", "Ayakta çalışmaya uygun elektrikli masa.", "move-vertical"),
        new("DOCK", "Dizüstü dock istasyonu", "Tek kabloyla bağlanan dock istasyonu.", "plug"),
        new("ACCESSIBLE", "Erişilebilir masa", "Tekerlekli sandalye erişimine uygun, geniş manevra alanlı masa.", "accessibility"),
    ];

    public static readonly RoomFeatureSpec[] RoomFeatures =
    [
        new("SCREEN", "Ekran / projeksiyon", "projector"),
        new("VIDEO", "Video konferans", "video"),
        new("WHITEBOARD", "Beyaz tahta", "presentation"),
        new("PHONE", "Konferans telefonu", "phone"),
        new("DAYLIGHT", "Doğal ışık", "sun"),
    ];

    public static readonly DepartmentSpec[] Departments =
    [
        new("TEK", "Teknoloji", "#0f4c81"),
        new("SAT", "Satış", "#14b8a6"),
        new("FIN", "Finans", "#6366f1"),
        new("IK", "İnsan Kaynakları", "#f59e0b"),
        new("OPS", "Operasyon", "#0ea5e9"),
    ];

    public static readonly EmployeeSpec[] Employees =
    [
        // --- Demo hesaplari ---
        new("Elif Yılmaz", "Kıdemli Yazılım Geliştirici", "TEK", "EMPLOYEE", 0.55, IsDemoAccount: true),
        new("Mert Demir", "Operasyon Direktörü", "OPS", "MANAGER", 0.7, IsDemoAccount: true),
        new("Zeynep Kaya", "İş Yeri Deneyimi Yöneticisi", "IK", "ADMIN", 0.85, IsDemoAccount: true),
        new("Burak Şahin", "Finansal Analist", "FIN", "EMPLOYEE", 0.5, AccessibilityApproved: true, IsDemoAccount: true),
        // --- Teknoloji ---
        new("Ahmet Korkmaz", "Yazılım Mimarı", "TEK", "EMPLOYEE", 0.4),
        new("Selin Aydın", "Ürün Yöneticisi", "TEK", "EMPLOYEE", 0.6),
        new("Emre Çelik", "Veri Mühendisi", "TEK", "EMPLOYEE", 0.35),
        new("Deniz Arslan", "Mobil Geliştirici", "TEK", "EMPLOYEE", 0.45),
        new("Gizem Öztürk", "Test Uzmanı", "TEK", "EMPLOYEE", 0.5),
        new("Onur Kılıç", "DevOps Mühendisi", "TEK", "EMPLOYEE", 0.3),
        new("Buse Yıldız", "UX Tasarımcısı", "TEK", "EMPLOYEE", 0.55),
        new("Kerem Aksoy", "Yazılım Geliştirici", "TEK", "EMPLOYEE", 0.42),
        new("Merve Doğan", "Veri Bilimci", "TEK", "EMPLOYEE", 0.38),
        new("Serkan Polat", "Sistem Yöneticisi", "TEK", "EMPLOYEE", 0.62),
        // --- Satış ---
        new("Ayşe Erdem", "Satış Müdürü", "SAT", "MANAGER", 0.75),
        new("Cem Bulut", "Kurumsal Satış Uzmanı", "SAT", "EMPLOYEE", 0.68),
        new("Ece Tan", "Satış Destek Uzmanı", "SAT", "EMPLOYEE", 0.72),
        new("Halil Güneş", "Bölge Satış Temsilcisi", "SAT", "EMPLOYEE", 0.58),
        new("İrem Acar", "Müşteri İlişkileri Uzmanı", "SAT", "EMPLOYEE", 0.65),
        new("Kaan Yalçın", "İş Geliştirme Uzmanı", "SAT", "EMPLOYEE", 0.6),
        new("Nazlı Şen", "Satış Analisti", "SAT", "EMPLOYEE", 0.52),
        new("Umut Karaca", "Kanal Satış Uzmanı", "SAT", "EMPLOYEE", 0.48),
        // --- Finans ---
        new("Pınar Uysal", "Finans Müdürü", "FIN", "MANAGER", 0.7),
        new("Tolga Ateş", "Muhasebe Uzmanı", "FIN", "EMPLOYEE", 0.66),
        new("Sibel Koç", "Bütçe ve Raporlama Uzmanı", "FIN", "EMPLOYEE", 0.6),
        new("Barış Ünal", "Hazine Uzmanı", "FIN", "EMPLOYEE", 0.44),
        new("Ceren Aslan", "İç Kontrol Uzmanı", "FIN", "EMPLOYEE", 0.5),
        new("Fatih Turan", "Finansal Planlama Uzmanı", "FIN", "EMPLOYEE", 0.4),
        new("Melis Sarı", "Risk Analisti", "FIN", "EMPLOYEE", 0.46),
        // --- İnsan Kaynakları ---
        new("Gökhan Duman", "İK İş Ortağı", "IK", "EMPLOYEE", 0.74),
        new("Aslı Çetin", "İşe Alım Uzmanı", "IK", "EMPLOYEE", 0.8),
        new("Volkan Ekin", "Eğitim ve Gelişim Uzmanı", "IK", "EMPLOYEE", 0.62),
        new("Damla Ünver", "Bordro Uzmanı", "IK", "EMPLOYEE", 0.7),
        new("Sinem Bora", "Çalışan Deneyimi Uzmanı", "IK", "EMPLOYEE", 0.68),
        new("Okan Tekin", "İK Analisti", "IK", "EMPLOYEE", 0.55),
        // --- Operasyon ---
        new("Hande Yavuz", "Operasyon Uzmanı", "OPS", "EMPLOYEE", 0.78),
        new("İlker Balcı", "Tedarik Zinciri Uzmanı", "OPS", "EMPLOYEE", 0.72),
        new("Şeyma Kurt", "Süreç Geliştirme Uzmanı", "OPS", "EMPLOYEE", 0.6),
        new("Yusuf Ergin", "Lojistik Uzmanı", "OPS", "EMPLOYEE", 0.82),
        new("Tuğçe Aydemir", "Kalite Uzmanı", "OPS", "EMPLOYEE", 0.64),
        new("Emirhan Soylu", "Saha Operasyon Uzmanı", "OPS", "EMPLOYEE", 0.7),
        new("Nihan Özkan", "Operasyon Analisti", "OPS", "EMPLOYEE", 0.56),
        new("Cansu Erol", "Ofis Yönetimi Sorumlusu", "OPS", "EMPLOYEE", 0.88),
        new("Berk Şimşek", "İş Sürekliliği Uzmanı", "OPS", "EMPLOYEE", 0.5),
    ];

    private static readonly string[][] StandardSets =
    [
        ["DUAL_MONITOR", "DOCK", "ERGONOMIC"],
        ["SINGLE_MONITOR", "DOCK"],
        ["DUAL_MONITOR", "STANDING", "DOCK"],
        ["SINGLE_MONITOR", "ERGONOMIC"],
    ];

    private static readonly string[][] WindowSets =
    [
        ["WINDOW", "DUAL_MONITOR", "DOCK", "ERGONOMIC"],
        ["WINDOW", "DUAL_MONITOR", "STANDING"],
        ["WINDOW", "SINGLE_MONITOR", "DOCK"],
    ];

    private static readonly string[][] QuietSets =
    [
        ["QUIET", "DUAL_MONITOR", "DOCK"],
        ["QUIET", "SINGLE_MONITOR", "ERGONOMIC"],
        ["QUIET", "DUAL_MONITOR", "ERGONOMIC", "STANDING"],
    ];

    public static readonly FloorSpec[] Floors =
    [
        new(1, "1. Kat", "Giriş katı: karşılama, ortak çalışma alanı ve toplantı odaları.", true, 1040, 660,
            [
                new("CORE", "Asansör ve Merdiven", "CORE", 30, 270, 110, 150, Reservable: false),
                new("KOR", "Ana Koridor", "CORRIDOR", 160, 300, 850, 56, Reservable: false),
                new("A", "A Bölgesi – Pencere Kenarı", "WORK", 160, 40, 380, 240, WindowSide: true,
                    Desks: new DeskGridSpec(4, 2, "F1-A", WindowSets, PopularRows: [0])),
                new("B", "B Bölgesi – Ortak Alan", "WORK", 560, 40, 200, 240, Desks: new DeskGridSpec(2, 2, "F1-B", StandardSets)),
                new("MTG-K", "Toplantı Bölgesi (Kuzey)", "MEETING", 780, 40, 230, 240, Reservable: false),
                new("KAFE", "Karşılama ve Kafe", "SOCIAL", 160, 376, 300, 250, Reservable: false),
                new("C", "C Bölgesi – Sessiz Alan", "WORK", 480, 376, 280, 250, Quiet: true,
                    Desks: new DeskGridSpec(2, 2, "F1-C", QuietSets, AccessibleIndexes: [0])),
                new("MTG-G", "Toplantı Bölgesi (Güney)", "MEETING", 780, 376, 230, 250, Reservable: false),
            ],
            [
                new("T101", "Boğaziçi", 12, 792, 52, 206, 105, ["SCREEN", "VIDEO", "WHITEBOARD", "PHONE"], "Müşteri sunumları için tercih edilir."),
                new("T102", "Kapadokya", 8, 792, 168, 206, 100, ["SCREEN", "VIDEO", "WHITEBOARD"]),
                new("T103", "Efes", 6, 792, 388, 206, 110, ["SCREEN", "WHITEBOARD"]),
                new("T104", "Truva", 4, 792, 508, 206, 106, ["VIDEO", "PHONE"], "Kısa görüşmeler ve birebir toplantılar için uygundur."),
            ]),
        new(2, "2. Kat", "Ortak çalışma alanları, mutfak ve iki toplantı odası.", true, 1040, 660,
            [
                new("CORE", "Asansör ve Merdiven", "CORE", 30, 40, 110, 140, Reservable: false),
                new("KOR", "Dikey Koridor", "CORRIDOR", 470, 40, 60, 580, Reservable: false),
                new("D", "D Bölgesi – Batı Pencere", "WORK", 30, 200, 410, 200, WindowSide: true, Desks: new DeskGridSpec(4, 2, "F2-D", WindowSets, PopularRows: [0])),
                new("E", "E Bölgesi – Batı İç Alan", "WORK", 30, 420, 410, 200, Desks: new DeskGridSpec(3, 2, "F2-E", StandardSets, AccessibleIndexes: [0])),
                new("F", "F Bölgesi – Doğu Pencere", "WORK", 560, 40, 450, 250, WindowSide: true, Desks: new DeskGridSpec(3, 2, "F2-F", WindowSets, PopularRows: [0])),
                new("MUTFAK", "Mutfak ve Dinlenme", "SOCIAL", 560, 320, 210, 140, Reservable: false),
                new("MTG", "Toplantı Bölgesi", "MEETING", 800, 320, 210, 300, Reservable: false),
            ],
            [
                new("T201", "Uludağ", 10, 812, 332, 186, 130, ["SCREEN", "VIDEO", "WHITEBOARD"]),
                new("T202", "Erciyes", 6, 812, 480, 186, 128, ["SCREEN", "WHITEBOARD", "DAYLIGHT"]),
            ]),
        new(3, "3. Kat", "Ortak çalışma alanları, sessiz odak bölgesi ve sosyal alan.", true, 1040, 660,
            [
                new("CORE", "Asansör ve Merdiven", "CORE", 450, 40, 140, 120, Reservable: false),
                new("KOR", "Halka Koridor", "CORRIDOR", 60, 300, 920, 56, Reservable: false),
                new("H", "H Bölgesi – Kuzeybatı Pencere", "WORK", 60, 40, 360, 240, WindowSide: true, Desks: new DeskGridSpec(3, 2, "F3-H", WindowSets, PopularRows: [0])),
                new("I", "I Bölgesi – Kuzeydoğu", "WORK", 620, 40, 360, 240, Desks: new DeskGridSpec(3, 2, "F3-I", StandardSets)),
                new("J", "J Bölgesi – Güney Sessiz Alan", "WORK", 60, 380, 520, 240, Quiet: true, Desks: new DeskGridSpec(2, 2, "F3-J", QuietSets, AccessibleIndexes: [0])),
                new("SOSYAL", "Sosyal Alan", "SOCIAL", 620, 380, 170, 240, Reservable: false),
                new("MTG", "Toplantı Bölgesi", "MEETING", 810, 380, 170, 240, Reservable: false),
            ],
            [
                new("T301", "Ağrı", 6, 818, 392, 154, 216, ["SCREEN", "VIDEO", "WHITEBOARD", "DAYLIGHT"]),
            ]),
        new(4, "4. Kat", "Genel Müdürlük katı. Çalışan rezervasyonuna kapalıdır.", false, 1040, 660,
            [
                new("GM", "Genel Müdürlük Ofisleri", "RESTRICTED", 60, 40, 500, 250, Reservable: false, Desks: new DeskGridSpec(3, 2, "F4-G", StandardSets)),
                new("MTG", "Yönetim Toplantı Bölgesi", "MEETING", 620, 40, 360, 250, Reservable: false),
                new("CORE", "Asansör ve Merdiven", "CORE", 60, 340, 140, 140, Reservable: false),
                new("KOR", "Yönetim Koridoru", "CORRIDOR", 240, 360, 740, 56, Reservable: false),
                new("KAFE", "Yönetim Kafe", "SOCIAL", 240, 460, 300, 160, Reservable: false),
            ],
            [
                new("T401", "Yönetim Kurulu Salonu", 20, 632, 52, 336, 226, ["SCREEN", "VIDEO", "WHITEBOARD", "PHONE", "DAYLIGHT"], "Genel Müdürlük onayı ile kullanılır."),
            ]),
    ];

    public static readonly string[] MeetingTitles =
    [
        "Haftalık ekip toplantısı", "Sprint planlama", "Müşteri sunumu", "Bütçe değerlendirme", "İşe alım görüşmesi",
        "Proje durum toplantısı", "Operasyon koordinasyon", "Ürün yol haritası", "Aylık performans değerlendirme",
        "Tedarikçi görüşmesi", "Süreç iyileştirme çalıştayı", "Eğitim oturumu",
    ];

    public static readonly string[] MeetingPurposes =
    [
        "Ekip içi koordinasyon", "Müşteri görüşmesi", "Proje çalışması", "Değerlendirme toplantısı", "Eğitim ve gelişim", "İşe alım süreci",
    ];

    public static readonly AnnouncementSpec[] Announcements =
    [
        new("Yeni dönem hibrit çalışma takvimi",
            "Eylül ayı itibarıyla ekiplerin ortak ofis günü Çarşamba olarak belirlenmiştir. Rezervasyonlarınızı en geç bir gün önceden oluşturmanızı rica ederiz."),
        new("2. kat mutfak yenileme çalışması",
            "Mutfak alanındaki yenileme çalışması nedeniyle hafta boyunca 2. kat mutfağı sınırlı hizmet verecektir. 1. kattaki kafe alanını kullanabilirsiniz."),
    ];
}
