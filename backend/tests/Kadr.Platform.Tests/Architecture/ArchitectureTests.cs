using System.Reflection;
using Kadr.Platform.Application;
using Kadr.Platform.Domain.Tenants;
using Kadr.SharedKernel.Domain;
using NetArchTest.Rules;
using Xunit;

namespace Kadr.Platform.Tests.Architecture;

public sealed class ArchitectureTests
{
    private static readonly Assembly DomainAssembly = typeof(Tenant).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(DependencyInjection).Assembly;
    private static readonly Assembly SharedKernelAssembly = typeof(AggregateRoot).Assembly;

    [Fact]
    public void Domain_should_not_reference_application_infrastructure_ef_or_aspnet()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Kadr.Platform.Application",
                "Kadr.Platform.Infrastructure",
                "Kadr.Infrastructure.Persistence",
                "Kadr.Identity.Application",
                "Kadr.Identity.Infrastructure",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "Npgsql")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(Environment.NewLine, result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Application_should_not_reference_infrastructure_or_ef()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Kadr.Platform.Infrastructure",
                "Kadr.Infrastructure.Persistence",
                "Microsoft.EntityFrameworkCore",
                "Npgsql")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(Environment.NewLine, result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Aggregate_roots_should_not_expose_public_property_setters()
    {
        var aggregates = DomainAssembly
            .GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsAssignableTo(typeof(AggregateRoot)))
            .ToList();

        Assert.NotEmpty(aggregates);

        var violations = aggregates
            .SelectMany(type => type
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.SetMethod is { IsPublic: true })
                .Select(p => $"{type.Name}.{p.Name}"))
            .ToList();

        Assert.True(violations.Count == 0, "Public setters found: " + string.Join(", ", violations));
    }

    /// <summary>
    /// Files under src/ that may call IgnoreQueryFilters. Each performs a cross-tenant lookup that happens
    /// before a tenant context exists (login-adjacent flows, background jobs) and is reviewed individually.
    /// </summary>
    private static readonly HashSet<string> IgnoreQueryFiltersAllowList = new(StringComparer.Ordinal)
    {
        "GoogleAccountResolver.cs",
        "GoogleAuthService.cs",
        "PasswordResetService.cs",
        "RefreshTokenService.cs",
        "InvitationService.cs",
        "TrialExpiryRunner.cs",
        "VerifyEmailCommandHandler.cs",
        "PlatformTenantQueries.cs",
        "PlatformAuditLogQueries.cs",
        "ActiveEmployeeCounter.cs",
        "SystemRolePermissionSeeder.cs",
    };

    [Fact]
    public void IgnoreQueryFilters_should_only_be_called_from_the_allow_listed_services()
    {
        var srcRoot = FindSourceRoot();
        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(srcRoot, file);
            var segments = relative.Split(Path.DirectorySeparatorChar);
            if (segments.Contains("obj") || segments.Contains("bin") || segments.Contains("Migrations"))
            {
                continue;
            }

            var callSites = File.ReadLines(file)
                .Select((line, index) => (Line: line.Trim(), Number: index + 1))
                .Where(x => !x.Line.StartsWith("//", StringComparison.Ordinal)
                            && x.Line.Contains("IgnoreQueryFilters(", StringComparison.Ordinal))
                .ToList();

            if (callSites.Count > 0 && !IgnoreQueryFiltersAllowList.Contains(Path.GetFileName(file)))
            {
                violations.AddRange(callSites.Select(x => $"{relative}:{x.Number}"));
            }
        }

        Assert.True(
            violations.Count == 0,
            "IgnoreQueryFilters is only allowed in: "
            + string.Join(", ", IgnoreQueryFiltersAllowList.Order())
            + ". Found in: " + string.Join(", ", violations));
    }

    [Fact]
    public void IgnoreQueryFilters_allow_list_should_not_contain_stale_entries()
    {
        var srcRoot = FindSourceRoot();
        var usingFiles = Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}"))
            .Where(f => File.ReadLines(f).Any(l =>
                !l.TrimStart().StartsWith("//", StringComparison.Ordinal)
                && l.Contains("IgnoreQueryFilters(", StringComparison.Ordinal)))
            .Select(Path.GetFileName)
            .ToHashSet(StringComparer.Ordinal);

        var stale = IgnoreQueryFiltersAllowList.Where(name => !usingFiles.Contains(name)).ToList();

        Assert.True(stale.Count == 0, "Allow-list entries without a call site: " + string.Join(", ", stale));
    }

    private static string FindSourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src");
            if (File.Exists(Path.Combine(directory.FullName, "Kadr.slnx")) && Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the backend src directory from " + AppContext.BaseDirectory);
    }

    [Fact]
    public void SharedKernel_should_not_reference_ef_or_aspnet()
    {
        var result = Types.InAssembly(SharedKernelAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Npgsql")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(Environment.NewLine, result.FailingTypeNames ?? []));
    }
}
