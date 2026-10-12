using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Ids;

namespace Kadr.Platform.Domain.Employees;

public sealed class Employee : AggregateRoot, ITenantOwned, IAuditable
{
    private readonly List<EmergencyContact> _emergencyContacts = [];

    private Employee()
    {
    }

    public Guid TenantId { get; private set; }
    public Guid LegalEntityId { get; private set; }
    public Guid BranchId { get; private set; }
    public string EmployeeNumber { get; private set; } = string.Empty;
    public string FullNameAr { get; private set; } = string.Empty;
    public string? FullNameEn { get; private set; }
    public string? NationalId { get; private set; }
    public EmployeeGender Gender { get; private set; }
    public DateOnly? DateOfBirth { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? Address { get; private set; }
    public Guid? PhotoFileId { get; private set; }
    public Guid? DepartmentId { get; private set; }
    public Guid? JobTitleId { get; private set; }
    public Guid? ManagerEmployeeId { get; private set; }
    public DateOnly HireDate { get; private set; }
    public DateOnly? ProbationEndDate { get; private set; }
    public ContractType ContractType { get; private set; }
    public DateOnly? ContractStartDate { get; private set; }
    public DateOnly? ContractEndDate { get; private set; }
    public EmployeeStatus Status { get; private set; }
    public Guid? UserId { get; private set; }
    public string SearchName { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    public IReadOnlyList<EmergencyContact> EmergencyContacts => _emergencyContacts;

    public static Employee Hire(
        Guid tenantId,
        Guid legalEntityId,
        Guid branchId,
        string employeeNumber,
        string fullNameAr,
        string? fullNameEn,
        DateOnly hireDate,
        EmployeeStatus status,
        ContractType contractType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullNameAr);

        var employee = new Employee
        {
            Id = GuidV7.NewGuid(),
            TenantId = tenantId,
            LegalEntityId = legalEntityId,
            BranchId = branchId,
            EmployeeNumber = employeeNumber.Trim(),
            FullNameAr = fullNameAr.Trim(),
            FullNameEn = string.IsNullOrWhiteSpace(fullNameEn) ? null : fullNameEn.Trim(),
            HireDate = hireDate,
            Status = status,
            ContractType = contractType,
            Gender = EmployeeGender.Unspecified,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        employee.RefreshSearchName();
        return employee;
    }

    public void UpdatePersonal(
        string fullNameAr,
        string? fullNameEn,
        string? nationalId,
        EmployeeGender gender,
        DateOnly? dateOfBirth,
        string? email,
        string? phone,
        string? address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullNameAr);
        FullNameAr = fullNameAr.Trim();
        FullNameEn = string.IsNullOrWhiteSpace(fullNameEn) ? null : fullNameEn.Trim();
        NationalId = string.IsNullOrWhiteSpace(nationalId) ? null : nationalId.Trim();
        Gender = gender;
        DateOfBirth = dateOfBirth;
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        RefreshSearchName();
        Touch();
    }

    public void UpdateJob(
        Guid branchId,
        Guid? departmentId,
        Guid? jobTitleId,
        Guid? managerEmployeeId,
        DateOnly hireDate,
        DateOnly? probationEndDate,
        ContractType contractType,
        DateOnly? contractStartDate,
        DateOnly? contractEndDate)
    {
        if (managerEmployeeId == Id)
        {
            throw new InvalidOperationException("employees.manager_cycle");
        }

        BranchId = branchId;
        DepartmentId = departmentId;
        JobTitleId = jobTitleId;
        ManagerEmployeeId = managerEmployeeId;
        HireDate = hireDate;
        ProbationEndDate = probationEndDate;
        ContractType = contractType;
        ContractStartDate = contractStartDate;
        ContractEndDate = contractEndDate;
        Touch();
    }

    public void SetStatus(EmployeeStatus status)
    {
        Status = status;
        Touch();
    }

    public void SetPhoto(Guid? photoFileId)
    {
        PhotoFileId = photoFileId;
        Touch();
    }

    public void LinkUser(Guid? userId)
    {
        UserId = userId;
        Touch();
    }

    public void SetEmployeeNumber(string employeeNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeNumber);
        EmployeeNumber = employeeNumber.Trim();
        RefreshSearchName();
        Touch();
    }

    public void ReplaceEmergencyContacts(IEnumerable<(string Name, string Relationship, string Phone)> contacts)
    {
        _emergencyContacts.Clear();
        var order = 0;
        foreach (var c in contacts)
        {
            if (string.IsNullOrWhiteSpace(c.Name) || string.IsNullOrWhiteSpace(c.Phone))
            {
                continue;
            }

            _emergencyContacts.Add(
                EmergencyContact.Create(TenantId, Id, c.Name, c.Relationship, c.Phone, order++));
        }

        Touch();
    }

    private void RefreshSearchName()
    {
        SearchName = Kadr.SharedKernel.Text.ArabicText.NormalizeForSearch(
            string.Join(
                ' ',
                new[] { FullNameAr, FullNameEn, EmployeeNumber, Phone, NationalId, Email }
                    .Where(s => !string.IsNullOrWhiteSpace(s))));
    }

    private void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
