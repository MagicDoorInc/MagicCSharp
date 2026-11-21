using MagicCSharp.Clients.Tests.Fakes;
using MagicCSharp.Clients.Tests.Leases;
using MagicCSharp.Testing;

namespace MagicCSharp.Clients.Tests;

public class FakeBaseTests
{
    [Fact]
    public async Task FakeBase_ThrowsNotScripted_NamingTheMemberAndItsParameterTypes()
    {
        // Arrange
        ILeasesClient fakeLeasesClient = new FakeLeasesClient();

        // Act
        var notScriptedException = await Assert.ThrowsAsync<NotScriptedException>(() =>
            fakeLeasesClient.GetLeases(new LeaseFilter(), new PageRequest { Page = 1, PageSize = 10 }));

        // Assert
        Assert.Equal("ILeasesClient.GetLeases(LeaseFilter, PageRequest)", notScriptedException.Member);
        Assert.Equal("ILeasesClient.GetLeases(LeaseFilter, PageRequest) is not scripted. Override it in the fake.", notScriptedException.Message);
    }

    [Fact]
    public async Task FakeBase_RunsTheFakesOverride()
    {
        // Arrange
        ILeasesClient fakeLeasesClient = new FakeLeasesClient();

        // Act
        var leaseDto = await fakeLeasesClient.GetLease(7, 42);

        // Assert
        Assert.Equal(new LeaseDto { Id = 7, TenantName = "Ana Lee" }, leaseDto);
    }

    [Fact]
    public void FakeBase_KeepsTheDeclaredBaseType()
    {
        // Arrange
        var fakeLeasesClient = new FakeLeasesClient();

        // Act
        fakeLeasesClient.Reset();

        // Assert
        Assert.Equal(typeof(ResettableFake), typeof(FakeLeasesClientBase).BaseType);
        Assert.Equal(1, fakeLeasesClient.ResetCount);
    }

    [Fact]
    public async Task FakeBase_StubsMembersOfInheritedInterfaces()
    {
        // Arrange
        ILeaseNotes fakeLeaseNotes = new FakeLeaseNotes();

        // Act
        var notScriptedException = await Assert.ThrowsAsync<NotScriptedException>(() => fakeLeaseNotes.FindLease(7));

        // Assert
        Assert.Equal("ILeaseLookup.FindLease(long)", notScriptedException.Member);
    }

    [Fact]
    public void FakeBase_LeavesMembersTheBaseClassImplements()
    {
        // Arrange
        ILeaseNotes fakeLeaseNotes = new FakeLeaseNotes();

        // Act
        fakeLeaseNotes.Region = "North";

        // Assert
        Assert.Equal("North", fakeLeaseNotes.Region);
    }
}
