using MagicCSharp.Infrastructure;
using Xunit;

namespace MagicCSharp.Tests;

public class FilterMatchingTests
{
    [Fact]
    public void MatchesListFilter_NullList_MatchesEveryValue_AndAMissingOne()
    {
        // Act
        var isValueMatched = FilterMatching.MatchesListFilter<long>(null, 5);
        var isMissingValueMatched = FilterMatching.MatchesListFilter<long>(null, null);

        // Assert
        Assert.True(isValueMatched);
        Assert.True(isMissingValueMatched);
    }

    [Fact]
    public void MatchesListFilter_EmptyList_MatchesNothing()
    {
        // Act
        var isValueMatched = FilterMatching.MatchesListFilter<long>([], 5);
        var isMissingValueMatched = FilterMatching.MatchesListFilter<long>([], null);

        // Assert
        Assert.False(isValueMatched);
        Assert.False(isMissingValueMatched);
    }

    [Fact]
    public void MatchesListFilter_ListWithTheValue_Matches()
    {
        // Act
        var isMatched = FilterMatching.MatchesListFilter<long>([4, 5], 5);

        // Assert
        Assert.True(isMatched);
    }

    [Fact]
    public void MatchesListFilter_ListWithoutTheValue_DoesNotMatch()
    {
        // Act
        var isMatched = FilterMatching.MatchesListFilter<long>([4, 6], 5);

        // Assert
        Assert.False(isMatched);
    }

    [Fact]
    public void MatchesListFilter_MissingValue_DoesNotMatchAListWithValues()
    {
        // Act
        var isMatched = FilterMatching.MatchesListFilter<long>([4, 5], null);

        // Assert
        Assert.False(isMatched);
    }

    [Fact]
    public void MatchesListFilter_NullStringList_MatchesEveryString_AndAMissingOne()
    {
        // Act
        var isStringMatched = FilterMatching.MatchesListFilter((IEnumerable<string>?)null, "TRK-1");
        var isMissingStringMatched = FilterMatching.MatchesListFilter((IEnumerable<string>?)null, null);

        // Assert
        Assert.True(isStringMatched);
        Assert.True(isMissingStringMatched);
    }

    [Fact]
    public void MatchesListFilter_EmptyStringList_MatchesNothing()
    {
        // Act
        var isMatched = FilterMatching.MatchesListFilter(Array.Empty<string>(), "TRK-1");

        // Assert
        Assert.False(isMatched);
    }

    [Fact]
    public void MatchesListFilter_StringsDifferingOnlyInCase_DoNotMatch()
    {
        // Act
        var isMatched = FilterMatching.MatchesListFilter(["TRK-1"], "trk-1");

        // Assert
        Assert.False(isMatched);
    }

    [Fact]
    public void MatchesListFilter_MissingString_DoesNotMatchAListWithStrings()
    {
        // Act
        var isMatched = FilterMatching.MatchesListFilter(["TRK-1"], null);

        // Assert
        Assert.False(isMatched);
    }

    [Fact]
    public void MatchesNullableValueFilter_NoFilterValue_MatchesEveryValue_AndAMissingOne()
    {
        // Act
        var isValueMatched = FilterMatching.MatchesNullableValueFilter<long>(null, 5);
        var isMissingValueMatched = FilterMatching.MatchesNullableValueFilter<long>(null, null);

        // Assert
        Assert.True(isValueMatched);
        Assert.True(isMissingValueMatched);
    }

    [Fact]
    public void MatchesNullableValueFilter_EqualValue_Matches()
    {
        // Act
        var isMatched = FilterMatching.MatchesNullableValueFilter<long>(5, 5);

        // Assert
        Assert.True(isMatched);
    }

    [Fact]
    public void MatchesNullableValueFilter_OtherValue_DoesNotMatch()
    {
        // Act
        var isMatched = FilterMatching.MatchesNullableValueFilter<long>(5, 6);

        // Assert
        Assert.False(isMatched);
    }

