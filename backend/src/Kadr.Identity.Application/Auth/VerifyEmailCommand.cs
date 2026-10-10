using Kadr.SharedKernel.Application;

namespace Kadr.Identity.Application.Auth;

public sealed record VerifyEmailCommand(string Token) : ICommand;

public sealed record ResendVerificationCommand(string Email, string CompanySlug) : ICommand;
