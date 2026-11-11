using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Ofis360.Application.Persistence;
using Ofis360.Domain.Common;
using Ofis360.Domain.Entities;

namespace Ofis360.Application.Services;

public static class UserImportActions
{
    public const string Create = "CREATE";
    public const string Update = "UPDATE";
    public const string Unchanged = "UNCHANGED";
    public const string Error = "ERROR";
}

public sealed record UserImportRow(int Line, string Email, string? FullName, string Action, IReadOnlyList<string> Errors, IReadOnlyList<string> Changes);

public sealed record UserImportResult(
    bool Applied, int Total, int Creates, int Updates, int Unchanged, int Errors,
    IReadOnlyList<string> NewDepartments, string? FileError, IReadOnlyList<UserImportRow> Rows);

/// <summary>
/// Personel listesini toplu yukler. Once onizleme: dosya dogrulanir, hicbir sey yazilmaz. Uygulama hepsi-ya-hic'tir;
/// tek hatali satir varsa yazma yapilmaz. Dosyada olmayan kullanicilara dokunulmaz; Rol ve aktiflik degistirilmez,
/// bos hucre mevcut degeri korur.
/// </summary>
public sealed partial class UserImportService(IOfis360Db db, AuditService audit)
{
    private static readonly string[] Yes = ["evet", "e", "var", "1", "true", "x"];
    private static readonly string[] No = ["hayir", "h", "yok", "0", "false"];

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();

