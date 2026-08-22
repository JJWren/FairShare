using System;

namespace FairShare.Domain.Seeds
{
    /// <summary>
    /// Oregon guideline parameters that move on their own statutory schedules, keyed by the
    /// effective date of the rules that set them. <see cref="Current"/> is what calculators use, and
    /// estimates display its <see cref="EffectiveDate"/> ("Implements OAR 137-050 effective ...").
    /// The self-support reserve is re-set every July 1 (federal poverty guideline × 1.30, OAR
    /// 137-050-0745) — expect a new record here each year; when a second vintage exists, grow this
    /// into an effective-date-keyed history rather than editing values in place.
    /// </summary>
    public sealed record OregonRuleParameters
    {
        /// <summary>
        /// The parameters in force today. Values read from the official DOJ Guidelines Calculator
        /// workbook (saved 6/30/2026) and the rules cited on each property.
        /// </summary>
        public static readonly OregonRuleParameters Current = new()
        {
            EffectiveDate = new DateOnly(2026, 7, 1),
            SelfSupportReserve = 1729,
            MinimumOrder = 100,
            MedicalCostCapRate = 0.04m,
            HighestMinimumWageMonthly = 2912,
            MetroChildCareCaps = new ChildCareCaps(1705, 1705, 1400, 1100),
            NonMetroChildCareCaps = new ChildCareCaps(1190, 1083, 860, 629),
        };

        /// <summary>The date the rule set producing these values took effect.</summary>
        public required DateOnly EffectiveDate { get; init; }

        /// <summary>
        /// OAR 137-050-0745: monthly adjusted income kept back for a parent's own support; what
        /// remains is the available income that caps the total obligation.
        /// </summary>
        public required int SelfSupportReserve { get; init; }

        /// <summary>
        /// OAR 137-050-0755: the rebuttable presumption that an obligor can pay at least this much
        /// total support a month (with the rule's $0 exceptions).
        /// </summary>
        public required int MinimumOrder { get; init; }

        /// <summary>
        /// OAR 137-050-0750: "reasonable in cost" — each parent's medical-support cap as a share of
        /// that parent's adjusted income.
        /// </summary>
        public required decimal MedicalCostCapRate { get; init; }

        /// <summary>
        /// Full-time monthly earnings at the highest of Oregon's minimum-wage tiers; a parent at or
        /// below it contributes $0 to medical support (OAR 137-050-0750). The lowest tier — used for
        /// income presumption — is a different number and belongs to the income rules, not here.
        /// </summary>
        public required int HighestMinimumWageMonthly { get; init; }

        /// <summary>
        /// OAR 137-050-0735 Table 1 (eff. 7/7/2023) for the listed metro areas (Portland, Bend,
        /// Eugene, Corvallis, Springfield, Monmouth, Ashland).
        /// </summary>
        public required ChildCareCaps MetroChildCareCaps { get; init; }

        /// <summary>OAR 137-050-0735 Table 1 (eff. 7/7/2023) for all other locations.</summary>
        public required ChildCareCaps NonMetroChildCareCaps { get; init; }
    }

    /// <summary>
    /// Monthly per-child caps on allowed child care costs by the child's age band
    /// (OAR 137-050-0735 Table 1).
    /// </summary>
    public sealed record ChildCareCaps(int UnderOne, int OneToThree, int ThreeToSix, int SixAndOlder);
}
