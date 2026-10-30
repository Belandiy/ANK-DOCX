using AcademicDocx.Core;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace AcademicDocx.Tests;

public sealed class NoteTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "academic-note-tests-" + Guid.NewGuid().ToString("N"));

    public NoteTests() => Directory.CreateDirectory(folder);

    [Fact]
    public void SingleNoteRendersAsParagraphWithPrefixAndNoTables()
    {
        var path = Path.Combine(folder, "single_note.docx");

        using (var package = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();

            // Single note paragraph: "Примечание — Текст единичного примечания."
            var noteParagraph = new W.Paragraph(
                new W.Run(new W.Text("Примечание")),
                new W.Run(new W.Text(" ")),
                new W.Run(new W.Text("—")),
                new W.Run(new W.Text(" ")),
                new W.Run(new W.Text("Текст единичного примечания."))
            );

            main.Document = new W.Document(new W.Body(noteParagraph));
        }

        // 1. Validate OpenXML schema correctness
        var validationReport = DocxInspection.Validate(path);
        Assert.Equal(0, validationReport.ErrorCount);

        // 2. Validate absence of callout tables or borders and prefix presence
        using (var doc = WordprocessingDocument.Open(path, false))
        {
            var body = doc.MainDocumentPart!.Document.Body!;
            var tables = body.Elements<W.Table>().ToList();
            Assert.Empty(tables);

            var paragraphs = body.Elements<W.Paragraph>().ToList();
            Assert.Single(paragraphs);

            var text = paragraphs[0].InnerText;
            Assert.StartsWith("Примечание — ", text);
            Assert.Equal("Примечание — Текст единичного примечания.", text);
        }
    }

    [Fact]
    public void MultipleNotesGroupRendersAsHeaderAndNumberedParagraphsAndNoTables()
    {
        var path = Path.Combine(folder, "multiple_notes.docx");

        using (var package = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();

            // Group header paragraph: "Примечания"
            var headerParagraph = new W.Paragraph(
                new W.Run(new W.Text("Примечания"))
            );

            // Note 1: "1 Текст первого примечания."
            var note1 = new W.Paragraph(
                new W.Run(new W.Text("1")),
                new W.Run(new W.Text(" ")),
                new W.Run(new W.Text("Текст первого примечания."))
            );

            // Note 2: "2 Текст второго примечания."
            var note2 = new W.Paragraph(
                new W.Run(new W.Text("2")),
                new W.Run(new W.Text(" ")),
                new W.Run(new W.Text("Текст второго примечания."))
            );

            main.Document = new W.Document(new W.Body(headerParagraph, note1, note2));
        }

        // 1. Validate OpenXML schema correctness
        var validationReport = DocxInspection.Validate(path);
        Assert.Equal(0, validationReport.ErrorCount);

        // 2. Validate absence of callout tables or borders and header + numbered paragraphs presence
        using (var doc = WordprocessingDocument.Open(path, false))
        {
            var body = doc.MainDocumentPart!.Document.Body!;
            var tables = body.Elements<W.Table>().ToList();
            Assert.Empty(tables);

            var paragraphs = body.Elements<W.Paragraph>().ToList();
            Assert.Equal(3, paragraphs.Count);

            Assert.Equal("Примечания", paragraphs[0].InnerText);
            Assert.Equal("1 Текст первого примечания.", paragraphs[1].InnerText);
            Assert.Equal("2 Текст второго примечания.", paragraphs[2].InnerText);
        }
    }

    public void Dispose() => Directory.Delete(folder, true);
}
