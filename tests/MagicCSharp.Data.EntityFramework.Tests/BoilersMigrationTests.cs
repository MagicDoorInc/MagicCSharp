using MagicCSharp.Data.EntityFramework.Tests.Fixtures.Boilers;
using MagicCSharp.Testing.Database;
using Xunit.Abstractions;

namespace MagicCSharp.Data.EntityFramework.Tests;

/// <summary>The one-line migration test a context gets: its migrations upgrade an empty database to the latest one.</summary>
[Trait("Category", "Database")]
public class BoilersMigrationTests(ITestOutputHelper output) : MigrationTestBase<BoilersDbContext>(output);
