using System.Collections.Generic;
using System.Linq;
using FairShare.Domain.Helpers;
using static FairShare.Domain.Helpers.Enums;

namespace FairShare.Domain.Calculators
{
    /// <summary>
    /// Collects <see cref="WorksheetLine"/>s in the order a calculator walks the form, so the result
    /// carries the worksheet exactly as the paper version reads top to bottom.
    /// </summary>
    public sealed class WorksheetBuilder
    {
        private readonly List<WorksheetLine> _lines = [];

        /// <summary>
        /// Appends one worksheet line. Pass <see langword="null"/> for a column the form leaves blank on that line.
        /// </summary>
        public WorksheetBuilder Add(
            string number,
            string label,
            decimal? plaintiff = null,
            decimal? defendant = null,
            decimal? combined = null,
            LineFormat format = LineFormat.Currency)
        {
            _lines.Add(new WorksheetLine
            {
                Number = number,
                Label = label,
                Plaintiff = plaintiff,
                Defendant = defendant,
                Combined = combined,
                Format = format
            });

            return this;
        }

        /// <summary>
        /// The lines collected so far, in insertion order.
        /// </summary>
        public IReadOnlyList<WorksheetLine> Build() => _lines.ToArray();
    }
}
