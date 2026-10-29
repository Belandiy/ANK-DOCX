using System.IO.Compression;
using System.Xml.Linq;
using AcademicDocx.Core;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace AcademicDocx.Tests;

public sealed class BibliographyTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "academic-bib-tests-" + Guid.NewGuid().ToString("N"));

    public BibliographyTests() => Directory.CreateDirectory(folder);

    [Fact]
    public void ReadCitationsIdentifiesCitedKeysAndDetectsUncitedSources()
    {
        var path = Path.Combine(folder, "bib_test.docx");
        using (var package = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            main.Document = new W.Document(
                new W.Body(
                    new W.Paragraph(
                        new W.Run(new W.Text("Согласно исследованию ")),
                        new W.Hyperlink(new W.Run(new W.Text("[1]"))) { Anchor = "ref-knuth1984" },
                        new W.Run(new W.Text(" и стандартам ")),
                        new W.Hyperlink(new W.Run(new W.Text("[2]"))) { Anchor = "ref-gost2008" }
                    )
                )
            );

            var customXml = main.AddCustomXmlPart(CustomXmlPartType.CustomXml);
            using (var writer = new StreamWriter(customXml.GetStream()))
            {
                writer.Write(@"<b:Sources xmlns:b=""http://schemas.openxmlformats.org/officeDocument/2006/bibliography"">
                    <b:Source><b:Tag>knuth1984</b:Tag><b:Title>Literate Programming</b:Title></b:Source>
                    <b:Source><b:Tag>gost2008</b:Tag><b:Title>GOST standard</b:Title></b:Source>
                    <b:Source><b:Tag>extra_source</b:Tag><b:Title>Uncited book</b:Title></b:Source>
                </b:Sources>");
            }
        }

        var facts = DocxInspection.ReadCitations(path);

        Assert.Equal(2, facts.CitedKeys.Count);
        Assert.Contains("knuth1984", facts.CitedKeys);
        Assert.Contains("gost2008", facts.CitedKeys);

        Assert.Equal(3, facts.SourceTags.Count);
        Assert.Contains("extra_source", facts.SourceTags);

        Assert.Single(facts.UncitedSources);
        Assert.Equal("extra_source", facts.UncitedSources[0]);
    }

    [Fact]
    public void ReadCitationsReturnsEmptyUncitedSourcesWhenAllAreCited()
    {
        var path = Path.Combine(folder, "bib_complete.docx");
        using (var package = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            main.Document = new W.Document(
                new W.Body(
                    new W.Paragraph(
                        new W.Hyperlink(new W.Run(new W.Text("[1]"))) { Anchor = "ref-knuth1984" },
                        new W.Hyperlink(new W.Run(new W.Text("[2]"))) { Anchor = "ref-gost2008" }
                    )
                )
            );

            var customXml = main.AddCustomXmlPart(CustomXmlPartType.CustomXml);
            using (var writer = new StreamWriter(customXml.GetStream()))
            {
                writer.Write(@"<b:Sources xmlns:b=""http://schemas.openxmlformats.org/officeDocument/2006/bibliography"">
                    <b:Source><b:Tag>knuth1984</b:Tag><b:Title>Literate Programming</b:Title></b:Source>
                    <b:Source><b:Tag>gost2008</b:Tag><b:Title>GOST standard</b:Title></b:Source>
                </b:Sources>");
            }
        }

        var facts = DocxInspection.ReadCitations(path);

        Assert.Equal(2, facts.CitedKeys.Count);
        Assert.Empty(facts.UncitedSources);
    }

    public void Dispose() => Directory.Delete(folder, true);
}
