using ClosedXML.Excel;
using CoachFlow.Application.Exercises;
using CoachFlow.Application.Plans;
using CoachFlow.Domain;
using CoachFlow.Export.Xlsx;
using Microsoft.Extensions.Time.Testing;

namespace CoachFlow.Export.Tests;

public class XlsxPlanExporterTests
{
    private static readonly FocusArea K1 = new(Guid.NewGuid(), "K1", true);
    private static readonly FocusArea Own = new(Guid.NewGuid(), "Aufschlag-Serie", false);

    private static ExerciseContent Exercise(string title, int minutes, int min, int? max, params ExerciseOption[] options) =>
        new(title, $"Ablauf von {title}", minutes, min, max, [ExperienceLevel.Hobby], [], options);

    private static TrainingPlan Plan(params PlanItem[] items) => new(
        Guid.NewGuid(), Guid.NewGuid(), "Dienstag Hobby",
        new TrainingParameters(10, ExperienceLevel.Hobby, ExperienceSpread.Anpassungen, 90, [K1.Id, Own.Id]),
        items, new DateTimeOffset(2026, 11, 9, 17, 30, 0, TimeSpan.Zero));

    private static IXLWorksheet Export(TrainingPlan plan)
    {
        var exporter = new XlsxPlanExporter(new FakeTimeProvider(new DateTimeOffset(2026, 11, 10, 8, 0, 0, TimeSpan.Zero)));
        var stream = new MemoryStream();
        exporter.Export(plan, [K1, Own], stream);
        stream.Position = 0;
        return new XLWorkbook(stream).Worksheet("Plan");
    }

    private static IXLRow RowStartingWith(IXLWorksheet sheet, string text) =>
        sheet.RowsUsed().First(r => r.Cell(1).GetString() == text || r.Cell(3).GetString() == text);

    [Fact]
    public void Header_shows_title_and_training_data_in_german()
    {
        var sheet = Export(Plan());

        Assert.Equal("Dienstag Hobby", sheet.Cell(1, 1).GetString());
        Assert.Contains("Stand des Plans: 09.11.2026, 18:30 Uhr", sheet.Cell(2, 1).GetString()); // UTC 17:30 = Berlin 18:30
        Assert.Equal("10", RowStartingWith(sheet, "Teilnehmende").Cell(2).GetString());
        Assert.Equal("Hobby", RowStartingWith(sheet, "Niveau").Cell(2).GetString());
        Assert.Equal("2 – gemischt, Anpassungen nötig", RowStartingWith(sheet, "Streuung").Cell(2).GetString());
        Assert.Equal("K1, Aufschlag-Serie", RowStartingWith(sheet, "Schwerpunkte").Cell(2).GetString());
    }

    [Fact]
    public void Items_get_time_windows_options_notes_and_a_sum()
    {
        var sheet = Export(Plan(
            new PlanItem(null, Exercise("Einlaufen", 15, 1, null, new ExerciseOption(OptionKind.Anpassung, "Leichter", "Ball fangen")), 15, "Halle 2"),
            new PlanItem(null, Exercise("Annahme", 20, 4, 12, new ExerciseOption(OptionKind.Anpassung, "Kürzer", null)), 20, null)));

        var first = RowStartingWith(sheet, "Einlaufen");
        Assert.Equal("0–15", first.Cell(2).GetString());
        Assert.Equal("ab 1", first.Cell(5).GetString());
        Assert.Equal("Anpassung: Leichter – Ball fangen", first.Cell(7).GetString());
        Assert.Equal("Halle 2", first.Cell(8).GetString());

        var second = RowStartingWith(sheet, "Annahme");
        Assert.Equal("15–35", second.Cell(2).GetString());
        Assert.Equal("4–12", second.Cell(5).GetString());

        Assert.Equal(35, RowStartingWith(sheet, "Summe").Cell(4).GetValue<int>());
        Assert.Equal("35 von 90 Minuten", RowStartingWith(sheet, "Verplant").Cell(2).GetString());
    }

    [Fact]
    public void Findings_are_listed_so_the_printout_shows_open_points()
    {
        // Streuung 2 ohne Anpassung und Dauer überschritten.
        var sheet = Export(Plan(new PlanItem(null, Exercise("Spielform", 100, 2, null), 100, null)));

        var findings = sheet.RowsUsed().SkipWhile(r => r.Cell(1).GetString() != "Prüfhinweise").Skip(1).ToList();
        Assert.True(findings.Count >= 2);
        Assert.All(findings, r => Assert.Equal("Hinweis", r.Cell(1).GetString()));
    }

    [Fact]
    public void Plan_without_findings_has_no_findings_section()
    {
        var sheet = Export(Plan(new PlanItem(null,
            Exercise("Einlaufen", 15, 1, null, new ExerciseOption(OptionKind.Anpassung, "Leichter", "Ball fangen")), 15, null)));

        Assert.DoesNotContain(sheet.RowsUsed(), r => r.Cell(1).GetString() == "Prüfhinweise");
    }

    [Fact]
    public void Text_that_looks_like_a_formula_stays_text()
    {
        var formula = "=HYPERLINK(\"http://example.org\",\"Klick\")";
        var sheet = Export(Plan(new PlanItem(null, Exercise(formula, 10, 1, null), 10, "+1+1")));

        var row = RowStartingWith(sheet, formula);
        Assert.False(row.Cell(3).HasFormula);
        Assert.Equal(formula, row.Cell(3).GetString());
        Assert.False(row.Cell(8).HasFormula);
    }

    [Fact]
    public void Overlong_text_is_cut_to_the_excel_cell_limit()
    {
        var content = Exercise("Lang", 10, 1, null) with { Description = new string('x', 40_000) };
        var sheet = Export(Plan(new PlanItem(null, content, 10, null)));

        Assert.Equal(32_767, RowStartingWith(sheet, "Lang").Cell(6).GetString().Length);
    }

    [Fact]
    public void Printout_is_landscape_and_one_page_wide()
    {
        var sheet = Export(Plan());

        Assert.Equal(XLPageOrientation.Landscape, sheet.PageSetup.PageOrientation);
        Assert.Equal(1, sheet.PageSetup.PagesWide);
    }
}
