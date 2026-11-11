using System.Text.Json;

namespace Ofis360.Domain.Plan;

public readonly record struct PlanPoint(double X, double Y);

public readonly record struct PlanRect(double X, double Y, double Width, double Height);

/// <summary>Kaynak: src/lib/plan.ts PlanElementStyle (JSON: fill, stroke, strokeWidth, fontSize).</summary>
public sealed record PlanElementStyle(
    string? Fill = null,
    string? Stroke = null,
    double? StrokeWidth = null,
    double? FontSize = null)
{
    public static readonly PlanElementStyle Empty = new();
}

/// <summary>Kaynak: src/lib/plan.ts geometri yardimcilari.</summary>
public static class PlanGeometry
{
    public const int GridSize = 10;
    public const int MinSize = 8;
    public const int MaxElements = 400;

    public static readonly (int Width, int Height) DeskSize = (46, 34);
    public static readonly (int Width, int Height) RoomSize = (200, 120);

    /// <summary>JS Math.round: .5 her zaman yukari (pozitif sonsuza) yuvarlanir.</summary>
    public static double JsRound(double value) => Math.Floor(value + 0.5);

    public static double SnapToGrid(double value, bool enabled, int grid = GridSize)
    {
        if (!enabled) return JsRound(value);
        return JsRound(value / grid) * grid;
    }

    public static double Clamp(double value, double min, double max) => Math.Max(min, Math.Min(max, value));

    /// <summary>Bir dikdortgeni plan sinirlari icinde tutar; oge plandan buyukse once boyutu kisilir.</summary>
    public static PlanRect ClampRect(PlanRect rect, double planWidth, double planHeight)
    {
        var width = Clamp(rect.Width, MinSize, planWidth);
        var height = Clamp(rect.Height, MinSize, planHeight);
        return new PlanRect(
            Clamp(rect.X, 0, planWidth - width),
            Clamp(rect.Y, 0, planHeight - height),
            width,
            height);
    }

    /// <summary>Negatif genislik/yukseklikle cizilen dikdortgeni normalize eder.</summary>
    public static PlanRect NormalizeRect(PlanPoint start, PlanPoint end) => new(
        Math.Min(start.X, end.X),
        Math.Min(start.Y, end.Y),
        Math.Abs(end.X - start.X),
        Math.Abs(end.Y - start.Y));

    /// <summary>Duvar cizimi: iki nokta arasindaki kalin cizgiyi dikdortgene cevirir; axisLock en yakin eksene sabitler.</summary>
    public static PlanRect WallRectFromPoints(PlanPoint start, PlanPoint end, double thickness, bool axisLock)
    {
        var target = end;
        if (axisLock)
        {
            var deltaX = Math.Abs(end.X - start.X);
            var deltaY = Math.Abs(end.Y - start.Y);
            target = deltaX >= deltaY ? new PlanPoint(end.X, start.Y) : new PlanPoint(start.X, end.Y);
        }

        var horizontal = Math.Abs(target.X - start.X) >= Math.Abs(target.Y - start.Y);
        if (horizontal)
        {
            var x = Math.Min(start.X, target.X);
            var width = Math.Max(Math.Abs(target.X - start.X), MinSize);
            return new PlanRect(x, start.Y - thickness / 2, width, thickness);
        }

        var y = Math.Min(start.Y, target.Y);
        var height = Math.Max(Math.Abs(target.Y - start.Y), MinSize);
        return new PlanRect(start.X - thickness / 2, y, thickness, height);
    }

    private static readonly JsonSerializerOptions StyleJson = new(JsonSerializerDefaults.Web);

    /// <summary>Bozuk JSON, bos deger veya nesne olmayan JSON'da bos stil doner.</summary>
    public static PlanElementStyle ParseStyle(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return PlanElementStyle.Empty;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return PlanElementStyle.Empty;
            return JsonSerializer.Deserialize<PlanElementStyle>(raw, StyleJson) ?? PlanElementStyle.Empty;
        }
        catch (JsonException)
        {
            return PlanElementStyle.Empty;
        }
    }

    public static string SerializeStyle(PlanElementStyle style) => JsonSerializer.Serialize(style, StyleJson);
}
