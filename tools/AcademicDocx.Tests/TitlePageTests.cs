using System.IO.Compression;
using System.Xml.Linq;
using AcademicDocx.Core;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace AcademicDocx.Tests;

public sealed class TitlePageTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "academic-title-tests-" + Guid.NewGuid().ToString("N"));

    public TitlePageTests() => Directory.CreateDirectory(folder);

    [Fact]
    public void MetadataFileConfiguresSusuCourseworkProfile()
    {
        var metaPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "academic", "metadata.yml");
        if (!File.Exists(metaPath))
        {
            metaPath = Path.Combine(Directory.GetCurrentDirectory(), "academic", "metadata.yml");
        }
        Assert.True(File.Exists(metaPath), $"metadata.yml should exist at {metaPath}");

        var content = File.ReadAllText(metaPath);
        Assert.Contains("profile: \"susu-coursework-v1\"", content);
        Assert.Contains("university:", content);
        Assert.Contains("department:", content);
        Assert.Contains("discipline:", content);
        Assert.Contains("group:", content);
        Assert.Contains("supervisor:", content);
        Assert.Contains("normocontroler:", content);
    }

    [Fact]
    public void CourseworkTitleTemplateIsPresentAndValid()
    {
        var templatePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "academic", "_extensions", "academic", "sheets", "coursework-title.openxml");
        if (!File.Exists(templatePath))
        {
            templatePath = Path.Combine(Directory.GetCurrentDirectory(), "academic", "_extensions", "academic", "sheets", "coursework-title.openxml");
        }
        Assert.True(File.Exists(templatePath), $"coursework-title.openxml should exist at {templatePath}");

        var xml = XDocument.Load(templatePath);
        Assert.Equal("title-sheet", xml.Root!.Name.LocalName);
        Assert.Equal("susu-coursework-v1", xml.Root.Attribute("profile")?.Value);

        var raw = File.ReadAllText(templatePath);
        Assert.Contains("$university$", raw);
        Assert.Contains("$department$", raw);
        Assert.Contains("$discipline$", raw);
        Assert.Contains("$title$", raw);
        Assert.Contains("$group$", raw);
        Assert.Contains("$author$", raw);
        Assert.Contains("$supervisor$", raw);
        Assert.Contains("$normocontroler$", raw);
    }

    [Fact]
    public void TitlePagePackageContainsRequiredElementsAndTitlePgProperty()
    {
        var path = Path.Combine(folder, "title_page_test.docx");

        using (var package = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();

            var body = new W.Body(
                // University & Ministry
                new W.Paragraph(new W.Run(new W.Text("Министерство науки и высшего образования Российской Федерации"))),
                new W.Paragraph(new W.Run(new W.Text("ФГАОУ ВО «ЮУрГУ (НИУ)»"))),
                // Department
                new W.Paragraph(new W.Run(new W.Text("Кафедра прикладных исследований"))),
                // Work type
                new W.Paragraph(new W.Run(new W.Text("КУРСОВАЯ РАБОТА"))),
                // Topic
                new W.Paragraph(new W.Run(new W.Text("Исследование суммы математических функций"))),
                // Student
                new W.Paragraph(new W.Run(new W.Text("Выполнил: студент группы ММ-101 Иванов И.И."))),
                // Supervisor
                new W.Paragraph(new W.Run(new W.Text("Руководитель: Петрова А.С."))),
                // Normocontroler
                new W.Paragraph(new W.Run(new W.Text("Нормоконтролер: Сидоров В.В."))),
                // Page break
                new W.Paragraph(new W.Run(new W.Break() { Type = W.BreakValues.Page })),
                // Body content
                new W.Paragraph(new W.Run(new W.Text("Основной текст документа"))),
                // Section properties with TitlePage (<w:titlePg/>)
                new W.SectionProperties(
                    new W.PageSize() { Width = 11906, Height = 16838 },
                    new W.PageMargin() { Top = 1134, Right = 567, Bottom = 1474, Left = 1417, Header = 1134, Footer = 1134 },
                    new W.TitlePage()
                )
            );

            main.Document = new W.Document(body);
        }

        // 1. Validate Open XML schema correctness
        var validationReport = DocxInspection.Validate(path);
        Assert.Equal(0, validationReport.ErrorCount);

        // 2. Inspect document structure
        using (var doc = WordprocessingDocument.Open(path, false))
        {
            var mainDoc = doc.MainDocumentPart!.Document;
            var fullText = mainDoc.Body!.InnerText;

            // Check title page text elements
            Assert.Contains("ЮУрГУ", fullText);
            Assert.Contains("Кафедра прикладных исследований", fullText);
            Assert.Contains("Исследование суммы математических функций", fullText);
            Assert.Contains("Иванов И.И.", fullText);
            Assert.Contains("Петрова А.С.", fullText);
            Assert.Contains("Сидоров В.В.", fullText);

            // Check <w:titlePg/> presence in SectionProperties
            var sectPrs = mainDoc.Descendants<W.SectionProperties>();
            Assert.NotEmpty(sectPrs);
            Assert.Contains(sectPrs, s => s.GetFirstChild<W.TitlePage>() != null);
        }
    }

    public void Dispose() => Directory.Delete(folder, true);
}
