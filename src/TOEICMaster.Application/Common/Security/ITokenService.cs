namespace TOEICMaster.Application.Common.Security;

public interface ITokenService
{
    string GenerateToken(Guid userId, string email, IEnumerable<string> roles);
}
