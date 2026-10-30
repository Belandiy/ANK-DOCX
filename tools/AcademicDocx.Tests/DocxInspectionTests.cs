using AcademicDocx.Core;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using M = DocumentFormat.OpenXml.Math;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace AcademicDocx.Tests;

public sealed class DocxInspectionTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "academic-inspection-tests-" + Guid.NewGuid().ToString("N"));

    public DocxInspectionTests() => Directory.CreateDirectory(folder);

    [Fact]
    public void ReadTableParsesHeaderRowsHeaderShapeGridWidthsAndMathCount()
    {
        var path = Path.Combine(folder, "table_inspection.docx");

        using (var package = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();

            var table = new W.Table(
                new W.TableProperties(
                    new W.TableStyle { Val = "TableGrid" }
                ),
                new W.TableGrid(
                    new W.GridColumn { Width = "3000" },
                    new W.GridColumn { Width = "4500" }
                ),
                // Row 1 (Header row 1: merged horizontally across 2 columns, vMerge restart)
                new W.TableRow(
                    new W.TableRowProperties(new W.TableHeader()),
                    new W.TableCell(
                        new W.TableCellProperties(
                            new W.GridSpan { Val = 2 },
                            new W.VerticalMerge { Val = W.MergedCellValues.Restart }
                        ),
                        new W.Paragraph(
                            new W.Run(new W.Text("Заголовок таблицы ")),
                            new M.OfficeMath(new M.Run(new M.Text("x^2")))
                        )
                    )
                ),
                // Row 2 (Header row 2: split columns, col1 vMerge continue, col2 vMerge none)
                new W.TableRow(
                    new W.TableRowProperties(new W.TableHeader()),
                    new W.TableCell(
                        new W.TableCellProperties(
                            new W.GridSpan { Val = 1 },
                            new W.VerticalMerge { Val = W.MergedCellValues.Continue }
                        ),
                        new W.Paragraph(new W.Run(new W.Text("Колонка 1")))
                    ),
                    new W.TableCell(
                        new W.TableCellProperties(
                            new W.GridSpan { Val = 1 }
                        ),
                        new W.Paragraph(
                            new W.Run(new W.Text("Колонка 2 ")),
                            new M.OfficeMath(new M.Run(new M.Text("y=2x")))
                        )
                    )
                ),
                // Row 3 (Data row)
                new W.TableRow(
                    new W.TableCell(
                        new W.Paragraph(new W.Run(new W.Text("Значение 1")))
                    ),
                    new W.TableCell(
                        new W.Paragraph(new W.Run(new W.Text("Значение 2")))
                    )
                )
            );

            main.Document = new W.Document(new W.Body(table));
        }

        // Validate OpenXML schema
        var validation = DocxInspection.Validate(path);
        Assert.Equal(0, validation.ErrorCount);

        // Read table facts using DocxInspection
        using (var doc = WordprocessingDocument.Open(path, false))
        {
            var tbl = doc.MainDocumentPart!.Document.Body!.Elements<W.Table>().First();
            var tableFacts = DocxInspection.ReadTable(tbl);

            Assert.Equal(2, tableFacts.HeaderRows);
            Assert.Equal(new[] { 3000, 4500 }, tableFacts.GridWidths);
            Assert.Equal(2, tableFacts.MathCount);

            // Check HeaderShape (elements for cell 1: "2:restart", cell 2: "1:continue", cell 3: "1:none")
            Assert.Equal(3, tableFacts.HeaderShape.Count);
            Assert.Equal("2:restart", tableFacts.HeaderShape[0]);
            Assert.Equal("1:continue", tableFacts.HeaderShape[1]);
            Assert.Equal("1:none", tableFacts.HeaderShape[2]);

            // Check rows data
            Assert.Equal(3, tableFacts.Rows.Count);
            Assert.Equal(new[] { "Заголовок таблицы x^2" }, tableFacts.Rows[0]);
            Assert.Equal(new[] { "Колонка 1", "Колонка 2 y=2x" }, tableFacts.Rows[1]);
            Assert.Equal(new[] { "Значение 1", "Значение 2" }, tableFacts.Rows[2]);
        }

        // Check document facts via DocxInspection.Read
        var facts = DocxInspection.Read(path);
        Assert.Equal(2, facts.MathCount);
        Assert.Single(facts.Tables);
        Assert.Equal(2, facts.Tables[0].HeaderRows);
    }

    [Fact]
    public void SameRowsComparesLogicalRowsCorrectly()
    {
        var rows1 = new List<string[]>
        {
            new[] { "Заголовок 1", "Заголовок 2" },
            new[] { "Данные A", "Данные B" }
        };

        var rows2 = new List<string[]>
        {
            new[] { "Заголовок 1", "Заголовок 2" },
            new[] { "Данные A", "Данные B" }
        };

        var rowsDifferentContent = new List<string[]>
        {
            new[] { "Заголовок 1", "Заголовок 2" },
            new[] { "Данные A", "Другие данные" }
        };

        var rowsDifferentLength = new List<string[]>
        {
            new[] { "Заголовок 1", "Заголовок 2" }
        };

        Assert.True(DocxInspection.SameRows(rows1, rows2));
        Assert.False(DocxInspection.SameRows(rows1, rowsDifferentContent));
        Assert.False(DocxInspection.SameRows(rows1, rowsDifferentLength));
    }

    public void Dispose() => Directory.Delete(folder, true);
}
