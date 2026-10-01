using SplitIt.Infrastructure.Services;

namespace SplitIt.Tests;

/// <summary>
/// Boundary and validation cases for the split math, complementing
/// <see cref="SplitCalculatorTests"/> so mutation testing has fewer survivors.
/// </summary>
public class SplitCalculatorBoundaryTests
{
    [Fact]
    public void EqualSplit_ZeroTotal_Throws() =>
        Assert.Throws<ArgumentException>(() => SplitCalculator.EqualSplit(0m, new[] { 1, 2 }));

    [Fact]
    public void EqualSplit_NullParticipants_Throws() =>
        Assert.Throws<ArgumentException>(() => SplitCalculator.EqualSplit(100m, null!));

    [Fact]
    public void EqualSplit_EmptyParticipants_Throws() =>
        Assert.Throws<ArgumentException>(() => SplitCalculator.EqualSplit(100m, Array.Empty<int>()));

    [Fact]
    public void ByAmount_NullEntries_Throws() =>
        Assert.Throws<ArgumentException>(() => SplitCalculator.ByAmount(null!, 100m));

    [Fact]
    public void ByAmount_EmptyEntries_Throws() =>
        Assert.Throws<ArgumentException>(() => SplitCalculator.ByAmount(Array.Empty<(int, decimal)>(), 100m));

    [Fact]
    public void ByPercentage_NullEntries_Throws() =>
        Assert.Throws<ArgumentException>(() => SplitCalculator.ByPercentage(null!, 100m));

    [Fact]
    public void ByPercentage_ZeroTotal_Throws() =>
        Assert.Throws<ArgumentException>(() => SplitCalculator.ByPercentage(new[] { (1, 100m) }, 0m));

    [Fact]
    public void ByPercentage_AcceptsBoundaryPercentagesZeroAndHundred()
    {
        var result = SplitCalculator.ByPercentage(new[] { (1, 0m), (2, 100m) }, 100m);

        Assert.Equal(0m, result[0].AmountOwed);
        Assert.Equal(100m, result[1].AmountOwed);
    }

    [Fact]
    public void ByPercentage_AbsorbsRoundingDriftIntoLastParticipant()
    {
        var result = SplitCalculator.ByPercentage(
            new[] { (1, 33.33m), (2, 33.33m), (3, 33.33m) },
            100m);

        Assert.Equal(100m, result.Sum(r => r.AmountOwed));
        Assert.Equal(33.33m, result[0].AmountOwed);
        Assert.Equal(33.34m, result[2].AmountOwed);
    }
}
