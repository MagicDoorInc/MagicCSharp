using MagicCSharp.Data.Postgres;

namespace Acme.Leasing.Data.EntityFramework;

/// <summary>
///     Used by <c>dotnet ef</c>, which builds the context without the application's DI container.
///     Reads DB_HOST / DB_PORT / DB_NAME / DB_USER / DB_PASSWORD, falling back to the values below.
/// </summary>
public class MagicLeasingContextFactory() : MagicDbContextFactory<MagicLeasingContext>("leasing");
