using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using W = DocumentFormat.OpenXml.Wordprocessing;
using M = DocumentFormat.OpenXml.Math;

namespace AcademicDocx.Core;

public sealed record SchemaError(string Id, string Type, string Description, string? Part, string? Path);
public sealed record ValidationReport(string File, int ErrorCount, IReadOnlyList<SchemaError> Errors);
public sealed record TableFacts(IReadOnlyList<string[]> Rows, int HeaderRows, IReadOnlyList<string> HeaderShape,
    int MathCount, IReadOnlyList<int> GridWidths);
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

    // Compare logical rows, not serialization details that Word is allowed to normalize.
    public static bool SameRows(IEnumerable<string[]> first, IEnumerable<string[]> second) =>
        first.Select(row => string.Join('\u001f', row)).SequenceEqual(second.Select(row => string.Join('\u001f', row)));
}
