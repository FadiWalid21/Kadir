using Kadr.Identity.Application.Auth;
using Kadr.Infrastructure.Persistence.Auth;
using Kadr.Infrastructure.Persistence.Commands;
using Kadr.Infrastructure.Persistence.Dispatcher;
using Kadr.Infrastructure.Persistence.DomainEvents;
using Kadr.Infrastructure.Persistence.Interceptors;
using Kadr.Infrastructure.Persistence.Seeding;
using Kadr.Infrastructure.Persistence.Audit;
using Kadr.Infrastructure.Persistence.Roles;
using Kadr.Infrastructure.Persistence.Templates;
using Kadr.Infrastructure.Persistence.Tenancy;
using Kadr.Infrastructure.Persistence.Subscriptions;
using Kadr.Infrastructure.Persistence.Users;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Tenants;
using Kadr.SharedKernel.Application;
using Kadr.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kadr.Infrastructure.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddScoped<AmbientTenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<AmbientTenantContext>());
        services.AddScoped<ITenantContextAccessor>(sp => sp.GetRequiredService<AmbientTenantContext>());
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<TenantSaveChangesInterceptor>();
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddScoped<IClientRequestInfo, NullClientRequestInfo>();
        services.AddScoped<PlanSeeder>();
        services.AddScoped<IndustryTemplateSeeder>();
        services.AddScoped<ITemplateApplyService, TemplateApplyService>();
        services.AddScoped<ITenantTemplateService, TenantTemplateService>();
        services.AddScoped<IAuditLogWriter, AuditLogWriter>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<PrivilegeGuard>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IUserService, Users.UserService>();
        services.AddScoped<IInvitationService, Invitations.InvitationService>();
        services.AddSingleton<ISubscriptionCache, MemorySubscriptionCache>();
        services.AddScoped<IActiveEmployeeCounter, StubActiveEmployeeCounter>();
        services.AddScoped<ISubscriptionGuard, SubscriptionGuard>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<IDevSubscriptionService, DevSubscriptionService>();
        services.AddSingleton<ITrialExpiryRunner, TrialExpiryRunner>();
        services.AddHostedService<TrialExpiryHostedService>();
        services.AddScoped<ICommandDispatcher, CommandDispatcher>();
        services.AddScoped<IEmailUniquenessChecker, EmailUniquenessChecker>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<PermissionService>();
        services.AddScoped<IPermissionService>(sp => sp.GetRequiredService<PermissionService>());
        services.AddScoped<IPermissionCacheInvalidator>(sp => sp.GetRequiredService<PermissionService>());
        services.AddScoped<ICommandHandler<RegisterTenantCommand, RegisterTenantResult>, RegisterTenantCommandHandler>();
        services.AddScoped<ICommandHandler<LoginCommand, LoginResult>, LoginCommandHandler>();
        services.AddScoped<ICommandHandler<VerifyEmailCommand>, VerifyEmailCommandHandler>();
        services.AddScoped<ICommandHandler<ResendVerificationCommand>, ResendVerificationCommandHandler>();
        services.AddScoped<ICommandHandler<UpdateTenantSettingsCommand, TenantSettingsResult>, UpdateTenantSettingsCommandHandler>();
        services.AddScoped<ICommandHandler<UploadTenantLogoCommand, TenantLogoResult>, UploadTenantLogoCommandHandler>();
        services.AddScoped<ICommandHandler<DeleteTenantLogoCommand>, DeleteTenantLogoCommandHandler>();

        services.AddDbContext<KadrDbContext>((sp, options) =>
        {
            options
                .UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(
                    sp.GetRequiredService<TenantSaveChangesInterceptor>(),
                    sp.GetRequiredService<AuditSaveChangesInterceptor>());
        });

        return services;
    }
}
