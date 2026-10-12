using Kadr.Platform.Application.Reports;

namespace Kadr.Platform.Application.Abstractions;

public interface IReportExportService
{
    byte[] ExportExcel(ReportTableModel model);

    byte[] ExportPdf(ReportTableModel model, bool rtlArabic);
}

public interface IHeadcountReportService
{
    Task<SharedKernel.Results.Result<IReadOnlyList<ReportHubItemDto>>> ListHubAsync(
        CancellationToken cancellationToken = default);

    Task<SharedKernel.Results.Result<HeadcountReportDto>> GetHeadcountAsync(
        HeadcountReportQuery query,
        CancellationToken cancellationToken = default);

    Task<SharedKernel.Results.Result<ReportExportFileDto>> ExportHeadcountAsync(
        HeadcountReportQuery query,
        string format,
        string? acceptLanguage,
        CancellationToken cancellationToken = default);
}
