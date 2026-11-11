using Microsoft.EntityFrameworkCore;
using Ofis360.Application.Common;
using Ofis360.Application.Persistence;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;

namespace Ofis360.Application.Services;

/// <summary>Resmi tatil takvimi (admin). Is gunu hesabi ve "yalnizca calisma gunleri" kurali bu listeyi kullanir.</summary>
public sealed class HolidayService(IOfis360Db db, AuditService audit)
{
    public Task<List<Holiday>> ListAsync(CancellationToken ct = default) =>
        db.Holidays.AsNoTracking().OrderBy(h => h.Date).ToListAsync(ct);

    public async Task<Holiday> CreateAsync(string dayKey, string name, AuditActor actor, CancellationToken ct = default)
    {
        var date = DayKeys.ToDateOnly(dayKey);
        if (await db.Holidays.AnyAsync(h => h.Date == date, ct)) throw AppErrors.Conflict("Bu tarih zaten tatil olarak tanımlı.");

        var holiday = new Holiday { Date = date, Name = name, CreatedById = actor.Id };
        db.Holidays.Add(holiday);
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(actor, AuditActions.HolidayCreate, "Holiday", holiday.Id,
            $"{DayKeys.FormatLong(dayKey)} resmi tatil olarak eklendi: {name}.", new { date = dayKey, name }, ct);
        return holiday;
    }

    public async Task DeleteAsync(string id, AuditActor actor, CancellationToken ct = default)
    {
        var holiday = await db.Holidays.FirstOrDefaultAsync(h => h.Id == id, ct) ?? throw AppErrors.NotFound("Tatil bulunamadı.");
        var dayKey = holiday.DayKey;
        var name = holiday.Name;
        db.Holidays.Remove(holiday);
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(actor, AuditActions.HolidayDelete, "Holiday", id,
            $"{DayKeys.FormatLong(dayKey)} resmi tatil listesinden çıkarıldı: {name}.", new { date = dayKey, name }, ct);
    }
}
