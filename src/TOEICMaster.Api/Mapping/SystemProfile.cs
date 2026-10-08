using AutoMapper;
using TOEICMaster.Api.Controllers;
using TOEICMaster.Application.System.Queries.GetSystemStatus;

namespace TOEICMaster.Api.Mapping;

public sealed class SystemProfile : Profile
{
    public SystemProfile() => CreateMap<SystemStatusDto, PingResponse>();
}
