namespace Ofis360.Domain.Plan;

/// <summary>Kaynak: src/lib/plan.ts PLAN_ELEMENT_TYPES / LINKABLE_RESOURCE_TYPES.</summary>
public static class PlanElementTypes
{
    public const string Wall = "WALL";
    public const string Room = "ROOM";
    public const string Corridor = "CORRIDOR";
    public const string Door = "DOOR";
    public const string Window = "WINDOW";
    public const string Elevator = "ELEVATOR";
    public const string Stairs = "STAIRS";
    public const string Kitchen = "KITCHEN";
    public const string Wc = "WC";
    public const string Restricted = "RESTRICTED";
    public const string Terrace = "TERRACE";
    public const string Text = "TEXT";

    public static readonly IReadOnlyList<string> All =
        [Wall, Room, Corridor, Door, Window, Elevator, Stairs, Kitchen, Wc, Restricted, Terrace, Text];

    public static bool IsValid(string? type) => type is not null && All.Contains(type);
}

public static class LinkedResourceTypes
{
    public const string Desk = "DESK";
    public const string MeetingRoom = "MEETING_ROOM";

    public static readonly IReadOnlyList<string> All = [Desk, MeetingRoom];

    public static bool IsValid(string? type) => type is not null && All.Contains(type);
}

public sealed record PlanElementDefaultStyle(string Fill, string Stroke, double StrokeWidth, double? FontSize = null);

public sealed record PlanElementTypeDefinition(
    string Type,
    string Label,
    string Description,
    string Icon,
    int DefaultWidth,
    int DefaultHeight,
    string? DefaultLabel,
    PlanElementDefaultStyle DefaultStyle,
    bool Rotatable,
    bool LineTool,
    int BaseLayer);

public sealed record PlanColorOption(string Value, string Label);

/// <summary>Kaynak: PLAN_ELEMENT_DEFINITIONS; degerler birebir.</summary>
public static class PlanElementDefinitions
{
    public static readonly IReadOnlyDictionary<string, PlanElementTypeDefinition> All = new Dictionary<string, PlanElementTypeDefinition>
    {
        [PlanElementTypes.Room] = new(PlanElementTypes.Room, "Oda / bölge", "Çalışma alanı, ofis veya bölge dikdörtgeni", "square",
            260, 180, "Yeni bölge", new("#f8fafc", "#cbd5e1", 2), Rotatable: false, LineTool: false, BaseLayer: 10),
        [PlanElementTypes.Corridor] = new(PlanElementTypes.Corridor, "Koridor", "Geçiş alanı", "move-horizontal",
            400, 56, "Koridor", new("#f1f5f9", "#e2e8f0", 2), Rotatable: true, LineTool: false, BaseLayer: 12),
        [PlanElementTypes.Wall] = new(PlanElementTypes.Wall, "Duvar", "Başlangıç ve bitiş noktasıyla çizilir", "minus",
            200, 10, null, new("#334155", "#1e293b", 0), Rotatable: true, LineTool: true, BaseLayer: 30),
        [PlanElementTypes.Door] = new(PlanElementTypes.Door, "Kapı", "Duvar üzerindeki geçiş", "door-open",
            44, 12, null, new("#fbbf24", "#b45309", 1.5), Rotatable: true, LineTool: false, BaseLayer: 40),
        [PlanElementTypes.Window] = new(PlanElementTypes.Window, "Pencere", "Cephe / doğal ışık", "panel-top",
            120, 10, null, new("#bae6fd", "#0284c7", 1.5), Rotatable: true, LineTool: false, BaseLayer: 40),
        [PlanElementTypes.Elevator] = new(PlanElementTypes.Elevator, "Asansör", "Asansör kovası", "arrow-up-down",
            100, 90, "Asansör", new("#e2e8f0", "#94a3b8", 2), Rotatable: false, LineTool: false, BaseLayer: 20),
        [PlanElementTypes.Stairs] = new(PlanElementTypes.Stairs, "Merdiven", "Merdiven boşluğu", "stairs",
            100, 120, "Merdiven", new("#e2e8f0", "#94a3b8", 2), Rotatable: true, LineTool: false, BaseLayer: 20),
        [PlanElementTypes.Kitchen] = new(PlanElementTypes.Kitchen, "Mutfak / sosyal alan", "Kafe, mutfak veya dinlenme alanı", "coffee",
            220, 160, "Mutfak", new("#f0fdfa", "#5eead4", 2), Rotatable: false, LineTool: false, BaseLayer: 15),
        [PlanElementTypes.Wc] = new(PlanElementTypes.Wc, "WC", "Islak hacim", "droplets",
            110, 110, "WC", new("#eef2ff", "#a5b4fc", 2), Rotatable: false, LineTool: false, BaseLayer: 15),
        [PlanElementTypes.Restricted] = new(PlanElementTypes.Restricted, "Rezervasyona kapalı alan", "Çalışan rezervasyonuna kapalı bölüm", "lock",
            200, 150, "Kapalı alan", new("#fef2f2", "#fca5a5", 2), Rotatable: false, LineTool: false, BaseLayer: 15),
        [PlanElementTypes.Terrace] = new(PlanElementTypes.Terrace, "Teras / açık alan", "Teras, bahçe veya açık hava alanı", "tree-pine",
            300, 120, "Teras", new("#ecfdf5", "#86efac", 2), Rotatable: false, LineTool: false, BaseLayer: 5),
        [PlanElementTypes.Text] = new(PlanElementTypes.Text, "Metin / etiket", "Serbest açıklama", "type",
            160, 28, "Etiket", new("transparent", "transparent", 0, FontSize: 14), Rotatable: true, LineTool: false, BaseLayer: 50),
    };

    public static PlanElementTypeDefinition Defaults(string type) => All[type];

    /// <summary>Krokide gosterilecek tur etiketi; tanimsiz turde turun kendisi.</summary>
    public static string Label(string type) => All.TryGetValue(type, out var def) ? def.Label : type;

    /// <summary>Ozellik panelinde sunulan kontrollu renk secenekleri.</summary>
    public static readonly IReadOnlyList<PlanColorOption> FillColors =
    [
        new("#ffffff", "Beyaz"), new("#f8fafc", "Açık gri"), new("#e2e8f0", "Gri"), new("#eef4fb", "Açık mavi"),
        new("#dbeafe", "Mavi"), new("#f0fdfa", "Turkuaz"), new("#fef9c3", "Sarı"), new("#fef2f2", "Kırmızı"),
        new("#f5f3ff", "Mor"), new("#334155", "Koyu gri"), new("transparent", "Saydam"),
    ];

    public static readonly IReadOnlyList<PlanColorOption> StrokeColors =
    [
        new("#1e293b", "Koyu"), new("#94a3b8", "Gri"), new("#cbd5e1", "Açık gri"), new("#2f6db5", "Mavi"),
        new("#0d9488", "Turkuaz"), new("#b45309", "Turuncu"), new("#dc2626", "Kırmızı"), new("transparent", "Çizgisiz"),
    ];
}
