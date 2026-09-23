using System.Linq.Expressions;
using System.Reflection;
using MagicCSharp.Infrastructure;

namespace MagicCSharp.Data.Utils;

public static class QueryFilterExtensions
{
    private static readonly MethodInfo StringToLowerMethod = typeof(string).GetMethod("ToLower", Type.EmptyTypes)!;
    private static readonly MethodInfo StringContainsMethod = typeof(string).GetMethod("Contains", [typeof(string)])!;

    private static readonly MethodInfo StringStartsWithMethod =
        typeof(string).GetMethod("StartsWith", [typeof(string)])!;

    private static readonly MethodInfo StringEndsWithMethod = typeof(string).GetMethod("EndsWith", [typeof(string)])!;
    private static readonly MethodInfo StringEqualsMethod = typeof(string).GetMethod("Equals", [typeof(string)])!;

    private static readonly MethodInfo EnumerableContainsMethod =
        typeof(Enumerable).GetMethods().First(m => m.Name == "Contains" && m.GetParameters().Length == 2)!;

    private static readonly MethodInfo EnumerableAnyMethod =
        typeof(Enumerable).GetMethods().First(m => m.Name == "Any" && m.GetParameters().Length == 2)!;

    public static IQueryable<T> ApplyNullableValueFilter<T, TValue>(
        this IQueryable<T> query,
        TValue? value,
        Expression<Func<T, TValue?>> fieldSelector)
        where TValue : struct
    {
        if (value == null)
        {
            return query;
        }

        var condition = Expression.Equal(fieldSelector.Body, Expression.Constant(value, typeof(TValue?)));
        return query.Where(Expression.Lambda<Func<T, bool>>(condition, fieldSelector.Parameters));
    }

    public static IQueryable<T> ApplyStringNullableValueFilter<T>(
        this IQueryable<T> query,
        string? value,
        Expression<Func<T, string?>> fieldSelector,
        StringFilterOperation operation = StringFilterOperation.Equals)
    {
        if (value == null)
        {
            return query;
        }

        var method = operation switch
        {
            StringFilterOperation.Contains => StringContainsMethod,
            StringFilterOperation.StartsWith => StringStartsWithMethod,
            StringFilterOperation.EndsWith => StringEndsWithMethod,
            _ => StringEqualsMethod,
        };

        var lowerValue = Expression.Call(fieldSelector.Body, StringToLowerMethod);

        var condition = Expression.Call(lowerValue, method, Expression.Constant(value.ToLower(), typeof(string)));

        return query.Where(Expression.Lambda<Func<T, bool>>(condition, fieldSelector.Parameters));
    }

    public static IQueryable<T> ApplyComparableRangeFilter<T, TValue>(
        this IQueryable<T> query,
        ComparableRange<TValue>? range,
        Expression<Func<T, TValue?>> selector)
        where TValue : struct, IComparable<TValue>
    {
        if (range == null)
        {
            return query;
        }

        if (range.Start != null)
        {
            object value = range.Start.Value;
            if (value is DateTimeOffset dateTimeOffset)
            {
                // ensure the value is in UTC
                value = dateTimeOffset.ToUniversalTime();
            }

            var expression = range.StartInclusive
                ? Expression.GreaterThanOrEqual(selector.Body, Expression.Constant(value, typeof(TValue?)))
                : Expression.GreaterThan(selector.Body, Expression.Constant(value, typeof(TValue?)));
            query = query.Where(Expression.Lambda<Func<T, bool>>(expression, selector.Parameters));
        }

        if (range.End != null)
        {
            object value = range.End.Value;
            if (value is DateTimeOffset dateTimeOffset)
            {
                // ensure the value is in UTC
                value = dateTimeOffset.ToUniversalTime();
            }

            var expression = range.EndInclusive
                ? Expression.LessThanOrEqual(selector.Body, Expression.Constant(value, typeof(TValue?)))
                : Expression.LessThan(selector.Body, Expression.Constant(value, typeof(TValue?)));
            query = query.Where(Expression.Lambda<Func<T, bool>>(expression, selector.Parameters));
        }

        return query;
    }

