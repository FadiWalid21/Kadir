using FluentValidation;
using Kadr.SharedKernel.Constants;

namespace Kadr.Platform.Application.Branches;

public static class BranchLimits
{
    public const int MaxNameLength = 200;
    public const int MaxAddressLength = 500;
}

public sealed class CreateBranchRequestValidator : AbstractValidator<CreateBranchRequest>
{
    public CreateBranchRequestValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty()
            .WithErrorCode(ErrorCodes.BranchesValidationFailed);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(BranchLimits.MaxNameLength)
            .WithErrorCode(ErrorCodes.BranchesValidationFailed);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(BranchLimits.MaxNameLength)
            .WithErrorCode(ErrorCodes.BranchesValidationFailed);
        RuleFor(x => x.Address).MaximumLength(BranchLimits.MaxAddressLength)
            .When(x => x.Address is not null)
            .WithErrorCode(ErrorCodes.BranchesValidationFailed);
    }
}

public sealed class UpdateBranchRequestValidator : AbstractValidator<UpdateBranchRequest>
{
    public UpdateBranchRequestValidator()
    {
        RuleFor(x => x.LegalEntityId).NotEmpty()
            .WithErrorCode(ErrorCodes.BranchesValidationFailed);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(BranchLimits.MaxNameLength)
            .WithErrorCode(ErrorCodes.BranchesValidationFailed);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(BranchLimits.MaxNameLength)
            .WithErrorCode(ErrorCodes.BranchesValidationFailed);
        RuleFor(x => x.Address).MaximumLength(BranchLimits.MaxAddressLength)
            .When(x => x.Address is not null)
            .WithErrorCode(ErrorCodes.BranchesValidationFailed);
    }
}
