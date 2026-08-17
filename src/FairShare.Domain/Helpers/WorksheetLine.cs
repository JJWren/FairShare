using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static FairShare.Domain.Helpers.Enums;

namespace FairShare.Domain.Helpers
{
    /// <summary>
    /// One numbered line of an official child-support worksheet (e.g. Alabama Form CS-42 line "8"),
    /// carrying the value shown in each column exactly as the paper form lays it out.
    /// </summary>
    public sealed class WorksheetLine
    {
        /// <summary>
        /// The line number printed on the form: "1", "1a", "1b", "2" ... "14".
        /// </summary>
        public required string Number { get; init; }

        /// <summary>
        /// The item text printed on the form for this line.
        /// </summary>
        public required string Label { get; init; }

        /// <summary>
        /// Value in the Plaintiff column, or <see langword="null"/> when the form has no Plaintiff cell on this line.
        /// </summary>
        public decimal? Plaintiff { get; init; }

        /// <summary>
        /// Value in the Defendant column, or <see langword="null"/> when the form has no Defendant cell on this line.
        /// </summary>
        public decimal? Defendant { get; init; }

        /// <summary>
        /// Value in the Combined column, or <see langword="null"/> when the form has no Combined cell on this line.
        /// </summary>
        public decimal? Combined { get; init; }

        /// <summary>
        /// How the values on this line should be displayed. Currency lines carry whole-dollar amounts;
        /// percent lines carry fractions (0.57 = 57%).
        /// </summary>
        public LineFormat Format { get; init; } = LineFormat.Currency;
    }
}
