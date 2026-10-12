using System.Reflection;
using ClosedXML.Excel;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Reports;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Kadr.Infrastructure.Persistence.Reports;

internal sealed class ReportExportService : IReportExportService
{
    private static readonly object FontLock = new();
    private static bool _fontsRegistered;

    static ReportExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] ExportExcel(ReportTableModel model)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(SanitizeSheetName(model.Title));
        var col = 1;
        foreach (var header in model.Headers)
        {
            sheet.Cell(1, col).Value = header;
            sheet.Cell(1, col).Style.Font.Bold = true;
            col++;
        }

        var rowIndex = 2;
        foreach (var row in model.Rows)
        {
            for (var c = 0; c < row.Count; c++)
            {
                sheet.Cell(rowIndex, c + 1).Value = row[c];
            }

            rowIndex++;
        }

        sheet.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] ExportPdf(ReportTableModel model, bool rtlArabic)
    {
        EnsureFonts();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontFamily("Tajawal").FontSize(9));
                page.ContentFromRightToLeft();
                if (!rtlArabic)
                {
                    page.ContentFromLeftToRight();
                }

                page.Header().Column(col =>
                {
                    col.Item().Text(model.Title).FontSize(14).Bold();
                    if (model.Meta is not null)
                    {
                        foreach (var (k, v) in model.Meta)
                        {
                            col.Item().Text($"{k}: {v}").FontSize(8).FontColor(Colors.Grey.Darken2);
                        }
                    }
                });

                page.Content().PaddingTop(12).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        foreach (var _ in model.Headers)
                        {
                            columns.RelativeColumn();
                        }
                    });

                    table.Header(header =>
                    {
                        foreach (var h in model.Headers)
                        {
                            header.Cell().Element(CellHeader).Text(h).Bold();
                        }
                    });

                    foreach (var row in model.Rows)
                    {
                        foreach (var cell in row)
                        {
                            table.Cell().Element(CellBody).Text(cell ?? string.Empty);
                        }
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span(model.ReportKey + " · ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    private static IContainer CellHeader(IContainer container) =>
        container.BorderBottom(1).BorderColor(Colors.Grey.Medium).Padding(4).Background(Colors.Grey.Lighten3);

    private static IContainer CellBody(IContainer container) =>
        container.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4);

    private static void EnsureFonts()
    {
        if (_fontsRegistered)
        {
            return;
        }

        lock (FontLock)
        {
            if (_fontsRegistered)
            {
                return;
            }

            var asm = Assembly.GetExecutingAssembly();
            var names = asm.GetManifestResourceNames()
                .Where(n => n.Contains("Tajawal", StringComparison.OrdinalIgnoreCase) && n.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var name in names)
            {
                using var stream = asm.GetManifestResourceStream(name);
                if (stream is null)
                {
                    continue;
                }

                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                ms.Position = 0;
                FontManager.RegisterFontWithCustomName("Tajawal", ms);
            }

            _fontsRegistered = names.Count > 0;
        }
    }

    private static string SanitizeSheetName(string title)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(title.Select(ch => invalid.Contains(ch) || ch is '[' or ']' or '*' or '?' or '/' or '\\' ? '_' : ch).ToArray());
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            cleaned = "Report";
        }

        return cleaned.Length <= 31 ? cleaned : cleaned[..31];
    }
}
