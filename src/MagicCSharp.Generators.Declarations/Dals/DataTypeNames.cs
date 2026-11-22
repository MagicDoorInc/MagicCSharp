namespace MagicCSharp.Generators.Declarations.Dals;

/// <summary>
///     Metadata names of the MagicCSharp data types the Dal generators recognise. Matched by name, so the generators
///     need no reference to the data packages; a consumer's own contracts count when they extend these.
/// </summary>
public static class DataTypeNames
{
    public const string GenerateDalMappingAttribute = "MagicCSharp.Data.EntityFramework.Dals.GenerateDalMappingAttribute";

    public const string DalTransform = "MagicCSharp.Data.EntityFramework.Dals.IDalTransform`2";

    public const string DalId = "MagicCSharp.Data.EntityFramework.Dals.IDalId";

    public const string DalKey = "MagicCSharp.Data.EntityFramework.Dals.IDalKey";

    public const string DalDeleted = "MagicCSharp.Data.EntityFramework.Dals.IDalDeleted";

    public const string DalFromEdit = "MagicCSharp.Data.EntityFramework.Dals.IDalFromEdit`2";

    public const string Repository = "MagicCSharp.Data.Repositories.IRepository`4";

    public const string KeyAttribute = "System.ComponentModel.DataAnnotations.KeyAttribute";
}
