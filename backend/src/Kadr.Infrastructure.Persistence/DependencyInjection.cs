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
using Kadr.Infrastructure.Persistence.Attendance;
using Kadr.Infrastructure.Persistence.Home;
using Kadr.Infrastructure.Persistence.Leave;
using Kadr.Infrastructure.Persistence.Notifications;
using Kadr.Infrastructure.Persistence.Subscriptions;
using Kadr.Platform.Domain.Notifications;
using Kadr.Infrastructure.Persistence.Users;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.PlatformAdmin;
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
        services.AddScoped<PlatformAdminSeeder>();
        services.AddScoped<StatutoryRuleSetSeeder>();
        services.AddScoped<IPlatformAuthService, PlatformAuthService>();
        services.AddScoped<IPlatformTenantQueries, PlatformTenants.PlatformTenantQueries>();
        services.AddScoped<IPlatformTenantCommands, PlatformTenants.PlatformTenantCommands>();
        services.AddScoped<IPlatformPlanService, PlatformPlans.PlatformPlanService>();
        services.AddScoped<IPlatformAuditLogQueries, PlatformAudit.PlatformAuditLogQueries>();
        services.AddScoped<IPlatformHealthService, PlatformHealth.PlatformHealthService>();
        services.AddScoped<ITemplateApplyService, TemplateApplyService>();
        services.AddScoped<ITenantTemplateService, TenantTemplateService>();
        services.AddScoped<IAuditLogWriter, AuditLogWriter>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<PrivilegeGuard>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IUserService, Users.UserService>();
        services.AddScoped<IInvitationService, Invitations.InvitationService>();
        services.AddSingleton<ISubscriptionCache, MemorySubscriptionCache>();
        services.AddScoped<IActiveEmployeeCounter, Employees.ActiveEmployeeCounter>();
        services.AddScoped<IEmployeeService, Employees.EmployeeService>();
        services.AddScoped<IOrgService, Employees.OrgService>();
        services.AddScoped<ILookupService, Lookups.LookupService>();
        services.AddScoped<IBranchSettingsService, Branches.BranchSettingsService>();
        services.AddScoped<IPayComponentService, Payroll.PayComponentService>();
        services.AddScoped<IEmployeeSalaryService, Payroll.EmployeeSalaryService>();
        services.AddScoped<IEmployeeInsuranceService, Payroll.EmployeeInsuranceService>();
        services.AddScoped<ISalaryImportService, Payroll.SalaryImportService>();
        services.AddScoped<IOvertimeMultipliersService, Payroll.OvertimeMultipliersService>();
        services.AddScoped<IPayrollRulesService, Payroll.PayrollRulesService>();
        services.AddScoped<IPayrollRunService, Payroll.PayrollRunService>();

        services.AddScoped<IPlatformLookupService, Lookups.PlatformLookupService>();
        services.AddScoped<IEmployeeImportService, Employees.EmployeeImportService>();
        services.AddScoped<IAttendanceService, AttendanceService>();
        services.AddScoped<IShiftAssignmentRuleService, Attendance.ShiftAssignmentRuleService>();
        services.AddScoped<WorkflowEngine>();
        services.AddScoped<IWorkflowEngine>(sp => sp.GetRequiredService<WorkflowEngine>());
        services.AddScoped<ILeaveService, LeaveService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IPushSubscriptionService, Notifications.PushSubscriptionService>();
        services.AddScoped<IHomeDashboardService, HomeDashboardService>();
        services.AddSingleton<IReportExportService, Reports.ReportExportService>();
        services.AddScoped<IHeadcountReportService, Reports.HeadcountReportService>();
        services.AddScoped<IDomainEventHandler<ApprovalRequiredNotificationRequested>, ApprovalRequiredNotificationHandler>();
        services.AddScoped<IDomainEventHandler<RequestDecisionNotificationRequested>, RequestDecisionNotificationHandler>();
        services.AddSingleton<INotificationDeliveryRunner, NotificationDeliveryRunner>();
        services.AddHostedService<NotificationDeliveryHostedService>();
        services.AddScoped<SystemRolePermissionSeeder>();
        services.AddScoped<ISubscriptionGuard, SubscriptionGuard>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<IDevSubscriptionService, DevSubscriptionService>();
        services.AddSingleton<ITrialExpiryRunner, TrialExpiryRunner>();
        services.AddHostedService<TrialExpiryHostedService>();
        services.AddSingleton<IAttendanceAbsenceRunner, AttendanceAbsenceRunner>();
        services.AddHostedService<AttendanceAbsenceHostedService>();
        services.AddScoped<ICommandDispatcher, CommandDispatcher>();
        services.AddScoped<IEmailUniquenessChecker, EmailUniquenessChecker>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<IGoogleAccountResolver, GoogleAccountResolver>();
        services.AddScoped<IGoogleAuthService, GoogleAuthService>();
        services.AddScoped<ISignInMethodsService, SignInMethodsService>();
        services.AddScoped<IPasswordResetService, PasswordResetService>();
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
