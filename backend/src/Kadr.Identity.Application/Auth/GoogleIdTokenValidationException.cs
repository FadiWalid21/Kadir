namespace Kadr.Identity.Application.Auth;

public sealed class GoogleIdTokenValidationException : Exception
{
    public GoogleIdTokenValidationException(string errorCode, Exception? inner = null)
        : base(errorCode, inner)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}
