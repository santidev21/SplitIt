using SplitIt.Infrastructure.Services;

namespace SplitIt.Tests;

/// <summary>
/// Pure unit tests for the expense splitting math (no DB required).
/// Covers: equal split with remainder cents, exact amounts, percentages,
/// rounding drift absorption and every validation failure.
/// </summary>
public class SplitCalculatorTests
{
    // ---------- Equal split ----------

    [Fact]
    public void EqualSplit_100_Among3_DistributesRemainderCents()
    {
        var result = SplitCalculator.EqualSplit(100m, new[] { 1, 2, 3 });

        Assert.Equal(3, result.Count);
        Assert.Equal(33.34m, result[0].AmountOwed); // leftover cent goes to the first participants
        Assert.Equal(33.33m, result[1].AmountOwed);
        Assert.Equal(33.33m, result[2].AmountOwed);
        Assert.Equal(100m, result.Sum(r => r.AmountOwed));
    }

    [Fact]
    public void EqualSplit_100_Among2_IsExact()
    {
        var result = SplitCalculator.EqualSplit(100m, new[] { 1, 2 });

        Assert.Equal(50m, result[0].AmountOwed);
        Assert.Equal(50m, result[1].AmountOwed);
        Assert.Equal(100m, result.Sum(r => r.AmountOwed));
    }

    [Fact]
    public void EqualSplit_10_Among3_DistributesOneCent()
    {
        var result = SplitCalculator.EqualSplit(10m, new[] { 1, 2, 3 });

        Assert.Equal(3.34m, result[0].AmountOwed);
        Assert.Equal(3.33m, result[1].AmountOwed);
        Assert.Equal(3.33m, result[2].AmountOwed);
        Assert.Equal(10m, result.Sum(r => r.AmountOwed));
    }

    [Fact]
    public void EqualSplit_99_99_Among3_HasNoRemainder()
    {
        var result = SplitCalculator.EqualSplit(99.99m, new[] { 1, 2, 3 });

        Assert.All(result, r => Assert.Equal(33.33m, r.AmountOwed));
        Assert.Equal(99.99m, result.Sum(r => r.AmountOwed));
    }

    [Theory]
    [InlineData(50, 7)]
    [InlineData(1000, 23)]
    [InlineData(0.51, 17)]
    public void EqualSplit_AlwaysSumsExactlyToTotal(decimal total, int participants)
    {
        var ids = Enumerable.Range(1, participants).ToList();
        var result = SplitCalculator.EqualSplit(total, ids);

        Assert.Equal(participants, result.Count);
        Assert.Equal(total, result.Sum(r => r.AmountOwed));
        Assert.All(result, r => Assert.True(r.AmountOwed > 0));
        // Each participant differs by at most one cent
        Assert.True(result.Max(r => r.AmountOwed) - result.Min(r => r.AmountOwed) <= 0.01m);
    }

    [Fact]
    public void EqualSplit_AmountTooSmall_ForManyParticipants_Throws()
    {
        // 0.02 among 5 people: floor gives 0.00 and only 2 leftover cents
        Assert.Throws<ArgumentException>(() => SplitCalculator.EqualSplit(0.02m, new[] { 1, 2, 3, 4, 5 }));
    }

    [Fact]
    public void EqualSplit_EmptyParticipants_Throws()
    {
        Assert.Throws<ArgumentException>(() => SplitCalculator.EqualSplit(100m, Array.Empty<int>()));
    }

    [Fact]
    public void EqualSplit_ZeroTotal_Throws()
    {
        Assert.Throws<ArgumentException>(() => SplitCalculator.EqualSplit(0m, new[] { 1, 2 }));
    }

    [Fact]
    public void EqualSplit_NegativeTotal_Throws()
    {
        Assert.Throws<ArgumentException>(() => SplitCalculator.EqualSplit(-50m, new[] { 1, 2 }));
    }

    // ---------- Split by exact amounts ----------

    [Fact]
    public void ByAmount_50_30_20_Of100_IsValid()
    {
        var result = SplitCalculator.ByAmount(new[] { (1, 50m), (2, 30m), (3, 20m) }, 100m);

        Assert.Equal(50m, result[0].AmountOwed);
        Assert.Equal(30m, result[1].AmountOwed);
        Assert.Equal(20m, result[2].AmountOwed);
        Assert.Equal(100m, result.Sum(r => r.AmountOwed));
    }

