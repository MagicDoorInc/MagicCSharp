namespace MagicCSharp.Infrastructure;

public record ComparableRange<T>
    where T : struct, IComparable<T>
{
    public T? Start { get; init; }
    public T? End { get; init; }
    public bool StartInclusive { get; init; } = true;
    public bool EndInclusive { get; init; } = true;

    public bool Contains(T value)
    {
        if (Start == null && End == null)
        {
            return true;
        }

        if (Start != null)
        {
            var startComparison = value.CompareTo(Start.Value);
            if (StartInclusive ? startComparison < 0 : startComparison <= 0)
            {
                return false;
            }
        }

        if (End != null)
        {
            var endComparison = value.CompareTo(End.Value);
            if (EndInclusive ? endComparison > 0 : endComparison >= 0)
            {
                return false;
            }
        }

        return true;
    }

    public bool Overlaps(ComparableRange<T> other)
    {
        if ((Start == null && End == null) || (other.Start == null && other.End == null))
        {
            return true;
        }

        if (Start != null && other.End != null)
        {
            var startToOtherEnd = Start.Value.CompareTo(other.End.Value);
            if (StartInclusive && other.EndInclusive ? startToOtherEnd > 0 : startToOtherEnd >= 0)
            {
                return false;
            }
        }

        if (End != null && other.Start != null)
        {
            var endToOtherStart = End.Value.CompareTo(other.Start.Value);
            if (EndInclusive && other.StartInclusive ? endToOtherStart < 0 : endToOtherStart <= 0)
            {
                return false;
            }
        }

        return true;
    }

    public bool Contains(ComparableRange<T> other)
    {
        if (Start == null && End == null)
        {
            return true;
        }

        if (Start != null && other.Start != null)
        {
            var startComparison = Start.Value.CompareTo(other.Start.Value);
            if (StartInclusive && other.StartInclusive ? startComparison < 0 : startComparison <= 0)
            {
                return false;
            }
        }

        if (End != null && other.End != null)
        {
            var endComparison = End.Value.CompareTo(other.End.Value);
            if (EndInclusive && other.EndInclusive ? endComparison > 0 : endComparison >= 0)
            {
                return false;
            }
        }

        return true;
    }

    public override string ToString()
    {
        if (Start == null && End == null)
        {
            return "any value";
        }

        var startBracket = StartInclusive ? "[" : "(";
        var endBracket = EndInclusive ? "]" : ")";
        var startValue = Start?.ToString() ?? "-∞";
        var endValue = End?.ToString() ?? "∞";

        if (Start != null && End == null)
        {
            return $"{(StartInclusive ? "greater than or equal to" : "greater than")} {Start}";
        }

        if (Start == null && End != null)
        {
            return $"{(EndInclusive ? "less than or equal to" : "less than")} {End}";
        }

        return $"{startBracket}{startValue}, {endValue}{endBracket}";
    }

    public string ToMathNotation()
    {
        if (Start == null && End == null)
        {
            return "(-∞, ∞)";
        }

        var startBracket = StartInclusive ? "[" : "(";
        var endBracket = EndInclusive ? "]" : ")";
        var startValue = Start?.ToString() ?? "-∞";
        var endValue = End?.ToString() ?? "∞";

        return $"{startBracket}{startValue}, {endValue}{endBracket}";
    }
}