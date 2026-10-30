using System.IO.Compression;
using AcademicDocx.Core;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace AcademicDocx.Tests;

public sealed class ExplicationTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "academic-explication-tests-" + Guid.NewGuid().ToString("N"));

    public ExplicationTests() => Directory.CreateDirectory(folder);

    [Fact]
    public void ExplicationTableStructureAndSchemaAreValid()
    {
        var path = Path.Combine(folder, "explication_test.docx");

        using (var package = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();

            // Create borderless 2-column table representing formula symbol explication
            var table = new W.Table(
                new W.TableProperties(
                    new W.TableStyle { Val = "TableGrid" },
                    new W.TableWidth { Width = "0", Type = W.TableWidthUnitValues.Auto },
                    new W.TableIndentation { Width = 709, Type = W.TableWidthUnitValues.Dxa },
                    new W.TableBorders(
                        new W.TopBorder { Val = W.BorderValues.None },
                        new W.LeftBorder { Val = W.BorderValues.None },
                        new W.BottomBorder { Val = W.BorderValues.None },
                        new W.RightBorder { Val = W.BorderValues.None },
                        new W.InsideHorizontalBorder { Val = W.BorderValues.None },
                        new W.InsideVerticalBorder { Val = W.BorderValues.None }
                    ),
                    new W.TableLook { Val = "04A0" }
                ),
                new W.TableGrid(
                    new W.GridColumn { Width = "1701" },
                    new W.GridColumn { Width = "7512" }
                ),
                // Row 1
                new W.TableRow(
                    new W.TableRowProperties(new W.CantSplit()),
                    new W.TableCell(
                        new W.TableCellProperties(new W.TableCellWidth { Width = "1701", Type = W.TableWidthUnitValues.Dxa }),
                        new W.Paragraph(
                            new W.ParagraphProperties(
                                new W.ParagraphStyleId { Val = "FormulaDefinitions" },
                                new W.SpacingBetweenLines { Before = "0", After = "0", Line = "240", LineRule = W.LineSpacingRuleValues.Auto },
                                new W.Indentation { FirstLine = "0", Left = "0" }
                            ),
                            new W.Run(new W.Text("где f(x)"))
                        )
                    ),
                    new W.TableCell(
                        new W.TableCellProperties(new W.TableCellWidth { Width = "7512", Type = W.TableWidthUnitValues.Dxa }),
                        new W.Paragraph(
                            new W.ParagraphProperties(
                                new W.ParagraphStyleId { Val = "FormulaDefinitions" },
                                new W.SpacingBetweenLines { Before = "0", After = "0", Line = "240", LineRule = W.LineSpacingRuleValues.Auto },
                                new W.Indentation { FirstLine = "0", Left = "0" }
                            ),
                            new W.Run(new W.Text("— квадратичное слагаемое;"))
                        )
                    )
                ),
                // Row 2 (last row)
                new W.TableRow(
                    new W.TableRowProperties(new W.CantSplit()),
                    new W.TableCell(
                        new W.TableCellProperties(new W.TableCellWidth { Width = "1701", Type = W.TableWidthUnitValues.Dxa }),
                        new W.Paragraph(
                            new W.ParagraphProperties(
                                new W.ParagraphStyleId { Val = "FormulaDefinitions" },
                                new W.SpacingBetweenLines { Before = "0", After = "0", Line = "240", LineRule = W.LineSpacingRuleValues.Auto },
                                new W.Indentation { FirstLine = "0", Left = "0" }
                            ),
                            new W.Run(new W.Text("x"))
                        )
                    ),
                    new W.TableCell(
                        new W.TableCellProperties(new W.TableCellWidth { Width = "7512", Type = W.TableWidthUnitValues.Dxa }),
                        new W.Paragraph(
                            new W.ParagraphProperties(
                                new W.ParagraphStyleId { Val = "FormulaDefinitions" },
                                new W.SpacingBetweenLines { Before = "0", After = "0", Line = "240", LineRule = W.LineSpacingRuleValues.Auto },
                                new W.Indentation { FirstLine = "0", Left = "0" }
                            ),
                            new W.Run(new W.Text("— аргумент функции."))
                        )
                    )
                )
            );

            var body = new W.Body(
                new W.Paragraph(new W.Run(new W.Text("Исходная формула"))),
                table
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
            var tables = mainDoc.Body!.Elements<W.Table>().ToList();
            Assert.Single(tables);

            var tbl = tables[0];
            var tblPr = tbl.GetFirstChild<W.TableProperties>();
            Assert.NotNull(tblPr);

            // Table indent must be 709 dxa (1.25 cm)
            Assert.Equal(709, tblPr!.TableIndentation!.Width!.Value);

            // Table borders must be borderless (none)
            var borders = tblPr.TableBorders;
            Assert.NotNull(borders);
            Assert.Equal(W.BorderValues.None, borders!.TopBorder!.Val!.Value);
            Assert.Equal(W.BorderValues.None, borders.LeftBorder!.Val!.Value);
            Assert.Equal(W.BorderValues.None, borders.BottomBorder!.Val!.Value);
            Assert.Equal(W.BorderValues.None, borders.RightBorder!.Val!.Value);

            // Rows check
            var rows = tbl.Elements<W.TableRow>().ToList();
            Assert.Equal(2, rows.Count);

            // Row 1 checks
            var row1Cells = rows[0].Elements<W.TableCell>().ToList();
            Assert.Equal(2, row1Cells.Count);

            var cell11Text = row1Cells[0].InnerText;
            Assert.StartsWith("где", cell11Text);

            var cell12Text = row1Cells[1].InnerText;
            Assert.StartsWith("—", cell12Text);
            Assert.EndsWith(";", cell12Text);

            // Row 2 checks (last row)
            var row2Cells = rows[1].Elements<W.TableCell>().ToList();
            Assert.Equal(2, row2Cells.Count);

            var cell22Text = row2Cells[1].InnerText;
            Assert.StartsWith("—", cell22Text);
            Assert.EndsWith(".", cell22Text);
        }
    }

    public void Dispose() => Directory.Delete(folder, true);
}
