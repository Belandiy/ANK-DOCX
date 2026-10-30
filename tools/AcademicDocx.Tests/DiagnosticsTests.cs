using System.Text.Json;
using AcademicDocx.Core;
using Xunit;

namespace AcademicDocx.Tests;

public sealed class DiagnosticsTests
{
    [Fact]
    public void DiagnosticItemAndReportSerializationWorkCorrectly()
    {
        var diagnostics = new List<DiagnosticItem>
        {
            new(
                Rule: "SEC_INTRO_MISSING",
                Severity: "Warning",
                File: "academic/chapter7.qmd",
                SectionId: "sec-methodology",
                SectionTitle: "Методика исследований",
                ObjectId: null,
                Location: "после раздела 7, перед 7.1",
                Message: "Раздел 7 не содержит вводного текста перед подразделом 7.1.",
                Recommendation: "Добавьте краткий вводный абзац о содержании раздела."
            ),
            new(
                Rule: "FIG_REF_MISSING",
                Severity: "Warning",
                File: "academic/model.qmd",
                SectionId: null,
                SectionTitle: null,
                ObjectId: "fig-graph1",
                Location: "объект #fig-graph1",
                Message: "Перед рисунком #fig-graph1 в предшествующем абзаце отсутствует ссылка @fig-graph1.",
                Recommendation: "Добавьте во вступительный абзац перед рисунком текстовую ссылку на рисунок (@fig-graph1)."
            ),
            new(
                Rule: "TBL_REF_MISSING",
                Severity: "Warning",
                File: "index.qmd",
                SectionId: "sec-tables",
                SectionTitle: "Таблицы",
                ObjectId: "tbl-results",
                Location: "объект #tbl-results",
                Message: "Перед таблицей #tbl-results в предшествующем абзаце отсутствует ссылка @tbl-results.",
                Recommendation: "Добавьте во вступительный абзац перед таблицей текстовую ссылку на таблицу (@tbl-results)."
            ),
            new(
                Rule: "LST_REF_MISSING",
                Severity: "Warning",
                File: "index.qmd",
                SectionId: null,
                SectionTitle: null,
                ObjectId: "lst-code1",
                Location: "объект #lst-code1",
                Message: "Перед листингом #lst-code1 в предшествующем абзаце отсутствует ссылка @lst-code1.",
                Recommendation: "Добавьте во вступительный абзац перед листингом текстовую ссылку на листинг (@lst-code1)."
            )
        };

        var report = new DiagnosticReport(diagnostics);
        var json = JsonReport.Serialize(report);

        Assert.Contains("SEC_INTRO_MISSING", json);
        Assert.Contains("FIG_REF_MISSING", json);
        Assert.Contains("TBL_REF_MISSING", json);
        Assert.Contains("LST_REF_MISSING", json);
        Assert.Contains("section_title", json);

        var deserialized = JsonSerializer.Deserialize<DiagnosticReport>(json, JsonReport.Options);
        Assert.NotNull(deserialized);
        Assert.Equal(4, deserialized.Diagnostics.Count);
        Assert.Equal("SEC_INTRO_MISSING", deserialized.Diagnostics[0].Rule);
        Assert.Equal("FIG_REF_MISSING", deserialized.Diagnostics[1].Rule);
        Assert.Equal("TBL_REF_MISSING", deserialized.Diagnostics[2].Rule);
        Assert.Equal("LST_REF_MISSING", deserialized.Diagnostics[3].Rule);
    }

