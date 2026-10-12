using Kadr.Platform.Application.Payroll;
using Kadr.SharedKernel.Results;

namespace Kadr.Platform.Application.Abstractions;

public interface ISalaryImportService
{
    Task<Result<byte[]>> GetTemplateAsync(CancellationToken cancellationToken = default);

    Task<Result<SalaryImportPreviewResultDto>> PreviewAsync(
        Stream content,
        string fileName,
        CancellationToken cancellationToken = default);

    Task<Result<SalaryImportCommitResultDto>> CommitAsync(
        IReadOnlyList<SalaryImportCommitRowDto> rows,
        CancellationToken cancellationToken = default);
}
