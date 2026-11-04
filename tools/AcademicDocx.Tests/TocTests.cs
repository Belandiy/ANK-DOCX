using AcademicDocx.Core;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;
using Xunit;

namespace AcademicDocx.Tests;

public sealed class TocTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "academic-toc-tests-" + Guid.NewGuid().ToString("N"));

    public TocTests() => Directory.CreateDirectory(folder);

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void BookBeforeWordFixtureHasTocNativeFieldAndDirtyFlag()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "book-before-word.docx");
        Assert.True(File.Exists(fixturePath), "Fixture book-before-word.docx must exist");

        using var package = WordprocessingDocument.Open(fixturePath, false);
        var facts = DocxInspection.ReadToc(package);

        Assert.True(facts.HasToc, "Expected document to have a TOC");
        Assert.True(facts.HasNativeField, "Expected document to have a native TOC field");
        Assert.True(facts.IsDirty, "Expected TOC field to have w:dirty='true'");
        Assert.NotNull(facts.Title);
    }

    [Fact]
    public void SyntheticDocumentWithTocHeadingAndNativeFieldPassesValidation()
    {
        var path = Path.Combine(folder, "synthetic-toc.docx");

        using (var package = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            main.Document = new W.Document(
                new W.Body(
                    new W.Paragraph(
                        new W.ParagraphProperties(
                            new W.ParagraphStyleId { Val = "TOCHeading" }
                        ),
                        new W.Run(new W.Text("СОДЕРЖАНИЕ"))
                    ),
                    new W.Paragraph(
                        new W.Run(
                            new W.FieldChar { FieldCharType = W.FieldCharValues.Begin, Dirty = true },
                            new W.FieldCode(@"TOC \o ""1-3"" \h \z \u") { Space = SpaceProcessingModeValues.Preserve },
                            new W.FieldChar { FieldCharType = W.FieldCharValues.Separate },
                            new W.FieldChar { FieldCharType = W.FieldCharValues.End }
                        )
                    )
                )
            );
            main.Document.Save();
        }

        var facts = DocxInspection.ReadToc(path);
        Assert.True(facts.HasToc);
        Assert.True(facts.HasNativeField);
        Assert.True(facts.IsDirty);
        Assert.Equal("СОДЕРЖАНИЕ", facts.Title);

        var report = DocxInspection.Validate(path);
        Assert.Equal(0, report.ErrorCount);
    }
}
