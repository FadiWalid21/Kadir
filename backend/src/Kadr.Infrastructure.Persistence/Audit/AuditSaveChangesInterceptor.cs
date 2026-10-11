using System.Text.Json;
using System.Text.Json.Serialization;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Domain.Audit;
using Kadr.SharedKernel.Domain;
using Kadr.SharedKernel.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Kadr.Infrastructure.Persistence.Audit;

public sealed class AuditSaveChangesInterceptor(
    ITenantContext tenantContext,
    IClientRequestInfo clientInfo) : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        AppendAuditEntries(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        AppendAuditEntries(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AppendAuditEntries(DbContext? context)
    {
        if (context is null || !tenantContext.HasTenant || tenantContext.TenantId is null)
        {
            return;
        }

        var tenantId = tenantContext.TenantId.Value;
        var userId = tenantContext.UserId;

        var explicitEntityIds = context.ChangeTracker.Entries<AuditLog>()
            .Where(e => e.State == EntityState.Added && e.Entity.EntityId.HasValue)
            .Select(e => e.Entity.EntityId!.Value)
            .ToHashSet();

        var candidates = context.ChangeTracker.Entries()
            .Where(e =>
                e.Entity is IAuditable
                && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        foreach (var entry in candidates)
        {
            if (entry.Entity is not Entity entity)
            {
                continue;
            }

            if (explicitEntityIds.Contains(entity.Id))
            {
                continue;
            }

            var action = entry.State switch
            {
                EntityState.Added => "entity.created",
                EntityState.Modified => "entity.updated",
                EntityState.Deleted => "entity.deleted",
                _ => null,
            };

            if (action is null)
            {
                continue;
            }

            var entityType = entry.Metadata.ClrType.Name;
            string? oldJson = null;
            string? newJson = null;

            if (entry.State == EntityState.Added)
            {
                newJson = Serialize(Redact(ToDictionary(entry, useOriginal: false)));
            }
            else if (entry.State == EntityState.Deleted)
            {
                oldJson = Serialize(Redact(ToDictionary(entry, useOriginal: true)));
            }
            else
            {
                var oldValues = new Dictionary<string, object?>(StringComparer.Ordinal);
                var newValues = new Dictionary<string, object?>(StringComparer.Ordinal);

                foreach (var prop in entry.Properties)
                {
                    if (!prop.IsModified || IsDenied(prop.Metadata.Name))
                    {
                        continue;
                    }

                    oldValues[prop.Metadata.Name] = Normalize(prop.OriginalValue);
                    newValues[prop.Metadata.Name] = Normalize(prop.CurrentValue);
                }

                if (oldValues.Count == 0)
                {
                    continue;
                }

                oldJson = Serialize(oldValues);
                newJson = Serialize(newValues);
            }

            context.Set<AuditLog>().Add(AuditLog.Create(
                tenantId,
                userId,
                action,
                entityType,
                entity.Id,
                oldJson,
                newJson,
                clientInfo.IpAddress,
                clientInfo.UserAgent));

            if (entity.Id != Guid.Empty)
            {
                explicitEntityIds.Add(entity.Id);
            }
        }
    }

    private static Dictionary<string, object?> ToDictionary(EntityEntry entry, bool useOriginal)
    {
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var prop in entry.Properties)
        {
            if (IsDenied(prop.Metadata.Name))
            {
                continue;
            }

            dict[prop.Metadata.Name] = Normalize(useOriginal ? prop.OriginalValue : prop.CurrentValue);
        }

        return dict;
    }

    private static Dictionary<string, object?> Redact(Dictionary<string, object?> values) => values;

    private static bool IsDenied(string propertyName)
    {
        if (string.Equals(propertyName, "PasswordHash", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return propertyName.Contains("Token", StringComparison.OrdinalIgnoreCase)
               || propertyName.Contains("Password", StringComparison.OrdinalIgnoreCase)
               || propertyName.Contains("Secret", StringComparison.OrdinalIgnoreCase);
    }

    private static object? Normalize(object? value) =>
        value switch
        {
            null => null,
            DateTimeOffset dto => dto.ToString("O"),
            DateTime dt => DateTime.SpecifyKind(dt, DateTimeKind.Utc).ToString("O"),
            Guid g => g.ToString(),
            Enum e => e.ToString(),
            _ => value,
        };

    private static string? Serialize(Dictionary<string, object?> values) =>
        values.Count == 0 ? null : JsonSerializer.Serialize(values, JsonOptions);
}