    public async Task<UserImportResult> ImportAsync(string csv, bool apply, AuditActor actor, CancellationToken ct = default)
    {
        var parsed = UserImportParser.Parse(csv);
        if (parsed.Error is not null) return new UserImportResult(false, 0, 0, 0, 0, 0, [], parsed.Error, []);

        var users = await db.Users.ToListAsync(ct);
        var userByEmail = users.GroupBy(u => u.Email.ToLowerInvariant()).ToDictionary(g => g.Key, g => g.First());

        var departments = await db.Departments.ToListAsync(ct);
        var departmentByKey = new Dictionary<string, Department>();
        foreach (var department in departments)
        {
            departmentByKey.TryAdd(TurkishText.Key(department.Name), department);
            departmentByKey.TryAdd(TurkishText.Key(department.Code), department);
        }

        var newDepartments = new Dictionary<string, Department>();
        var usedCodes = departments.Select(d => d.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Department ResolveDepartment(string text)
        {
            var key = TurkishText.Key(text);
            if (departmentByKey.TryGetValue(key, out var found)) return found;
            if (newDepartments.TryGetValue(key, out var pending)) return pending;

            var created = new Department { Name = text, Code = UniqueCode(key, usedCodes) };
            newDepartments[key] = created;
            return created;
        }

        var seen = new HashSet<string>();
        var rows = new List<UserImportRow>();
        var writes = new List<Action>();

        foreach (var row in parsed.Rows)
        {
            var errors = new List<string>();
            var changes = new List<string>();

            if (!EmailPattern().IsMatch(row.Email)) errors.Add("Geçersiz e-posta.");
            else if (!seen.Add(row.Email)) errors.Add("Bu e-posta dosyada birden fazla kez geçiyor.");

            string? grade = null;
            if (row.Grade is not null)
            {
                grade = Grades.Parse(row.Grade);
                if (grade is null) errors.Add($"Bilinmeyen kademe: {row.Grade}");
            }

            bool? secretary = null;
            if (row.Secretary is not null)
            {
                var key = TurkishText.Key(row.Secretary);
                if (Yes.Contains(key)) secretary = true;
                else if (No.Contains(key)) secretary = false;
                else errors.Add($"Sekreter sütunu Evet veya Hayır olmalı: {row.Secretary}");
            }

            if (row.FullName is { Length: < 3 or > 80 }) errors.Add("Ad soyad 3-80 karakter olmalı.");
            if (row.Title is { Length: < 2 or > 80 }) errors.Add("Unvan 2-80 karakter olmalı.");
            if (row.Department is { Length: > 200 }) errors.Add("Departman adı çok uzun.");

            var existing = userByEmail.GetValueOrDefault(row.Email);
            if (existing is null)
            {
                if (row.FullName is null) errors.Add("Yeni kullanıcı için ad soyad zorunlu.");
                if (row.Department is null) errors.Add("Yeni kullanıcı için departman zorunlu.");
            }

            if (errors.Count > 0)
            {
                rows.Add(new UserImportRow(row.Line, row.Email, row.FullName, UserImportActions.Error, errors, []));
                continue;
            }

            var targetDepartment = row.Department is null ? null : ResolveDepartment(row.Department);

            if (existing is null)
            {
                changes.Add("Yeni kullanıcı");
                writes.Add(() => db.Users.Add(new AppUser
                {
                    Email = row.Email,
                    FullName = row.FullName!,
                    Title = row.Title ?? Grades.Label(grade) ?? "Çalışan",
                    DepartmentId = targetDepartment!.Id,
                    Grade = grade,
                    IsSecretary = secretary ?? false,
                }));
                rows.Add(new UserImportRow(row.Line, row.Email, row.FullName, UserImportActions.Create, [], changes));
                continue;
            }

            if (row.FullName is not null && row.FullName != existing.FullName) changes.Add($"Ad soyad: {row.FullName}");
            if (row.Title is not null && row.Title != existing.Title) changes.Add($"Unvan: {row.Title}");
            if (grade is not null && grade != existing.Grade) changes.Add($"Kademe: {Grades.Label(existing.Grade) ?? "Atanmamış"} > {Grades.Label(grade)}");
            if (targetDepartment is not null && targetDepartment.Id != existing.DepartmentId) changes.Add($"Departman: {targetDepartment.Name}");
            if (secretary is bool s && s != existing.IsSecretary) changes.Add(s ? "Sekreter yetkisi verilecek" : "Sekreter yetkisi kaldırılacak");

            if (changes.Count > 0)
            {
                writes.Add(() =>
                {
                    if (row.FullName is not null) existing.FullName = row.FullName;
                    if (row.Title is not null) existing.Title = row.Title;
                    if (grade is not null) existing.Grade = grade;
                    if (targetDepartment is not null) existing.DepartmentId = targetDepartment.Id;
                    if (secretary is bool value) existing.IsSecretary = value;
                });
            }
            rows.Add(new UserImportRow(row.Line, row.Email, existing.FullName,
                changes.Count > 0 ? UserImportActions.Update : UserImportActions.Unchanged, [], changes));
        }

        int Count(string action) => rows.Count(r => r.Action == action);
        var errorCount = Count(UserImportActions.Error);
        var hasWrites = writes.Count > 0;
        var applied = apply && errorCount == 0 && hasWrites;

        if (applied)
        {
            await db.InTransactionAsync(async token =>
            {
                db.Departments.AddRange(newDepartments.Values);
                await db.SaveChangesAsync(token);
                foreach (var write in writes) write();
                await db.SaveChangesAsync(token);
                await audit.RecordAsync(actor, AuditActions.UserImport, "User", null,
                    $"Personel listesi yüklendi: {Count(UserImportActions.Create)} yeni, {Count(UserImportActions.Update)} güncellenen kullanıcı.",
                    new { creates = Count(UserImportActions.Create), updates = Count(UserImportActions.Update), newDepartments = newDepartments.Values.Select(d => d.Name) }, token);
                return true;
            }, ct);
        }

        return new UserImportResult(applied, rows.Count, Count(UserImportActions.Create), Count(UserImportActions.Update),
            Count(UserImportActions.Unchanged), errorCount, newDepartments.Values.Select(d => d.Name).ToList(), null, rows);
    }

    private static string UniqueCode(string key, HashSet<string> used)
    {
        var stem = key.ToUpperInvariant();
        if (stem.Length == 0) stem = "DEPARTMAN";
        if (stem.Length > 34) stem = stem[..34];

        var code = stem;
        for (var n = 2; !used.Add(code); n++) code = $"{stem}{n}";
        return code;
    }
}
