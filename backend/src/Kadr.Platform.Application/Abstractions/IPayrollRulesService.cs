using Kadr.Platform.Application.Payroll;
using Kadr.SharedKernel.Results;

namespace Kadr.Platform.Application.Abstractions;

public interface IPayrollRulesService
{
    Task<Result<PayrollRulesListDto>> ListCurrentAsync(CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<PayrollRuleHistoryItemDto>>> GetHistoryAsync(
        string ruleKey,
        CancellationToken cancellationToken = default);

    Task<Result<PayrollRuleListItemDto>> UpsertAsync(
        string ruleKey,
        UpsertPayrollRuleRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<PayrollRuleListItemDto>> ResetAsync(
        string ruleKey,
        string reason,
        DateOnly effectiveFrom,
        CancellationToken cancellationToken = default);

    Task<Result<ConfirmPayrollRulesDto>> ConfirmForLockAsync(
        ConfirmPayrollRulesRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> AreRulesConfirmedForLockAsync(CancellationToken cancellationToken = default);
}
