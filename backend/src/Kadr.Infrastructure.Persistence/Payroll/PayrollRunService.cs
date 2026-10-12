using System.Text.Json;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Payroll;
using Kadr.Platform.Application.Payroll.Calculation;
using Kadr.Platform.Domain.Attendance;
using Kadr.Platform.Domain.Employees;
using Kadr.Platform.Domain.Leave;
using Kadr.Platform.Domain.Payroll;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Tenancy;
using Kadr.SharedKernel.Text;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Payroll;

public sealed class PayrollRunService(
    KadrDbContext db,
    ITenantContext tenantContext,
    IAuditLogWriter audit,
    IPayrollCalculationEngine engine,
    IPayrollRulesService rulesService) : IPayrollRunService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<Result<PayrollRunDetailDto>> CreateAsync(
        CreatePayrollRunRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetTenantUser(out var tenantId, out var userId, out var failure))
        {
            return failure!;
        }

        if (request.PeriodEnd < request.PeriodStart)
        {
            return FailDetail(ErrorCodes.PayrollRunValidationFailed, "Invalid period.");
        }

        var legalEntityId = await GetDefaultLegalEntityIdAsync(cancellationToken).ConfigureAwait(false);
        if (legalEntityId is null)
        {
            return FailDetail(ErrorCodes.PayrollRunValidationFailed, "Default legal entity is required.");
        }

        var run = PayrollRun.CreateDraft(
            tenantId,
            legalEntityId.Value,
            request.PeriodStart,
            request.PeriodEnd,
            userId);

        db.PayrollRuns.Add(run);
        audit.Stage(
            tenantId,
            userId,
            "payroll.run.create",
            nameof(PayrollRun),
            run.Id,
            null,
            JsonSerializer.Serialize(new { run.PeriodStart, run.PeriodEnd, Status = run.Status.ToString() }, JsonOptions));

        return await SaveAndMapAsync(run, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<PagedPayrollRunsDto>> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.PayrollRuns.AsNoTracking();
        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query
            .OrderByDescending(x => x.PeriodEnd)
            .ThenByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new PayrollRunListItemDto(
                x.Id,
                x.LegalEntityId,
                x.PeriodStart,
                x.PeriodEnd,
                x.Status.ToString(),
                x.WarningCount,
                x.Headcount,
                x.TotalGross,
                x.TotalNet,
                x.TotalEmployerCost,
                x.CreatedBy,
                x.ApprovedBy,
                x.LockedAt,
                x.ReopenedFromRunId,
                x.Version,
                x.CreatedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new PagedPayrollRunsDto(items, page, pageSize, total));
    }

    public async Task<Result<PayrollRunDetailDto>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dto = await MapDetailAsync(id, cancellationToken).ConfigureAwait(false);
        return dto is null
            ? FailDetail(ErrorCodes.PayrollRunNotFound, ErrorCodes.PayrollRunNotFound)
            : Result.Success(dto);
    }

    public async Task<Result<PayrollRunDetailDto>> CalculateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryGetTenantUser(out var tenantId, out var userId, out var failure))
        {
            return failure!;
        }

        var run = await db.PayrollRuns.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (run is null)
        {
            return FailDetail(ErrorCodes.PayrollRunNotFound, ErrorCodes.PayrollRunNotFound);
        }

        if (run.IsImmutable)
        {
            return FailDetail(ErrorCodes.PayrollRunImmutable, ErrorCodes.PayrollRunImmutable);
        }

        if (run.Status != PayrollRunStatus.Draft)
        {
            return FailDetail(ErrorCodes.PayrollRunInvalidTransition, ErrorCodes.PayrollRunInvalidTransition);
        }

        var priorHolds = await db.PayrollEntries.AsNoTracking()
            .Where(e => e.RunId == id && e.IsHeld)
            .Select(e => new { e.EmployeeId, e.HoldReason })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var holdByEmployee = priorHolds.ToDictionary(x => x.EmployeeId, x => x.HoldReason);

        try
        {
            run.MarkCalculating();
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            return FailDetail(ErrorCodes.PayrollRunConflict, ErrorCodes.PayrollRunConflict);
        }

        try
        {
            var snapshotValues = await LoadRulesSnapshotValuesAsync(
                    run.LegalEntityId,
                    run.PeriodStart,
                    cancellationToken)
                .ConfigureAwait(false);
            var rulesSnapshotJson = JsonSerializer.Serialize(snapshotValues, JsonOptions);
            var rules = RulesSnapshot.From(snapshotValues);

            var employees = await db.Employees.AsNoTracking()
                .Where(e =>
                    e.LegalEntityId == run.LegalEntityId
                    && e.HireDate <= run.PeriodEnd
                    && (e.Status == EmployeeStatus.Active
                        || e.Status == EmployeeStatus.Probation
                        || e.Status == EmployeeStatus.OnLeave))
                .Select(e => new { e.Id, e.HireDate })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var employeeIds = employees.Select(e => e.Id).ToList();

            var salaries = await db.EmployeeSalaries.AsNoTracking()
                .Where(s =>
                    employeeIds.Contains(s.EmployeeId)
                    && s.EffectiveFrom <= run.PeriodEnd
                    && (s.EffectiveTo == null || s.EffectiveTo >= run.PeriodStart))
                .Select(s => new
                {
                    s.Id,
                    s.EmployeeId,
                    s.EffectiveFrom,
                    s.EffectiveTo,
                    s.BasicAmount,
                    Components = s.Components.Select(c => new
                    {
                        c.PayComponentId,
                        c.AmountOrRate,
                    }).ToList(),
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var payComponentIds = salaries
                .SelectMany(s => s.Components.Select(c => c.PayComponentId))
                .Distinct()
                .ToList();
            var payComponents = await db.PayComponents.AsNoTracking()
                .Where(p => payComponentIds.Contains(p.Id))
                .Select(p => new
                {
                    p.Id,
                    p.Key,
                    p.NameAr,
                    p.NameEn,
                    p.Kind,
                    p.IsTaxable,
                    p.IsInsurable,
                })
                .ToDictionaryAsync(p => p.Id, cancellationToken)
                .ConfigureAwait(false);

            var insurance = await db.EmployeeInsuranceProfiles.AsNoTracking()
                .Where(i => employeeIds.Contains(i.EmployeeId))
                .Select(i => new { i.EmployeeId, i.IsInsured, i.InsurableWageOverride })
                .ToDictionaryAsync(i => i.EmployeeId, cancellationToken)
                .ConfigureAwait(false);

            var daySummaries = await db.AttendanceDaySummaries.AsNoTracking()
                .Where(d =>
                    employeeIds.Contains(d.EmployeeId)
                    && d.WorkDate >= run.PeriodStart
                    && d.WorkDate <= run.PeriodEnd)
                .Select(d => new
                {
                    d.EmployeeId,
                    d.WorkDate,
                    d.Status,
                    d.LateMinutes,
                    d.OvertimeMinutes,
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var unpaidLeaves = await (
                    from lr in db.LeaveRequests.AsNoTracking()
                    join lt in db.LeaveTypes.AsNoTracking() on lr.LeaveTypeId equals lt.Id
                    where employeeIds.Contains(lr.EmployeeId)
                          && lr.Status == LeaveRequestStatus.Approved
                          && !lt.Paid
                          && lr.StartDate <= run.PeriodEnd
                          && lr.EndDate >= run.PeriodStart
                    select new
                    {
                        lr.EmployeeId,
                        lr.StartDate,
                        lr.EndDate,
                        lr.Days,
                        lr.IsHalfDay,
                    })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var existingEntryIds = await db.PayrollEntries
                .Where(e => e.RunId == id)
                .Select(e => e.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            if (existingEntryIds.Count > 0)
            {
                await db.PayrollEntryLines
                    .Where(l => existingEntryIds.Contains(l.EntryId))
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);
                await db.PayrollEntries.Where(e => e.RunId == id)
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            var newEntries = new List<PayrollEntry>();
            var warningTotal = 0;
            decimal totalGross = 0;
            decimal totalNet = 0;
            decimal totalEmployer = 0;

            foreach (var emp in employees)
            {
                var empSalaries = salaries.Where(s => s.EmployeeId == emp.Id).ToList();
                var segments = empSalaries
                    .OrderBy(s => s.EffectiveFrom)
                    .Select(s =>
                    {
                        var comps = s.Components
                            .Select(c =>
                            {
                                if (!payComponents.TryGetValue(c.PayComponentId, out var pc))
                                {
                                    return null;
                                }

                                return new SalaryComponentFact(
                                    pc.Key,
                                    pc.NameAr,
                                    pc.NameEn,
                                    c.AmountOrRate,
                                    pc.IsTaxable,
                                    pc.IsInsurable,
                                    MapKind(pc.Kind));
                            })
                            .Where(x => x is not null)
                            .Select(x => x!)
                            .ToList();

                        return new SalarySegment(
                            s.EffectiveFrom,
                            s.EffectiveTo,
                            s.BasicAmount,
                            comps);
                    })
                    .ToList();

                insurance.TryGetValue(emp.Id, out var ins);
                var insuranceFacts = new InsuranceFacts(
                    ins?.IsInsured ?? false,
                    ins?.InsurableWageOverride);

                var empDays = daySummaries.Where(d => d.EmployeeId == emp.Id).ToList();
                var absenceDays = empDays.Count(d => d.Status == AttendanceDayStatus.Absent);
                var lateMinutes = empDays.Sum(d => d.LateMinutes);
                var otDay = empDays
                    .Where(d => d.Status is not AttendanceDayStatus.Holiday)
                    .Sum(d => d.OvertimeMinutes) / 60m;
                var otHoliday = empDays
                    .Where(d => d.Status == AttendanceDayStatus.Holiday)
                    .Sum(d => d.OvertimeMinutes) / 60m;

                var unpaidDays = unpaidLeaves
                    .Where(l => l.EmployeeId == emp.Id)
                    .Sum(l => CountOverlapDays(l.StartDate, l.EndDate, run.PeriodStart, run.PeriodEnd, l.IsHalfDay, l.Days));

                var inputs = new PeriodAttendanceInputs(
                    absenceDays,
                    (int)Math.Round(unpaidDays, MidpointRounding.AwayFromZero),
                    lateMinutes,
                    otDay,
                    OvertimeHoursNight: 0m,
                    otHoliday);

                var calcInput = new PayrollCalcInput(
                    emp.Id,
                    run.PeriodStart,
                    run.PeriodEnd,
                    segments,
                    insuranceFacts,
                    inputs);

                var result = engine.Calculate(calcInput, rules);
                warningTotal += result.Warnings.Count;
                totalGross += result.Gross;
                totalNet += result.Net;
                totalEmployer += result.EmployerCostTotal;

                var salarySnapshot = JsonSerializer.Serialize(
                    new
                    {
                        segments = segments.Select(s => new
                        {
                            s.EffectiveFrom,
                            s.EffectiveTo,
                            s.BasicAmount,
                            components = s.Components,
                        }),
                    },
                    JsonOptions);
                var inputsJson = JsonSerializer.Serialize(inputs, JsonOptions);
                var warningsJson = result.Warnings.Count == 0
                    ? null
                    : JsonSerializer.Serialize(result.Warnings, JsonOptions);

                var lineDrafts = result.Lines
                    .Select(l => new PayrollEntryLineDraft(
                        l.LineType,
                        l.ComponentKey,
                        l.LabelAr,
                        l.LabelEn,
                        l.Amount,
                        l.Explanation is null
                            ? null
                            : JsonSerializer.Serialize(new { detail = l.Explanation }, JsonOptions)))
                    .ToList();

                var entry = PayrollEntry.Create(
                    tenantId,
                    run.Id,
                    emp.Id,
                    salarySnapshot,
                    inputsJson,
                    result.Gross,
                    result.InsurableWage,
                    result.EmployeeInsurance,
                    result.EmployerInsurance,
                    result.TaxableIncome,
                    result.IncomeTax,
                    result.OtherDeductions,
                    result.Net,
                    result.EmployerCostTotal,
                    result.Warnings.Count,
                    warningsJson,
                    lineDrafts);

                if (holdByEmployee.TryGetValue(emp.Id, out var holdReason))
                {
                    entry.Hold(holdReason);
                }

                newEntries.Add(entry);
            }

            db.PayrollEntries.AddRange(newEntries);
            run.CompleteCalculation(
                rulesSnapshotJson,
                warningTotal,
                newEntries.Count,
                totalGross,
                totalNet,
                totalEmployer);

            audit.Stage(
                tenantId,
                userId,
                "payroll.run.calculate",
                nameof(PayrollRun),
                run.Id,
                null,
                JsonSerializer.Serialize(new
                {
                    run.Headcount,
                    run.WarningCount,
                    run.TotalGross,
                    run.TotalNet,
                }, JsonOptions));

            return await SaveAndMapAsync(run, cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            return FailDetail(ErrorCodes.PayrollRunConflict, ErrorCodes.PayrollRunConflict);
        }
        catch (Exception)
        {
            run.FailCalculationBackToDraft();
            try
            {
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Best-effort restore; original error surfaces below.
            }

            throw;
        }
    }

    public async Task<Result<PayrollRunDetailDto>> SubmitAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryGetTenantUser(out var tenantId, out var userId, out var failure))
        {
            return failure!;
        }

        var run = await db.PayrollRuns.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (run is null)
        {
            return FailDetail(ErrorCodes.PayrollRunNotFound, ErrorCodes.PayrollRunNotFound);
        }

        if (run.IsImmutable)
        {
            return FailDetail(ErrorCodes.PayrollRunImmutable, ErrorCodes.PayrollRunImmutable);
        }

        if (run.Status != PayrollRunStatus.Draft)
        {
            return FailDetail(ErrorCodes.PayrollRunInvalidTransition, ErrorCodes.PayrollRunInvalidTransition);
        }

        run.Submit();
        audit.Stage(
            tenantId,
            userId,
            "payroll.run.submit",
            nameof(PayrollRun),
            run.Id,
            JsonSerializer.Serialize(new { from = nameof(PayrollRunStatus.Draft) }, JsonOptions),
            JsonSerializer.Serialize(new { to = nameof(PayrollRunStatus.InReview) }, JsonOptions));
        return await SaveAndMapAsync(run, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<PayrollRunDetailDto>> ApproveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryGetTenantUser(out var tenantId, out var userId, out var failure))
        {
            return failure!;
        }

        var run = await db.PayrollRuns.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (run is null)
        {
            return FailDetail(ErrorCodes.PayrollRunNotFound, ErrorCodes.PayrollRunNotFound);
        }

        if (run.IsImmutable)
        {
            return FailDetail(ErrorCodes.PayrollRunImmutable, ErrorCodes.PayrollRunImmutable);
        }

        if (run.Status != PayrollRunStatus.InReview)
        {
            return FailDetail(ErrorCodes.PayrollRunInvalidTransition, ErrorCodes.PayrollRunInvalidTransition);
        }

        var requireSecond = ReadSecondApprovalRequired(run.RulesSnapshotJson);
        if (requireSecond && userId == run.CreatedBy)
        {
            return FailDetail(
                ErrorCodes.PayrollRunSecondApproverRequired,
                ErrorCodes.PayrollRunSecondApproverRequired);
        }

        run.Approve(userId, requireSecondApprover: false);
        audit.Stage(
            tenantId,
            userId,
            "payroll.run.approve",
            nameof(PayrollRun),
            run.Id,
            JsonSerializer.Serialize(new { from = nameof(PayrollRunStatus.InReview) }, JsonOptions),
            JsonSerializer.Serialize(new { to = nameof(PayrollRunStatus.Approved), approvedBy = userId }, JsonOptions));
        return await SaveAndMapAsync(run, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<PayrollRunDetailDto>> LockAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryGetTenantUser(out var tenantId, out var userId, out var failure))
        {
            return failure!;
        }

        var run = await db.PayrollRuns.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (run is null)
        {
            return FailDetail(ErrorCodes.PayrollRunNotFound, ErrorCodes.PayrollRunNotFound);
        }

        if (run.IsImmutable)
        {
            return FailDetail(ErrorCodes.PayrollRunImmutable, ErrorCodes.PayrollRunImmutable);
        }

        if (run.Status != PayrollRunStatus.Approved)
        {
            return FailDetail(ErrorCodes.PayrollRunInvalidTransition, ErrorCodes.PayrollRunInvalidTransition);
        }

        var confirmed = await rulesService.AreRulesConfirmedForLockAsync(cancellationToken)
            .ConfigureAwait(false);
        if (!confirmed)
        {
            return FailDetail(ErrorCodes.PayrollRulesNotConfirmed, ErrorCodes.PayrollRulesNotConfirmed);
        }

        run.Lock();
        audit.Stage(
            tenantId,
            userId,
            "payroll.run.lock",
            nameof(PayrollRun),
            run.Id,
            JsonSerializer.Serialize(new { from = nameof(PayrollRunStatus.Approved) }, JsonOptions),
            JsonSerializer.Serialize(new { to = nameof(PayrollRunStatus.Locked), run.LockedAt }, JsonOptions));
        return await SaveAndMapAsync(run, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<PayrollRunDetailDto>> ReopenAsync(
        Guid id,
        ReopenPayrollRunRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetTenantUser(out var tenantId, out var userId, out var failure))
        {
            return failure!;
        }

        var run = await db.PayrollRuns.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            .ConfigureAwait(false);
        if (run is null)
        {
            return FailDetail(ErrorCodes.PayrollRunNotFound, ErrorCodes.PayrollRunNotFound);
        }

        if (run.Status != PayrollRunStatus.Locked)
        {
            return FailDetail(ErrorCodes.PayrollRunInvalidTransition, ErrorCodes.PayrollRunInvalidTransition);
        }

        run.MarkSuperseded();
        var draft = PayrollRun.CreateDraft(
            tenantId,
            run.LegalEntityId,
            run.PeriodStart,
            run.PeriodEnd,
            userId,
            reopenedFromRunId: run.Id);
        db.PayrollRuns.Add(draft);

        audit.Stage(
            tenantId,
            userId,
            "payroll.run.reopen",
            nameof(PayrollRun),
            run.Id,
            JsonSerializer.Serialize(new { status = nameof(PayrollRunStatus.Locked) }, JsonOptions),
            JsonSerializer.Serialize(new
            {
                supersededRunId = run.Id,
                newRunId = draft.Id,
                reason = request.Reason.Trim(),
            }, JsonOptions));

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            return FailDetail(ErrorCodes.PayrollRunConflict, ErrorCodes.PayrollRunConflict);
        }

        var dto = await MapDetailAsync(draft.Id, cancellationToken).ConfigureAwait(false);
        return Result.Success(dto!);
    }

    public async Task<Result<PagedPayrollEntriesDto>> ListEntriesAsync(
        Guid runId,
        int page,
        int pageSize,
        string? q,
        Guid? departmentId,
        bool? warnings,
        bool? held,
        CancellationToken cancellationToken = default)
    {
        var runExists = await db.PayrollRuns.AsNoTracking()
            .AnyAsync(r => r.Id == runId, cancellationToken)
            .ConfigureAwait(false);
        if (!runExists)
        {
            return Result.Failure<PagedPayrollEntriesDto>(
                ErrorCodes.PayrollRunNotFound,
                ErrorCodes.PayrollRunNotFound);
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query =
            from e in db.PayrollEntries.AsNoTracking()
            join emp in db.Employees.AsNoTracking() on e.EmployeeId equals emp.Id
            where e.RunId == runId
            select new { Entry = e, Emp = emp };

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = ArabicText.NormalizeForSearch(q);
            query = query.Where(x =>
                x.Emp.SearchName.Contains(term)
                || x.Emp.EmployeeNumber.ToLower().Contains(term));
        }

        if (departmentId is not null)
        {
            query = query.Where(x => x.Emp.DepartmentId == departmentId);
        }

        if (warnings is true)
        {
            query = query.Where(x => x.Entry.WarningCount > 0);
        }
        else if (warnings is false)
        {
            query = query.Where(x => x.Entry.WarningCount == 0);
        }

        if (held is not null)
        {
            query = query.Where(x => x.Entry.IsHeld == held);
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query
            .OrderBy(x => x.Emp.EmployeeNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new PayrollEntryListItemDto(
                x.Entry.Id,
                x.Entry.RunId,
                x.Entry.EmployeeId,
                x.Emp.EmployeeNumber,
                x.Emp.FullNameAr,
                x.Emp.FullNameEn,
                x.Emp.DepartmentId,
                x.Entry.Gross,
                x.Entry.Net,
                x.Entry.EmployerCostTotal,
                x.Entry.WarningCount,
                x.Entry.IsHeld,
                x.Entry.HoldReason))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new PagedPayrollEntriesDto(items, page, pageSize, total));
    }

    public async Task<Result<PayrollEntryDetailDto>> GetEntryAsync(
        Guid entryId,
        CancellationToken cancellationToken = default)
    {
        var dto = await MapEntryDetailAsync(entryId, cancellationToken).ConfigureAwait(false);
        return dto is null
            ? Result.Failure<PayrollEntryDetailDto>(ErrorCodes.PayrollRunNotFound, ErrorCodes.PayrollRunNotFound)
            : Result.Success(dto);
    }

    public async Task<Result<PayrollEntryDetailDto>> HoldEntryAsync(
        Guid entryId,
        HoldPayrollEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetTenantUser(out var tenantId, out var userId, out var failureEntry))
        {
            return Result.Failure<PayrollEntryDetailDto>(failureEntry!.ErrorCode!, failureEntry.ErrorMessage!);
        }

        var entry = await db.PayrollEntries.FirstOrDefaultAsync(e => e.Id == entryId, cancellationToken)
            .ConfigureAwait(false);
        if (entry is null)
        {
            return Result.Failure<PayrollEntryDetailDto>(
                ErrorCodes.PayrollRunNotFound,
                ErrorCodes.PayrollRunNotFound);
        }

        var run = await db.PayrollRuns.FirstOrDefaultAsync(r => r.Id == entry.RunId, cancellationToken)
            .ConfigureAwait(false);
        if (run is null)
        {
            return Result.Failure<PayrollEntryDetailDto>(
                ErrorCodes.PayrollRunNotFound,
                ErrorCodes.PayrollRunNotFound);
        }

        if (run.IsImmutable)
        {
            return Result.Failure<PayrollEntryDetailDto>(
                ErrorCodes.PayrollRunImmutable,
                ErrorCodes.PayrollRunImmutable);
        }

        if (run.Status is not (PayrollRunStatus.Draft or PayrollRunStatus.InReview))
        {
            return Result.Failure<PayrollEntryDetailDto>(
                ErrorCodes.PayrollRunInvalidTransition,
                ErrorCodes.PayrollRunInvalidTransition);
        }

        entry.Hold(request.Reason);
        audit.Stage(
            tenantId,
            userId,
            "payroll.entry.hold",
            nameof(PayrollEntry),
            entry.Id,
            null,
            JsonSerializer.Serialize(new { entry.EmployeeId, entry.HoldReason }, JsonOptions));

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<PayrollEntryDetailDto>(
                ErrorCodes.PayrollRunConflict,
                ErrorCodes.PayrollRunConflict);
        }

        var dto = await MapEntryDetailAsync(entryId, cancellationToken).ConfigureAwait(false);
        return Result.Success(dto!);
    }

    private async Task<Result<PayrollRunDetailDto>> SaveAndMapAsync(
        PayrollRun run,
        CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            return FailDetail(ErrorCodes.PayrollRunConflict, ErrorCodes.PayrollRunConflict);
        }

        var dto = await MapDetailAsync(run.Id, cancellationToken).ConfigureAwait(false);
        return Result.Success(dto!);
    }

    private async Task<PayrollRunDetailDto?> MapDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        return await db.PayrollRuns.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new PayrollRunDetailDto(
                x.Id,
                x.LegalEntityId,
                x.PeriodStart,
                x.PeriodEnd,
                x.Status.ToString(),
                x.RulesSnapshotJson,
                x.WarningCount,
                x.Headcount,
                x.TotalGross,
                x.TotalNet,
                x.TotalEmployerCost,
                x.CreatedBy,
                x.ApprovedBy,
                x.LockedAt,
                x.ReopenedFromRunId,
                x.Version,
                x.CreatedAt,
                x.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<PayrollEntryDetailDto?> MapEntryDetailAsync(Guid entryId, CancellationToken cancellationToken)
    {
        var row = await (
                from e in db.PayrollEntries.AsNoTracking()
                join emp in db.Employees.AsNoTracking() on e.EmployeeId equals emp.Id
                where e.Id == entryId
                select new { Entry = e, Emp = emp })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
        {
            return null;
        }

        var lines = await db.PayrollEntryLines.AsNoTracking()
            .Where(l => l.EntryId == entryId)
            .OrderBy(l => l.SortOrder)
            .Select(l => new PayrollEntryLineDto(
                l.Id,
                l.LineType,
                l.ComponentKey,
                l.LabelAr,
                l.LabelEn,
                l.Amount,
                l.CalcExplanationJson,
                l.SortOrder))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PayrollEntryDetailDto(
            row.Entry.Id,
            row.Entry.RunId,
            row.Entry.EmployeeId,
            row.Emp.EmployeeNumber,
            row.Emp.FullNameAr,
            row.Emp.FullNameEn,
            row.Entry.SalarySnapshotJson,
            row.Entry.InputsJson,
            row.Entry.Gross,
            row.Entry.InsurableWage,
            row.Entry.EmployeeInsurance,
            row.Entry.EmployerInsurance,
            row.Entry.TaxableIncome,
            row.Entry.IncomeTax,
            row.Entry.OtherDeductions,
            row.Entry.Net,
            row.Entry.EmployerCostTotal,
            row.Entry.IsHeld,
            row.Entry.HoldReason,
            row.Entry.WarningCount,
            row.Entry.WarningsJson,
            lines);
    }

    private async Task<Dictionary<string, string>> LoadRulesSnapshotValuesAsync(
        Guid legalEntityId,
        DateOnly asOf,
        CancellationToken cancellationToken)
    {
        var rows = await db.TenantRuleValues.AsNoTracking()
            .Where(x => x.LegalEntityId == legalEntityId && x.EffectiveFrom <= asOf)
            .Select(x => new { x.RuleKey, x.ValueJson, x.EffectiveFrom, x.CreatedAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .GroupBy(x => x.RuleKey)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.CreatedAt).First().ValueJson,
                StringComparer.Ordinal);
    }

    private async Task<Guid?> GetDefaultLegalEntityIdAsync(CancellationToken cancellationToken) =>
        await db.LegalEntities.AsNoTracking()
            .Where(x => x.IsDefault)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    private bool TryGetTenantUser(
        out Guid tenantId,
        out Guid userId,
        out Result<PayrollRunDetailDto>? failure)
    {
        failure = null;
        tenantId = default;
        userId = default;
        if (tenantContext.TenantId is null || tenantContext.UserId is null)
        {
            failure = FailDetail(ErrorCodes.PayrollRunValidationFailed, "Tenant context required.");
            return false;
        }

        tenantId = tenantContext.TenantId.Value;
        userId = tenantContext.UserId.Value;
        return true;
    }

    private static Result<PayrollRunDetailDto> FailDetail(string code, string message) =>
        Result.Failure<PayrollRunDetailDto>(code, message);

    private static bool ReadSecondApprovalRequired(string rulesSnapshotJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(rulesSnapshotJson);
            if (!doc.RootElement.TryGetProperty(PayrollRuleKeys.PayrollSecondApprovalRequired, out var raw))
            {
                return true;
            }

            var json = raw.ValueKind == JsonValueKind.String ? raw.GetString() : raw.GetRawText();
            if (string.IsNullOrWhiteSpace(json))
            {
                return true;
            }

            using var inner = JsonDocument.Parse(json);
            return !inner.RootElement.TryGetProperty("value", out var v)
                   || v.ValueKind != JsonValueKind.False;
        }
        catch (JsonException)
        {
            return true;
        }
    }

    private static SalaryComponentKind MapKind(PayComponentKind kind) => kind switch
    {
        PayComponentKind.Earning => SalaryComponentKind.Earning,
        PayComponentKind.Deduction => SalaryComponentKind.Deduction,
        PayComponentKind.EmployerCost => SalaryComponentKind.EmployerCost,
        _ => SalaryComponentKind.Earning,
    };

    private static decimal CountOverlapDays(
        DateOnly leaveStart,
        DateOnly leaveEnd,
        DateOnly periodStart,
        DateOnly periodEnd,
        bool isHalfDay,
        decimal declaredDays)
    {
        var start = leaveStart > periodStart ? leaveStart : periodStart;
        var end = leaveEnd < periodEnd ? leaveEnd : periodEnd;
        if (end < start)
        {
            return 0;
        }

        if (leaveStart >= periodStart && leaveEnd <= periodEnd)
        {
            return declaredDays;
        }

        var days = end.DayNumber - start.DayNumber + 1;
        if (isHalfDay && days == 1)
        {
            return 0.5m;
        }

        return days;
    }
}
