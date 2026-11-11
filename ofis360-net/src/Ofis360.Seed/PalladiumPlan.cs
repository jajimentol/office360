namespace Ofis360.Seed;

/// <summary>
/// Palladium Tower - 36. Kat yerlesim verisi (kaynak: prisma/palladium-plan.ts, sayisal olarak birebir).
/// Mimari plan 4800 px genisliginde raster olarak okundu; olculer piksel, ToPlan() ile plan birimine cevrilir
/// (1 birim ~ 2,4 px ~ 3 cm). Plan simetriktir: B kanadi, A kanadinin x = 2432 ekseninde aynasidir.
/// Masa ve oda kodlari TEMSILIDIR; is birimi resmi isimleri verdiginde yalnizca Name alanlari degisir.
/// </summary>
public static class PalladiumPlan
{
    public static readonly (string Code, string Name, string Address, int Order, bool Active, bool IsDemo) Building =
        ("PT", "Palladium Tower", "Barbaros Mah. Kardelen Sok. Palladium Tower, Ataşehir / İstanbul", 10, true, false);

    public static readonly (int Number, string Name, string Description, bool Reservable, bool Active, int PlanWidth, int PlanHeight) Floor =
        (36, "Palladium Tower-36.Kat", "Anadolu Bölge Çalışma Ofisi", true, true, 1500, 1260);

    private const int OriginX = 636;
    private const int OriginY = 210;
    private const double Scale = 2.4;
    private const int AxisX = 2432;

    public static int ToPlan(double px, char axis) => (int)Math.Floor((px - (axis == 'x' ? OriginX : OriginY)) / Scale + 0.5);

    public static double MirrorX(double px) => 2 * AxisX - px;

    public readonly record struct Rect(int X, int Y, int Width, int Height);

    /// <summary>Piksel kutusunu plan dikdortgenine cevirir.</summary>
    public static Rect RectFromPx(double x0, double y0, double x1, double y1)
    {
        var x = ToPlan(Math.Min(x0, x1), 'x');
        var y = ToPlan(Math.Min(y0, y1), 'y');
        return new Rect(x, y, Math.Max(4, ToPlan(Math.Max(x0, x1), 'x') - x), Math.Max(4, ToPlan(Math.Max(y0, y1), 'y') - y));
    }

    /// <summary>Merkez + boyut (piksel) → plan merkez + boyut.</summary>
    public static Rect CenterFromPx(double cx, double cy, double width, double height) =>
        new(ToPlan(cx, 'x'), ToPlan(cy, 'y'), (int)Math.Floor(width / Scale + 0.5), (int)Math.Floor(height / Scale + 0.5));

    /// <summary>Mutfagin batisindaki kapali alan: kirmizi taramali tek kroki ogesi, uzerinde bu yazi.</summary>
    public const string AnadoluAreaName = "Anadolu Bölge Müdürlüğü";

    /// <summary>Kroki ogesinin dolgusu; arayuz bunu acik kirmizi tarama olarak cizer.</summary>
    public const string HatchRedFill = "hatch-red";

    /// <summary>Anadolu Bolge Mudurlugu alani: 36-A-04 + 36-A-03 + eski Galata yeri (mutfagin batisi).</summary>
    public static readonly double[] AnadoluAreaPx = [650, 2600, 2222, 3210];

    /// <summary>Galata'nin eski (36-A-02) ve yeni (36-B-04) konumu.</summary>
    public static readonly double[] GalataOldPx = [1875, 2605, 2215, 3210];
    public static readonly double[] GalataNewPx = [3830, 2605, 4210, 3210];

    public sealed record ZoneSpec(string Code, string Name, string Kind, double[] Px, bool WindowSide = false, bool Quiet = false, bool Reservable = true);

