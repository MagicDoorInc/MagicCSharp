namespace MagicCSharp.Analyzers.Tests;

public static class KnownTypeStubs
{
    public const string FrameworkSource = """
                                           namespace MagicCSharp.Infrastructure.KeyGen
                                           {
                                               public interface IKeyGenService
                                               {
                                                   long NextId();
                                               }
                                           }

                                           namespace MagicCSharp.UseCases
                                           {
                                               public interface IMagicUseCase
                                               {
                                               }
                                           }

                                           namespace MagicCSharp.Events.Events
                                           {
                                               public interface IEventDispatcher
                                               {
                                               }

                                               public interface IEventHandler<T>
                                               {
                                               }
                                           }
                                           """;

    public const string SiblingSource = """
                                           namespace Acme.Libraries.Shared
                                           {
                                               public interface ILeaseArchiveService
                                               {
                                               }

                                               public interface ILeaseDocumentsRepository
                                               {
                                               }

                                               public class LeaseArchiveEntry
                                               {
                                               }
                                           }
                                           """;

    public const string LookalikeSource = """
                                           namespace AcmeTools
                                           {
                                               public interface IAuditService
                                               {
                                               }
                                           }
                                           """;

    public const string ThirdPartySource = """
                                           namespace Microsoft.Extensions.Logging
                                           {
                                               public interface ILogger
                                               {
                                               }

                                               public interface ILogger<T> : ILogger
                                               {
                                               }
                                           }

                                           namespace Microsoft.EntityFrameworkCore
                                           {
                                               public class DbSet<T>
                                               {
                                               }
                                           }

                                           namespace Microsoft.AspNetCore.Mvc
                                           {
                                               public abstract class ControllerBase
                                               {
                                               }

                                               [System.AttributeUsage(System.AttributeTargets.Parameter)]
                                               public sealed class FromServicesAttribute : System.Attribute
                                               {
                                               }
                                           }

                                           namespace Medallion.Threading
                                           {
                                               public interface IDistributedLockProvider
                                               {
                                               }
                                           }

                                           namespace ThirdParty
                                           {
                                               public class ExternalWidget
                                               {
                                               }

                                               public interface IExternalService
                                               {
                                               }

                                               public abstract class ExternalBase
                                               {
                                                   public abstract bool Enabled { get; }

                                                   public abstract void Configure(int first, int second, int third, int fourth, int fifth);
                                               }
                                           }
                                           """;
}
