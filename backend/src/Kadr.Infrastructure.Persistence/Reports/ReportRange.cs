using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;

namespace Kadr.Infrastructure.Persistence.Reports;

internal static class ReportRange
{
    public const int MaxMonths = 12;
    public const int MaxExportRows = 50_000;
    public const int MaxPageSize = 100;

    public static Result ValidatePeriod(DateOnly from, DateOnly to)
    {
        if (to < from)
        {
            return Result.Failure(ErrorCodes.ReportsValidationFailed, ErrorCodes.ReportsValidationFailed);
        }

        if (to > from.AddMonths(MaxMonths))
        {
            return Result.Failure(ErrorCodes.ReportsRangeTooLarge, ErrorCodes.ReportsRangeTooLarge);
        }

        return Result.Success();
    }

    public static int ClampPage(int page) => page < 1 ? 1 : page;

    public static int ClampPageSize(int pageSize) =>
        pageSize < 1 ? 20 : Math.Min(pageSize, MaxPageSize);
}