    public static readonly ZoneSpec[] Zones =
    [
        new("A05", "A Kanadı · Açık Ofis (Batı)", "WORK", [650, 1010, 1320, 2570], WindowSide: true),
        new("B05", "B Kanadı · Açık Ofis (Doğu)", "WORK", [3544, 1010, 4214, 2570], WindowSide: true),
        // Ek Bina Faz 1 formu: bolge mudurlugu alanlari rezervasyona kapali, yalnizca bolge personeli kullanir.
        // Mutfagin batisi (36-A-04, 36-A-03 ve eski Galata yeri) Anadolu Bolge Mudurlugu'ne ait; rezervasyona kapali.
        new("A03", AnadoluAreaName, "RESTRICTED", [1056, 2600, 1862, 3210], WindowSide: true, Reservable: false),
        new("B03", "B Kanadı · Bölge Müdürlüğü Ortak Alanı", "RESTRICTED", [3002, 2600, 3808, 3210], WindowSide: true, Reservable: false),
        new("AMT", "A Kanadı · Kuzey Toplantı", "MEETING", [1000, 505, 1667, 990], WindowSide: true),
        new("BMT", "B Kanadı · Kuzey Toplantı", "MEETING", [3197, 505, 3864, 990], WindowSide: true),
        new("AEK", "A Kanadı · Ekip Odaları", "MEETING", [1325, 1000, 1667, 2320], Quiet: true),
        new("BEK", "B Kanadı · Ekip Odaları", "MEETING", [3198, 1000, 3539, 2330], Quiet: true),
        new("GIR", "Giriş · Resepsiyon ve Misafir Toplantı", "MEETING", [1862, 2440, 3002, 3210]),
        new("COR", "Ana Koridor", "CORRIDOR", [700, 2435, 4160, 2575], Reservable: false),
        new("CEK", "Çekirdek · Asansör, Merdiven, WC", "CORE", [1667, 489, 3197, 2424], Reservable: false),
        new("TER", "Teras", "SOCIAL", [646, 220, 4218, 500], Reservable: false),
        new("A04", AnadoluAreaName, "RESTRICTED", [650, 2600, 1042, 3210], Reservable: false),
        // Galata ile yer degistirdi: B04 kodu artik eski Galata yerini (Anadolu Bolge Mudurlugu icinde) tutar.
        new("B04", AnadoluAreaName, "RESTRICTED", GalataOldPx, Reservable: false),
    ];

    public sealed record StyleSpec(string? Fill = null, string? Stroke = null, double? StrokeWidth = null, double? FontSize = null);

    public sealed record ElementSpec(string Type, double[] Px, string? Label = null, int? Rotation = null, StyleSpec? Style = null, int? Layer = null);

    private const double WallT = 16;
    private const double GlassT = 10;

    private static ElementSpec HWall(double x0, double x1, double y, double t = WallT) => new("WALL", [x0, y - t / 2, x1, y + t / 2]);
    private static ElementSpec VWall(double x, double y0, double y1, double t = WallT) => new("WALL", [x - t / 2, y0, x + t / 2, y1]);
    private static ElementSpec HGlass(double x0, double x1, double y) => new("WINDOW", [x0, y - GlassT / 2, x1, y + GlassT / 2]);
    private static ElementSpec VGlass(double x, double y0, double y1) => new("WINDOW", [x - GlassT / 2, y0, x + GlassT / 2, y1]);
    private static ElementSpec HDoor(double x0, double x1, double y) => new("DOOR", [x0, y - 8, x1, y + 8]);

    private static ElementSpec VDoor(double x, double y0, double y1)
    {
        var length = y1 - y0;
        var cy = (y0 + y1) / 2;
        return new("DOOR", [x - length / 2, cy - 8, x + length / 2, cy + 8], Rotation: 90);
    }

    private static ElementSpec Column(double cx, double cy, double size = 68) =>
        new("WALL", [cx - size / 2, cy - size / 2, cx + size / 2, cy + size / 2], Style: new StyleSpec(Fill: "#475569"));

    /// <summary>A kanadi ogelerini B kanadina aynalar.</summary>
    private static ElementSpec Mirrored(ElementSpec spec) => spec with
    {
        Px = [MirrorX(spec.Px[2]), spec.Px[1], MirrorX(spec.Px[0]), spec.Px[3]],
        Rotation = spec.Rotation is int r && r != 0 ? -r : spec.Rotation,
    };

