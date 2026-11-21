namespace MagicCSharp.Clients;

/// <summary>
///     Names the base class and the registration method of every client generated in this assembly and in the
///     assemblies that reference it, in place of <see cref="MagicHttpClientBase" /> and
///     <see cref="MagicHttpClientRegistration.AddMagicHttpClient{TInterface,TClient}" />. Declare it once, in the
///     library the client libraries share.
/// </summary>
/// <remarks>
///     The base offers what the generated code calls: <c>SendRequestAsync&lt;TResponse&gt;(HttpMethod method, string path,
///     object? body, CancellationToken cancellationToken)</c>, <c>SendRequestAsync(HttpMethod method, string path,
///     object? body, CancellationToken cancellationToken)</c>, <c>WithQuery(string path, params object?[] values)</c>
///     and <c>IsNotFound(Exception exception)</c>. The registration method is
///     <c>static IServiceCollection {name}&lt;TInterface, TClient&gt;(this IServiceCollection services, IConfiguration
///     configuration, string? baseUrl)</c>.
/// </remarks>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class MagicClientConventionAttribute(Type clientBase, Type registrationType, string registrationMethod) : Attribute
{
    public Type ClientBase { get; } = clientBase;

    public Type RegistrationType { get; } = registrationType;

    public string RegistrationMethod { get; } = registrationMethod;
}
