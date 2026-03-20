namespace eSale.Application.Modules.Auth.DTOs;

public sealed record AuthResponseDto(
    string Token,
    string UserId,
    string Email,
    string FirstName,
    string LastName);
