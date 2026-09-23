using Acme.Leasing.Domains.Charges.App.Models;
using Acme.Leasing.Domains.Charges.Models.Entities;
using Acme.Leasing.Domains.Charges.UseCases;
using Acme.Libraries.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace Acme.Leasing.Domains.Charges.App.Controllers;

[ApiController]
[Route("charges")]
public class ChargesController : ControllerBase
{
    [HttpGet]
    public async Task<PaginationDto<ChargeDto>> GetCharges(
        [FromQuery] PaginationRequestDto? pagination,
        [FromQuery] ChargeFilterDto? filter,
        [FromServices] IGetChargesUseCase getCharges)
    {
        var paginationRequest = pagination?.ToRequest() ?? new PaginationRequestDto().ToRequest();
        var chargeFilter = filter?.ToFilter() ?? new ChargeFilter();

        var charges = await getCharges.Execute(paginationRequest, chargeFilter);
        return PaginationDto<ChargeDto>.From(charges, ChargeDto.FromEntity);
    }

    [HttpPost("{id:long}/pay")]
    public async Task<ChargeDto> PayCharge(
        [FromRoute] long id,
        [FromServices] IPayChargeUseCase payCharge)
    {
        var charge = await payCharge.Execute(id);
        return ChargeDto.FromEntity(charge);
    }
}
