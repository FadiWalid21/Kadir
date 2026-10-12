using FluentValidation;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Payroll.Calculation;
using Kadr.Platform.Application.Tenants;
using Microsoft.Extensions.DependencyInjection;

namespace Kadr.Platform.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddPlatformApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<RegisterTenantCommandValidator>();
        services.AddSingleton<IRegisterTenantFailureInjector, NoOpRegisterTenantFailureInjector>();
        services.AddSingleton<IPayrollCalculationEngine, PayrollCalculationEngine>();
        return services;
    }
}
