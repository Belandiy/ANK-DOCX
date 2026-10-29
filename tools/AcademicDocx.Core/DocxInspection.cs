using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using W = DocumentFormat.OpenXml.Wordprocessing;
using M = DocumentFormat.OpenXml.Math;

namespace AcademicDocx.Core;

public sealed record DiagnosticItem(
    string Rule,
    string Severity,
    string? File,
    string? SectionId,
    string? SectionTitle,
    string? ObjectId,
    string? Location,
    string Message,
    string Recommendation
);

public sealed record DiagnosticReport(IReadOnlyList<DiagnosticItem> Diagnostics)
{
    public string FormatConsoleSummary()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Сводка диагностики ANK ===");
        sb.AppendLine();

        if (Diagnostics.Count == 0)
        {
            sb.AppendLine("[Информация] BINDING_SUCCESS");
            sb.AppendLine("  Сборка завершена успешно. Замечаний не обнаружено.");
            return sb.ToString();
        }

        foreach (var item in Diagnostics)
        {
            var severityTitle = item.Severity switch
            {
                "Error" => "Ошибка",
                "Warning" => "Предупреждение",
                "Info" => "Информация",
                _ => item.Severity
            };

            sb.AppendLine($"[{severityTitle}] {item.Rule}");

            var locationParts = new List<string>();
            if (!string.IsNullOrEmpty(item.File)) locationParts.Add(item.File);
            if (!string.IsNullOrEmpty(item.SectionTitle))
            {
                var sec = $"заголовок «{item.SectionTitle}»";
                if (!string.IsNullOrEmpty(item.SectionId)) sec += $" (#{item.SectionId})";
                locationParts.Add(sec);
            }
            else if (!string.IsNullOrEmpty(item.SectionId))
            {
                locationParts.Add($"раздел #{item.SectionId}");
            }
            if (!string.IsNullOrEmpty(item.ObjectId)) locationParts.Add($"объект #{item.ObjectId}");
            if (!string.IsNullOrEmpty(item.Location) && locationParts.Count == 0) locationParts.Add(item.Location);

            if (locationParts.Count > 0)
            {
                sb.AppendLine($"  Источник: {string.Join(", ", locationParts)}");
            }

            sb.AppendLine($"  Проблема: {item.Message}");
            sb.AppendLine($"  Рекомендация: {item.Recommendation}");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}

public sealed record SchemaError(string Id, string Type, string Description, string? Part, string? Path);
public sealed record ValidationReport(string File, int ErrorCount, IReadOnlyList<SchemaError> Errors);
public sealed record TableFacts(IReadOnlyList<string[]> Rows, int HeaderRows, IReadOnlyList<string> HeaderShape,
    int MathCount, IReadOnlyList<int> GridWidths);
public sealed record CitationFacts(IReadOnlyList<string> CitedKeys, IReadOnlyList<string> SourceTags,
    IReadOnlyList<string> UncitedSources);
public sealed record DocumentFacts(int MathCount, int SequenceCount, IReadOnlyList<TableFacts> Tables,
    IReadOnlyList<string> Fields, IReadOnlyList<string> SourceTags);

public static class JsonReport
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All)
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options) + "\n";

    public static void Write<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, Serialize(value));
    }
}

public static class DocxInspection
{
    public static ValidationReport Validate(string path)
    {
        using var package = WordprocessingDocument.Open(path, false);
        var errors = new OpenXmlValidator(FileFormatVersions.Microsoft365) { MaxNumberOfErrors = 0 }.Validate(package)
            .Select(error => new SchemaError(error.Id, error.ErrorType.ToString(), error.Description,
                error.Part?.Uri.ToString(), error.Path?.XPath)).ToArray();
        return new(Path.GetFileName(path), errors.Length, errors);
    }

    public static DocumentFacts Read(string path)
    {
        using var package = WordprocessingDocument.Open(path, false);
        var main = package.MainDocumentPart ?? throw new InvalidDataException("DOCX has no main document part");
        var document = main.Document;
        var fields = document.Descendants<W.FieldCode>().Select(code => code.Text.Trim())
            .Concat(document.Descendants<W.SimpleField>().Select(field => field.Instruction?.Value?.Trim() ?? "")).ToArray();
        var tables = document.Body!.Elements<W.Table>().Select(ReadTable).ToArray();
        var tags = new List<string>();
        foreach (var part in main.CustomXmlParts)
        {
            using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
            var xml = System.Xml.Linq.XDocument.Load(stream);
            System.Xml.Linq.XNamespace b = "http://schemas.openxmlformats.org/officeDocument/2006/bibliography";
            tags.AddRange(xml.Descendants(b + "Tag").Select(tag => tag.Value));
        }
        return new(document.Descendants<M.OfficeMath>().Count(),
            fields.Count(field => field.StartsWith("SEQ ", StringComparison.Ordinal)), tables, fields, tags);
    }

    public static TableFacts ReadTable(W.Table table)
    {
        var rows = table.Elements<W.TableRow>().ToArray();
        var headerRows = rows.TakeWhile(row =>
        {
            var header = row.TableRowProperties?.GetFirstChild<W.TableHeader>();
            return header is not null && header.Val?.Value != W.OnOffOnlyValues.Off;
        }).Count();
        var shape = rows.Take(headerRows).SelectMany(row => row.Elements<W.TableCell>()).Select(cell =>
        {
            var props = cell.TableCellProperties;
            return $"{props?.GridSpan?.Val?.Value ?? 1}:{props?.VerticalMerge?.Val?.Value.ToString() ?? (props?.VerticalMerge is null ? "none" : "continue")}";
        }).ToArray();
        return new(rows.Select(row => row.Elements<W.TableCell>().Select(cell => cell.InnerText).ToArray()).ToArray(),
            headerRows, shape, table.Descendants<M.OfficeMath>().Count(),
            table.GetFirstChild<W.TableGrid>()?.Elements<W.GridColumn>()
                .Select(column => int.Parse(column.Width?.Value ?? "0", System.Globalization.CultureInfo.InvariantCulture)).ToArray() ?? []);
    }

    public static CitationFacts ReadCitations(string path)
    {
        using var package = WordprocessingDocument.Open(path, false);
        var main = package.MainDocumentPart ?? throw new InvalidDataException("DOCX has no main document part");
        var document = main.Document;

        var citedKeys = document.Descendants<W.Hyperlink>()
            .Select(h => h.Anchor?.Value)
            .Where(anchor => !string.IsNullOrEmpty(anchor) && anchor.StartsWith("ref-", StringComparison.Ordinal))
            .Select(anchor => anchor!["ref-".Length..])
            .Distinct()
            .ToArray();

        var sourceTags = new List<string>();
        foreach (var part in main.CustomXmlParts)
        {
            using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
            var xml = System.Xml.Linq.XDocument.Load(stream);
            System.Xml.Linq.XNamespace b = "http://schemas.openxmlformats.org/officeDocument/2006/bibliography";
            sourceTags.AddRange(xml.Descendants(b + "Tag").Select(tag => tag.Value));
        }

        var uncited = sourceTags.Where(tag => !citedKeys.Contains(tag)).Distinct().ToArray();
        return new(citedKeys, sourceTags, uncited);
    }

    // Compare logical rows, not serialization details that Word is allowed to normalize.
    public static bool SameRows(IEnumerable<string[]> first, IEnumerable<string[]> second) =>
        first.Select(row => string.Join('\u001f', row)).SequenceEqual(second.Select(row => string.Join('\u001f', row)));
}
