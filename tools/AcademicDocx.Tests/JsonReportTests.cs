using AcademicDocx.Core;
using Xunit;

namespace AcademicDocx.Tests;

public sealed class JsonReportTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "academic-json-tests-" + Guid.NewGuid().ToString("N"));

    public JsonReportTests() => Directory.CreateDirectory(folder);

    private sealed record SampleReport(
        string ReportTitle,
        int SectionCount,
        int MathCount,
        IReadOnlyList<string> ChapterTitles,
        DiagnosticItem PrimaryDiagnostic
    );

    [Fact]
    public void JsonReportSerializePreservesCyrillicAndUsesSnakeCase()
    {
        var sample = new SampleReport(
            ReportTitle: "Отчет по нормоконтролю",
            SectionCount: 5,
            MathCount: 12,
            ChapterTitles: new[] { "Введение", "Методология", "Заключение" },
            PrimaryDiagnostic: new DiagnosticItem(
                Rule: "TBL_REF_MISSING",
                Severity: "Warning",
                File: "index.qmd",
                SectionId: "sec-tables",
                SectionTitle: "Таблицы результатов",
                ObjectId: "tbl-1",
                Location: "объект #tbl-1",
                Message: "Перед таблицей #tbl-1 отсутствует ссылка.",
                Recommendation: "Добавьте ссылку @tbl-1."
            )
        );

        var json = JsonReport.Serialize(sample);

        // Verify snake_case property keys
        Assert.Contains("\"report_title\":", json);
        Assert.Contains("\"section_count\":", json);
        Assert.Contains("\"math_count\":", json);
        Assert.Contains("\"chapter_titles\":", json);
        Assert.Contains("\"primary_diagnostic\":", json);
        Assert.Contains("\"section_title\":", json);
        Assert.Contains("\"object_id\":", json);

        // Verify unescaped Cyrillic characters (UnicodeRanges.All)
        Assert.Contains("Отчет по нормоконтролю", json);
        Assert.Contains("Введение", json);
        Assert.Contains("Методология", json);
        Assert.Contains("Заключение", json);
        Assert.Contains("Таблицы результатов", json);
        Assert.Contains("объект #tbl-1", json);

        // Ensure unicode escape sequences are not used for Cyrillic letters
        Assert.DoesNotContain("\\u041E", json);
        Assert.DoesNotContain("\\u0432", json);
    }

    [Fact]
    public void JsonReportWriteCreatesFileWithUnescapedJson()
    {
        var path = Path.Combine(folder, "nested", "report.json");

        var sample = new DiagnosticReport(new List<DiagnosticItem>
        {
            new(
                Rule: "FIG_REF_MISSING",
                Severity: "Warning",
                File: "chapter1.qmd",
                SectionId: "sec-fig",
                SectionTitle: "Иллюстрации",
                ObjectId: "fig-1",
                Location: "объект #fig-1",
                Message: "Перед рисунком #fig-1 отсутствует ссылка.",
                Recommendation: "Добавьте ссылку @fig-1."
            )
        });

        JsonReport.Write(path, sample);

        Assert.True(File.Exists(path));
        var content = File.ReadAllText(path);

        Assert.Contains("\"diagnostics\":", content);
        Assert.Contains("\"section_title\":", content);
        Assert.Contains("Иллюстрации", content);
        Assert.Contains("объект #fig-1", content);
        Assert.DoesNotContain("\\u04", content);
    }

    public void Dispose() => Directory.Delete(folder, true);
}
