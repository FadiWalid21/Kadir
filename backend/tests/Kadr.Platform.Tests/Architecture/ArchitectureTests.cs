using System.Reflection;
using Kadr.Platform.Application;
using Kadr.Platform.Domain.Notes;
using Kadr.SharedKernel.Domain;
using NetArchTest.Rules;
using Xunit;

namespace Kadr.Platform.Tests.Architecture;

public sealed class ArchitectureTests
{
    private static readonly Assembly DomainAssembly = typeof(TenantNote).Assembly;
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
