using Kadr.Platform.Application.Templates;

namespace Kadr.Platform.Application.Abstractions;

public interface IIndustryTemplateSeedSource
{
    IReadOnlyList<IndustryTemplateSeedDocument> LoadAll();
}
