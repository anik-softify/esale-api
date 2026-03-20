using AutoMapper;
using eSale.Application.Modules.Auth.Commands;
using eSale.Domain.Modules.Auth.Entities;

namespace eSale.Application.Modules.Auth.Mappings;

public sealed class AuthMappingProfile : Profile
{
    public AuthMappingProfile()
    {
        CreateMap<RegisterCommand, ApplicationUser>()
            .ForMember(dest => dest.UserName, opt => opt.MapFrom(src => src.Email));
    }
}