    [Fact]
    public void FormatConsoleSummaryOutputsFormattedTextForAllSeveritiesAndLocationCombinations()
    {
        var diagnostics = new List<DiagnosticItem>
        {
            new(
                Rule: "CRITICAL_ERROR",
                Severity: "Error",
                File: "index.qmd",
                SectionId: "sec-main",
                SectionTitle: "Введение",
                ObjectId: null,
                Location: "раздел #sec-main",
                Message: "Критическая ошибка разметки.",
                Recommendation: "Исправьте синтаксис."
            ),
            new(
                Rule: "TBL_REF_MISSING",
                Severity: "Warning",
                File: "index.qmd",
                SectionId: "sec-tables",
                SectionTitle: null,
                ObjectId: "tbl-data",
                Location: "объект #tbl-data",
                Message: "Отсутствует ссылка на таблицу.",
                Recommendation: "Добавьте ссылку на таблицу."
            ),
            new(
                Rule: "LST_REF_MISSING",
                Severity: "Warning",
                File: "index.qmd",
                SectionId: null,
                SectionTitle: null,
                ObjectId: "lst-main",
                Location: "объект #lst-main",
                Message: "Отсутствует ссылка на листинг.",
                Recommendation: "Добавьте ссылку на листинг."
            ),
            new(
                Rule: "INFO_NOTE",
                Severity: "Info",
                File: null,
                SectionId: null,
                SectionTitle: null,
                ObjectId: null,
                Location: "абзац 5",
                Message: "Информационная заметка.",
                Recommendation: "Замечаний нет."
            ),
            new(
                Rule: "CUSTOM_SEVERITY_NOTE",
                Severity: "CustomSeverity",
                File: null,
                SectionId: null,
                SectionTitle: null,
                ObjectId: null,
                Location: null,
                Message: "Пользовательское сообщение.",
                Recommendation: "Действие."
            )
        };

        var report = new DiagnosticReport(diagnostics);
        var summary = report.FormatConsoleSummary();

        Assert.Contains("=== Сводка диагностики ANK ===", summary);
        Assert.Contains("[Ошибка] CRITICAL_ERROR", summary);
        Assert.Contains("[Предупреждение] TBL_REF_MISSING", summary);
        Assert.Contains("[Предупреждение] LST_REF_MISSING", summary);
        Assert.Contains("[Информация] INFO_NOTE", summary);
        Assert.Contains("[CustomSeverity] CUSTOM_SEVERITY_NOTE", summary);

        // Location parts checks
        Assert.Contains("Источник: index.qmd, заголовок «Введение» (#sec-main)", summary);
        Assert.Contains("Источник: index.qmd, раздел #sec-tables, объект #tbl-data", summary);
        Assert.Contains("Источник: index.qmd, объект #lst-main", summary);
        Assert.Contains("Источник: абзац 5", summary);
    }

    [Fact]
    public void FormatConsoleSummaryEmptyReturnsSuccessInfo()
    {
        var report = new DiagnosticReport(new List<DiagnosticItem>());
        var summary = report.FormatConsoleSummary();

        Assert.Contains("=== Сводка диагностики ANK ===", summary);
        Assert.Contains("[Информация] BINDING_SUCCESS", summary);
    }

    [Fact]
    public void QuartoAcademicDiagnosticsJsonFileSerializationAndDeserialization()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "academic-diag-file-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);
        var jsonFilePath = Path.Combine(tempFolder, ".quarto", "academic-diagnostics.json");

        try
        {
            var originalItems = new List<DiagnosticItem>
            {
                new(
                    Rule: "TBL_REF_MISSING",
                    Severity: "Warning",
                    File: "index.qmd",
                    SectionId: "sec-tables",
                    SectionTitle: "Таблицы результатов",
                    ObjectId: "tbl-1",
                    Location: "объект #tbl-1",
                    Message: "Перед таблицей #tbl-1 в предшествующем абзаце отсутствует ссылка @tbl-1.",
                    Recommendation: "Добавьте во вступительный абзац перед таблицей текстовую ссылку на таблицу (@tbl-1)."
                ),
                new(
                    Rule: "LST_REF_MISSING",
                    Severity: "Warning",
                    File: "index.qmd",
                    SectionId: "sec-code",
                    SectionTitle: "Программный код",
                    ObjectId: "lst-1",
                    Location: "объект #lst-1",
                    Message: "Перед листингом #lst-1 в предшествующем абзаце отсутствует ссылка @lst-1.",
                    Recommendation: "Добавьте во вступительный абзац перед листингом текстовую ссылку на листинг (@lst-1)."
                )
            };

            var originalReport = new DiagnosticReport(originalItems);
            JsonReport.Write(jsonFilePath, originalReport);

            Assert.True(File.Exists(jsonFilePath));
            var fileContent = File.ReadAllText(jsonFilePath);

            Assert.Contains("TBL_REF_MISSING", fileContent);
            Assert.Contains("LST_REF_MISSING", fileContent);
            Assert.Contains("section_title", fileContent);
            Assert.Contains("Таблицы результатов", fileContent);

            var readReport = JsonSerializer.Deserialize<DiagnosticReport>(fileContent, JsonReport.Options);
            Assert.NotNull(readReport);
            Assert.Equal(2, readReport.Diagnostics.Count);
            Assert.Equal("TBL_REF_MISSING", readReport.Diagnostics[0].Rule);
            Assert.Equal("LST_REF_MISSING", readReport.Diagnostics[1].Rule);
        }
        finally
        {
            Directory.Delete(tempFolder, true);
        }
    }
}
