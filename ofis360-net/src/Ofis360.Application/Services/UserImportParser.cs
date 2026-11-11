using System.Text;
using Ofis360.Domain.Common;

namespace Ofis360.Application.Services;

public sealed record ParsedImportRow(int Line, string Email, string? FullName, string? Title, string? Grade, string? Department, string? Secretary);

public sealed record ParsedImport(IReadOnlyList<ParsedImportRow> Rows, string? Error);

/// <summary>
/// Personel listesi CSV ayristirici. Excel'in Turkce "CSV" ciktisi (noktali virgul), virgul ve sekme ayraclari,
/// tirnakli alanlar ve BOM desteklenir. Sutunlar basliktan bulunur; sira ve yazim farki onemsizdir.
/// </summary>
public static class UserImportParser
{
    public const int MaxRows = 5000;

    private static readonly IReadOnlyDictionary<string, string> Columns = new Dictionary<string, string>
    {
        ["eposta"] = "email", ["email"] = "email", ["mail"] = "email", ["kurumsaleposta"] = "email", ["epostaadresi"] = "email",
        ["adsoyad"] = "name", ["adisoyadi"] = "name", ["adsoyadi"] = "name", ["isim"] = "name",
        ["unvan"] = "title", ["gorev"] = "title",
        ["kademe"] = "grade", ["kidem"] = "grade",
        ["departman"] = "department", ["birim"] = "department", ["departmanadi"] = "department",
        ["sekreter"] = "secretary",
    };

    public static ParsedImport Parse(string text)
    {
        var records = ReadRecords(text.TrimStart('﻿'));
        if (records.Count == 0) return new ParsedImport([], "Dosyada veri bulunamadı.");

        var header = records[0].Cells;
        var index = new Dictionary<string, int>();
        for (var i = 0; i < header.Count; i++)
        {
            if (Columns.TryGetValue(TurkishText.Key(header[i]), out var column) && !index.ContainsKey(column)) index[column] = i;
        }
        if (!index.ContainsKey("email")) return new ParsedImport([], "Başlık satırında \"e-posta\" sütunu bulunamadı.");
        if (records.Count - 1 > MaxRows) return new ParsedImport([], $"Dosya en fazla {MaxRows} satır içerebilir.");

        string? Cell(IReadOnlyList<string> cells, string column)
        {
            if (!index.TryGetValue(column, out var i) || i >= cells.Count) return null;
            var value = cells[i].Trim();
            return value.Length == 0 ? null : value;
        }

        var rows = records.Skip(1)
            .Select(r => new ParsedImportRow(
                r.Line,
                (Cell(r.Cells, "email") ?? string.Empty).ToLowerInvariant(),
                Cell(r.Cells, "name"), Cell(r.Cells, "title"), Cell(r.Cells, "grade"), Cell(r.Cells, "department"), Cell(r.Cells, "secretary")))
            .ToList();
        return new ParsedImport(rows, null);
    }

    private sealed record Record(int Line, IReadOnlyList<string> Cells);

    private static char DetectDelimiter(string text)
    {
        var end = text.IndexOfAny(['\r', '\n']);
        var first = end < 0 ? text : text[..end];
        return new[] { ';', ',', '\t' }.OrderByDescending(d => first.Count(c => c == d)).First();
    }

    private static List<Record> ReadRecords(string text)
    {
        var delimiter = DetectDelimiter(text);
        var records = new List<Record>();
        var cells = new List<string>();
        var cell = new StringBuilder();
        var inQuotes = false;
        var line = 1;
        var recordLine = 1;

        void EndRecord()
        {
            cells.Add(cell.ToString());
            cell.Clear();
            if (cells.Any(c => !string.IsNullOrWhiteSpace(c))) records.Add(new Record(recordLine, cells));
            cells = [];
        }

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else if (c == '"') inQuotes = false;
                else
                {
                    if (c == '\n') line++;
                    cell.Append(c);
                }
                continue;
            }

            if (c == '"' && cell.Length == 0) inQuotes = true;
            else if (c == delimiter) { cells.Add(cell.ToString()); cell.Clear(); }
            else if (c == '\r' || c == '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                EndRecord();
                line++;
                recordLine = line;
            }
            else cell.Append(c);
        }

        if (cell.Length > 0 || cells.Count > 0) EndRecord();
        return records;
    }
}
