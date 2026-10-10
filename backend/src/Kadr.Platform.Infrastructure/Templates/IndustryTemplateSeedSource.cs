using System.Reflection;
using System.Text.Json;
using Kadr.Platform.Application.Abstractions;
using Kadr.Platform.Application.Templates;

namespace Kadr.Platform.Infrastructure.Templates;

public sealed class IndustryTemplateSeedSource : IIndustryTemplateSeedSource
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly Lazy<IReadOnlyList<IndustryTemplateSeedDocument>> _documents = new(LoadEmbedded);

    public IReadOnlyList<IndustryTemplateSeedDocument> LoadAll() => _documents.Value;

    private static IReadOnlyList<IndustryTemplateSeedDocument> LoadEmbedded()
    {
        var assembly = typeof(IndustryTemplateSeedSource).Assembly;
        var names = assembly.GetManifestResourceNames()
            .Where(n => n.Contains(".Templates.Seeds.", StringComparison.Ordinal)
                        && n.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        if (names.Count == 0)
        {
            throw new InvalidOperationException("No industry template seed resources were embedded.");
        }

        var list = new List<IndustryTemplateSeedDocument>(names.Count);
        foreach (var name in names)
        {
            using var stream = assembly.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException($"Missing seed resource {name}.");
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            var doc = JsonSerializer.Deserialize<IndustryTemplateSeedDocument>(json, JsonOptions)
                ?? throw new InvalidOperationException($"Invalid seed resource {name}.");
            list.Add(doc);
        }

        return list;
    }
}
