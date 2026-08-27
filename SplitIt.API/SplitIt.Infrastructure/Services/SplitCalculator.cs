namespace SplitIt.Infrastructure.Services
{
    public class SplitParticipant
    {
        public int UserId { get; set; }
        public decimal AmountOwed { get; set; }
    }

    /// <summary>
    /// Pure, DB-free expense splitting math shared by backend validation and tests.
    /// All results are in cents-precision (2 decimals, AwayFromZero rounding).
    /// </summary>
    public static class SplitCalculator
    {
        public const decimal SumTolerance = 0.01m;

        /// <summary>
        /// Splits <paramref name="total"/> equally among the given users.
        /// The leftover cents (from flooring) are distributed one cent at a time
        /// to the first participants, e.g. 100/3 => 33.33, 33.33, 33.34.
        /// Throws if any participant would receive 0 (amount too small).
        /// </summary>
        public static List<SplitParticipant> EqualSplit(decimal total, IReadOnlyList<int> userIds)
        {
            if (userIds == null || userIds.Count == 0)
                throw new ArgumentException("At least one participant is required for an equal split.");
            if (total <= 0)
                throw new ArgumentException("Total must be greater than zero for an equal split.");

            var count = userIds.Count;
            var perPerson = Math.Floor(total / count * 100m) / 100m;
            var remainderCents = (int)Math.Round((total - perPerson * count) * 100m, MidpointRounding.AwayFromZero);

            if (perPerson <= 0 && remainderCents < count)
                throw new ArgumentException("Amount is too small to split equally among this many participants.");

            var result = new List<SplitParticipant>(count);
            for (var i = 0; i < count; i++)
            {
                var extra = i < remainderCents ? 0.01m : 0m;
                result.Add(new SplitParticipant { UserId = userIds[i], AmountOwed = perPerson + extra });
            }
            return result;
        }

        /// <summary>
        /// Splits by exact per-user amounts. Sum must equal <paramref name="total"/>
        /// within <see cref="SumTolerance"/> and every amount must be positive.
        /// </summary>
        public static List<SplitParticipant> ByAmount(IReadOnlyList<(int UserId, decimal Amount)> entries, decimal total)
        {
            if (entries == null || entries.Count == 0)
                throw new ArgumentException("At least one participant amount is required.");

            var sum = entries.Sum(e => e.Amount);
            if (Math.Abs(sum - total) > SumTolerance)
                throw new ArgumentException($"Amounts add up to {sum:0.00} which does not match the expense total {total:0.00}.");

            foreach (var entry in entries)
            {
                if (entry.Amount <= 0)
                    throw new ArgumentException("Each participant amount must be greater than zero.");
            }

            return entries
                .Select(e => new SplitParticipant { UserId = e.UserId, AmountOwed = Math.Round(e.Amount, 2, MidpointRounding.AwayFromZero) })
                .ToList();
        }

        /// <summary>
        /// Splits by percentages that must add up to 100 (within tolerance) and be within [0, 100].
        /// Rounding drift is absorbed by the last participant so the result always sums exactly to the total.
        /// </summary>
        public static List<SplitParticipant> ByPercentage(IReadOnlyList<(int UserId, decimal Percentage)> entries, decimal total)
        {
            if (entries == null || entries.Count == 0)
                throw new ArgumentException("At least one participant percentage is required.");
            if (total <= 0)
                throw new ArgumentException("Total must be greater than zero for a percentage split.");

            var sumPct = entries.Sum(e => e.Percentage);
            if (Math.Abs(sumPct - 100m) > SumTolerance)
                throw new ArgumentException($"Percentages add up to {sumPct:0.00}% but must add up to 100%.");

            foreach (var entry in entries)
            {
                if (entry.Percentage < 0 || entry.Percentage > 100)
                    throw new ArgumentException("Each percentage must be between 0 and 100.");
            }

            var result = entries
                .Select(e => new SplitParticipant
                {
                    UserId = e.UserId,
                    AmountOwed = Math.Round(e.Percentage / 100m * total, 2, MidpointRounding.AwayFromZero)
                })
                .ToList();

            // Absorb rounding drift into the last participant to guarantee the sum matches the total.
            var drift = Math.Round(total - result.Sum(r => r.AmountOwed), 2, MidpointRounding.AwayFromZero);
            if (drift != 0m)
            {
                var last = result[^1];
                last.AmountOwed = Math.Round(last.AmountOwed + drift, 2, MidpointRounding.AwayFromZero);
                if (last.AmountOwed <= 0)
                    throw new ArgumentException("Percentage split produced a non-positive amount; adjust the percentages.");
            }

            return result;
        }
    }
}
