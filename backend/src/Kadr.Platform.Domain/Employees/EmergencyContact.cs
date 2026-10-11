using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Employees;

public sealed class EmergencyContact : Entity, ITenantOwned
{
    private EmergencyContact()
    {
    }

    public Guid TenantId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Relationship { get; private set; } = string.Empty;
    public string Phone { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }

    public static EmergencyContact Create(
        Guid tenantId,
        Guid employeeId,
        string name,
        string relationship,
        string phone,
        int sortOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(phone);

        return new EmergencyContact
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            EmployeeId = employeeId,
            Name = name.Trim(),
            Relationship = relationship?.Trim() ?? string.Empty,
            Phone = phone.Trim(),
            SortOrder = sortOrder,
        };
    }

    public void Update(string name, string relationship, string phone, int sortOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(phone);
        Name = name.Trim();
        Relationship = relationship?.Trim() ?? string.Empty;
        Phone = phone.Trim();
        SortOrder = sortOrder;
    }
}
