using SplitIt.Infrastructure.Services;

namespace SplitIt.Tests;

public class DebtSimplifierTests
{
    [Fact]
    public void Chain_IsCollapsedIntoASingleTransfer()
    {
        // Me owes Carlos 200 and Carlos owes Luisa 200 => Me pays Luisa 200.
        const int me = 1, carlos = 2, luisa = 3;
        var balances = new Dictionary<int, decimal> { [me] = -200m, [carlos] = 0m, [luisa] = 200m };

        var plan = DebtSimplifier.Simplify(balances);

        Assert.Equal(new[] { new DebtTransfer(me, luisa, 200m) }, plan);
    }

    [Fact]
    public void NoBalances_ProducesNoTransfers()
    {
        Assert.Empty(DebtSimplifier.Simplify(new Dictionary<int, decimal>()));
        Assert.Empty(DebtSimplifier.Simplify(new Dictionary<int, decimal> { [1] = 0m, [2] = 0m }));
    }

    [Fact]
    public void FindsTheOptimum_WhereLargestFirstGreedyDoesNot()
    {
        // Largest-first greedy needs 4 transfers here; the optimum splits the members into
        // {+6, -3, -3} and {+4, -4} and needs 3.
        var balances = new Dictionary<int, decimal> { [1] = 6m, [2] = 4m, [3] = -3m, [4] = -3m, [5] = -4m };

        var plan = DebtSimplifier.Simplify(balances);

        AssertValidPlan(balances, plan);
        Assert.Equal(3, plan.Count);
    }

    [Fact]
    public void KeepsCents_Exactly()
    {
        var balances = new Dictionary<int, decimal> { [1] = -33.33m, [2] = -33.34m, [3] = 66.67m };

        var plan = DebtSimplifier.Simplify(balances);

        AssertValidPlan(balances, plan);
        Assert.Equal(2, plan.Count);
    }

    [Fact]
    public void UnbalancedInput_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            DebtSimplifier.Simplify(new Dictionary<int, decimal> { [1] = -10m, [2] = 9.99m }));
    }

    [Fact]
    public void SubCentInput_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            DebtSimplifier.Simplify(new Dictionary<int, decimal> { [1] = -0.005m, [2] = 0.005m }));
    }

    [Fact]
    public void IsDeterministic()
    {
        var balances = new Dictionary<int, decimal> { [4] = 10m, [2] = 10m, [3] = -10m, [1] = -10m };

        Assert.Equal(DebtSimplifier.Simplify(balances), DebtSimplifier.Simplify(balances));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void SmallRandomGroups_MatchBruteForceOptimum(int seed)
    {
        var random = new Random(seed);
        for (var round = 0; round < 300; round++)
        {
            // Small value range => many zero-sum subsets, the hard case for the optimum.
            var balances = RandomBalances(random, members: random.Next(2, 9), maxCents: 10 * random.Next(1, 4));

            var plan = DebtSimplifier.Simplify(balances);

            AssertValidPlan(balances, plan);
            Assert.Equal(BruteForceMinTransfers(balances.Values.Where(v => v != 0).Select(v => (long)(v * 100)).ToList()), plan.Count);
        }
    }

    [Theory]
    [InlineData(10, 16)]
    [InlineData(11, 17)]
    [InlineData(12, 40)]
    [InlineData(13, 150)]
    public void LargeRandomGroups_PreserveBalancesWithAtMostNMinusOneTransfers(int seed, int members)
    {
        var random = new Random(seed);
        for (var round = 0; round < 20; round++)
        {
            var balances = RandomBalances(random, members, maxCents: 500_000);

            var plan = DebtSimplifier.Simplify(balances);

            AssertValidPlan(balances, plan);
        }
    }

    [Fact]
    public void Simplify_NeverNeedsMoreTransfersThanTheOriginalDebts()
    {
        var random = new Random(42);
        for (var round = 0; round < 500; round++)
        {
            // Random pairwise debt graph among up to 10 people.
            var people = random.Next(2, 11);
            var debts = new List<(int From, int To, decimal Amount)>();
            for (var i = 0; i < random.Next(1, 25); i++)
            {
                var from = random.Next(people);
                var to = random.Next(people);
                if (from != to)
                    debts.Add((from, to, random.Next(1, 20_000) / 100m));
            }

            var balances = new Dictionary<int, decimal>();
            foreach (var (from, to, amount) in debts)
            {
                balances[from] = balances.GetValueOrDefault(from) - amount;
                balances[to] = balances.GetValueOrDefault(to) + amount;
            }
            var distinctPairs = debts
                .GroupBy(d => (Math.Min(d.From, d.To), Math.Max(d.From, d.To)))
                .Count(g => g.Sum(d => d.From < d.To ? d.Amount : -d.Amount) != 0);

            var plan = DebtSimplifier.Simplify(balances);

            AssertValidPlan(balances, plan);
            Assert.True(plan.Count <= distinctPairs, $"Plan has {plan.Count} transfers for {distinctPairs} pairwise debts.");
        }
    }

    /// <summary>
    /// Every member ends at zero, every transfer is positive and goes from a debtor to a
    /// creditor, and the plan never uses more than N - 1 transfers.
    /// </summary>
    private static void AssertValidPlan(IReadOnlyDictionary<int, decimal> balances, List<DebtTransfer> plan)
    {
        var after = balances.ToDictionary(b => b.Key, b => b.Value);
        foreach (var t in plan)
        {
            Assert.True(t.Amount > 0);
            Assert.NotEqual(t.FromUserId, t.ToUserId);
            Assert.True(balances[t.FromUserId] < 0, $"User {t.FromUserId} pays but is not a debtor.");
            Assert.True(balances[t.ToUserId] > 0, $"User {t.ToUserId} receives but is not a creditor.");
            after[t.FromUserId] += t.Amount;
            after[t.ToUserId] -= t.Amount;
        }
        Assert.All(after.Values, v => Assert.Equal(0m, v));

        var nonZero = balances.Values.Count(v => v != 0);
        Assert.True(plan.Count <= Math.Max(0, nonZero - 1));
    }

    private static Dictionary<int, decimal> RandomBalances(Random random, int members, int maxCents)
    {
        var cents = new long[members];
        for (var i = 0; i < members - 1; i++)
            cents[i] = random.Next(-maxCents, maxCents + 1);
        cents[^1] = -cents.Take(members - 1).Sum();
        return Enumerable.Range(0, members).ToDictionary(i => 100 + i, i => cents[i] / 100m);
    }

    /// <summary>
    /// Independent reference: classic backtracking that settles the first open balance
    /// against every opposite-sign balance and keeps the cheapest branch.
    /// </summary>
    private static int BruteForceMinTransfers(List<long> balances)
    {
        int Solve(int start)
        {
            while (start < balances.Count && balances[start] == 0) start++;
            if (start == balances.Count) return 0;

            var best = int.MaxValue;
            for (var i = start + 1; i < balances.Count; i++)
            {
                if (balances[i] == 0 || Math.Sign(balances[i]) == Math.Sign(balances[start])) continue;
                balances[i] += balances[start];
                best = Math.Min(best, 1 + Solve(start + 1));
                balances[i] -= balances[start];
            }
            return best;
        }
        return Solve(0);
    }
}
