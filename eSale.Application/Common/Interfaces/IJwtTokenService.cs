namespace eSale.Application.Common.Interfaces;

public interface IJwtTokenService
{
    string GenerateToken(string userId, string email, string firstName, string lastName, Guid tenantId);
}