    [Fact]
    public void MatchesNullableValueFilter_MissingValue_DoesNotMatchAFilterValue()
    {
        // Act
        var isMatched = FilterMatching.MatchesNullableValueFilter<long>(5, null);

        // Assert
        Assert.False(isMatched);
    }

    [Fact]
    public void MatchesNullablePresenceFilter_Null_MatchesEntitiesWithAndWithoutAValue()
    {
        // Act
        var isEntityWithValueMatched = FilterMatching.MatchesNullablePresenceFilter<long>(null, 5);
        var isEntityWithoutValueMatched = FilterMatching.MatchesNullablePresenceFilter<long>(null, null);

        // Assert
        Assert.True(isEntityWithValueMatched);
        Assert.True(isEntityWithoutValueMatched);
    }

    [Fact]
    public void MatchesNullablePresenceFilter_True_MatchesOnlyEntitiesWithAValue()
    {
        // Act
        var isEntityWithValueMatched = FilterMatching.MatchesNullablePresenceFilter<long>(true, 5);
        var isEntityWithoutValueMatched = FilterMatching.MatchesNullablePresenceFilter<long>(true, null);

        // Assert
        Assert.True(isEntityWithValueMatched);
        Assert.False(isEntityWithoutValueMatched);
    }

    [Fact]
    public void MatchesNullablePresenceFilter_False_MatchesOnlyEntitiesWithoutAValue()
    {
        // Act
        var isEntityWithValueMatched = FilterMatching.MatchesNullablePresenceFilter<long>(false, 5);
        var isEntityWithoutValueMatched = FilterMatching.MatchesNullablePresenceFilter<long>(false, null);

        // Assert
        Assert.False(isEntityWithValueMatched);
        Assert.True(isEntityWithoutValueMatched);
    }

    [Fact]
    public void MatchesComparableRangeFilter_NullRange_MatchesEveryValue_AndAMissingOne()
    {
        // Act
        var isValueMatched = FilterMatching.MatchesComparableRangeFilter<int>(null, 5);
        var isMissingValueMatched = FilterMatching.MatchesComparableRangeFilter<int>(null, null);

        // Assert
        Assert.True(isValueMatched);
        Assert.True(isMissingValueMatched);
    }

    [Fact]
    public void MatchesComparableRangeFilter_RangeWithoutEnds_MatchesEveryValue_AndAMissingOne()
    {
        // Arrange
        var range = new ComparableRange<int>();

        // Act
        var isValueMatched = FilterMatching.MatchesComparableRangeFilter(range, 5);
        var isMissingValueMatched = FilterMatching.MatchesComparableRangeFilter(range, null);

        // Assert
        Assert.True(isValueMatched);
        Assert.True(isMissingValueMatched);
    }

    [Fact]
    public void MatchesComparableRangeFilter_RangeWithAnEnd_DoesNotMatchAMissingValue()
    {
        // Arrange
        var range = new ComparableRange<int> { Start = 1 };

        // Act
        var isMatched = FilterMatching.MatchesComparableRangeFilter(range, null);

        // Assert
        Assert.False(isMatched);
    }

    [Fact]
    public void MatchesComparableRangeFilter_ValueInsideTheRange_Matches()
    {
        // Arrange
        var range = new ComparableRange<int> { Start = 1, End = 10 };

        // Act
        var isMatched = FilterMatching.MatchesComparableRangeFilter(range, 5);

        // Assert
        Assert.True(isMatched);
    }

    [Fact]
    public void MatchesComparableRangeFilter_ValueOutsideTheRange_DoesNotMatch()
    {
        // Arrange
        var range = new ComparableRange<int> { Start = 1, End = 10 };

        // Act
        var isMatched = FilterMatching.MatchesComparableRangeFilter(range, 11);

        // Assert
        Assert.False(isMatched);
    }
}