    public static IQueryable<T> ApplyListFilter<T>(
        this IQueryable<T> query,
        IEnumerable<string>? values,
        Expression<Func<T, string?>> fieldSelector)
    {
        if (values == null)
        {
            return query;
        }

        if (!values.Any())
        {
            return query.Where(x => false);
        }

        Expression condition = values.Count() == 1
            ? Expression.Equal(fieldSelector.Body, Expression.Constant(values.First(), typeof(string)))
            : Expression.Call(EnumerableContainsMethod.MakeGenericMethod(typeof(string)), Expression.Constant(values),
                Expression.Convert(fieldSelector.Body, typeof(string)));

        return query.Where(Expression.Lambda<Func<T, bool>>(condition, fieldSelector.Parameters));
    }

    public static IQueryable<T> ApplyListFilter<T, TValue>(
        this IQueryable<T> query,
        IEnumerable<TValue>? values,
        Expression<Func<T, TValue?>> fieldSelector)
        where TValue : struct
    {
        if (values == null)
        {
            return query;
        }

        if (!values.Any())
        {
            return query.Where(x => false);
        }

        Expression condition = values.Count() == 1
            ? Expression.Equal(fieldSelector.Body, Expression.Constant(values.First(), typeof(TValue?)))
            : Expression.Call(EnumerableContainsMethod.MakeGenericMethod(typeof(TValue)), Expression.Constant(values),
                Expression.Convert(fieldSelector.Body, typeof(TValue)));

        return query.Where(Expression.Lambda<Func<T, bool>>(condition, fieldSelector.Parameters));
    }

    public static IQueryable<T> ApplyNavigationNullableFilter<T, TNavigation, TValue>(
        this IQueryable<T> query,
        IEnumerable<TValue>? values,
        Expression<Func<T, IEnumerable<TNavigation>>> navigationProperty,
        Expression<Func<TNavigation, TValue?>> valueSelector)
        where TValue : struct
    {
        if (values == null)
        {
            return query;
        }

        if (!values.Any())
        {
            return query.Where(x => false);
        }

        Expression innerCondition = values.Count() == 1
            ? Expression.Equal(valueSelector.Body, Expression.Constant(values.First(), typeof(TValue?)))
            : Expression.Call(EnumerableContainsMethod.MakeGenericMethod(typeof(TValue)), Expression.Constant(values),
                Expression.Convert(valueSelector.Body, typeof(TValue)));

        var innerLambda = Expression.Lambda<Func<TNavigation, bool>>(innerCondition, valueSelector.Parameters);
        var anyCondition = Expression.Call(EnumerableAnyMethod.MakeGenericMethod(typeof(TNavigation)),
            navigationProperty.Body, innerLambda);
        return query.Where(Expression.Lambda<Func<T, bool>>(anyCondition, navigationProperty.Parameters));
    }

    /// <summary>
    ///     Apply IsDeleted filter based on DeletedAt field
    ///     null = all records (deleted + non-deleted)
    ///     false = non-deleted only (DeletedAt == null)
    ///     true = deleted only (DeletedAt != null)
    /// </summary>
    public static IQueryable<T> ApplyIsDeletedFilter<T>(
        this IQueryable<T> query,
        bool? isDeleted,
        Expression<Func<T, DateTimeOffset?>> deletedAtSelector)
    {
        if (isDeleted == null)
        {
            return query;
        }

        if (isDeleted.Value)
        {
            // Return only deleted records (DeletedAt != null)
            var condition =
                Expression.NotEqual(deletedAtSelector.Body, Expression.Constant(null, typeof(DateTimeOffset?)));
            return query.Where(Expression.Lambda<Func<T, bool>>(condition, deletedAtSelector.Parameters));
        }
        else
        {
            // Return only non-deleted records (DeletedAt == null)
            var condition =
                Expression.Equal(deletedAtSelector.Body, Expression.Constant(null, typeof(DateTimeOffset?)));
            return query.Where(Expression.Lambda<Func<T, bool>>(condition, deletedAtSelector.Parameters));
        }
    }
}

public enum StringFilterOperation
{
    Equals,
    Contains,
    StartsWith,
    EndsWith,
}