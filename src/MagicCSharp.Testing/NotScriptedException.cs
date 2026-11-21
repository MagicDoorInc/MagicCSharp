namespace MagicCSharp.Testing;

/// <summary>
///     A test called a member its fake does not script: the generated fake base's default for every member. Override
///     the member in the fake.
/// </summary>
/// <param name="member">The interface member, as <c>IOrdersClient.GetOrders(OrderFilter)</c>.</param>
public class NotScriptedException(string member) : NotSupportedException($"{member} is not scripted. Override it in the fake.")
{
    public string Member { get; } = member;
}
