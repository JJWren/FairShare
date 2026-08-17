using System;

namespace FairShare.Domain.Helpers
{
    /// <summary>
    /// Thrown by the schedule lookup when the combined adjusted gross income rounds to a $50 bracket above the
    /// top of the Basic Child-Support Obligation schedule. Calculators translate it into an
    /// <see cref="CalcErrorCodes.IncomeAboveSchedule"/> error on the result.
    /// </summary>
    public sealed class IncomeAboveScheduleException(int combinedAdjustedGrossIncome)
        : Exception($"Combined adjusted gross income of {combinedAdjustedGrossIncome} is above the top of the schedule.")
    {
        /// <summary>
        /// The combined adjusted gross income (worksheet line 2, combined) that fell above the schedule.
        /// </summary>
        public int CombinedAdjustedGrossIncome { get; } = combinedAdjustedGrossIncome;
    }
}
