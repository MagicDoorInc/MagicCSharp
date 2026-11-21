using MagicCSharp.Data.EntityFramework.Repositories;
using MagicCSharp.Data.Repositories;

namespace MagicCSharp.Data.EntityFramework.Tests.Fixtures;

/// <summary>A destroyer contract of a consumer's own, as a codebase names its tenant ("company") has one.</summary>
public interface INoteTenantDestroyer : ITenantScopedDestroyer;

public interface INotesRepository :
    IRepository<Note, long, NoteEdit, NoteFilter>,
    ISoftDeleteRepository<Note, long, NoteFilter>,
    INoteTenantDestroyer;

/// <summary>
///     A repository on the bundle: no constructor plumbing, no <c>CreateDal</c>, no <c>DestroyTenant</c>. Only the
///     filter is its own.
/// </summary>
public class NotesRepository(RepositoryServices<TestDbContext> repositoryServices)
    : BaseIdSoftDeleteRepository<TestDbContext, NoteDal, Note, NoteFilter, NoteEdit>(repositoryServices), INotesRepository
{
    protected override IQueryable<NoteDal> ApplyFilter(IQueryable<NoteDal> query, NoteFilter filter)
    {
        if (filter.TenantId != null)
        {
            query = query.Where(note => note.TenantId == filter.TenantId);
        }

        return query;
    }
}
