using eSale.Application.Common.Interfaces;
using eSale.Application.Modules.Auth.DTOs;
using eSale.Domain.Modules.Auth.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace eSale.Application.Modules.Auth.Commands;

public sealed record LoginCommand(string Email, string Password) : IRequest<AuthResponseDto>;

public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResponseDto>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ITenantProvider _tenantProvider;

    public LoginCommandHandler(
        UserManager<ApplicationUser> userManager,
        IJwtTokenService jwtTokenService,
        ITenantProvider tenantProvider)
    {
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
        _tenantProvider = tenantProvider;
    }

    public async Task<AuthResponseDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetTenantId();

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
            throw new UnauthorizedAccessException("Invalid email or password.");

        var validPassword = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!validPassword)
            throw new UnauthorizedAccessException("Invalid email or password.");

        var token = _jwtTokenService.GenerateToken(
            user.Id, user.Email!, user.FirstName, user.LastName, tenantId);

        return new AuthResponseDto(token, user.Id, user.Email!, user.FirstName, user.LastName);
    }
}
