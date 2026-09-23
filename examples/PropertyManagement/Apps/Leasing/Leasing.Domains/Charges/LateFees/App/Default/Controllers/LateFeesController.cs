using Acme.Leasing.Domains.Charges.LateFees.App.Models;
using Acme.Leasing.Domains.Charges.LateFees.UseCases;
using MagicCSharp.Infrastructure.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Acme.Leasing.Domains.Charges.LateFees.App.Controllers;

[ApiController]
[Route("late-fees")]
public class LateFeesController : ControllerBase
{
    [HttpPut("policies/{propertyId:long}")]
    public async Task<LateFeePolicyDto> SetLateFeePolicy(
        [FromRoute] long propertyId,
        [FromBody] SetLateFeePolicyDto request,
        [FromServices] ISetLateFeePolicyUseCase setLateFeePolicy)
    {
        var lateFeePolicy = await setLateFeePolicy.Execute(propertyId, request.ToRequest());
        return LateFeePolicyDto.FromEntity(lateFeePolicy);
    }

    [HttpGet("policies/{propertyId:long}")]
    public async Task<LateFeePolicyDto> GetLateFeePolicy(
        [FromRoute] long propertyId,
        [FromServices] IGetLateFeePoliciesUseCase getLateFeePolicies)
    {
        var lateFeePolicy = await getLateFeePolicies.Execute(propertyId);
        NotFoundException.ThrowIfNull(lateFeePolicy, propertyId);

        return LateFeePolicyDto.FromEntity(lateFeePolicy);
    }

    /// <summary>
    ///     Runs now what the hourly job runs. Here so the flow can be tried without waiting for the hour;
    ///     running it twice charges nothing twice.
    /// </summary>
    [HttpPost("apply")]
    public async Task<ApplyLateFeesResultDto> ApplyLateFees(
        [FromServices] IApplyLateFeesUseCase applyLateFees)
    {
        var result = await applyLateFees.Execute();
        return ApplyLateFeesResultDto.FromResult(result);
    }
}
