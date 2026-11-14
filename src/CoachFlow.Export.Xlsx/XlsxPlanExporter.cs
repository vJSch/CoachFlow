using ClosedXML.Excel;
using CoachFlow.Application.Exercises;
using CoachFlow.Application.Plans;
using CoachFlow.Domain;

namespace CoachFlow.Export.Xlsx;

/// <summary>
/// Schreibt einen Plan als xlsx (K7), gedacht zum Ausdrucken: Querformat, eine Seite breit.
/// Texte der Nutzerinnen werden als Text gesetzt, nie als Formel.
/// </summary>
public sealed class XlsxPlanExporter(TimeProvider time) : IPlanExporter
{
    private const int MaxCellText = 32_767; // Grenze von Excel je Zelle
    private static readonly string[] Columns =
        ["Nr.", "Zeit (Min.)", "Übung", "Minuten", "Personen", "Beschreibung", "Anpassungen und Varianten", "Notiz"];

    public string ContentType => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public string FileExtension => ".xlsx";

    public void Export(TrainingPlan plan, IReadOnlyList<FocusArea> focusAreas, Stream output)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Plan");
        var p = plan.Parameters;

        sheet.Cell(1, 1).SetValue(Text(plan.Title));
        sheet.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);
        sheet.Cell(2, 1).SetValue($"Exportiert aus CoachFlow am {Local(time.GetUtcNow()):dd.MM.yyyy}, Stand des Plans: {Local(plan.UpdatedAt):dd.MM.yyyy, HH:mm} Uhr");
        sheet.Cell(2, 1).Style.Font.SetFontColor(XLColor.Gray);

        var focusNames = p.FocusAreaIds
            .Select(id => focusAreas.FirstOrDefault(f => f.Id == id)?.Name ?? "Unbekannter Schwerpunkt")
            .ToList();
        var row = 4;
        foreach (var (label, value) in new (string, string)[]
                 {
                     ("Teilnehmende", p.ParticipantCount.ToString()),
                     ("Niveau", Labels.Level(p.Level)),
                     ("Streuung", Labels.Spread(p.Spread)),
                     ("Trainingsdauer", $"{p.DurationMinutes} Minuten"),
                     ("Schwerpunkte", focusNames.Count == 0 ? "–" : string.Join(", ", focusNames)),
                     ("Verplant", $"{plan.TotalMinutes} von {p.DurationMinutes} Minuten")
                 })
        {
            sheet.Cell(row, 1).SetValue(label);
            sheet.Cell(row, 1).Style.Font.SetBold();
            sheet.Cell(row, 2).SetValue(Text(value));
            sheet.Range(row, 2, row, Columns.Length).Merge();
            row++;
        }

        row++;
        var headerRow = row;
        for (var c = 0; c < Columns.Length; c++) sheet.Cell(headerRow, c + 1).SetValue(Columns[c]);
        var header = sheet.Range(headerRow, 1, headerRow, Columns.Length);
        header.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#DCE6F1"));

        var start = 0;
        for (var i = 0; i < plan.Items.Count; i++)
        {
            var item = plan.Items[i];
            var exercise = item.Exercise;
            row++;
            sheet.Cell(row, 1).SetValue(i + 1);
            sheet.Cell(row, 2).SetValue($"{start}–{start + item.PlannedMinutes}");
            sheet.Cell(row, 3).SetValue(Text(exercise.Title ?? "Ohne Titel"));
            sheet.Cell(row, 4).SetValue(item.PlannedMinutes);
            sheet.Cell(row, 5).SetValue(Labels.Participants(exercise.MinParticipants, exercise.MaxParticipants));
            sheet.Cell(row, 6).SetValue(Text(exercise.Description ?? ""));
            sheet.Cell(row, 7).SetValue(Text(string.Join("\n", exercise.Options.Select(o =>
                $"{Labels.Option(o.Kind)}: {o.Title}{(string.IsNullOrWhiteSpace(o.Description) ? "" : " – " + o.Description)}"))));
            sheet.Cell(row, 8).SetValue(Text(item.Note ?? ""));
            start += item.PlannedMinutes;
        }

        row++;
        sheet.Cell(row, 3).SetValue("Summe");
        sheet.Cell(row, 4).SetValue(plan.TotalMinutes);
        sheet.Range(row, 1, row, Columns.Length).Style.Font.SetBold().Border.SetTopBorder(XLBorderStyleValues.Thin);
        var tableEnd = row;

        var findings = plan.Findings;
        if (findings.Count > 0)
        {
            row += 2;
            sheet.Cell(row, 1).SetValue("Prüfhinweise");
            sheet.Cell(row, 1).Style.Font.SetBold();
            foreach (var finding in findings)
            {
                row++;
                sheet.Cell(row, 1).SetValue(Labels.Severity(finding.Severity));
                sheet.Cell(row, 2).SetValue(Text(finding.Message));
                sheet.Range(row, 2, row, Columns.Length).Merge();
            }
        }

        Layout(sheet, headerRow, tableEnd);
        workbook.SaveAs(output);
    }

    private static void Layout(IXLWorksheet sheet, int headerRow, int tableEnd)
    {
        double[] widths = [5, 10, 28, 9, 10, 50, 40, 25];
        for (var c = 0; c < widths.Length; c++) sheet.Column(c + 1).Width = widths[c];

        var table = sheet.Range(headerRow, 1, tableEnd, Columns.Length);
        table.Style.Alignment.SetWrapText().Alignment.SetVertical(XLAlignmentVerticalValues.Top);
        table.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin).Border.SetInsideBorder(XLBorderStyleValues.Hair);

        sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        sheet.PageSetup.PaperSize = XLPaperSize.A4Paper;
        sheet.PageSetup.FitToPages(1, 0); // eine Seite breit, beliebig viele hoch
        sheet.PageSetup.SetRowsToRepeatAtTop(headerRow, headerRow);
    }

    private static string Text(string value) => value.Length <= MaxCellText ? value : value[..(MaxCellText - 1)] + "…";

    // Die Nutzerinnen sind deutsche Vereine; der Server läuft in UTC.
    private static readonly TimeZoneInfo Berlin = FindBerlin();

    private static DateTimeOffset Local(DateTimeOffset value) => TimeZoneInfo.ConvertTime(value, Berlin);

    private static TimeZoneInfo FindBerlin()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.Utc; }
    }
}
