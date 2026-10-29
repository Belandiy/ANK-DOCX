// Bounded architecture probe, not a replacement finalizer.
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using W = DocumentFormat.OpenXml.Wordprocessing;
using M = DocumentFormat.OpenXml.Math;
using Word = Microsoft.Office.Interop.Word;

internal static class Program
{
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);

    private static readonly List<object> Owned = [];
    private static T Own<T>(T value) where T : class
    {
        Owned.Add(value);
        return value;
    }

    private static int Page(Word.Range range)
    {
        var point = Own(range.Duplicate);
        point.Collapse(Word.WdCollapseDirection.wdCollapseStart);
        return (int)point.Information[Word.WdInformation.wdActiveEndPageNumber];
    }

    private static SortedDictionary<int, (int Start, int End)> Rows(Word.Table table)
    {
        var result = new SortedDictionary<int, (int Start, int End)>();
        var range = Own(table.Range);
        var cells = Own(range.Cells);
        foreach (Word.Cell item in cells)
        {
            var cell = Own(item);
            var cr = Own(cell.Range);
            var index = cell.RowIndex;
            result[index] = result.TryGetValue(index, out var old)
                ? (old.Start, Math.Max(old.End, cr.End)) : (cr.Start, cr.End);
        }
        return result;
    }

    private static void Refresh(Word.Document document)
    {
        for (var pass = 0; pass < 2; pass++)
        {
            document.Repaginate();
            if (Own(document.Fields).Update() != 0)
                throw new InvalidOperationException("Word field update failed");
            foreach (Word.TableOfContents item in Own(document.TablesOfContents))
                Own(item).Update();
        }
    }

    private static List<string[]> TextRows(W.Table table) => table.Elements<W.TableRow>()
        .Select(row => row.Elements<W.TableCell>().Select(cell => cell.InnerText).ToArray()).ToList();

    private static List<string> HeaderShape(W.Table table) => table.Elements<W.TableRow>().Take(2)
        .SelectMany(row => row.Elements<W.TableCell>()).Select(cell =>
        {
            var props = cell.TableCellProperties;
            return $"{props?.GridSpan?.Val?.Value ?? 1}:{props?.VerticalMerge?.Val?.Value.ToString() ?? (props?.VerticalMerge is null ? "none" : "continue")}";
        }).ToList();

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 3) throw new ArgumentException("input.docx output.docx report.json");
        var input = Path.GetFullPath(args[0]);
        var output = Path.GetFullPath(args[1]);
        var reportPath = Path.GetFullPath(args[2]);
        File.Copy(input, output, true);
        using var baseline = WordprocessingDocument.Open(input, false);
        var original = baseline.MainDocumentPart!.Document.Body!.Elements<W.Table>().ToArray();
        if (original.Length != 3) throw new InvalidOperationException("Expected the existing three-table fixture");
        var expectedRows = TextRows(original[1]);
        var expectedHeaders = HeaderShape(original[1]);
        var expectedMath = baseline.MainDocumentPart.Document.Descendants<M.OfficeMath>().Count();
        var checks = new Dictionary<string, bool>();
        var report = new Dictionary<string, object?>
        {
            ["runtime"] = RuntimeInformation.FrameworkDescription,
            ["os"] = RuntimeInformation.OSDescription,
            ["input"] = input,
            ["checks"] = checks,
            ["scope"] = "One split of the existing merged-header table; not complete pagination or migration parity"
        };
        Word.Application? app = null;
        Word.Document? document = null;
        uint wordPid = 0;
        int result = 1;
        try
        {
            app = Own(new Word.Application());
            app.Visible = false;
            app.DisplayAlerts = Word.WdAlertLevel.wdAlertsNone;
            report["word_version"] = app.Version;
            report["word_build"] = app.Build;
            var documents = Own(app.Documents);
            document = Own(documents.Open(output, ReadOnly: false, AddToRecentFiles: false));
            var window = Own(document.ActiveWindow);
            GetWindowThreadProcessId((nint)window.Hwnd, out wordPid);
            report["word_pid"] = wordPid;
            Own(window.View).Type = Word.WdViewType.wdPrintView;
            Refresh(document);
            var tables = Own(document.Tables);
            var table = Own(tables[2]);
            table.AutoFitBehavior(Word.WdAutoFitBehavior.wdAutoFitFixed);
            document.Repaginate();
            var rows = Rows(table);
            var firstBody = rows[3];
            var firstPage = Page(Own(document.Range(firstBody.Start, firstBody.Start)));
            var split = rows.Where(row => row.Key > 3).First(row =>
                Page(Own(document.Range(row.Value.Start, row.Value.Start))) > firstPage);
            report["split_at_original_row"] = split.Key;
            var header = Own(document.Range(Own(table.Range).Start, firstBody.Start));
            Own(document.Range(split.Value.Start, split.Value.Start)).Select();
            Own(app.Selection).SplitTable();
            checks["selection_split_table"] = tables.Count == 4;
            var tail = Own(tables[3]);
            var at = Own(Own(tail.Range).Duplicate);
            at.Collapse(Word.WdCollapseDirection.wdCollapseStart);
            at.FormattedText = Own(header.FormattedText);
            tail = Own(tables[3]);
            var separator = Own(Own(document.Range(Own(tail.Range).Start - 1,
                Own(tail.Range).Start - 1)).Paragraphs)[1];
            Own(separator);
            var caption = Own(separator.Range);
            caption.InsertBefore("Продолжение таблицы ");
            var format = Own(caption.ParagraphFormat);
            format.PageBreakBefore = -1;
            format.KeepWithNext = -1;
            var insert = Own(caption.Duplicate);
            insert.End--;
            insert.Collapse(Word.WdCollapseDirection.wdCollapseEnd);
            Own(Own(document.Fields).Add(insert, Word.WdFieldType.wdFieldRef, "tbl_squares \\h", false));
            Refresh(document);
            var totalPages = document.ComputeStatistics(Word.WdStatistic.wdStatisticPages);
            var numPages = new List<string>();
            foreach (Word.Field item in Own(document.Fields))
            {
                var field = Own(item);
                if (field.Type == Word.WdFieldType.wdFieldNumPages)
                {
                    field.Update();
                    numPages.Add(Own(field.Result).Text.Trim());
                }
            }
            report["pages_before_normalization"] = totalPages;
            report["num_pages_before_normalization"] = numPages;
            document.Save();
            document.Close(Word.WdSaveOptions.wdDoNotSaveChanges);
            document = null;
            document = Own(documents.Open(output, ReadOnly: false, AddToRecentFiles: false));
            checks["saved_and_reopened"] = Own(document.Tables).Count == 4;
            Own(Own(document.ActiveWindow).View).Type = Word.WdViewType.wdPrintView;
            // Retain the proven PS1 order: Word normalizes on save/reopen,
            // then refresh page-dependent fields until their values stabilize.
            checks["num_pages_current"] = false;
            for (var pass = 0; pass < 4; pass++)
            {
                numPages.Clear();
                foreach (Word.Field item in Own(document.Fields))
                {
                    var field = Own(item);
                    if (field.Type == Word.WdFieldType.wdFieldNumPages)
                    {
                        field.Update();
                        numPages.Add(Own(field.Result).Text.Trim());
                    }
                }
                document.Repaginate();
                totalPages = document.ComputeStatistics(Word.WdStatistic.wdStatisticPages);
                if (numPages.Count > 0 && numPages.All(text => text == totalPages.ToString()) &&
                    (int)Own(document.Content).Information[Word.WdInformation.wdNumberOfPagesInDocument] == totalPages)
                {
                    checks["num_pages_current"] = true;
                    report["pagination_passes_after_reopen"] = pass + 1;
                    break;
                }
            }
            report["pages_after_normalization"] = totalPages;
            report["num_pages_after_normalization"] = numPages;
            document.Save();
            document.Close(Word.WdSaveOptions.wdDoNotSaveChanges);
            document = null;

            using var saved = WordprocessingDocument.Open(output, false);
            var body = saved.MainDocumentPart!.Document.Body!;
            var parts = body.Elements<W.Table>().ToArray();
            var actualRows = TextRows(parts[1]).Concat(TextRows(parts[2]).Skip(2)).ToList();
            checks["body_rows_and_footer_preserved"] = expectedRows.Count == actualRows.Count &&
                expectedRows.Zip(actualRows).All(pair => pair.First.SequenceEqual(pair.Second));
            checks["merged_header_preserved"] = expectedHeaders.SequenceEqual(HeaderShape(parts[1])) &&
                expectedHeaders.SequenceEqual(HeaderShape(parts[2]));
            checks["repeating_header_preserved"] = parts.Skip(1).Take(2).All(part =>
                part.Elements<W.TableRow>().Take(2).All(row => row.TableRowProperties?.GetFirstChild<W.TableHeader>() is not null));
            checks["editable_math_preserved"] = expectedMath > 0 && saved.MainDocumentPart.Document.Descendants<M.OfficeMath>().Count() == expectedMath;
            checks["native_continuation_ref"] = body.Descendants<W.FieldCode>().Any(code => code.Text.Trim() == "REF tbl_squares \\h");
            var errors = new OpenXmlValidator(FileFormatVersions.Microsoft365).Validate(saved).ToArray();
            report["sdk_error_count"] = errors.Length;
            checks["sdk_schema_valid"] = errors.Length == 0;
            report["interop_assembly_dependencies"] = typeof(Program).Assembly.GetReferencedAssemblies()
                .Where(name => name.Name!.Contains("Interop.Word")).Select(name => name.FullName).ToArray();
            checks["interop_types_embedded"] = !typeof(Program).Assembly.GetReferencedAssemblies()
                .Any(name => name.Name!.Contains("Interop.Word"));
            result = checks.Values.All(value => value) ? 0 : 1;
        }
        catch (Exception error)
        {
            report["error"] = error.ToString();
        }
        finally
        {
            if (document is not null)
                try { document.Close(Word.WdSaveOptions.wdDoNotSaveChanges); } catch (Exception e) { report["close_error"] = e.Message; }
            if (app is not null)
                try { app.Quit(Word.WdSaveOptions.wdDoNotSaveChanges); } catch (Exception e) { report["quit_error"] = e.Message; }
            var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
            for (var index = Owned.Count - 1; index >= 0; index--)
                if (seen.Add(Owned[index]) && Marshal.IsComObject(Owned[index]))
                    Marshal.FinalReleaseComObject(Owned[index]);
            Owned.Clear();
            seen.Clear();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            if (wordPid != 0)
            {
                try { using var process = Process.GetProcessById((int)wordPid); checks["own_word_process_exited"] = process.WaitForExit(5000); }
                catch (ArgumentException) { checks["own_word_process_exited"] = true; }
            }
            if (report.ContainsKey("close_error") || report.ContainsKey("quit_error") || checks.Values.Any(value => !value)) result = 1;
            report["passed"] = result == 0;
            File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        }
        Console.WriteLine(File.ReadAllText(reportPath));
        return result;
    }
}
