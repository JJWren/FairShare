using System;

namespace FairShare.Domain.Helpers
{
    /// <summary>
    /// Thrown by a schedule lookup whose guidelines leave income above the schedule's top bracket to
    /// the court. The throwing schedule words <paramref name="message"/> in its own state's terms
    /// (ADR 0005); calculators surface it verbatim as a
    /// <see cref="CalcErrorCodes.IncomeAboveSchedule"/> error on the result.
    /// </summary>
    public sealed class IncomeAboveScheduleException(int combinedAdjustedGrossIncome, string message)
        : Exception(message)
    {
        /// <summary>
        /// The combined adjusted gross income (worksheet line 2, combined) that fell above the schedule.
        /// </summary>
        public int CombinedAdjustedGrossIncome { get; } = combinedAdjustedGrossIncome;
    }
}