    private static readonly ElementSpec[] WestWing =
    [
        HGlass(1000, 1667, 505),
        VGlass(1000, 505, 990),
        HWall(650, 1000, 1000),
        VGlass(641, 1010, 3215),
        VGlass(1325, 1000, 2320),
        HWall(1325, 1667, 1000),
        HWall(1325, 1667, 1442),
        HWall(1325, 1667, 1890),
        HWall(1325, 1667, 2320),
        new("KITCHEN", [1360, 2328, 1660, 2415], "Yazıcı & kahve", Style: new StyleSpec(FontSize: 10)),
        HWall(1360, 1667, 2415, 10),
        VWall(1360, 2320, 2415, 10),
        HWall(757, 1760, 2590),
        HWall(1862, 1979, 2590),
        new("WALL", [2030, 2585, 2240, 2601], Rotation: 43),
        VWall(2222, 2740, 3200),
        VWall(1042, 2600, 2945),
        VWall(1042, 3035, 3200),
        HDoor(1500, 1590, 505),
        new("DOOR", [1000, 992, 1320, 1008], "Sürgülü kapı"),
        VDoor(1325, 1335, 1425),
        VDoor(1325, 1775, 1865),
        VDoor(1325, 2205, 2295),
        VDoor(1042, 2945, 3035),
        HDoor(1979, 2060, 2590),
        Column(793, 284),
        Column(793, 1059),
        Column(788, 1849),
        Column(788, 2635),
        Column(861, 3153),
        Column(1647, 3153),
    ];

    private static readonly ElementSpec[] EastOnly =
    [
        new("RESTRICTED", [3204, 1392, 3480, 1520], "36-B-08 Depo"),
        new("RESTRICTED", [3204, 1532, 3480, 1878], "36-B-07 Server odası"),
        HWall(3198, 3485, 1385),
        HWall(3198, 3485, 1525),
        VWall(3485, 1385, 1880, 10),
    ];

    private static ElementSpec[] CoreElements()
    {
        var list = new List<ElementSpec>
        {
            HWall(1667, 3197, 500, 28),
            HWall(1667, 2296, 2410, 28),
            HWall(2568, 3197, 2410, 28),
            VWall(1681, 489, 2424, 28),
            VWall(3183, 489, 2424, 28),
            new("CORRIDOR", [2300, 540, 2560, 2380], "Asansör holü"),
        };
        foreach (var cy in new[] { 880, 1165, 1463, 1748 })
        {
            list.Add(new("ELEVATOR", [2085, cy - 90, 2265, cy + 90], "Asansör"));
            list.Add(new("ELEVATOR", [2600, cy - 90, 2780, cy + 90], "Asansör"));
        }
        list.AddRange(
        [
            new("ELEVATOR", [2865, 2165, 3110, 2305], "Servis asansörü"),
            new("STAIRS", [1712, 690, 1985, 1110], "Merdiven S1"),
            new("STAIRS", [2880, 1540, 3150, 2000], "Merdiven S2"),
            new("WC", [2585, 540, 3150, 1180], "WC"),
            new("WC", [1712, 1620, 2000, 2360], "WC"),
            new("RESTRICTED", [2000, 1850, 2290, 2360], "Elektrik", Style: new StyleSpec(FontSize: 10)),
            new("RESTRICTED", [1712, 1130, 1985, 1600], "Şaft", Style: new StyleSpec(FontSize: 10)),
            new("RESTRICTED", [2800, 1190, 3150, 1470], "Şaft", Style: new StyleSpec(FontSize: 10)),
            HDoor(2400, 2470, 500),
            HDoor(2380, 2490, 2415),
        ]);
        return list.ToArray();
    }

    private static readonly ElementSpec[] SouthCenter =
    [
        new("ROOM", [2225, 2440, 2640, 2748], "36-00 Giriş · Resepsiyon", Style: new StyleSpec(Fill: "#eef4fb", Stroke: "#2f6db5")),
        new("WALL", [2340, 2690, 2540, 2755], Style: new StyleSpec(Fill: "#2f6db5")),
        new("KITCHEN", [2284, 2765, 2590, 3210], "Mutfak"),
        HWall(2338, 2601, 2755),
        VWall(2596, 2755, 3200),
        HDoor(2284, 2338, 2755),
        new("CORRIDOR", [700, 2440, 4160, 2575], "Ana koridor"),
        HGlass(646, 4218, 3217),
        Column(2442, 3153),
    ];

