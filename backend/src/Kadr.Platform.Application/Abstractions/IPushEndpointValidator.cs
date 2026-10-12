using Kadr.SharedKernel.Results;

namespace Kadr.Platform.Application.Abstractions;

public interface IPushEndpointValidator
{
    Result Validate(string endpoint);
}
