using MagicCSharp.Data.Repositories;
using MagicCSharp.Infrastructure.Exceptions;
using Xunit;

namespace MagicCSharp.Tests;

public class RepositoryExtensionsTests
{
    [Fact]
    public async Task Returns_the_entity_when_it_is_there()
    {
        var repository = new StubRepository<long> { Stored = new Thing { Id = 5 } };

        Assert.Equal(5, (await repository.GetOrThrow(5L)).Id);
    }

    [Fact]
    public async Task Throws_when_it_is_not()
    {
        var repository = new StubRepository<long>();

        await Assert.ThrowsAsync<NotFoundIdException>(() => repository.GetOrThrow(5L));
    }

    [Fact]
    public async Task The_exception_names_the_id_that_was_missing()
    {
        var repository = new StubRepository<long>();

        var thrown = await Assert.ThrowsAsync<NotFoundIdException>(() => repository.GetOrThrow(5L));

        Assert.Equal(5, thrown.Id);
        Assert.Contains("Thing", thrown.Message);
    }

    [Fact]
    public async Task A_string_keyed_entity_reports_its_key()
    {
        var repository = new StubRepository<string>();

        var thrown = await Assert.ThrowsAsync<NotFoundKeyException>(() => repository.GetOrThrow("abc"));

        Assert.Equal("abc", thrown.Key);
    }

    private class Thing
    {
        public long Id { get; init; }
    }

    private class StubRepository<TKey> : IRepository<Thing, TKey, Thing, object>
        where TKey : IEquatable<TKey>
    {
        public Thing? Stored { get; init; }

        public Task<Thing?> Get(TKey key) => Task.FromResult(Stored);

        public Task<int> Count(object filter) => throw new NotSupportedException();
        public Task<IReadOnlyList<TKey>> GetKeys(object filter) => throw new NotSupportedException();
        public Task<IReadOnlyList<Thing>> Get(object filter) => throw new NotSupportedException();
        public Task<IReadOnlyList<Thing>> Get(IReadOnlyList<TKey> keys) => throw new NotSupportedException();
        public Task<Thing> Create(Thing edit) => throw new NotSupportedException();
        public Task<IReadOnlyList<TKey>> Create(IReadOnlyList<Thing> edits) => throw new NotSupportedException();
        public Task<Thing> Update(TKey key, Thing edit) => throw new NotSupportedException();
        public Task<int> Update(IReadOnlyDictionary<TKey, Thing> edits) => throw new NotSupportedException();
        public Task<Thing> Update(Thing entity) => throw new NotSupportedException();
        public Task<int> Update(IReadOnlyList<Thing> entities) => throw new NotSupportedException();
        public Task<int> Delete(TKey key) => throw new NotSupportedException();
        public Task<int> Delete(IReadOnlyList<TKey> keys) => throw new NotSupportedException();
        public Task<int> Delete(object filter) => throw new NotSupportedException();
    }
}
