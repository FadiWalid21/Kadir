namespace Kadr.Api.Localization;

public interface IErrorLocalizer
{
    string GetDetail(string errorCode, string? acceptLanguage);
}
