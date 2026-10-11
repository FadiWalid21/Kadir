using Kadr.Platform.Application.Audit;
using Kadr.SharedKernel.Results;

namespace Kadr.Platform.Application.Abstractions;

public interface IAuditLogService
{
    Task<Result<AuditLogListResultDto>> ListAsync(
        AuditLogQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<byte[]>> ExportCsvAsync(
        AuditLogQuery query,
        CancellationToken cancellationToken = default);
}
