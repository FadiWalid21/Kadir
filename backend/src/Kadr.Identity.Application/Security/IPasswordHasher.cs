namespace Kadr.Identity.Application.Security;

public interface IPasswordHasher
{
    string HashPassword(string password);
}
