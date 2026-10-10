namespace Kadr.SharedKernel.Domain;

/// <summary>Copied from an industry template into a real tenant table.</summary>
public interface ITemplateSourced
{
    string? SourceKey { get; }
    Guid? SourceTemplateId { get; }
    int SourceVersion { get; }
    bool IsCustomized { get; }
    bool IsActive { get; }
}
