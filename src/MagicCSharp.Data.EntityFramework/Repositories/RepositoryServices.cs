using MagicCSharp.Infrastructure.KeyGen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MagicCSharp.Data.EntityFramework.Repositories;

/// <summary>
///     What every repository needs, in one constructor parameter: a repository written as
///     <c>XEfRepository(RepositoryServices&lt;TContext&gt; repositoryServices) : BaseIdRepository&lt;…&gt;(repositoryServices)</c>
///     declares no other plumbing. <c>AddRepositoriesFrom&lt;TContext&gt;()</c> registers it.
/// </summary>
/// <typeparam name="TContext">The database context the repositories read and write.</typeparam>
public class RepositoryServices<TContext>(
    IDbContextFactory<TContext> contextFactory,
    IKeyGenService keyGenService,
    TimeProvider timeProvider,
    ILoggerFactory loggerFactory)
    where TContext : DbContext
{
    /// <summary>Opens a short-lived context for each repository call.</summary>
    public IDbContextFactory<TContext> ContextFactory { get; } = contextFactory;

    /// <summary>Gives a new row its id or key.</summary>
    public IKeyGenService KeyGenService { get; } = keyGenService;

    /// <summary>Stamps <c>Created</c>, <c>Updated</c> and <c>Deleted</c>.</summary>
    public TimeProvider TimeProvider { get; } = timeProvider;

    /// <summary>Makes each repository a logger named after its own type.</summary>
    public ILoggerFactory LoggerFactory { get; } = loggerFactory;
}
