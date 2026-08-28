using FairShare.Domain.Interfaces;

namespace FairShare.Domain.Seeds
{
    /// <summary>
    /// Alabama's schedule behind <see cref="IObligationSchedule"/>: <see cref="BcsoLookup"/>'s data
    /// and lookup semantics (nearest-$50 bracket, $250 floor, error above $30,000 per ADR 0001),
    /// shared by both Alabama forms.
    /// </summary>
    public sealed class AlabamaObligationSchedule : IObligationSchedule
    {
        public static readonly AlabamaObligationSchedule Instance = new();

        private AlabamaObligationSchedule()
        {
        }

        // The workbook's own label for this table revision. A future revision APPENDS a new
        // schedule/vintage rather than editing this in place - old results cite this string.
        public string Vintage => "AL Realigned Sept 2021";

        public int MinChildren => BcsoLookup.MinChildren;

        public int MaxChildren => BcsoLookup.MaxChildren;

        public int GetBasicObligation(int combinedIncome, int children)
            => BcsoLookup.Get(combinedIncome, children);
    }
}
