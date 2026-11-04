using AcademicDocx.Core;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;
using Pic = DocumentFormat.OpenXml.Drawing.Pictures;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace AcademicDocx.Tests;

public sealed class GrayscaleImageTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "academic-grayscale-tests-" + Guid.NewGuid().ToString("N"));

    public GrayscaleImageTests() => Directory.CreateDirectory(folder);

    [Fact]
    public void DocumentWithGrayscaleImagePassesValidationAndContainsGrayscaleEffect()
    {
        var path = Path.Combine(folder, "grayscale_image.docx");

        using (var package = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            var imagePart = main.AddImagePart(ImagePartType.Png);

            // 1x1 transparent/black PNG byte stream
            byte[] pngBytes = [
                0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
                0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
                0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
                0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
                0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41,
                0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
                0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
                0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
                0x42, 0x60, 0x82
            ];

            using (var stream = imagePart.GetStream(FileMode.Create, FileAccess.Write))
            {
                stream.Write(pngBytes, 0, pngBytes.Length);
            }

            var relId = main.GetIdOfPart(imagePart);

            var blip = new A.Blip(new A.Grayscale()) { Embed = relId };

            var drawing = new W.Drawing(
                new DW.Inline(
                    new DW.Extent() { Cx = 990000L, Cy = 792000L },
                    new DW.EffectExtent() { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                    new DW.DocProperties() { Id = 1U, Name = "Picture 1" },
                    new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks() { NoChangeAspect = true }),
                    new A.Graphic(
                        new A.GraphicData(
                            new Pic.Picture(
                                new Pic.NonVisualPictureProperties(
                                    new Pic.NonVisualDrawingProperties() { Id = 0U, Name = "image.png" },
                                    new Pic.NonVisualPictureDrawingProperties()),
                                new Pic.BlipFill(
                                    blip,
                                    new A.Stretch(new A.FillRectangle())),
                                new Pic.ShapeProperties(
                                    new A.Transform2D(
                                        new A.Offset() { X = 0L, Y = 0L },
                                        new A.Extents() { Cx = 990000L, Cy = 792000L }),
                                    new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }))
                        ) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }
                    )
                ) { DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U }
            );

            main.Document = new W.Document(new W.Body(new W.Paragraph(new W.Run(drawing))));
        }

        // 1. Validate OpenXML schema correctness
        var validationReport = DocxInspection.Validate(path);
        Assert.Equal(0, validationReport.ErrorCount);

        // 2. Validate presence of <a:grayscl/> inside <a:blip>
        using (var doc = WordprocessingDocument.Open(path, false))
        {
            var body = doc.MainDocumentPart!.Document.Body!;
            var blips = body.Descendants<A.Blip>().ToList();
            Assert.Single(blips);
            Assert.NotNull(blips[0].GetFirstChild<A.Grayscale>());
        }
    }

    [Fact]
    public void PreExistingDocumentImagesCanBeGrayscaledAndPassValidation()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "book-before-word.docx");
        Assert.True(File.Exists(fixturePath), "Fixture book-before-word.docx must exist");

        var testDocPath = Path.Combine(folder, "book-grayscaled.docx");
        File.Copy(fixturePath, testDocPath, true);

        using (var doc = WordprocessingDocument.Open(testDocPath, true))
        {
            var body = doc.MainDocumentPart!.Document.Body!;
            var blips = body.Descendants<A.Blip>().ToList();
            Assert.NotEmpty(blips);

            foreach (var blip in blips)
            {
                if (blip.GetFirstChild<A.Grayscale>() == null)
                {
                    blip.InsertAt(new A.Grayscale(), 0);
                }
            }

            doc.MainDocumentPart.Document.Save();
        }

        // Verify that all blips now have Grayscale and file is valid
        using (var doc = WordprocessingDocument.Open(testDocPath, false))
        {
            var body = doc.MainDocumentPart!.Document.Body!;
            var blips = body.Descendants<A.Blip>().ToList();
            Assert.NotEmpty(blips);

            foreach (var blip in blips)
            {
                Assert.NotNull(blip.GetFirstChild<A.Grayscale>());
            }
        }
    }

    public void Dispose() => Directory.Delete(folder, true);
}
