using AcademicDocx.Core;
using Xunit;

namespace AcademicDocx.Tests;

public sealed class TemplateIntegrityTests
{
    private static string GetRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "template.yml")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root with template.yml");
    }

    [Fact]
    public void AllStarterTemplateFilesExistInRepositoryRoot()
    {
        var repoRoot = GetRepoRoot();

        var requiredFiles = new[]
        {
            "template.yml",
            "_quarto.yml",
            "metadata.yml",
            "index.qmd",
            "references.qmd",
            "references.bib",
            ".quartoignore"
        };

        foreach (var file in requiredFiles)
        {
            var path = Path.Combine(repoRoot, file);
            Assert.True(File.Exists(path), $"Starter template file '{file}' is missing in repo root.");
        }
    }

    [Fact]
    public void TemplateYmlContainsRequiredFields()
    {
        var repoRoot = GetRepoRoot();
        var content = File.ReadAllText(Path.Combine(repoRoot, "template.yml"));

        Assert.Contains("title:", content);
        Assert.Contains("author:", content);
        Assert.Contains("version:", content);
        Assert.Contains("quarto-required:", content);
    }

    [Fact]
    public void QuartoYmlConfiguresAcademicFormatScriptCslAndBibliography()
    {
        var repoRoot = GetRepoRoot();
        var content = File.ReadAllText(Path.Combine(repoRoot, "_quarto.yml"));

        Assert.Contains("academic-docx:", content);
        Assert.Contains("finalize.R", content);
        Assert.Contains("csl:", content);
        Assert.Contains("bibliography:", content);
        Assert.Contains("references.bib", content);
        Assert.Contains("metadata-files:", content);
    }

    [Fact]
    public void MetadataYmlConfiguresNormativeSusuProfileAndPlaceholders()
    {
        var repoRoot = GetRepoRoot();
        var content = File.ReadAllText(Path.Combine(repoRoot, "metadata.yml"));

        Assert.Contains("profile: \"susu-coursework-v1\"", content);
        Assert.Contains("ministry:", content);
        Assert.Contains("university:", content);
        Assert.Contains("institute:", content);
        Assert.Contains("department:", content);
        Assert.Contains("work-type:", content);
        Assert.Contains("discipline:", content);
        Assert.Contains("group:", content);
        Assert.Contains("supervisor:", content);
        Assert.Contains("normocontroler:", content);
        Assert.Contains("summary:", content);
    }

    [Fact]
    public void QuartoIgnoreAndQmdFilesAreValid()
    {
        var repoRoot = GetRepoRoot();

        var quartoIgnore = File.ReadAllText(Path.Combine(repoRoot, ".quartoignore"));
        Assert.Contains("tools/", quartoIgnore);
        Assert.Contains("docs/", quartoIgnore);

        var indexQmd = File.ReadAllText(Path.Combine(repoRoot, "index.qmd"));
        Assert.Contains("# Введение", indexQmd);
        Assert.Contains("# Заключение", indexQmd);

        var referencesQmd = File.ReadAllText(Path.Combine(repoRoot, "references.qmd"));
        Assert.Contains("# Список литературы", referencesQmd);
        Assert.Contains("::: {#refs}", referencesQmd);
    }
}
