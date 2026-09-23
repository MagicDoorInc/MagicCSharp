using System.ComponentModel.DataAnnotations;
using Acme.Leasing.Domains.Leases.UseCases;
using Acme.Leasing.Testing;

namespace Acme.Leasing.Domains.Leases.Tests.UseCases;

public class CreatePropertyUseCaseTests : LeasingTestBase
{
    public CreatePropertyUseCaseTests()
    {
        TimeProvider.SetUtcNow(new DateTimeOffset(2026, 3, 1, 18, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task Execute_StoresTheProperty()
    {
        // Arrange
        var createProperty = Resolve<ICreatePropertyUseCase>();
        var getProperties = Resolve<IGetPropertiesUseCase>();

        // Act
        var property = await createProperty.Execute(new CreatePropertyRequest
        {
            Name = "Harbour View",
            Address = "3 Nyhavn, Copenhagen",
            TimeZoneId = "Europe/Copenhagen",
        });

        // Assert
        var storedProperty = await getProperties.Execute(property.Id);
        Assert.NotNull(storedProperty);
        Assert.Equal("Harbour View", storedProperty.Name);
        Assert.Equal("Europe/Copenhagen", storedProperty.TimeZoneId);
    }

    [Fact]
    public async Task Execute_Throws_WhenTheTimeZoneIsUnknown()
    {
        // Arrange
        var createProperty = Resolve<ICreatePropertyUseCase>();

        // Act
        var creating = createProperty.Execute(new CreatePropertyRequest
        {
            Name = "Harbour View",
            Address = "3 Nyhavn, Copenhagen",
            TimeZoneId = "Mars/Olympus_Mons",
        });

        // Assert
        await Assert.ThrowsAsync<ValidationException>(() => creating);
    }
}
