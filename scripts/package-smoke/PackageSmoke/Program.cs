using MagicCSharp.Data.EntityFramework.Repositories;
using MagicCSharp.Modules;
using MagicCSharp.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using PackageSmoke.Library;

namespace PackageSmoke;

/// <summary>
///     Uses every generator from the packed packages and exits 1 naming each one that did not run or wrote the
///     wrong thing. Each check is its own method, so <c>Main</c> touches no type of <c>PackageSmoke.Library</c> before
///     the loader check.
/// </summary>
public static class Program
{
    public static async Task<int> Main()
    {
        var failures = new List<string>();

        CheckTheLoaderLoadedTheLibraryBeforeMain(failures);
        await CheckTheGeneratedInterfaceWritesItsExecutingLine(failures);
        CheckTheDalMappingThroughTheDefaultCreateDal(failures);
        CheckTheGeneratedFilterQuery(failures);
        await CheckTheGeneratedMatchesInMemory(failures);
        await CheckTheGeneratedClient(failures);
        await CheckTheGeneratedFakeBase(failures);

        foreach (var failure in failures)
        {
            Console.Error.WriteLine("FAILED: " + failure);
        }

        if (failures.Count > 0)
        {
            return 1;
        }

        Console.WriteLine("Package smoke check passed: every generator ran from the packed packages.");
        return 0;
    }

    private static void CheckTheLoaderLoadedTheLibraryBeforeMain(List<string> failures)
    {
        var isLibraryLoaded = AppDomain.CurrentDomain.GetAssemblies()
            .Any(assembly => assembly.GetName().Name == "PackageSmoke.Library");

        Expect(failures, isLibraryLoaded, "the assembly loader loads PackageSmoke.Library before Main");
    }

    private static async Task CheckTheGeneratedInterfaceWritesItsExecutingLine(List<string> failures)
    {
        var recordingLoggerProvider = new RecordingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(loggingBuilder => loggingBuilder.AddProvider(recordingLoggerProvider).SetMinimumLevel(LogLevel.Trace));
        services.AddMagicUseCases(assembly => assembly == typeof(RenameWidgetUseCase).Assembly);
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var renameWidget = scope.ServiceProvider.GetRequiredService<IRenameWidgetUseCase>();

        var renamed = await renameWidget.Execute(7, "Lamp");

        Expect(failures, renamed == "Lamp (7)", "[GenerateInterface] resolves the use case through IRenameWidgetUseCase");
        Expect(failures, recordingLoggerProvider.Messages.Contains("Executing: widgetId=7, name=Lamp"),
            "[LogExecuting] writes 'Executing: widgetId=7, name=Lamp'");
    }

    private static void CheckTheDalMappingThroughTheDefaultCreateDal(List<string> failures)
    {
        var widgetsRepository = CreateWidgetsRepository();

        var widgetDal = widgetsRepository.CreateRow(new WidgetEdit { OwnerId = 3, Name = "Lamp" });
        var widget = widgetDal.ToEntity();
        widgetDal.Apply(new WidgetEdit { OwnerId = 4, Name = "Desk" });

        Expect(failures, widgetDal.Id != 0 && widget.Id == widgetDal.Id, "the default CreateDal gives the row an id through the generated From");
        Expect(failures, widget.OwnerId == 3 && widget.Name == "Lamp", "the generated ToEntity copies every column");
        Expect(failures, widgetDal.OwnerId == 4 && widgetDal.Name == "Desk", "the generated Apply copies every edit field");
    }

    private static void CheckTheGeneratedFilterQuery(List<string> failures)
    {
        var widgetsRepository = CreateWidgetsRepository();
        using var smokeContext = new ContextFactoryFromDesignTimeFactory().CreateDbContext();

        var filterSql = widgetsRepository.ToFilterSql(smokeContext, new WidgetFilter { Ids = [1, 2], OwnerId = 3 });

        Expect(failures, filterSql.Contains("owner_id") && filterSql.Contains("WHERE"), "the generated ApplyFilter narrows on owner_id: " + filterSql);
    }

    private static async Task CheckTheGeneratedMatchesInMemory(List<string> failures)
    {
        var widgetsRepository = new InMemoryRepository<Widget, WidgetEdit, WidgetFilter>(CreateFixedTimeProvider());
        await widgetsRepository.Create(new WidgetEdit { OwnerId = 3, Name = "Lamp" });
        await widgetsRepository.Create(new WidgetEdit { OwnerId = 4, Name = "Desk" });

        var ownedWidgets = await widgetsRepository.Get(new WidgetFilter { OwnerId = 3 });

        Expect(failures, ownedWidgets.Count == 1 && ownedWidgets[0].Name == "Lamp", "the generated Matches keeps only owner 3's widget in memory");
    }

    private static async Task CheckTheGeneratedClient(List<string> failures)
    {
        var stubWidgetsHandler = new StubWidgetsHandler();
        var services = new ServiceCollection();
        services.ConfigureHttpClientDefaults(httpClientBuilder => httpClientBuilder.ConfigurePrimaryHttpMessageHandler(() => stubWidgetsHandler));
        services.AddWidgetsClient(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();
        var widgetsClient = serviceProvider.GetRequiredService<IWidgetsClient>();

        var widgetDto = await widgetsClient.GetWidget(7);

        Expect(failures, widgetDto?.Name == "Lamp", "the generated client reads the answer");
        Expect(failures, stubWidgetsHandler.RequestedUri?.ToString() == "http://widgets.test/api/widgets/7",
            "the generated client calls http://widgets.test/api/widgets/7, not " + stubWidgetsHandler.RequestedUri);
    }

    private static async Task CheckTheGeneratedFakeBase(List<string> failures)
    {
        IWidgetsClient widgetsClient = new FakeWidgetsClient();

        try
        {
            await widgetsClient.GetWidget(7);
            failures.Add("the generated fake base throws for an unscripted member");
        }
        catch (NotScriptedException notScriptedException)
        {
            Expect(failures, notScriptedException.Message.Contains("GetWidget"), "NotScriptedException names GetWidget");
        }
    }

    private static WidgetsRepository CreateWidgetsRepository()
    {
        var timeProvider = CreateFixedTimeProvider();
        var repositoryServices = new RepositoryServices<SmokeContext>(
            new ContextFactoryFromDesignTimeFactory(), new FakeKeyGen(timeProvider), timeProvider, NullLoggerFactory.Instance);
        return new WidgetsRepository(repositoryServices);
    }

    private static FakeTimeProvider CreateFixedTimeProvider()
    {
        return new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    }

    private static void Expect(List<string> failures, bool isMet, string expectation)
    {
        if (!isMet)
        {
            failures.Add(expectation);
        }
    }
}
