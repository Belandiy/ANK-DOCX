using AcademicDocx.Core;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace AcademicDocx.Tests;

public sealed class TableContinuationTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "academic-table-continuation-tests-" + Guid.NewGuid().ToString("N"));

    public TableContinuationTests() => Directory.CreateDirectory(folder);

    [Fact]
    public void TableWithHeaderRepeatAndCantSplitValidatesSchemaAndFacts()
    {
        var path = Path.Combine(folder, "table_with_continuation.docx");

        using (var package = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();

            // Row 1: Header row with cantSplit and tblHeader (CT_TrPr order: cantSplit, tblHeader)
            var row1 = new W.TableRow(
                new W.TableRowProperties(
                    new W.CantSplit(),
                    new W.TableHeader()
                ),
                new W.TableCell(
                    new W.TableCellProperties(new W.TableCellWidth { Width = "4500", Type = W.TableWidthUnitValues.Dxa }),
                    new W.Paragraph(new W.Run(new W.Text("Параметр")))
                ),
                new W.TableCell(
                    new W.TableCellProperties(new W.TableCellWidth { Width = "4500", Type = W.TableWidthUnitValues.Dxa }),
                    new W.Paragraph(new W.Run(new W.Text("Значение")))
                )
            );

            // Row 2: Data row with cantSplit
            var row2 = new W.TableRow(
                new W.TableRowProperties(
                    new W.CantSplit()
                ),
                new W.TableCell(
                    new W.TableCellProperties(new W.TableCellWidth { Width = "4500", Type = W.TableWidthUnitValues.Dxa }),
                    new W.Paragraph(new W.Run(new W.Text("Строка 1")))
                ),
                new W.TableCell(
                    new W.TableCellProperties(new W.TableCellWidth { Width = "4500", Type = W.TableWidthUnitValues.Dxa }),
                    new W.Paragraph(new W.Run(new W.Text("100")))
                )
            );

            // Row 3: Data row with cantSplit
            var row3 = new W.TableRow(
                new W.TableRowProperties(
                    new W.CantSplit()
                ),
                new W.TableCell(
                    new W.TableCellProperties(new W.TableCellWidth { Width = "4500", Type = W.TableWidthUnitValues.Dxa }),
                    new W.Paragraph(new W.Run(new W.Text("Строка 2")))
                ),
                new W.TableCell(
                    new W.TableCellProperties(new W.TableCellWidth { Width = "4500", Type = W.TableWidthUnitValues.Dxa }),
                    new W.Paragraph(new W.Run(new W.Text("200")))
                )
            );

            var table = new W.Table(
                new W.TableProperties(
                    new W.TableStyle { Val = "TableGrid" }
                ),
                new W.TableGrid(
                    new W.GridColumn { Width = "4500" },
                    new W.GridColumn { Width = "4500" }
                ),
                row1,
                row2,
                row3
            );

            main.Document = new W.Document(new W.Body(table));
        }

        // 1. Verify OpenXML schema validity
        var validation = DocxInspection.Validate(path);
        Assert.True(validation.ErrorCount == 0, string.Join("; ", validation.Errors.Select(e => e.Description)));

        // 2. Verify TableFacts inspection
        using (var doc = WordprocessingDocument.Open(path, false))
        {
            var table = doc.MainDocumentPart!.Document.Body!.Elements<W.Table>().Single();
            var facts = DocxInspection.ReadTable(table);

            Assert.True(facts.HasHeaderRepeat);
            Assert.True(facts.HasCantSplit);
            Assert.Equal(1, facts.HeaderRows);
            Assert.Equal(3, facts.Rows.Count);

            // Verify OpenXML element structure
            var rows = table.Elements<W.TableRow>().ToList();
            var headerProps = rows[0].TableRowProperties;
            Assert.NotNull(headerProps);
            Assert.NotNull(headerProps!.GetFirstChild<W.TableHeader>());
            Assert.NotNull(headerProps.GetFirstChild<W.CantSplit>());

            for (var i = 1; i < rows.Count; i++)
            {
                var trPr = rows[i].TableRowProperties;
                Assert.NotNull(trPr);
                Assert.NotNull(trPr!.GetFirstChild<W.CantSplit>());
                Assert.Null(trPr.GetFirstChild<W.TableHeader>());
            }
        }
    }

    [Fact]
    public void TableWithoutHeaderRepeatOrCantSplitDetectedProperly()
    {
        var path = Path.Combine(folder, "table_without_continuation.docx");

        using (var package = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();

            // Table without TableRowProperties
            var row1 = new W.TableRow(
                new W.TableCell(new W.Paragraph(new W.Run(new W.Text("Шапка"))))
            );
            var row2 = new W.TableRow(
                new W.TableCell(new W.Paragraph(new W.Run(new W.Text("Данные"))))
            );

            var table = new W.Table(
                new W.TableProperties(new W.TableStyle { Val = "TableGrid" }),
                new W.TableGrid(new W.GridColumn { Width = "9000" }),
                row1,
                row2
            );

            main.Document = new W.Document(new W.Body(table));
        }

        var validation = DocxInspection.Validate(path);
        Assert.True(validation.ErrorCount == 0, string.Join("; ", validation.Errors.Select(e => e.Description)));

        using (var doc = WordprocessingDocument.Open(path, false))
        {
            var table = doc.MainDocumentPart!.Document.Body!.Elements<W.Table>().Single();
            var facts = DocxInspection.ReadTable(table);

            Assert.False(facts.HasHeaderRepeat);
            Assert.False(facts.HasCantSplit);
            Assert.Equal(0, facts.HeaderRows);
        }
    }

    [Fact]
    public void PartialCantSplitReportsFalseForHasCantSplit()
    {
        var path = Path.Combine(folder, "table_partial_cantsplit.docx");

        using (var package = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();

            var row1 = new W.TableRow(
                new W.TableRowProperties(new W.CantSplit(), new W.TableHeader()),
                new W.TableCell(new W.Paragraph(new W.Run(new W.Text("Шапка"))))
            );
            var row2 = new W.TableRow(
                // Missing CantSplit
                new W.TableCell(new W.Paragraph(new W.Run(new W.Text("Данные"))))
            );

            var table = new W.Table(
                new W.TableProperties(new W.TableStyle { Val = "TableGrid" }),
                new W.TableGrid(new W.GridColumn { Width = "9000" }),
                row1,
                row2
            );

            main.Document = new W.Document(new W.Body(table));
        }

        var validation = DocxInspection.Validate(path);
        Assert.True(validation.ErrorCount == 0, string.Join("; ", validation.Errors.Select(e => e.Description)));

        using (var doc = WordprocessingDocument.Open(path, false))
        {
            var table = doc.MainDocumentPart!.Document.Body!.Elements<W.Table>().Single();
            var facts = DocxInspection.ReadTable(table);

            Assert.True(facts.HasHeaderRepeat);
            Assert.False(facts.HasCantSplit);
        }
    }

    public void Dispose() => Directory.Delete(folder, true);
}
