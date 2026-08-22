using FairShare.Domain.Helpers;

namespace FairShare.Domain.Interfaces
{
    /// <summary>
    /// A state's schedule (or scale) of basic child-support obligations: its table data, the child
    /// counts it has columns for, its bracket-selection semantics, and what happens above its top
    /// bracket. These contradict between states (Alabama rounds to the nearest $50 row and errors
    /// above its ceiling; Oregon drops to the lower row and caps), so per ADR 0005 each lives with
    /// the state that owns it — never in shared calculator code.
    /// </summary>
    public interface IObligationSchedule
    {
        /// <summary>The lowest child count this schedule can price.</summary>
        int MinChildren { get; }

        /// <summary>
        /// The highest child count this schedule can price. Alabama's schedule prices exactly its
        /// six columns; Oregon prices any family size (its rule sends more than ten children to the
        /// ten-child column), so its bound is unlimited.
        /// </summary>
        int MaxChildren { get; }

        /// <summary>
        /// The monthly basic obligation for a combined income and child count, using this state's
        /// bracket-selection and ceiling semantics. A schedule whose guidelines leave above-ceiling
        /// incomes to the court throws <see cref="IncomeAboveScheduleException"/> carrying a message
        /// in that state's own words.
        /// </summary>
        int GetBasicObligation(int combinedIncome, int children);
    }
}
