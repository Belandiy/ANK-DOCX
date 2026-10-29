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

    [Fact]
    public void AppendixDocumentContainsNormativeHeadingAndLocalSeqFields()
    {
        var path = Path.Combine(folder, "appendix_test.docx");

        using (var package = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();

            static W.Paragraph CreateSeqParagraph(string prefix, string seqName, string suffix, string bookmarkName, string bookmarkId)
            {
                var p = new W.Paragraph();
                p.Append(new W.Run(new W.Text(prefix) { Space = SpaceProcessingModeValues.Preserve }));
                p.Append(new W.BookmarkStart { Name = bookmarkName, Id = bookmarkId });
                p.Append(new W.Run(new W.Text("А.") { Space = SpaceProcessingModeValues.Preserve }));
                p.Append(new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.Begin, Dirty = true }));
                p.Append(new W.Run(new W.FieldCode($" SEQ {seqName} \\* ARABIC ") { Space = SpaceProcessingModeValues.Preserve }));
                p.Append(new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.Separate }));
                p.Append(new W.Run(new W.Text("0")));
                p.Append(new W.Run(new W.FieldChar { FieldCharType = W.FieldCharValues.End }));
                p.Append(new W.BookmarkEnd { Id = bookmarkId });
                p.Append(new W.Run(new W.Text(suffix) { Space = SpaceProcessingModeValues.Preserve }));
                return p;
            }

            var body = new W.Body(
                // Main section heading
                new W.Paragraph(new W.Run(new W.Text("1 Основной раздел"))),
                new W.Paragraph(new W.Run(new W.Text("Текст основного раздела"))),

                // Page break before appendix
                new W.Paragraph(new W.Run(new W.Break { Type = W.BreakValues.Page })),

                // Appendix Heading (Unnumbered Heading style, centered)
                new W.Paragraph(
                    new W.ParagraphProperties(
                        new W.ParagraphStyleId { Val = "UnnumberedHeading" },
                        new W.PageBreakBefore(),
                        new W.Justification { Val = W.JustificationValues.Center }
                    ),
                    new W.Run(new W.Text("ПРИЛОЖЕНИЕ А")),
                    new W.Run(new W.Break()),
                    new W.Run(new W.Text("(обязательное)")),
                    new W.Run(new W.Break()),
                    new W.Run(new W.Text("Структурная схема и исходный код"))
                ),

                // Paragraph with formula
                new W.Paragraph(new W.Run(new W.Text("В приложении приведены формулы, рисунки, таблицы и листинги."))),
                CreateSeqParagraph("(", "Formula_A", ")", "eq_app1", "1001"),

                // Figure caption in Appendix A
                CreateSeqParagraph("Рисунок ", "Figure_A", " — Схема алгоритма", "fig_app1", "1002"),

                // Table caption in Appendix A
                CreateSeqParagraph("Таблица ", "Table_A", " — Сводные параметры", "tbl_app1", "1003"),

                // Listing caption in Appendix A
                CreateSeqParagraph("Листинг ", "Listing_A", " — Текст программы", "lst_app1", "1004"),

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

    public void Dispose() => Directory.Delete(folder, true);
}
