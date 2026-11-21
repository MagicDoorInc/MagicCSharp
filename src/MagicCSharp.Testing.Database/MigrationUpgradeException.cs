namespace MagicCSharp.Testing.Database;

/// <summary>The migrations did not bring the database to the code's latest migration, or a second run changed it.</summary>
public sealed class MigrationUpgradeException(string message) : Exception(message);
