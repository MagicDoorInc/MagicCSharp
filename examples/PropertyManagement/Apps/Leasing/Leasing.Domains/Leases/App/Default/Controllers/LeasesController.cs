using Acme.Leasing.Domains.Leases.App.Models;
using Acme.Leasing.Domains.Leases.Models.Entities;
using Acme.Leasing.Domains.Leases.UseCases;
using Acme.Libraries.Web.Models;
using MagicCSharp.Infrastructure.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Acme.Leasing.Domains.Leases.App.Controllers;

[ApiController]
[Route("leases")]
public class LeasesController : ControllerBase
{
    /// <summary>Signs a lease: creates it and raises the first month's rent and the deposit.</summary>
    [HttpPost]
    public async Task<LeaseDto> SignLease(
        [FromBody] SignLeaseDto request,
        [FromServices] ISignLeaseUseCase signLease)
    {
        var result = await signLease.Execute(request.ToRequest());
        return LeaseDto.FromEntity(result.Lease);
    }

    [HttpGet("{id:long}")]
    public async Task<LeaseDto> GetLease(
        [FromRoute] long id,
        [FromServices] IGetLeasesUseCase getLeases)
    {
        var lease = await getLeases.Execute(id);
        NotFoundException.ThrowIfNull(lease, id);

        return LeaseDto.FromEntity(lease);
    }

    [HttpGet]
    public async Task<PaginationDto<LeaseDto>> GetLeases(
        [FromQuery] PaginationRequestDto? pagination,
        [FromQuery] LeaseFilterDto? filter,
        [FromServices] IGetLeasesUseCase getLeases)
    {
        var paginationRequest = pagination?.ToRequest() ?? new PaginationRequestDto().ToRequest();
        var leaseFilter = filter?.ToFilter() ?? new LeaseFilter();

        var leases = await getLeases.Execute(paginationRequest, leaseFilter);
        return PaginationDto<LeaseDto>.From(leases, LeaseDto.FromEntity);
    }
}
