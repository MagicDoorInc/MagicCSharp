using System.ComponentModel.DataAnnotations;
using Acme.Leasing.Data.Repositories;
using Acme.Leasing.Domains.Leases.Models.Entities;
using MagicCSharp.UseCases;
using Microsoft.Extensions.Logging;

namespace Acme.Leasing.Domains.Leases.UseCases;

public interface ICreatePropertyUseCase : IMagicUseCase
{
    Task<Property> Execute(CreatePropertyRequest request);
}

public record CreatePropertyRequest
{
    public required string Name { get; init; }
    public required string Address { get; init; }
    public required string TimeZoneId { get; init; }
}

public class CreatePropertyUseCase(
    IPropertiesRepository propertiesRepository,
    ILogger<CreatePropertyUseCase> logger) : ICreatePropertyUseCase
{
    public async Task<Property> Execute(CreatePropertyRequest request)
    {
        logger.LogTrace("Executing: request={request}", request);

        // Checked here rather than on first use, where an unknown zone would surface as a 500 from
        // whichever job first asked what day it is at this property.
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(request.TimeZoneId, out _))
        {
            throw new ValidationException($"'{request.TimeZoneId}' is not a known time zone.");
        }

        return await propertiesRepository.Create(new PropertyEdit
        {
            Name = request.Name,
            Address = request.Address,
            TimeZoneId = request.TimeZoneId,
        });
    }
}
