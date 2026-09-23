namespace MagicCSharp.Analyzers;

internal static class KnownTypeNames
{
    public const string FrameworkAssemblySegment = "MagicCSharp";

    public const string MagicUseCase = "MagicCSharp.UseCases.IMagicUseCase";
    public const string KeyGenService = "MagicCSharp.Infrastructure.KeyGen.IKeyGenService";
    public const string EventDispatcher = "MagicCSharp.Events.Events.IEventDispatcher";
    public const string EventHandler = "MagicCSharp.Events.Events.IEventHandler`1";

    public const string TimeProvider = "System.TimeProvider";
    public const string DistributedLockProvider = "Medallion.Threading.IDistributedLockProvider";
    public const string Logger = "Microsoft.Extensions.Logging.ILogger";
    public const string GenericLogger = "Microsoft.Extensions.Logging.ILogger`1";
    public const string DbSet = "Microsoft.EntityFrameworkCore.DbSet`1";
    public const string ControllerBase = "Microsoft.AspNetCore.Mvc.ControllerBase";
    public const string FromServicesAttribute = "Microsoft.AspNetCore.Mvc.FromServicesAttribute";
}
