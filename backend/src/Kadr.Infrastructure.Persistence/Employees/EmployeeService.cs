using System.Text.Json;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Employees;
using Kadr.Platform.Domain.Employees;
using Kadr.Platform.Domain.Files;
using Kadr.SharedKernel.Abstractions;
using Kadr.SharedKernel.Constants;
using Kadr.SharedKernel.Results;
using Kadr.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Kadr.Infrastructure.Persistence.Employees;

public sealed class EmployeeService(
    KadrDbContext db,
    ITenantContext tenantContext,
    ISubscriptionGuard subscriptionGuard,
    IActiveEmployeeCounter employeeCounter,
    IFileStorage fileStorage,
    IAuditLogWriter audit) : IEmployeeService
{
    private const int MaxPageSize = 100;

    public async Task<Result<PagedEmployeesDto>> ListAsync(
        int page,
        int pageSize,
        string? q,
        string? status,
        Guid? departmentId,
        Guid? branchId,
        Guid? jobTitleId,
        Guid? managerId,
        CancellationToken cancellationToken = default)
    {
        var gate = await EnsureFeatureAsync(cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure<PagedEmployeesDto>(gate.ErrorCode!, gate.ErrorMessage!);
        }

        if (tenantContext.TenantId is null || tenantContext.UserId is null)
        {
            return Result.Failure<PagedEmployeesDto>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : Math.Min(pageSize, MaxPageSize);

        var scope = await EmployeeDataScope.ResolveAsync(db, tenantContext.UserId.Value, cancellationToken)
            .ConfigureAwait(false);
        var query = EmployeeDataScope.Apply(
            db.Employees.AsNoTracking(),
            scope.Scope,
            tenantContext.UserId.Value,
            scope.LinkedEmployeeId,
            scope.DepartmentId,
            scope.BranchId);

        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<EmployeeStatus>(status, true, out var statusFilter))
        {
            query = query.Where(e => e.Status == statusFilter);
        }

        if (departmentId is not null)
        {
            query = query.Where(e => e.DepartmentId == departmentId);
        }

        if (branchId is not null)
        {
            query = query.Where(e => e.BranchId == branchId);
        }

        if (jobTitleId is not null)
        {
            query = query.Where(e => e.JobTitleId == jobTitleId);
        }

        if (managerId is not null)
        {
            query = query.Where(e => e.ManagerEmployeeId == managerId);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLowerInvariant();
            query = query.Where(e => e.SearchName.Contains(term));
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var rows = await (
                from e in query
                join b in db.Branches.AsNoTracking() on e.BranchId equals b.Id
                join d in db.Departments.AsNoTracking() on e.DepartmentId equals d.Id into dj
                from d in dj.DefaultIfEmpty()
                join j in db.JobTitles.AsNoTracking() on e.JobTitleId equals j.Id into jj
                from j in jj.DefaultIfEmpty()
                join m in db.Employees.AsNoTracking() on e.ManagerEmployeeId equals m.Id into mj
                from m in mj.DefaultIfEmpty()
                orderby e.FullNameAr
                select new EmployeeListItemDto(
                    e.Id,
                    e.EmployeeNumber,
                    e.FullNameAr,
                    e.FullNameEn,
                    e.Email,
                    e.Phone,
                    e.NationalId,
                    e.Status.ToString(),
                    j != null ? j.NameAr : null,
                    j != null ? j.NameEn : null,
                    d != null ? d.NameAr : null,
                    d != null ? d.NameEn : null,
                    b.Name,
                    m != null ? m.FullNameAr : null,
                    m != null ? m.Id : null,
                    e.HireDate))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var summary = await BuildSummaryAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success(new PagedEmployeesDto(rows, page, pageSize, total, summary));
    }

    public async Task<Result<EmployeeDetailDto>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var gate = await EnsureFeatureAsync(cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure<EmployeeDetailDto>(gate.ErrorCode!, gate.ErrorMessage!);
        }

        var accessible = await GetAccessibleAsync(id, cancellationToken).ConfigureAwait(false);
        if (accessible.IsFailure)
        {
            return Result.Failure<EmployeeDetailDto>(accessible.ErrorCode!, accessible.ErrorMessage!);
        }

        return Result.Success(await MapDetailAsync(accessible.Value!, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<EmployeeDetailDto>> CreateAsync(
        CreateEmployeeRequest request,
        CancellationToken cancellationToken = default)
    {
        var writable = await EnsureWritableFeatureAsync(cancellationToken).ConfigureAwait(false);
        if (writable.IsFailure)
        {
            return Result.Failure<EmployeeDetailDto>(writable.ErrorCode!, writable.ErrorMessage!);
        }

        if (tenantContext.TenantId is null)
        {
            return Result.Failure<EmployeeDetailDto>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        var tenantId = tenantContext.TenantId.Value;
        if (string.IsNullOrWhiteSpace(request.FullNameAr))
        {
            return Result.Failure<EmployeeDetailDto>(
                ErrorCodes.EmployeesValidationFailed,
                ErrorCodes.EmployeesValidationFailed);
        }

        var status = ParseStatus(request.Status) ?? EmployeeStatus.Active;
        if (EmployeeStatusRules.CountsTowardCapacity(status))
        {
            var active = await employeeCounter.CountAsync(tenantId, cancellationToken).ConfigureAwait(false);
            var cap = await subscriptionGuard.EnsureEmployeeCapacityAsync(active + 1, cancellationToken)
                .ConfigureAwait(false);
            if (cap.IsFailure)
            {
                return Result.Failure<EmployeeDetailDto>(cap.ErrorCode!, cap.ErrorMessage!);
            }
        }

        var branchId = request.BranchId
            ?? await db.Branches.AsNoTracking()
                .Where(b => b.IsActive)
                .OrderBy(b => b.CreatedAt)
                .Select(b => (Guid?)b.Id)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        if (branchId is null)
        {
            return Result.Failure<EmployeeDetailDto>(
                ErrorCodes.EmployeesValidationFailed,
                ErrorCodes.EmployeesValidationFailed);
        }

        var legalEntityId = await db.Branches.AsNoTracking()
            .Where(b => b.Id == branchId)
            .Select(b => b.LegalEntityId)
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);

        var number = string.IsNullOrWhiteSpace(request.EmployeeNumber)
            ? await NextEmployeeNumberAsync(cancellationToken).ConfigureAwait(false)
            : request.EmployeeNumber.Trim();

        if (await db.Employees.AnyAsync(e => e.EmployeeNumber == number, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<EmployeeDetailDto>(
                ErrorCodes.EmployeesDuplicateNumber,
                ErrorCodes.EmployeesDuplicateNumber);
        }

        if (!string.IsNullOrWhiteSpace(request.NationalId)
            && await db.Employees.AnyAsync(e => e.NationalId == request.NationalId.Trim(), cancellationToken)
                .ConfigureAwait(false))
        {
            return Result.Failure<EmployeeDetailDto>(
                ErrorCodes.EmployeesDuplicateNationalId,
                ErrorCodes.EmployeesDuplicateNationalId);
        }

        if (request.ManagerEmployeeId is Guid mid
            && !await db.Employees.AnyAsync(e => e.Id == mid, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<EmployeeDetailDto>(
                ErrorCodes.EmployeesValidationFailed,
                ErrorCodes.EmployeesValidationFailed);
        }

        var employee = Employee.Hire(
            tenantId,
            legalEntityId,
            branchId.Value,
            number,
            request.FullNameAr,
            request.FullNameEn,
            request.HireDate == default ? DateOnly.FromDateTime(DateTime.UtcNow) : request.HireDate,
            status,
            ParseContract(request.ContractType));

        employee.UpdatePersonal(
            request.FullNameAr,
            request.FullNameEn,
            request.NationalId,
            ParseGender(request.Gender),
            request.DateOfBirth,
            request.Email,
            request.Phone,
            request.Address);

        employee.UpdateJob(
            branchId.Value,
            request.DepartmentId,
            request.JobTitleId,
            request.ManagerEmployeeId,
            employee.HireDate,
            request.ProbationEndDate,
            ParseContract(request.ContractType),
            request.ContractStartDate,
            request.ContractEndDate);

        if (request.EmergencyContacts is { Count: > 0 })
        {
            employee.ReplaceEmergencyContacts(
                request.EmergencyContacts.Select(c => (c.Name, c.Relationship, c.Phone)));
        }

        db.Employees.Add(employee);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await audit.WriteAsync(
                tenantId,
                tenantContext.UserId,
                "employee.hired",
                "Employee",
                employee.Id,
                null,
                JsonSerializer.Serialize(new { employee.EmployeeNumber, employee.FullNameAr, status = employee.Status.ToString() }),
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(await MapDetailAsync(employee, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<EmployeeDetailDto>> UpdateAsync(
        Guid id,
        UpdateEmployeeRequest request,
        CancellationToken cancellationToken = default)
    {
        var writable = await EnsureWritableFeatureAsync(cancellationToken).ConfigureAwait(false);
        if (writable.IsFailure)
        {
            return Result.Failure<EmployeeDetailDto>(writable.ErrorCode!, writable.ErrorMessage!);
        }

        var accessible = await GetAccessibleTrackedAsync(id, cancellationToken).ConfigureAwait(false);
        if (accessible.IsFailure)
        {
            return Result.Failure<EmployeeDetailDto>(accessible.ErrorCode!, accessible.ErrorMessage!);
        }

        var employee = accessible.Value!;
        if (!string.IsNullOrWhiteSpace(request.NationalId)
            && request.NationalId.Trim() != employee.NationalId
            && await db.Employees.AnyAsync(
                    e => e.Id != id && e.NationalId == request.NationalId.Trim(),
                    cancellationToken)
                .ConfigureAwait(false))
        {
            return Result.Failure<EmployeeDetailDto>(
                ErrorCodes.EmployeesDuplicateNationalId,
                ErrorCodes.EmployeesDuplicateNationalId);
        }

        if (request.ManagerEmployeeId is Guid mid)
        {
            if (mid == id || await WouldCreateManagerCycleAsync(id, mid, cancellationToken).ConfigureAwait(false))
            {
                return Result.Failure<EmployeeDetailDto>(
                    ErrorCodes.EmployeesManagerCycle,
                    ErrorCodes.EmployeesManagerCycle);
            }
        }

        employee.UpdatePersonal(
            request.FullNameAr,
            request.FullNameEn,
            request.NationalId,
            ParseGender(request.Gender),
            request.DateOfBirth,
            request.Email,
            request.Phone,
            request.Address);

        employee.UpdateJob(
            request.BranchId,
            request.DepartmentId,
            request.JobTitleId,
            request.ManagerEmployeeId,
            request.HireDate,
            request.ProbationEndDate,
            ParseContract(request.ContractType),
            request.ContractStartDate,
            request.ContractEndDate);

        if (request.EmergencyContacts is not null)
        {
            await db.EmergencyContacts.Where(c => c.EmployeeId == id)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
            employee.ReplaceEmergencyContacts(
                request.EmergencyContacts.Select(c => (c.Name, c.Relationship, c.Phone)));
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await audit.WriteAsync(
                tenantContext.TenantId!.Value,
                tenantContext.UserId,
                "employee.updated",
                "Employee",
                employee.Id,
                null,
                JsonSerializer.Serialize(new { employee.EmployeeNumber }),
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(await MapDetailAsync(employee, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<EmployeeDetailDto>> ChangeStatusAsync(
        Guid id,
        ChangeEmployeeStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var writable = await EnsureWritableFeatureAsync(cancellationToken).ConfigureAwait(false);
        if (writable.IsFailure)
        {
            return Result.Failure<EmployeeDetailDto>(writable.ErrorCode!, writable.ErrorMessage!);
        }

        var next = ParseStatus(request.Status);
        if (next is null)
        {
            return Result.Failure<EmployeeDetailDto>(
                ErrorCodes.EmployeesValidationFailed,
                ErrorCodes.EmployeesValidationFailed);
        }

        if (next is EmployeeStatus.Terminated or EmployeeStatus.Suspended)
        {
            // terminate permission checked at endpoint; still validate here for service reuse
        }

        var accessible = await GetAccessibleTrackedAsync(id, cancellationToken).ConfigureAwait(false);
        if (accessible.IsFailure)
        {
            return Result.Failure<EmployeeDetailDto>(accessible.ErrorCode!, accessible.ErrorMessage!);
        }

        var employee = accessible.Value!;
        var wasCounting = EmployeeStatusRules.CountsTowardCapacity(employee.Status);
        var willCount = EmployeeStatusRules.CountsTowardCapacity(next.Value);
        if (!wasCounting && willCount && tenantContext.TenantId is Guid tid)
        {
            var active = await employeeCounter.CountAsync(tid, cancellationToken).ConfigureAwait(false);
            var cap = await subscriptionGuard.EnsureEmployeeCapacityAsync(active + 1, cancellationToken)
                .ConfigureAwait(false);
            if (cap.IsFailure)
            {
                return Result.Failure<EmployeeDetailDto>(cap.ErrorCode!, cap.ErrorMessage!);
            }
        }

        var old = employee.Status.ToString();
        employee.SetStatus(next.Value);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await audit.WriteAsync(
                tenantContext.TenantId!.Value,
                tenantContext.UserId,
                "employee.status_changed",
                "Employee",
                employee.Id,
                JsonSerializer.Serialize(new { status = old }),
                JsonSerializer.Serialize(new { status = employee.Status.ToString() }),
                cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(await MapDetailAsync(employee, cancellationToken).ConfigureAwait(false));
    }

    public async Task<Result<IReadOnlyList<EmployeeDocumentListItemDto>>> ListDocumentsAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        var gate = await EnsureFeatureAsync(cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure<IReadOnlyList<EmployeeDocumentListItemDto>>(gate.ErrorCode!, gate.ErrorMessage!);
        }

        var accessible = await GetAccessibleAsync(employeeId, cancellationToken).ConfigureAwait(false);
        if (accessible.IsFailure)
        {
            return Result.Failure<IReadOnlyList<EmployeeDocumentListItemDto>>(
                accessible.ErrorCode!,
                accessible.ErrorMessage!);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var items = await (
                from d in db.EmployeeDocuments.AsNoTracking()
                join t in db.DocumentTypes.AsNoTracking() on d.DocumentTypeId equals t.Id
                join f in db.FileObjects.AsNoTracking() on d.FileObjectId equals f.Id
                where d.EmployeeId == employeeId
                orderby d.ExpiresOn
                select new EmployeeDocumentListItemDto(
                    d.Id,
                    d.DocumentTypeId,
                    t.NameAr,
                    t.NameEn,
                    d.ReferenceNumber,
                    d.IssuedOn,
                    d.ExpiresOn,
                    DocumentExpiryStatus.Compute(d.ExpiresOn, today),
                    d.FileObjectId,
                    f.FileName))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result.Success<IReadOnlyList<EmployeeDocumentListItemDto>>(items);
    }

    public async Task<Result<EmployeeDocumentListItemDto>> UploadDocumentAsync(
        Guid employeeId,
        UploadEmployeeDocumentRequest meta,
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var writable = await EnsureWritableFeatureAsync(cancellationToken).ConfigureAwait(false);
        if (writable.IsFailure)
        {
            return Result.Failure<EmployeeDocumentListItemDto>(writable.ErrorCode!, writable.ErrorMessage!);
        }

        if (tenantContext.TenantId is null)
        {
            return Result.Failure<EmployeeDocumentListItemDto>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        var accessible = await GetAccessibleAsync(employeeId, cancellationToken).ConfigureAwait(false);
        if (accessible.IsFailure)
        {
            return Result.Failure<EmployeeDocumentListItemDto>(accessible.ErrorCode!, accessible.ErrorMessage!);
        }

        var docType = await db.DocumentTypes.AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == meta.DocumentTypeId && t.IsActive, cancellationToken)
            .ConfigureAwait(false);
        if (docType is null)
        {
            return Result.Failure<EmployeeDocumentListItemDto>(
                ErrorCodes.EmployeesValidationFailed,
                ErrorCodes.EmployeesValidationFailed);
        }

        if (docType.HasExpiry && meta.ExpiresOn is null)
        {
            return Result.Failure<EmployeeDocumentListItemDto>(
                ErrorCodes.EmployeesValidationFailed,
                ErrorCodes.EmployeesValidationFailed);
        }

        StoredFile stored;
        try
        {
            stored = await fileStorage.SaveAsync(
                    tenantContext.TenantId.Value,
                    FilePurpose.Document,
                    content,
                    fileName,
                    contentType,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            return Result.Failure<EmployeeDocumentListItemDto>(ErrorCodes.FileInvalidType, ErrorCodes.FileInvalidType);
        }

        var hash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(stored.StoragePath + stored.SizeBytes))).ToLowerInvariant();
        var fileObject = FileObject.Create(
            stored.FileId,
            tenantContext.TenantId.Value,
            stored.StoragePath,
            stored.FileName,
            stored.ContentType,
            stored.SizeBytes,
            hash,
            FilePurpose.Document);
        db.FileObjects.Add(fileObject);

        var doc = EmployeeDocument.Create(
            tenantContext.TenantId.Value,
            employeeId,
            meta.DocumentTypeId,
            fileObject.Id,
            meta.ReferenceNumber,
            meta.IssuedOn,
            meta.ExpiresOn);
        db.EmployeeDocuments.Add(doc);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await audit.WriteAsync(
                tenantContext.TenantId.Value,
                tenantContext.UserId,
                "employee.document_uploaded",
                "EmployeeDocument",
                doc.Id,
                null,
                JsonSerializer.Serialize(new { employeeId, doc.DocumentTypeId }),
                cancellationToken)
            .ConfigureAwait(false);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return Result.Success(new EmployeeDocumentListItemDto(
            doc.Id,
            doc.DocumentTypeId,
            docType.NameAr,
            docType.NameEn,
            doc.ReferenceNumber,
            doc.IssuedOn,
            doc.ExpiresOn,
            DocumentExpiryStatus.Compute(doc.ExpiresOn, today),
            doc.FileObjectId,
            fileObject.FileName));
    }

    public async Task<Result> DeleteDocumentAsync(
        Guid employeeId,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var writable = await EnsureWritableFeatureAsync(cancellationToken).ConfigureAwait(false);
        if (writable.IsFailure)
        {
            return writable;
        }

        var accessible = await GetAccessibleAsync(employeeId, cancellationToken).ConfigureAwait(false);
        if (accessible.IsFailure)
        {
            return Result.Failure(accessible.ErrorCode!, accessible.ErrorMessage!);
        }

        var doc = await db.EmployeeDocuments
            .SingleOrDefaultAsync(d => d.Id == documentId && d.EmployeeId == employeeId, cancellationToken)
            .ConfigureAwait(false);
        if (doc is null)
        {
            return Result.Failure(ErrorCodes.EmployeesNotFound, ErrorCodes.EmployeesNotFound);
        }

        var file = await db.FileObjects.SingleOrDefaultAsync(f => f.Id == doc.FileObjectId, cancellationToken)
            .ConfigureAwait(false);
        db.EmployeeDocuments.Remove(doc);
        if (file is not null)
        {
            db.FileObjects.Remove(file);
            await fileStorage.DeleteAsync(file.StoragePath, cancellationToken).ConfigureAwait(false);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await audit.WriteAsync(
                tenantContext.TenantId!.Value,
                tenantContext.UserId,
                "employee.document_deleted",
                "EmployeeDocument",
                documentId,
                null,
                JsonSerializer.Serialize(new { employeeId }),
                cancellationToken)
            .ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<Result<(Stream Stream, string FileName, string ContentType)>> OpenDocumentAsync(
        Guid employeeId,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var gate = await EnsureFeatureAsync(cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure<(Stream, string, string)>(gate.ErrorCode!, gate.ErrorMessage!);
        }

        var accessible = await GetAccessibleAsync(employeeId, cancellationToken).ConfigureAwait(false);
        if (accessible.IsFailure)
        {
            return Result.Failure<(Stream, string, string)>(accessible.ErrorCode!, accessible.ErrorMessage!);
        }

        var row = await (
                from d in db.EmployeeDocuments.AsNoTracking()
                join f in db.FileObjects.AsNoTracking() on d.FileObjectId equals f.Id
                where d.Id == documentId && d.EmployeeId == employeeId
                select new { f.StoragePath, f.FileName, f.ContentType })
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return Result.Failure<(Stream, string, string)>(ErrorCodes.FileNotFound, ErrorCodes.FileNotFound);
        }

        var stream = await fileStorage.OpenReadAsync(row.StoragePath, cancellationToken).ConfigureAwait(false);
        return Result.Success((stream, row.FileName, row.ContentType));
    }

    public async Task<Result<ExpiringDocumentsResultDto>> ListExpiringAsync(
        string window,
        string? q,
        Guid? documentTypeId,
        Guid? departmentId,
        Guid? branchId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var gate = await EnsureFeatureAsync(cancellationToken).ConfigureAwait(false);
        if (gate.IsFailure)
        {
            return Result.Failure<ExpiringDocumentsResultDto>(gate.ErrorCode!, gate.ErrorMessage!);
        }

        if (tenantContext.UserId is null)
        {
            return Result.Failure<ExpiringDocumentsResultDto>(ErrorCodes.TenantNotFound, ErrorCodes.TenantNotFound);
        }

        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : Math.Min(pageSize, MaxPageSize);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var scope = await EmployeeDataScope.ResolveAsync(db, tenantContext.UserId.Value, cancellationToken)
            .ConfigureAwait(false);
        var employees = EmployeeDataScope.Apply(
            db.Employees.AsNoTracking(),
            scope.Scope,
            tenantContext.UserId.Value,
            scope.LinkedEmployeeId,
            scope.DepartmentId,
            scope.BranchId);

        var query =
            from d in db.EmployeeDocuments.AsNoTracking()
            join e in employees on d.EmployeeId equals e.Id
            join t in db.DocumentTypes.AsNoTracking() on d.DocumentTypeId equals t.Id
            join dep in db.Departments.AsNoTracking() on e.DepartmentId equals dep.Id into dj
            from dep in dj.DefaultIfEmpty()
            join j in db.JobTitles.AsNoTracking() on e.JobTitleId equals j.Id into jj
            from j in jj.DefaultIfEmpty()
            where d.ExpiresOn != null
            select new { d, e, t, dep, j };

        if (documentTypeId is not null)
        {
            query = query.Where(x => x.d.DocumentTypeId == documentTypeId);
        }

        if (departmentId is not null)
        {
            query = query.Where(x => x.e.DepartmentId == departmentId);
        }

        if (branchId is not null)
        {
            query = query.Where(x => x.e.BranchId == branchId);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLowerInvariant();
            query = query.Where(x => x.e.SearchName.Contains(term));
        }

        var within60 = today.AddDays(60);
        var within30 = today.AddDays(30);
        var within7 = today.AddDays(7);

        var expiredCount = await query.CountAsync(x => x.d.ExpiresOn! < today, cancellationToken)
            .ConfigureAwait(false);
        var count7 = await query.CountAsync(
                x => x.d.ExpiresOn! >= today && x.d.ExpiresOn! <= within7,
                cancellationToken)
            .ConfigureAwait(false);
        var count30 = await query.CountAsync(
                x => x.d.ExpiresOn! >= today && x.d.ExpiresOn! <= within30,
                cancellationToken)
            .ConfigureAwait(false);
        var count60 = await query.CountAsync(
                x => x.d.ExpiresOn! >= today && x.d.ExpiresOn! <= within60,
                cancellationToken)
            .ConfigureAwait(false);

        query = window.ToLowerInvariant() switch
        {
            "expired" => query.Where(x => x.d.ExpiresOn! < today),
            "7" => query.Where(x => x.d.ExpiresOn! >= today && x.d.ExpiresOn! <= within7),
            "30" => query.Where(x => x.d.ExpiresOn! >= today && x.d.ExpiresOn! <= within30),
            "60" => query.Where(x => x.d.ExpiresOn! >= today && x.d.ExpiresOn! <= within60),
            _ => query.Where(x => x.d.ExpiresOn! < today || x.d.ExpiresOn! <= within60),
        };

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query
            .OrderBy(x => x.d.ExpiresOn)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ExpiringDocumentRowDto(
                x.d.Id,
                x.e.Id,
                x.e.FullNameAr,
                x.j != null ? x.j.NameAr : null,
                x.dep != null ? x.dep.NameAr : null,
                x.d.DocumentTypeId,
                x.t.NameAr,
                x.t.NameEn,
                x.d.ReferenceNumber,
                x.d.ExpiresOn,
                DocumentExpiryStatus.Compute(x.d.ExpiresOn, today),
                x.d.ExpiresOn!.Value.DayNumber - today.DayNumber))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new ExpiringDocumentsResultDto(
            items,
            page,
            pageSize,
            total,
            count60,
            count30,
            count7,
            expiredCount));
    }

    private async Task<EmployeeSummaryDto> BuildSummaryAsync(CancellationToken cancellationToken)
    {
        var grouped = await db.Employees.AsNoTracking()
            .GroupBy(e => e.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        int C(EmployeeStatus s) => grouped.FirstOrDefault(x => x.Status == s)?.Count ?? 0;
        var counts = new EmployeeStatusCountsDto(
            grouped.Sum(x => x.Count),
            C(EmployeeStatus.Active),
            C(EmployeeStatus.Probation),
            C(EmployeeStatus.OnLeave),
            C(EmployeeStatus.Suspended),
            C(EmployeeStatus.Terminated));

        var active = counts.Active + counts.Probation + counts.OnLeave;
        var snap = await subscriptionGuard.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        return new EmployeeSummaryDto(counts, active, snap.IsSuccess ? snap.Value!.EmployeeCap : null);
    }

    private async Task<Result<Employee>> GetAccessibleAsync(Guid id, CancellationToken cancellationToken)
    {
        if (tenantContext.UserId is null)
        {
            return Result.Failure<Employee>(ErrorCodes.EmployeesNotFound, ErrorCodes.EmployeesNotFound);
        }

        var scope = await EmployeeDataScope.ResolveAsync(db, tenantContext.UserId.Value, cancellationToken)
            .ConfigureAwait(false);
        var query = EmployeeDataScope.Apply(
            db.Employees.AsNoTracking().Include(e => e.EmergencyContacts),
            scope.Scope,
            tenantContext.UserId.Value,
            scope.LinkedEmployeeId,
            scope.DepartmentId,
            scope.BranchId);

        var employee = await query.SingleOrDefaultAsync(e => e.Id == id, cancellationToken).ConfigureAwait(false);
        return employee is null
            ? Result.Failure<Employee>(ErrorCodes.EmployeesNotFound, ErrorCodes.EmployeesNotFound)
            : Result.Success(employee);
    }

    private async Task<Result<Employee>> GetAccessibleTrackedAsync(Guid id, CancellationToken cancellationToken)
    {
        if (tenantContext.UserId is null)
        {
            return Result.Failure<Employee>(ErrorCodes.EmployeesNotFound, ErrorCodes.EmployeesNotFound);
        }

        var scope = await EmployeeDataScope.ResolveAsync(db, tenantContext.UserId.Value, cancellationToken)
            .ConfigureAwait(false);
        var allowed = await EmployeeDataScope.Apply(
                db.Employees.AsNoTracking(),
                scope.Scope,
                tenantContext.UserId.Value,
                scope.LinkedEmployeeId,
                scope.DepartmentId,
                scope.BranchId)
            .AnyAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (!allowed)
        {
            return Result.Failure<Employee>(ErrorCodes.EmployeesNotFound, ErrorCodes.EmployeesNotFound);
        }

        var employee = await db.Employees
            .Include(e => e.EmergencyContacts)
            .SingleOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);
        return employee is null
            ? Result.Failure<Employee>(ErrorCodes.EmployeesNotFound, ErrorCodes.EmployeesNotFound)
            : Result.Success(employee);
    }

    private async Task<EmployeeDetailDto> MapDetailAsync(Employee e, CancellationToken cancellationToken)
    {
        var branchName = await db.Branches.AsNoTracking()
            .Where(b => b.Id == e.BranchId)
            .Select(b => b.Name)
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);

        string? depAr = null, depEn = null, jobAr = null, jobEn = null, managerName = null;
        if (e.DepartmentId is Guid did)
        {
            var dep = await db.Departments.AsNoTracking()
                .Where(d => d.Id == did)
                .Select(d => new { d.NameAr, d.NameEn })
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            depAr = dep?.NameAr;
            depEn = dep?.NameEn;
        }

        if (e.JobTitleId is Guid jid)
        {
            var job = await db.JobTitles.AsNoTracking()
                .Where(j => j.Id == jid)
                .Select(j => new { j.NameAr, j.NameEn })
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            jobAr = job?.NameAr;
            jobEn = job?.NameEn;
        }

        if (e.ManagerEmployeeId is Guid mid)
        {
            managerName = await db.Employees.AsNoTracking()
                .Where(m => m.Id == mid)
                .Select(m => m.FullNameAr)
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        var reports = await db.Employees.AsNoTracking()
            .Where(r => r.ManagerEmployeeId == e.Id)
            .OrderBy(r => r.FullNameAr)
            .Take(20)
            .Select(r => new EmployeeListItemDto(
                r.Id,
                r.EmployeeNumber,
                r.FullNameAr,
                r.FullNameEn,
                r.Email,
                r.Phone,
                null,
                r.Status.ToString(),
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                r.HireDate))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var urgent = await (
                from d in db.EmployeeDocuments.AsNoTracking()
                join t in db.DocumentTypes.AsNoTracking() on d.DocumentTypeId equals t.Id
                join f in db.FileObjects.AsNoTracking() on d.FileObjectId equals f.Id
                where d.EmployeeId == e.Id
                      && d.ExpiresOn != null
                      && d.ExpiresOn <= today.AddDays(30)
                orderby d.ExpiresOn
                select new EmployeeDocumentListItemDto(
                    d.Id,
                    d.DocumentTypeId,
                    t.NameAr,
                    t.NameEn,
                    d.ReferenceNumber,
                    d.IssuedOn,
                    d.ExpiresOn,
                    DocumentExpiryStatus.Compute(d.ExpiresOn, today),
                    d.FileObjectId,
                    f.FileName))
            .Take(5)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var masked = MaskNationalId(e.NationalId);

        return new EmployeeDetailDto(
            e.Id,
            e.EmployeeNumber,
            e.FullNameAr,
            e.FullNameEn,
            e.NationalId,
            masked,
            e.Gender.ToString(),
            e.DateOfBirth,
            e.Email,
            e.Phone,
            e.Address,
            e.PhotoFileId,
            e.LegalEntityId,
            e.BranchId,
            branchName,
            e.DepartmentId,
            depAr,
            depEn,
            e.JobTitleId,
            jobAr,
            jobEn,
            e.ManagerEmployeeId,
            managerName,
            e.HireDate,
            e.ProbationEndDate,
            e.ContractType.ToString(),
            e.ContractStartDate,
            e.ContractEndDate,
            e.Status.ToString(),
            e.UserId,
            e.EmergencyContacts
                .OrderBy(c => c.SortOrder)
                .Select(c => new EmergencyContactDto(c.Id, c.Name, c.Relationship, c.Phone, c.SortOrder))
                .ToList(),
            reports,
            urgent);
    }

    private async Task<string> NextEmployeeNumberAsync(CancellationToken cancellationToken)
    {
        var count = await db.Employees.CountAsync(cancellationToken).ConfigureAwait(false);
        for (var i = 1; i <= 1000; i++)
        {
            var candidate = $"EMP-{(count + i):D4}";
            if (!await db.Employees.AnyAsync(e => e.EmployeeNumber == candidate, cancellationToken)
                    .ConfigureAwait(false))
            {
                return candidate;
            }
        }

        return $"EMP-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
    }

    private async Task<bool> WouldCreateManagerCycleAsync(
        Guid employeeId,
        Guid managerId,
        CancellationToken cancellationToken)
    {
        var current = managerId;
        for (var depth = 0; depth < 50; depth++)
        {
            if (current == employeeId)
            {
                return true;
            }

            var next = await db.Employees.AsNoTracking()
                .Where(e => e.Id == current)
                .Select(e => e.ManagerEmployeeId)
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (next is null)
            {
                return false;
            }

            current = next.Value;
        }

        return true;
    }

    private async Task<Result> EnsureFeatureAsync(CancellationToken cancellationToken)
    {
        var feature = await subscriptionGuard.EnsureFeatureAsync(FeatureKeys.CoreHr, cancellationToken)
            .ConfigureAwait(false);
        return feature;
    }

    private async Task<Result> EnsureWritableFeatureAsync(CancellationToken cancellationToken)
    {
        var writable = await subscriptionGuard.EnsureWritableAsync(cancellationToken).ConfigureAwait(false);
        if (writable.IsFailure)
        {
            return writable;
        }

        return await EnsureFeatureAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string? MaskNationalId(string? nationalId)
    {
        if (string.IsNullOrWhiteSpace(nationalId) || nationalId.Length < 4)
        {
            return nationalId;
        }

        return new string('*', nationalId.Length - 4) + nationalId[^4..];
    }

    private static EmployeeStatus? ParseStatus(string? value) =>
        Enum.TryParse<EmployeeStatus>(value, true, out var s) ? s : null;

    private static EmployeeGender ParseGender(string? value) =>
        Enum.TryParse<EmployeeGender>(value, true, out var g) ? g : EmployeeGender.Unspecified;

    private static ContractType ParseContract(string? value) =>
        Enum.TryParse<ContractType>(value, true, out var c) ? c : ContractType.Unlimited;
}
