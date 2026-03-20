using AutoMapper;
using eSale.Application.Common.Interfaces;
using eSale.Application.Modules.Auth.DTOs;
using eSale.Domain.Modules.Auth.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace eSale.Application.Modules.Auth.Commands;

public sealed record RegisterCommand(
    string FirstName,
    string LastName,
    string Email,
    string Password) : IRequest<AuthResponseDto>;

public sealed class RegisterCommandHandler : IRequestHandler<RegisterCommand, AuthResponseDto>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ITenantProvider _tenantProvider;
    private readonly IMapper _mapper;

    public RegisterCommandHandler(
        UserManager<ApplicationUser> userManager,
        IJwtTokenService jwtTokenService,
        ITenantProvider tenantProvider,
        IMapper mapper)
    {
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
        _tenantProvider = tenantProvider;
        _mapper = mapper;
    }

    public async Task<AuthResponseDto> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var user = _mapper.Map<ApplicationUser>(request);
        user.TenantId = _tenantProvider.GetTenantId();

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new FluentValidation.ValidationException(errors);
        }

        var token = _jwtTokenService.GenerateToken(
            user.Id, user.Email!, user.FirstName, user.LastName, user.TenantId);

        return new AuthResponseDto(token, user.Id, user.Email!, user.FirstName, user.LastName);
    }
}
