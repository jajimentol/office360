using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Ofis360.Infrastructure.Persistence;

/// <summary>
/// Oracle EF saglayicisi DateOnly'yi dogrudan eslemez; takvim gunu Oracle DATE sutununda
/// saat 00:00 olarak tutulur (kaynaktaki "UTC gece yarisi" semantigiyle ayni).
/// </summary>
public sealed class DateOnlyConverter() : ValueConverter<DateOnly, DateTime>(
    d => d.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
    dt => DateOnly.FromDateTime(dt));

public sealed class DateOnlyComparer() : ValueComparer<DateOnly>(
    (a, b) => a.DayNumber == b.DayNumber,
    d => d.GetHashCode());
