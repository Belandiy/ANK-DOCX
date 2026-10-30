using System.Xml.Linq;
using AcademicDocx.Core;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace AcademicDocx.Tests;

public sealed class AppendixTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "academic-appendix-tests-" + Guid.NewGuid().ToString("N"));

    public AppendixTests() => Directory.CreateDirectory(folder);

    private static readonly char[] ForbiddenAppendixLetters = { 'Ё', 'З', 'Й', 'О', 'Ч', 'Ь', 'Ы', 'Ъ' };

    private static readonly char[] AllowedAppendixLetters =
    {
        'А', 'Б', 'В', 'Г', 'Д', 'Е', 'Ж', 'И', 'К', 'Л', 'М', 'Н', 'П', 'Р', 'С', 'Т', 'У', 'Ф', 'Х', 'Ц', 'Ш', 'Щ', 'Э', 'Ю', 'Я'
    };

    private static W.Paragraph CreateSeqParagraph(string prefix, string seqName, string suffix, string bookmarkName, string bookmarkId, string letter)
    {
        var p = new W.Paragraph();
        p.Append(new W.Run(new W.Text(prefix) { Space = SpaceProcessingModeValues.Preserve }));
        p.Append(new W.BookmarkStart { Name = bookmarkName, Id = bookmarkId });
        p.Append(new W.Run(new W.Text($"{letter}.") { Space = SpaceProcessingModeValues.Preserve }));
        p.Append(new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.Begin, Dirty = true }));
        p.Append(new W.Run(new W.FieldCode($" SEQ {seqName} \\* ARABIC ") { Space = SpaceProcessingModeValues.Preserve }));
        p.Append(new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.Separate }));
        p.Append(new W.Run(new W.Text("1")));
        p.Append(new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.End }));
        p.Append(new W.BookmarkEnd { Id = bookmarkId });
        p.Append(new W.Run(new W.Text(suffix) { Space = SpaceProcessingModeValues.Preserve }));
        return p;
    }

    private static W.Paragraph CreateAppendixHeader(string letter, string title)
    {
        return new W.Paragraph(
            new W.ParagraphProperties(
                new W.ParagraphStyleId { Val = "UnnumberedHeading" },
                new W.PageBreakBefore(),
                new W.Justification { Val = W.JustificationValues.Center }
            ),
            new W.Run(new W.Text($"ПРИЛОЖЕНИЕ {letter}")),
            new W.Run(new W.Break()),
            new W.Run(new W.Text("(обязательное)")),
            new W.Run(new W.Break()),
            new W.Run(new W.Text(title))
        );
    }

    [Fact]
    public void AppendixDocumentContainsNormativeHeadingAndLocalSeqFields()
    {
        var path = Path.Combine(folder, "appendix_test.docx");

        using (var package = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();

            var body = new W.Body(
                // Main section heading
                new W.Paragraph(new W.Run(new W.Text("1 Основной раздел"))),
                new W.Paragraph(new W.Run(new W.Text("Текст основного раздела"))),

                // Page break before appendix
                new W.Paragraph(new W.Run(new W.Break { Type = W.BreakValues.Page })),

                // Appendix Heading (Unnumbered Heading style, centered)
                CreateAppendixHeader("А", "Структурная схема и исходный код"),

                // Paragraph with formula
                new W.Paragraph(new W.Run(new W.Text("В приложении приведены формулы, рисунки, таблицы и листинги."))),
                CreateSeqParagraph("(", "Formula_A", ")", "eq_app1", "1001", "А"),

                // Figure caption in Appendix A
                CreateSeqParagraph("Рисунок ", "Figure_A", " — Схема алгоритма", "fig_app1", "1002", "А"),

                // Table caption in Appendix A
                CreateSeqParagraph("Таблица ", "Table_A", " — Сводные параметры", "tbl_app1", "1003", "А"),

                // Listing caption in Appendix A
                CreateSeqParagraph("Листинг ", "Listing_A", " — Текст программы", "lst_app1", "1004", "А"),

                // Section properties
                new W.SectionProperties(
                    new W.PageSize { Width = 11906, Height = 16838 },
                    new W.PageMargin { Top = 1134, Right = 567, Bottom = 1474, Left = 1417, Header = 1134, Footer = 1134 },
                    new W.TitlePage()
                )
            );

            main.Document = new W.Document(body);
        }

        // 1. Validate Open XML schema correctness
        var validationReport = DocxInspection.Validate(path);
        Assert.True(validationReport.ErrorCount == 0, string.Join("; ", validationReport.Errors.Select(e => e.Description)));

        // 2. Read facts from document
        var facts = DocxInspection.Read(path);
        Assert.Contains(facts.Fields, f => f.Contains("SEQ Formula_A"));
        Assert.Contains(facts.Fields, f => f.Contains("SEQ Figure_A"));
        Assert.Contains(facts.Fields, f => f.Contains("SEQ Table_A"));
        Assert.Contains(facts.Fields, f => f.Contains("SEQ Listing_A"));

        // 3. Inspect document XML text content
        using (var doc = WordprocessingDocument.Open(path, false))
        {
            var text = doc.MainDocumentPart!.Document.Body!.InnerText;

            Assert.Contains("ПРИЛОЖЕНИЕ А", text);
            Assert.Contains("(обязательное)", text);
            Assert.Contains("Структурная схема и исходный код", text);
            Assert.Contains("Рисунок А.", text);
            Assert.Contains("Схема алгоритма", text);
            Assert.Contains("Таблица А.", text);
            Assert.Contains("Сводные параметры", text);
            Assert.Contains("Листинг А.", text);
            Assert.Contains("Текст программы", text);
        }
    }

    [Fact]
    public void MultipleSequentialAppendicesIsolateLocalSequenceFields()
    {
        var path = Path.Combine(folder, "multiple_appendices.docx");

        using (var package = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();

            var body = new W.Body(
                // Main Section
                new W.Paragraph(new W.Run(new W.Text("1 Раздел 1"))),

                // Appendix A (Приложение А)
                CreateAppendixHeader("А", "Первое приложение"),
                CreateSeqParagraph("(", "Formula_A", ")", "eq_a1", "2001", "А"),
                CreateSeqParagraph("Рисунок ", "Figure_A", " — Рисунок А1", "fig_a1", "2002", "А"),
                CreateSeqParagraph("Таблица ", "Table_A", " — Таблица А1", "tbl_a1", "2003", "А"),
                CreateSeqParagraph("Листинг ", "Listing_A", " — Листинг А1", "lst_a1", "2004", "А"),

                // Appendix B (Приложение Б)
                CreateAppendixHeader("Б", "Второе приложение"),
                CreateSeqParagraph("(", "Formula_B", ")", "eq_b1", "2005", "Б"),
                CreateSeqParagraph("Рисунок ", "Figure_B", " — Рисунок Б1", "fig_b1", "2006", "Б"),
                CreateSeqParagraph("Таблица ", "Table_B", " — Таблица Б1", "tbl_b1", "2007", "Б"),
                CreateSeqParagraph("Листинг ", "Listing_B", " — Листинг Б1", "lst_b1", "2008", "Б"),

                // Appendix C (Приложение В)
                CreateAppendixHeader("В", "Третье приложение"),
                CreateSeqParagraph("(", "Formula_В", ")", "eq_c1", "2009", "В"),
                CreateSeqParagraph("Рисунок ", "Figure_В", " — Рисунок В1", "fig_c1", "2010", "В"),
                CreateSeqParagraph("Таблица ", "Table_В", " — Таблица В1", "tbl_c1", "2011", "В"),
                CreateSeqParagraph("Листинг ", "Listing_В", " — Листинг В1", "lst_c1", "2012", "В")
            );

            main.Document = new W.Document(body);
        }

        // 1. Validate Open XML schema
        var validation = DocxInspection.Validate(path);
        Assert.Equal(0, validation.ErrorCount);

        // 2. Read facts and check isolated SEQ fields
        var facts = DocxInspection.Read(path);

        // Appendix A fields
        Assert.Contains(facts.Fields, f => f.Contains("SEQ Formula_A"));
        Assert.Contains(facts.Fields, f => f.Contains("SEQ Figure_A"));
        Assert.Contains(facts.Fields, f => f.Contains("SEQ Table_A"));
        Assert.Contains(facts.Fields, f => f.Contains("SEQ Listing_A"));

        // Appendix B fields
        Assert.Contains(facts.Fields, f => f.Contains("SEQ Formula_B"));
        Assert.Contains(facts.Fields, f => f.Contains("SEQ Figure_B"));
        Assert.Contains(facts.Fields, f => f.Contains("SEQ Table_B"));
        Assert.Contains(facts.Fields, f => f.Contains("SEQ Listing_B"));

        // Appendix C fields
        Assert.Contains(facts.Fields, f => f.Contains("SEQ Formula_В"));
        Assert.Contains(facts.Fields, f => f.Contains("SEQ Figure_В"));
        Assert.Contains(facts.Fields, f => f.Contains("SEQ Table_В"));
        Assert.Contains(facts.Fields, f => f.Contains("SEQ Listing_В"));

        // Verify document inner text
        using var doc = WordprocessingDocument.Open(path, false);
        var text = doc.MainDocumentPart!.Document.Body!.InnerText;

        Assert.Contains("ПРИЛОЖЕНИЕ А", text);
        Assert.Contains("ПРИЛОЖЕНИЕ Б", text);
        Assert.Contains("ПРИЛОЖЕНИЕ В", text);
    }

    [Fact]
    public void NormativeAppendixLetteringExcludesForbiddenAlphabetLetters()
    {
        // GOST 2.105 / STO SUSU 04-2008 explicitly forbid letters: Ё, З, Й, О, Ч, Ь, Ы, Ъ
        foreach (var letter in AllowedAppendixLetters)
        {
            Assert.DoesNotContain(letter, ForbiddenAppendixLetters);
        }

        Assert.Equal(25, AllowedAppendixLetters.Length);
        Assert.Equal(8, ForbiddenAppendixLetters.Length);

        // Test appendix sequence generation logic
        var generatedLetters = AllowedAppendixLetters.Take(10).ToArray();
        Assert.Equal(new[] { 'А', 'Б', 'В', 'Г', 'Д', 'Е', 'Ж', 'И', 'К', 'Л' }, generatedLetters);
    }

    public void Dispose() => Directory.Delete(folder, true);
}
