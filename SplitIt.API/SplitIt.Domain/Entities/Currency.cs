using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SplitIt.Domain.Entities
{
    public class Currency
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Symbol { get; set; }

        /// <summary>
        /// Number of decimal places supported by this currency for monetary amounts.
        /// E.g. USD uses 2 (cents) while the Colombian Peso uses 0 (whole pesos).
        /// A group's currency is fixed at creation time, so expenses and payments
        /// are validated against this scale.
        /// </summary>
        public int DecimalPlaces { get; set; } = 2;
    }
}
