using FairShare.Domain.Interfaces;

namespace FairShare.Domain.Seeds
{
    /// <summary>
    /// Oregon's scale behind <see cref="IObligationSchedule"/>: <see cref="OregonScaleLookup"/>'s
    /// data and semantics — lower-bracket selection, and a rebuttable cap at the $30,000 row for
    /// incomes above the scale (never <see cref="Helpers.IncomeAboveScheduleException"/>; ADR 0005).
    /// </summary>
    public sealed class OregonObligationSchedule : IObligationSchedule
    {
        public static readonly OregonObligationSchedule Instance = new();

        private OregonObligationSchedule()
        {
        }

        public int MinChildren => OregonScaleLookup.MinChildren;

        // The scale has ten columns, but OAR 137-050-0725 prices any larger family with the
        // ten-child figure (the lookup clamps), so this schedule accepts every child count and
        // the base calculator's range check never rejects a large family. What range the UI
        // *offers* is form metadata, not a schedule concern.
        public int MaxChildren => int.MaxValue;

        public int GetBasicObligation(int combinedIncome, int children)
            => OregonScaleLookup.Get(combinedIncome, children);
    }
}
