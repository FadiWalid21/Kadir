using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Employees;

public sealed class EmployeeDocument : AggregateRoot, ITenantOwned, IAuditable
{
    private EmployeeDocument()
    {
    }

    public Guid TenantId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid DocumentTypeId { get; private set; }
    public string? ReferenceNumber { get; private set; }
    public DateOnly? IssuedOn { get; private set; }
    public DateOnly? ExpiresOn { get; private set; }
    public Guid FileObjectId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public static EmployeeDocument Create(
        Guid tenantId,
        Guid employeeId,
        Guid documentTypeId,
        Guid fileObjectId,
        string? referenceNumber,
        DateOnly? issuedOn,
        DateOnly? expiresOn)
    {
        return new EmployeeDocument
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            EmployeeId = employeeId,
            DocumentTypeId = documentTypeId,
            FileObjectId = fileObjectId,
            ReferenceNumber = string.IsNullOrWhiteSpace(referenceNumber) ? null : referenceNumber.Trim(),
            IssuedOn = issuedOn,
            ExpiresOn = expiresOn,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }
}
