using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MagicCSharp.Data.Postgres;

/// <summary>
///     Converts every <see cref="DateTimeOffset" /> command parameter to UTC just before the command runs.
///     <para>
///         This is the counterpart to the normalization <see cref="EntityFramework.MagicDbContext" /> does on save, and it exists
///         because that one is not enough. SaveChanges only sees entities being written. A <c>Where</c> comparing
///         a timestamp, an <c>ExecuteUpdate</c>, an <c>ExecuteDelete</c> and raw SQL all bind parameters without
///         going anywhere near the change tracker — so a value carrying a local offset reaches the driver
///         untouched. On Postgres, where a <c>timestamp with time zone</c> parameter must have offset zero, that
///         throws at bind time; the failure shows up only on machines that are not set to UTC, which is why it
///         tends to survive CI and appear on a laptop.
///     </para>
///     <para>
///         Add it wherever the context options are built:
///         <c>options.AddInterceptors(UtcDateTimeOffsetCommandInterceptor.Instance)</c>.
///     </para>
/// </summary>
public class UtcDateTimeOffsetCommandInterceptor : DbCommandInterceptor
{
    /// <summary>
    ///     Shared instance. The interceptor holds no state, so one is enough for every context.
    /// </summary>
    public static readonly UtcDateTimeOffsetCommandInterceptor Instance = new UtcDateTimeOffsetCommandInterceptor();

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        NormalizeParameters(command);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        NormalizeParameters(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        NormalizeParameters(command);
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        NormalizeParameters(command);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        NormalizeParameters(command);
        return base.ScalarExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        NormalizeParameters(command);
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    private static void NormalizeParameters(DbCommand command)
    {
        foreach (DbParameter parameter in command.Parameters)
        {
            if (parameter.Value is DateTimeOffset value && value.Offset != TimeSpan.Zero)
            {
                parameter.Value = value.ToUniversalTime();
            }
        }
    }
}
