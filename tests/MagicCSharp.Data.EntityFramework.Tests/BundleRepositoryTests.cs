using MagicCSharp.Data.EntityFramework.Repositories;
using MagicCSharp.Data.EntityFramework.Tests.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;

namespace MagicCSharp.Data.EntityFramework.Tests;

/// <summary>
///     A repository built from one <see cref="RepositoryServices{TContext}" />, over a Dal whose mapping is generated:
///     what it writes and reads back.
/// </summary>
public class BundleRepositoryTests : RepositoryTestBase
{
    private NotesRepository Notes => new NotesRepository(new RepositoryServices<TestDbContext>(DbContextFactory, KeyGen, TimeProvider, NullLoggerFactory.Instance));

    [Fact]
    public async Task BundleRepository_RoundTripsEveryColumn_ThroughAGeneratedDal()
    {
        // Arrange
        var pinned = new DateTimeOffset(2026, 3, 2, 9, 30, 0, TimeSpan.FromHours(-8));
        var created = await Notes.Create(ANote(7, "Boiler serviced"));
        TimeProvider.Advance(TimeSpan.FromMinutes(5));

        // Act
        await Notes.Update(created.Id, ANote(7, "Boiler checked again") with { Pinned = pinned });
        var note = await Notes.Get(created.Id);

        // Assert
        Assert.NotNull(note);
        Assert.NotEqual(0, created.Id);
        Assert.Equal(created.Id, note.Id);
        Assert.Equal(7, note.TenantId);
        Assert.Equal("Boiler checked again", note.Text);
        Assert.Equal(pinned, note.Pinned);
        Assert.Equal(created.Created, note.Created);
        Assert.Equal(created.Created.AddMinutes(5), note.Updated);
        Assert.Null(note.Deleted);
    }

    [Fact]
    public async Task BundleRepository_Update_KeepsTheStoredTenantId_WhenTheEditCarriesAnother()
    {
        // Arrange
        var created = await Notes.Create(ANote(7, "Boiler serviced"));

        // Act
        var updated = await Notes.Update(created.Id, ANote(8, "Boiler checked"));

        // Assert: the text moved, the note did not.
        Assert.Equal("Boiler checked", updated.Text);
        Assert.Equal(7, updated.TenantId);
        Assert.Equal("7", await ScalarText($"SELECT tenant_id FROM notes WHERE id = {created.Id}"));
    }

    [Fact]
    public async Task OldConstructorRepository_ThrowsNamingCreateDal_WhenItReliesOnTheFromDefault()
    {
        // Arrange
        var oldConstructorNotesRepository = new OldConstructorNotesRepository(DbContextFactory, TimeProvider, NullLoggerFactory.Instance);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => oldConstructorNotesRepository.Create(ANote(7, "Boiler serviced")));

        // Assert
        Assert.Contains("OldConstructorNotesRepository was built without RepositoryServices", exception.Message);
        Assert.Contains("override CreateDal", exception.Message);
    }

    private static NoteEdit ANote(long tenantId, string text)
    {
        return new NoteEdit { TenantId = tenantId, Text = text };
    }
}
