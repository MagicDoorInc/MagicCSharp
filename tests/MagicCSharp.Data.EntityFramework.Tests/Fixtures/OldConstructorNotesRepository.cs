using MagicCSharp.Data.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MagicCSharp.Data.EntityFramework.Tests.Fixtures;

/// <summary>
///     Built through the constructor that predates <see cref="RepositoryServices{TContext}" />, and relying on the
///     default <c>CreateDal</c> anyway — which has no key service to draw an id from.
/// </summary>
public class OldConstructorNotesRepository(
    IDbContextFactory<TestDbContext> contextFactory,
    TimeProvider timeProvider,
    ILoggerFactory loggerFactory)
    : BaseIdRepository<TestDbContext, NoteDal, Note, NoteFilter, NoteEdit>(contextFactory, timeProvider, loggerFactory)
{
    protected override IQueryable<NoteDal> ApplyFilter(IQueryable<NoteDal> query, NoteFilter filter)
    {
        return query;
    }
}
