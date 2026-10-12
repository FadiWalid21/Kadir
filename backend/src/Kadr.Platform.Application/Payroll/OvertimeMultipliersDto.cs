namespace Kadr.Platform.Application.Payroll;

public sealed record OvertimeMultipliersDto(
    decimal Day,
    decimal Night,
    decimal Holiday);
