namespace Kadr.Identity.Application.Security;

public interface ICommonPasswordList
{
    IReadOnlySet<string> Passwords { get; }
}
