namespace TOEICMaster.Application.Common.Security;

public interface IPasswordHasher
{
    string HashPassword(string password);

    bool VerifyHashedPassword(string hashedPassword, string providedPassword);
}