    private static readonly ElementSpec[] Terrace =
    [
        new("TERRACE", [646, 220, 4218, 500], "Teras"),
        new("TERRACE", [646, 500, 1000, 990], ""),
        new("TERRACE", [3864, 500, 4218, 990], ""),
        HWall(636, 4228, 214, 10),
        VWall(640, 210, 1010, 10),
        VWall(4224, 210, 1010, 10),
    ];

    private static readonly ElementSpec[] Labels =
    [
        new("TEXT", [860, 1105, 1060, 1135], "A KANADI", Style: new StyleSpec(FontSize: 16, Stroke: "#1f5493")),
        new("TEXT", [3800, 1105, 4000, 1135], "B KANADI", Style: new StyleSpec(FontSize: 16, Stroke: "#1f5493")),
        new("TEXT", [700, 2450, 1000, 2475], "36-A-05 Ortak çalışma alanı", Style: new StyleSpec(FontSize: 11)),
        new("TEXT", [3550, 2450, 3850, 2475], "36-B-05 Ortak çalışma alanı", Style: new StyleSpec(FontSize: 11)),
        new("TEXT", [3020, 2612, 3320, 2637], "36-B-03 Ortak çalışma alanı", Style: new StyleSpec(FontSize: 11)),
        new("TEXT", [2380, 240, 2500, 270], "KUZEY ↑", Style: new StyleSpec(FontSize: 12, Stroke: "#64748b")),
    ];

    public static readonly ElementSpec[] Elements =
    [
        .. Terrace,
        .. WestWing,
        .. WestWing
            .Where(item => item.Type != "RESTRICTED" || item.Label?.Contains("A-04") != true)
            .Where(item => !(item.Type == "WALL" && item.Px[1] > 1400 && item.Px[1] < 1460 && item.Px[0] == 1325))
            .Select(Mirrored),
        .. EastOnly,
        .. CoreElements(),
        .. SouthCenter,
        .. Labels,
        new("RESTRICTED", AnadoluAreaPx, AnadoluAreaName, Style: new StyleSpec(Fill: HatchRedFill, Stroke: "#f87171", FontSize: 22), Layer: 600),
    ];

    public sealed record DeskSpec(string Code, string ZoneCode, double Cx, double Cy, double W, double H, string[] Features, string? Notes = null);

    private const double BenchW = 118;
    private const double BenchH = 66;
    private const double EndW = 70;
    private const double EndH = 140;

    /// <summary>Yatay 4'lu bench + pencere tarafinda uc masa (A-05 / B-05 adalari). side = -1 bati (A), +1 dogu (B).</summary>
    private static IEnumerable<DeskSpec> IslandCluster(string prefix, string zone, int index, double cx, double cy, int side)
    {
        var baseIndex = (index - 1) * 5;
        string Code(int n) => $"{prefix}-{baseIndex + n:00}";
        string[] common = ["WINDOW", "SINGLE_MONITOR"];
        return
        [
            new(Code(1), zone, cx - 62, cy - 38, BenchW, BenchH, common),
            new(Code(2), zone, cx + 62, cy - 38, BenchW, BenchH, common),
            new(Code(3), zone, cx - 62, cy + 38, BenchW, BenchH, common),
            new(Code(4), zone, cx + 62, cy + 38, BenchW, BenchH, common),
            new(Code(5), zone, cx + side * 175, cy, EndW, EndH, ["WINDOW", "DUAL_MONITOR", "DOCK"], "Ada ucu, pencereye bakan geniş masa"),
        ];
    }

