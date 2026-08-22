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

        // The scale's column count. Whether the Oregon *form* accepts more than ten joint children
        // (the rule says >10 use the ten-child figure, and the lookup already clamps) is the
        // calculator's decision when it arrives - it would widen this bound, not the lookup.
        public int MaxChildren => OregonScaleLookup.MaxChildren;

        public int GetBasicObligation(int combinedIncome, int children)
            => OregonScaleLookup.Get(combinedIncome, children);
    }
}
