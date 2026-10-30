namespace SplitIt.Infrastructure.Services
{
    /// <summary>A single transfer of a simplified debt plan: <c>FromUserId</c> pays <c>ToUserId</c>.</summary>
    public sealed record DebtTransfer(int FromUserId, int ToUserId, decimal Amount);

    /// <summary>
    /// Pure, DB-free debt simplification ("minimize cash flow").
    /// Given each member's net balance (positive = is owed money, negative = owes money),
    /// returns a transfer plan that leaves every member with exactly the same net balance
    /// while using as few transfers as possible.
    /// </summary>
    /// <remarks>
    /// The minimum number of transfers equals <c>N - G</c>, where <c>N</c> is the number of
    /// members with a non-zero balance and <c>G</c> the maximum number of disjoint groups whose
    /// balances sum to zero (each group settles internally with <c>size - 1</c> transfers).
    /// Finding <c>G</c> is NP-hard, so it is solved exactly with a bitmask DP for up to
    /// <see cref="OptimalSearchLimit"/> members and with a greedy heuristic above that, which
    /// still guarantees at most <c>N - 1</c> transfers.
    /// </remarks>
    public static class DebtSimplifier
    {
        /// <summary>Largest number of non-zero balances solved with the exact (2^N) search.</summary>
        public const int OptimalSearchLimit = 16;

        public static List<DebtTransfer> Simplify(IReadOnlyDictionary<int, decimal> balances)
        {
            ArgumentNullException.ThrowIfNull(balances);

            // Work in integer cents: stored amounts are decimal(18,2), so this is exact.
            var members = new List<(int UserId, long Cents)>();
            foreach (var (userId, balance) in balances.OrderBy(b => b.Key))
            {
                var scaled = balance * 100m;
                if (scaled != decimal.Truncate(scaled))
                    throw new ArgumentException($"Balance {balance} of user {userId} has more than 2 decimal places.");
                if (scaled != 0)
                    members.Add((userId, (long)scaled));
            }

            if (members.Sum(m => m.Cents) != 0)
                throw new ArgumentException("Balances must sum to zero.");

            var groups = members.Count <= OptimalSearchLimit
                ? PartitionOptimally(members)
                : PartitionGreedily(members);

            var transfers = new List<DebtTransfer>();
            foreach (var group in groups)
                transfers.AddRange(SettleGroup(group));
            return transfers;
        }

        /// <summary>
        /// Splits the members into the maximum number of disjoint zero-sum groups.
        /// dp[mask] = max zero-sum groups the members in <c>mask</c> can be chained into:
        /// removing members one at a time, every prefix whose sum is zero closes a group.
        /// </summary>
        private static List<List<(int UserId, long Cents)>> PartitionOptimally(List<(int UserId, long Cents)> members)
        {
            var n = members.Count;
            var full = (1 << n) - 1;
            var sum = new long[1 << n];
            var dp = new int[1 << n];

            for (var mask = 1; mask <= full; mask++)
            {
                var lowest = System.Numerics.BitOperations.TrailingZeroCount(mask);
                sum[mask] = sum[mask & (mask - 1)] + members[lowest].Cents;

                var best = 0;
                for (var i = 0; i < n; i++)
                {
                    if ((mask & (1 << i)) != 0 && dp[mask ^ (1 << i)] > best)
                        best = dp[mask ^ (1 << i)];
                }
                dp[mask] = best + (sum[mask] == 0 ? 1 : 0);
            }

            // Walk the optimal removal chain back down to the empty set; the members removed
            // between two consecutive zero-sum masks form one zero-sum group.
            var groups = new List<List<(int UserId, long Cents)>>();
            var current = new List<(int UserId, long Cents)>();
            var m = full;
            while (m != 0)
            {
                var target = dp[m] - (sum[m] == 0 ? 1 : 0);
                for (var i = 0; i < n; i++)
                {
                    var bit = 1 << i;
                    if ((m & bit) == 0 || dp[m ^ bit] != target)
                        continue;

                    current.Add(members[i]);
                    m ^= bit;
                    if (sum[m] == 0)
                    {
                        groups.Add(current);
                        current = new List<(int UserId, long Cents)>();
                    }
                    break;
                }
            }
            return groups;
        }

        /// <summary>
        /// Heuristic for large groups: pair up debtors and creditors with exactly opposite
        /// balances (each pair is a zero-sum group settled with one transfer) and leave the
        /// rest as a single group for the greedy settlement.
        /// </summary>
        private static List<List<(int UserId, long Cents)>> PartitionGreedily(List<(int UserId, long Cents)> members)
        {
            var groups = new List<List<(int UserId, long Cents)>>();
            var debtors = members.Where(m => m.Cents < 0).ToList();
            var rest = new List<(int UserId, long Cents)>();

            foreach (var creditor in members.Where(m => m.Cents > 0))
            {
                var match = debtors.FindIndex(d => d.Cents == -creditor.Cents);
                if (match >= 0)
                {
                    groups.Add(new List<(int UserId, long Cents)> { debtors[match], creditor });
                    debtors.RemoveAt(match);
                }
                else
                {
                    rest.Add(creditor);
                }
            }
            rest.AddRange(debtors);
            if (rest.Count > 0)
                groups.Add(rest);
            return groups;
        }

        /// <summary>
        /// Settles a zero-sum group by repeatedly matching the largest debtor with the largest
        /// creditor. Every transfer zeroes at least one member and the last one zeroes two, so a
        /// group of size <c>s</c> needs at most <c>s - 1</c> transfers.
        /// </summary>
        private static IEnumerable<DebtTransfer> SettleGroup(List<(int UserId, long Cents)> group)
        {
            var creditors = group.Where(m => m.Cents > 0).Select(m => (m.UserId, Cents: m.Cents)).ToList();
            var debtors = group.Where(m => m.Cents < 0).Select(m => (m.UserId, Cents: -m.Cents)).ToList();

            while (creditors.Count > 0 && debtors.Count > 0)
            {
                var c = IndexOfLargest(creditors);
                var d = IndexOfLargest(debtors);
                var amount = Math.Min(creditors[c].Cents, debtors[d].Cents);

                yield return new DebtTransfer(debtors[d].UserId, creditors[c].UserId, amount / 100m);

                creditors[c] = (creditors[c].UserId, creditors[c].Cents - amount);
                debtors[d] = (debtors[d].UserId, debtors[d].Cents - amount);
                if (creditors[c].Cents == 0) creditors.RemoveAt(c);
                if (debtors[d].Cents == 0) debtors.RemoveAt(d);
            }
        }

        /// <summary>Index of the largest amount; ties go to the lowest user id for a deterministic plan.</summary>
        private static int IndexOfLargest(List<(int UserId, long Cents)> list)
        {
            var best = 0;
            for (var i = 1; i < list.Count; i++)
            {
                if (list[i].Cents > list[best].Cents ||
                    (list[i].Cents == list[best].Cents && list[i].UserId < list[best].UserId))
                    best = i;
            }
            return best;
        }
    }
}
