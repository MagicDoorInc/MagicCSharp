using Acme.Leasing.Domains.Leases.App.Models;
using Acme.Leasing.Domains.Leases.Models.Entities;
using Acme.Leasing.Domains.Leases.UseCases;
using Acme.Libraries.Web.Models;
using MagicCSharp.Infrastructure.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Acme.Leasing.Domains.Leases.App.Controllers;

[ApiController]
[Route("properties")]
public class PropertiesController : ControllerBase
{
    [HttpPost]
    public async Task<PropertyDto> CreateProperty(
        [FromBody] CreatePropertyDto request,
        [FromServices] ICreatePropertyUseCase createProperty)
    {
        var property = await createProperty.Execute(request.ToRequest());
        return PropertyDto.FromEntity(property);
    }

    [HttpGet("{id:long}")]
    public async Task<PropertyDto> GetProperty(
        [FromRoute] long id,
        [FromServices] IGetPropertiesUseCase getProperties)
    {
        var property = await getProperties.Execute(id);
        NotFoundException.ThrowIfNull(property, id);

        return PropertyDto.FromEntity(property);
    }

    [HttpGet]
    public async Task<PaginationDto<PropertyDto>> GetProperties(
        [FromQuery] PaginationRequestDto? pagination,
        [FromQuery] PropertyFilterDto? filter,
        [FromServices] IGetPropertiesUseCase getProperties)
    {
        var paginationRequest = pagination?.ToRequest() ?? new PaginationRequestDto().ToRequest();
        var propertyFilter = filter?.ToFilter() ?? new PropertyFilter();

        var properties = await getProperties.Execute(paginationRequest, propertyFilter);
        return PaginationDto<PropertyDto>.From(properties, PropertyDto.FromEntity);
    }
}