    [Fact]
    public void ByAmount_PreservesUserIds()
    {
        var result = SplitCalculator.ByAmount(new[] { (42, 10m), (7, 90m) }, 100m);

        Assert.Equal(42, result[0].UserId);
        Assert.Equal(7, result[1].UserId);
    }

    [Fact]
    public void ByAmount_SumMismatch_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            SplitCalculator.ByAmount(new[] { (1, 60m), (2, 30m) }, 100m)); // sums 90
    }

    [Fact]
    public void ByAmount_SumWithinTolerance_IsAccepted()
    {
        // 0.01 drift is allowed (mirrors frontend ±0.01 rule)
        var result = SplitCalculator.ByAmount(new[] { (1, 50.01m), (2, 50m) }, 100m);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void ByAmount_NegativeAmount_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            SplitCalculator.ByAmount(new[] { (1, 110m), (2, -10m) }, 100m));
    }

    [Fact]
    public void ByAmount_ZeroAmount_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            SplitCalculator.ByAmount(new[] { (1, 100m), (2, 0m) }, 100m));
    }

    [Fact]
    public void ByAmount_EmptyEntries_Throws()
    {
        Assert.Throws<ArgumentException>(() => SplitCalculator.ByAmount(Array.Empty<(int, decimal)>(), 100m));
    }

    // ---------- Split by percentage ----------

    [Fact]
    public void ByPercentage_50_30_20_Of100_IsValid()
    {
        var result = SplitCalculator.ByPercentage(new[] { (1, 50m), (2, 30m), (3, 20m) }, 100m);

        Assert.Equal(50m, result[0].AmountOwed);
        Assert.Equal(30m, result[1].AmountOwed);
        Assert.Equal(20m, result[2].AmountOwed);
    }

    [Fact]
    public void ByPercentage_30_70_Of90_MapsTo27And63()
    {
        var result = SplitCalculator.ByPercentage(new[] { (1, 30m), (2, 70m) }, 90m);

        Assert.Equal(27m, result[0].AmountOwed);
        Assert.Equal(63m, result[1].AmountOwed);
        Assert.Equal(90m, result.Sum(r => r.AmountOwed));
    }

    [Fact]
    public void ByPercentage_RoundingDrift_IsAbsorbedByLastParticipant()
    {
        // 33.33% each of 100 rounds to 33.33/33.33/33.33 = 99.99; last absorbs the drift
        var result = SplitCalculator.ByPercentage(new[] { (1, 33.33m), (2, 33.33m), (3, 33.34m) }, 100m);

        Assert.Equal(100m, result.Sum(r => r.AmountOwed));
        Assert.All(result, r => Assert.True(r.AmountOwed > 0));
    }

    [Fact]
    public void ByPercentage_RepeatingThirds_AlwaysSumsExactly()
    {
        // (100/3)% each of 200: heavy rounding drift expected
        var third = Math.Round(100m / 3m, 2, MidpointRounding.AwayFromZero); // 33.33
        var result = SplitCalculator.ByPercentage(new[] { (1, third), (2, third), (3, third), (4, 0.01m) }, 200m);

        Assert.Equal(200m, result.Sum(r => r.AmountOwed));
        Assert.All(result, r => Assert.True(r.AmountOwed > 0));
    }

    [Theory]
    [InlineData(new double[] { 50, 30, 10 })]   // sums 90
    [InlineData(new double[] { 60, 60 })]       // sums 120
    [InlineData(new double[] { 100, 0.5 })]     // sums 100.5
    public void ByPercentage_NotSummingTo100_Throws(double[] percentages)
    {
        var entries = percentages.Select((p, i) => (i + 1, (decimal)p)).ToList();
        Assert.Throws<ArgumentException>(() => SplitCalculator.ByPercentage(entries, 100m));
    }

    [Fact]
    public void ByPercentage_NegativePercentage_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            SplitCalculator.ByPercentage(new[] { (1, 110m), (2, -10m) }, 100m));
    }

    [Fact]
    public void ByPercentage_Over100Percentage_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            SplitCalculator.ByPercentage(new[] { (1, 120m), (2, -20m) }, 100m));
    }

    [Fact]
    public void ByPercentage_EmptyEntries_Throws()
    {
        Assert.Throws<ArgumentException>(() => SplitCalculator.ByPercentage(Array.Empty<(int, decimal)>(), 100m));
    }

    [Fact]
    public void ByPercentage_ZeroTotal_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            SplitCalculator.ByPercentage(new[] { (1, 50m), (2, 50m) }, 0m));
    }
}
