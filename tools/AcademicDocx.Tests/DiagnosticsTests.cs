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
            )
        };

        var report = new DiagnosticReport(diagnostics);
        var json = JsonReport.Serialize(report);

        Assert.Contains("SEC_INTRO_MISSING", json);
        Assert.Contains("FIG_REF_MISSING", json);
        Assert.Contains("section_title", json);

        var deserialized = JsonSerializer.Deserialize<DiagnosticReport>(json, JsonReport.Options);
        Assert.NotNull(deserialized);
        Assert.Equal(2, deserialized.Diagnostics.Count);
        Assert.Equal("SEC_INTRO_MISSING", deserialized.Diagnostics[0].Rule);
        Assert.Equal("FIG_REF_MISSING", deserialized.Diagnostics[1].Rule);
    }

    [Fact]
    public void FormatConsoleSummaryOutputsFormattedText()
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
                Location: "между разделом «Методика исследований» и подразделом «7.1»",
                Message: "Раздел 7 не содержит вводного текста перед подразделом 7.1.",
                Recommendation: "Добавьте краткий вводный абзац о содержании раздела перед подразделом 7.1."
            )
        };

        var report = new DiagnosticReport(diagnostics);
        var summary = report.FormatConsoleSummary();

        Assert.Contains("=== Сводка диагностики ANK ===", summary);
        Assert.Contains("[Предупреждение] SEC_INTRO_MISSING", summary);
        Assert.Contains("Источник: academic/chapter7.qmd, заголовок «Методика исследований» (#sec-methodology)", summary);
        Assert.Contains("Проблема: Раздел 7 не содержит вводного текста перед подразделом 7.1.", summary);
        Assert.Contains("Рекомендация: Добавьте краткий вводный абзац о содержании раздела перед подразделом 7.1.", summary);
    }

    [Fact]
    public void FormatConsoleSummaryEmptyReturnsSuccessInfo()
    {
        var report = new DiagnosticReport(new List<DiagnosticItem>());
        var summary = report.FormatConsoleSummary();

        Assert.Contains("=== Сводка диагностики ANK ===", summary);
        Assert.Contains("[Информация] BINDING_SUCCESS", summary);
    }
}
