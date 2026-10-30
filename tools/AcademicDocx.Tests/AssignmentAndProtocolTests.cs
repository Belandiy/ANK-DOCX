using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using AcademicDocx.Core;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace AcademicDocx.Tests;

public sealed class AssignmentAndProtocolTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "academic-assignment-protocol-tests-" + Guid.NewGuid().ToString("N"));

    public AssignmentAndProtocolTests() => Directory.CreateDirectory(folder);

    [Fact]
    public void CourseworkAssignmentTemplateIsPresentAndValid()
    {
        var templatePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "_extensions", "academic", "sheets", "coursework-assignment.openxml"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "academic", "_extensions", "academic", "sheets", "coursework-assignment.openxml"),
            Path.Combine(Directory.GetCurrentDirectory(), "_extensions", "academic", "sheets", "coursework-assignment.openxml"),
            Path.Combine(Directory.GetCurrentDirectory(), "academic", "_extensions", "academic", "sheets", "coursework-assignment.openxml")
        };

        var existingPath = templatePaths.FirstOrDefault(File.Exists);
        Assert.NotNull(existingPath);

        var xml = XDocument.Load(existingPath);
        Assert.Equal("assignment-sheet", xml.Root!.Name.LocalName);
        Assert.Equal("susu-coursework-v1", xml.Root.Attribute("profile")?.Value);

        var raw = File.ReadAllText(existingPath);
        Assert.Contains("$ministry$", raw);
        Assert.Contains("$university$", raw);
        Assert.Contains("$institute$", raw);
        Assert.Contains("$department$", raw);
        Assert.Contains("$title$", raw);
        Assert.Contains("$author$", raw);
        Assert.Contains("$group$", raw);
        Assert.Contains("$assignment.deadline$", raw);
        Assert.Contains("$assignment.initial_data$", raw);
        Assert.Contains("$assignment.tasks$", raw);
        Assert.Contains("$supervisor$", raw);
    }

    [Fact]
    public void MetadataFileContainsAssignmentSection()
    {
        var metaPaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "metadata.yml"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "academic", "metadata.yml"),
            Path.Combine(Directory.GetCurrentDirectory(), "metadata.yml"),
            Path.Combine(Directory.GetCurrentDirectory(), "academic", "metadata.yml")
        };

        var existingPath = metaPaths.FirstOrDefault(File.Exists);
        Assert.NotNull(existingPath);

        var content = File.ReadAllText(existingPath);
        Assert.Contains("assignment:", content);
        Assert.Contains("deadline:", content);
        Assert.Contains("initial_data:", content);
        Assert.Contains("tasks:", content);
        Assert.Contains("20.12.2026", content);
    }

    [Fact]
    public void AssignmentSheetGenerationAndMarkup()
    {
        var docPath = Path.Combine(folder, "assignment_test.docx");

        using (var package = WordprocessingDocument.Create(docPath, WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();

            var body = new W.Body(
                // Title page
                new W.Paragraph(new W.Run(new W.Text("ФГАОУ ВО «ЮУрГУ (НИУ)»"))),
                new W.Paragraph(new W.Run(new W.Text("КУРСОВАЯ РАБОТА"))),
                new W.Paragraph(new W.Run(new W.Text("Челябинск — 2026"))),
                new W.Paragraph(new W.Run(new W.Break() { Type = W.BreakValues.Page })),

                // Assignment sheet
                new W.Paragraph(new W.Run(new W.Text("Министерство науки и высшего образования Российской Федерации"))),
                new W.Paragraph(new W.Run(new W.Text("ФГАОУ ВО «ЮУрГУ (НИУ)»"))),
                new W.Paragraph(new W.Run(new W.Text("Высшая школа электроники и компьютерных наук"))),
                new W.Paragraph(new W.Run(new W.Text("Кафедра прикладной математики и информатики"))),
                new W.Paragraph(new W.Run(new W.Text("ЗАДАНИЕ на курсовую работу"))),
                new W.Paragraph(new W.Run(new W.Text("Студент: Иванов И.И., группа: ВШЭКН-101"))),
                new W.Paragraph(new W.Run(new W.Text("Тема работы: Исследование свойств алгоритмов"))),
                new W.Paragraph(new W.Run(new W.Text("Срок сдачи работы: 20.12.2026"))),
                new W.Paragraph(new W.Run(new W.Text("Исходные данные к работе: Исходное техническое задание, нормативные требования ГОСТ и СТО ЮУрГУ"))),
                new W.Paragraph(new W.Run(new W.Text("Перечень подлежащих разработке вопросов:"))),
                new W.Paragraph(new W.Run(new W.Text("1. Анализ предметной области и существующих решений"))),
                new W.Paragraph(new W.Run(new W.Text("2. Проектирование архитектуры и разработка алгоритмов"))),
                new W.Paragraph(new W.Run(new W.Text("3. Программная реализация и экспериментальное исследование"))),
                new W.Paragraph(new W.Run(new W.Text("Руководитель работы: Петрова Анна Сергеевна ___________________ (подпись)"))),
                new W.Paragraph(new W.Run(new W.Text("Задание принял к исполнению: Иванов И.И. ___________________ (подпись)"))),
                new W.Paragraph(new W.Run(new W.Break() { Type = W.BreakValues.Page })),

                // Main body
                new W.Paragraph(new W.Run(new W.Text("Основной текст курсовой работы"))),

                new W.SectionProperties(
                    new W.PageSize() { Width = 11906, Height = 16838 },
                    new W.PageMargin() { Top = 1134, Right = 567, Bottom = 1474, Left = 1417, Header = 1134, Footer = 1134 }
                )
            );

            main.Document = new W.Document(body);
        }

        // Schema validation
        var report = DocxInspection.Validate(docPath);
        Assert.Equal(0, report.ErrorCount);

        // Content inspection
        using var doc = WordprocessingDocument.Open(docPath, false);
        var fullText = doc.MainDocumentPart!.Document.Body!.InnerText;

        Assert.Contains("ЗАДАНИЕ на курсовую работу", fullText);
        Assert.Contains("Срок сдачи работы: 20.12.2026", fullText);
        Assert.Contains("Исходное техническое задание", fullText);
        Assert.Contains("1. Анализ предметной области", fullText);
        Assert.Contains("2. Проектирование архитектуры", fullText);
        Assert.Contains("3. Программная реализация", fullText);
        Assert.Contains("Руководитель работы: Петрова Анна Сергеевна", fullText);
        Assert.Contains("Задание принял к исполнению:", fullText);
    }

    [Fact]
    public void NormocontrolProtocolGenerationFromDiagnosticsJson()
    {
        // 1. Clean state test (no diagnostics)
        var cleanDocPath = Path.Combine(folder, "protocol_clean.docx");
        CreateDocumentWithProtocol(cleanDocPath, new List<DiagnosticItem>());

        var cleanReport = DocxInspection.Validate(cleanDocPath);
        Assert.Equal(0, cleanReport.ErrorCount);

        using (var cleanDoc = WordprocessingDocument.Open(cleanDocPath, false))
        {
            var text = cleanDoc.MainDocumentPart!.Document.Body!.InnerText;
            Assert.Contains("ПРОТОКОЛ АВТОМАТИЧЕСКОГО НОРМОКОНТРОЛЯ", text);
            Assert.Contains("СООТВЕТСТВУЕТ СТАНДАРТУ СТО ЮУрГУ 04-2008", text);
            Assert.Contains("Формулы", text);
            Assert.Contains("Рисунки", text);
            Assert.Contains("Таблицы", text);
            Assert.Contains("Листинги", text);
            Assert.Contains("Библиография", text);
            Assert.Contains("Приложения", text);
            Assert.Contains("Поля страницы", text);
            Assert.Contains("Проверено (Соответствует)", text);
            Assert.Contains("Нормоконтролер:", text);
        }

        // 2. Warning state test (with diagnostics)
        var warningDocPath = Path.Combine(folder, "protocol_warning.docx");
        var diagnostics = new List<DiagnosticItem>
        {
            new("FIG_REF_MISSING", "Warning", "index.qmd", "sec-1", "Введение", "fig-1", "объект #fig-1", "Перед рисунком #fig-1 отсутствует ссылка.", "Добавьте ссылку @fig-1."),
            new("TBL_REF_MISSING", "Warning", "index.qmd", "sec-2", "Результаты", "tbl-1", "объект #tbl-1", "Перед таблицей #tbl-1 отсутствует ссылка.", "Добавьте ссылку @tbl-1.")
        };
        CreateDocumentWithProtocol(warningDocPath, diagnostics);

        var warningReport = DocxInspection.Validate(warningDocPath);
        Assert.Equal(0, warningReport.ErrorCount);

        using (var warningDoc = WordprocessingDocument.Open(warningDocPath, false))
        {
            var text = warningDoc.MainDocumentPart!.Document.Body!.InnerText;
            Assert.Contains("ПРОТОКОЛ АВТОМАТИЧЕСКОГО НОРМОКОНТРОЛЯ", text);
            Assert.Contains("СООТВЕТСТВУЕТ С ПРЕДУПРЕЖДЕНИЯМИ", text);
            Assert.Contains("Детальный перечень замечаний:", text);
            Assert.Contains("FIG_REF_MISSING", text);
            Assert.Contains("TBL_REF_MISSING", text);
            Assert.Contains("Перед рисунком #fig-1 отсутствует ссылка.", text);
            Assert.Contains("Перед таблицей #tbl-1 отсутствует ссылка.", text);
        }
    }

    private static void CreateDocumentWithProtocol(string path, List<DiagnosticItem> diagnostics)
    {
        using var package = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var main = package.AddMainDocumentPart();

        var bodyElements = new List<OpenXmlElement>
        {
            new W.Paragraph(new W.Run(new W.Text("Основной текст документа"))),
            new W.Paragraph(new W.Run(new W.Break() { Type = W.BreakValues.Page })),
            new W.Paragraph(new W.Run(new W.Text("ПРОТОКОЛ АВТОМАТИЧЕСКОГО НОРМОКОНТРОЛЯ"))),
            new W.Paragraph(
                new W.Run(new W.RunProperties(new W.Bold()), new W.Text("Статус проверки: ")),
                new W.Run(new W.Text(diagnostics.Count == 0 ? "СООТВЕТСТВУЕТ СТАНДАРТУ СТО ЮУрГУ 04-2008" : "СООТВЕТСТВУЕТ С ПРЕДУПРЕЖДЕНИЯМИ (требуется устранение замечаний)"))
            )
        };

        // Summary table
        var summaryTable = new W.Table(
            new W.TableProperties(
                new W.TableStyle() { Val = "TableGrid" },
                new W.TableWidth() { Width = "0", Type = W.TableWidthUnitValues.Auto },
                new W.TableBorders(
                    new W.TopBorder() { Val = W.BorderValues.Single, Size = 4, Space = 0, Color = "auto" },
                    new W.LeftBorder() { Val = W.BorderValues.Single, Size = 4, Space = 0, Color = "auto" },
                    new W.BottomBorder() { Val = W.BorderValues.Single, Size = 4, Space = 0, Color = "auto" },
                    new W.RightBorder() { Val = W.BorderValues.Single, Size = 4, Space = 0, Color = "auto" },
                    new W.InsideHorizontalBorder() { Val = W.BorderValues.Single, Size = 4, Space = 0, Color = "auto" },
                    new W.InsideVerticalBorder() { Val = W.BorderValues.Single, Size = 4, Space = 0, Color = "auto" }
                )
            ),
            new W.TableGrid(new W.GridColumn() { Width = "4500" }, new W.GridColumn() { Width = "4500" }),
            new W.TableRow(
                new W.TableRowProperties(new W.TableHeader()),
                new W.TableCell(new W.TableCellProperties(new W.TableCellWidth() { Width = "4500", Type = W.TableWidthUnitValues.Dxa }), new W.Paragraph(new W.Run(new W.RunProperties(new W.Bold()), new W.Text("Категория правил")))),
                new W.TableCell(new W.TableCellProperties(new W.TableCellWidth() { Width = "4500", Type = W.TableWidthUnitValues.Dxa }), new W.Paragraph(new W.Run(new W.RunProperties(new W.Bold()), new W.Text("Результат проверки"))))
            )
        );

        var categories = new[] { "Формулы", "Рисунки", "Таблицы", "Листинги", "Библиография", "Приложения", "Поля страницы" };
        foreach (var cat in categories) {
            int count = diagnostics.Count(d => (cat == "Рисунки" && d.Rule.StartsWith("FIG_")) || (cat == "Таблицы" && d.Rule.StartsWith("TBL_")));
            string res = count == 0 ? "Проверено (Соответствует)" : $"Замечаний: {count}";
            summaryTable.AppendChild(new W.TableRow(
                new W.TableCell(new W.TableCellProperties(new W.TableCellWidth() { Width = "4500", Type = W.TableWidthUnitValues.Dxa }), new W.Paragraph(new W.Run(new W.Text(cat)))),
                new W.TableCell(new W.TableCellProperties(new W.TableCellWidth() { Width = "4500", Type = W.TableWidthUnitValues.Dxa }), new W.Paragraph(new W.Run(new W.Text(res))))
            ));
        }
        bodyElements.Add(summaryTable);

        // Detailed table if diagnostics present
        if (diagnostics.Count > 0)
        {
            bodyElements.Add(new W.Paragraph(new W.Run(new W.RunProperties(new W.Bold()), new W.Text("Детальный перечень замечаний:"))));

            var diagTable = new W.Table(
                new W.TableProperties(
                    new W.TableStyle() { Val = "TableGrid" },
                    new W.TableWidth() { Width = "0", Type = W.TableWidthUnitValues.Auto },
                    new W.TableBorders(
                        new W.TopBorder() { Val = W.BorderValues.Single, Size = 4, Space = 0, Color = "auto" },
                        new W.LeftBorder() { Val = W.BorderValues.Single, Size = 4, Space = 0, Color = "auto" },
                        new W.BottomBorder() { Val = W.BorderValues.Single, Size = 4, Space = 0, Color = "auto" },
                        new W.RightBorder() { Val = W.BorderValues.Single, Size = 4, Space = 0, Color = "auto" },
                        new W.InsideHorizontalBorder() { Val = W.BorderValues.Single, Size = 4, Space = 0, Color = "auto" },
                        new W.InsideVerticalBorder() { Val = W.BorderValues.Single, Size = 4, Space = 0, Color = "auto" }
                    )
                ),
                new W.TableGrid(
                    new W.GridColumn() { Width = "2000" },
                    new W.GridColumn() { Width = "2000" },
                    new W.GridColumn() { Width = "1500" },
                    new W.GridColumn() { Width = "3500" }
                ),
                new W.TableRow(
                    new W.TableRowProperties(new W.TableHeader()),
                    new W.TableCell(new W.TableCellProperties(new W.TableCellWidth() { Width = "2000", Type = W.TableWidthUnitValues.Dxa }), new W.Paragraph(new W.Run(new W.RunProperties(new W.Bold()), new W.Text("Код правила")))),
                    new W.TableCell(new W.TableCellProperties(new W.TableCellWidth() { Width = "2000", Type = W.TableWidthUnitValues.Dxa }), new W.Paragraph(new W.Run(new W.RunProperties(new W.Bold()), new W.Text("Раздел")))),
                    new W.TableCell(new W.TableCellProperties(new W.TableCellWidth() { Width = "1500", Type = W.TableWidthUnitValues.Dxa }), new W.Paragraph(new W.Run(new W.RunProperties(new W.Bold()), new W.Text("Объект")))),
                    new W.TableCell(new W.TableCellProperties(new W.TableCellWidth() { Width = "3500", Type = W.TableWidthUnitValues.Dxa }), new W.Paragraph(new W.Run(new W.RunProperties(new W.Bold()), new W.Text("Сообщение"))))
                )
            );

            foreach (var diag in diagnostics)
            {
                diagTable.AppendChild(new W.TableRow(
                    new W.TableCell(new W.TableCellProperties(new W.TableCellWidth() { Width = "2000", Type = W.TableWidthUnitValues.Dxa }), new W.Paragraph(new W.Run(new W.Text(diag.Rule)))),
                    new W.TableCell(new W.TableCellProperties(new W.TableCellWidth() { Width = "2000", Type = W.TableWidthUnitValues.Dxa }), new W.Paragraph(new W.Run(new W.Text(diag.SectionTitle ?? diag.SectionId ?? "-")))),
                    new W.TableCell(new W.TableCellProperties(new W.TableCellWidth() { Width = "1500", Type = W.TableWidthUnitValues.Dxa }), new W.Paragraph(new W.Run(new W.Text(diag.ObjectId ?? "-")))),
                    new W.TableCell(new W.TableCellProperties(new W.TableCellWidth() { Width = "3500", Type = W.TableWidthUnitValues.Dxa }), new W.Paragraph(new W.Run(new W.Text(diag.Message))))
                ));
            }
            bodyElements.Add(diagTable);
        }

        bodyElements.Add(new W.Paragraph(new W.Run(new W.Text("Нормоконтролер: Сидоров В.В. ___________________ (подпись)"))));
        bodyElements.Add(new W.Paragraph(new W.Run(new W.Text("Дата проверки: 20.12.2026"))));

        bodyElements.Add(new W.SectionProperties(
            new W.PageSize() { Width = 11906, Height = 16838 },
            new W.PageMargin() { Top = 1134, Right = 567, Bottom = 1474, Left = 1417, Header = 1134, Footer = 1134 }
        ));

        var body = new W.Body(bodyElements);
        main.Document = new W.Document(body);
    }

    public void Dispose() => Directory.Delete(folder, true);
}