    /// <summary>Dikey 4'lu bench (A-03 / B-03). withEnd: kuzeyde uc masa var.</summary>
    private static IEnumerable<DeskSpec> SouthCluster(string prefix, string zone, int startIndex, double cx, double cy, bool withEnd)
    {
        string Code(int n) => $"{prefix}-{startIndex + n - 1:00}";
        string[] common = ["WINDOW", "SINGLE_MONITOR"];
        var desks = new List<DeskSpec>
        {
            new(Code(1), zone, cx - 36, cy - 60, 66, 116, common),
            new(Code(2), zone, cx + 36, cy - 60, 66, 116, common),
            new(Code(3), zone, cx - 36, cy + 60, 66, 116, common),
            new(Code(4), zone, cx + 36, cy + 60, 66, 116, common),
        };
        if (withEnd) desks.Add(new(Code(5), zone, cx, cy - 188, 132, 70, ["DUAL_MONITOR", "DOCK"], "Ada ucu masa"));
        return desks;
    }

    private static readonly (double Cx, double Cy)[] WestIslands = [(1034, 1242), (1034, 1608), (1034, 1987), (1033, 2353)];

    public static readonly DeskSpec[] Desks =
    [
        .. WestIslands.SelectMany((p, i) => IslandCluster("PT36-A05", "A05", i + 1, p.Cx, p.Cy, -1)),
        .. WestIslands.SelectMany((p, i) => IslandCluster("PT36-B05", "B05", i + 1, MirrorX(p.Cx), p.Cy, 1)),
        .. SouthCluster("PT36-A03", "A03", 1, 1313, 2978, true),
        .. SouthCluster("PT36-A03", "A03", 6, 1669, 2901, false),
        .. SouthCluster("PT36-B03", "B03", 1, MirrorX(1313), 2978, true),
        .. SouthCluster("PT36-B03", "B03", 6, MirrorX(1669), 2901, false),
    ];

    public const string GalataNotes = "Misafir toplantı odası. 36-B-04 konumuna taşındı (eski yeri Anadolu Bölge Müdürlüğü'ne katıldı). Temsili isim.";

    public sealed record RoomSpec(string Code, string Name, string PlanLabel, int Capacity, double[] Px, string[] Features, string? Notes = null);

    public static readonly RoomSpec[] Rooms =
    [
        new("PT36-A09", "Boğaz", "36-A-09 Toplantı Odası", 12, [1010, 515, 1660, 985], ["SCREEN", "VIDEO", "WHITEBOARD", "DAYLIGHT"], "98\" ekran, terasa açılan kapı. Temsili isim; iş birimi resmi adı verecek."),
        new("PT36-B10", "Marmara", "36-B-10 Toplantı Odası", 12, [3204, 515, 3854, 985], ["SCREEN", "VIDEO", "WHITEBOARD", "DAYLIGHT"], "98\" ekran, terasa açılan kapı. Temsili isim."),
        new("PT36-A02", "Galata", "36-B-04 Toplantı (eski 36-A-02)", 8, GalataNewPx, ["SCREEN", "WHITEBOARD"], GalataNotes),
        new("PT36-B02", "Haliç", "36-B-02 Giriş Toplantı", 8, [2649, 2605, 2989, 3210], ["SCREEN", "WHITEBOARD"], "Giriş yanı misafir toplantı odası. Temsili isim."),
        new("PT36-A08", "Ekip Odası A-1", "36-A-08 Yönetici ve Ekip Çalışma Odası", 5, [1332, 1008, 1660, 1432], ["SCREEN"], "65\" ekran. Yönetici odayı sahiplendiğinde yönetim kapatabilir."),
        new("PT36-A07", "Ekip Odası A-2", "36-A-07 Yönetici ve Ekip Çalışma Odası", 5, [1332, 1450, 1660, 1882], ["SCREEN"], "65\" ekran."),
        new("PT36-A06", "Ekip Odası A-3", "36-A-06 Yönetici ve Ekip Çalışma Odası", 5, [1332, 1898, 1660, 2312], ["SCREEN"], "65\" ekran."),
        new("PT36-B09", "Ekip Odası B-1", "36-B-09 Yönetici ve Ekip Çalışma Odası", 5, [3204, 1008, 3532, 1378], ["SCREEN"], "65\" ekran."),
        new("PT36-B06", "Ekip Odası B-2", "36-B-06 Yönetici ve Ekip Çalışma Odası", 5, [3204, 1888, 3532, 2322], ["SCREEN"], "65\" ekran."),
    ];
}
