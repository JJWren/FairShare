using System;

namespace FairShare.Domain.Seeds
{
    /// <summary>
    /// Oregon guideline parameters that move on their own statutory schedules, keyed by the
    /// effective date of the rules that set them. <see cref="Current"/> is the newest vintage;
    /// <see cref="ForDate(DateOnly)"/> selects the vintage in force on a given date (scenario
    /// reopen "as originally computed", court-prep for a past filing date). Estimates display
    /// the selected <see cref="EffectiveDate"/> ("Implements OAR 137-050 effective ...").
    /// The self-support reserve is re-set every July 1 (federal poverty guideline × 1.30, OAR
    /// 137-050-0745) — the yearly refresh (the June reminder issue) APPENDS the new official
    /// values to <see cref="Vintages"/>; never edit an existing vintage in place, because old
    /// scenarios and printed estimates cite it.
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

        /// <summary>
        /// Every vintage this build carries, oldest to newest. One entry today: only values
        /// verified against an official source belong here, so prior years appear when their
        /// official figures are fetched (the schedule-refresh checklist), never reconstructed.
        /// </summary>
        private static readonly OregonRuleParameters[] Vintages = [Current];

        /// <summary>The vintage in force on <paramref name="date"/> from this build's history.</summary>
        public static OregonRuleParameters ForDate(DateOnly date) => ForDate(date, Vintages);

        /// <summary>
        /// The newest vintage whose effective date is on or before <paramref name="date"/>.
        /// <paramref name="vintages"/> must be sorted oldest to newest (validated - a silently
        /// wrong vintage on a legal figure is far worse than a loud failure). A date before the
        /// earliest vintage gets the earliest — this build simply has no older rules to offer,
        /// and the outcome names the vintage actually used, so the substitution is always visible.
        /// </summary>
        public static OregonRuleParameters ForDate(DateOnly date, System.Collections.Generic.IReadOnlyList<OregonRuleParameters> vintages)
        {
            if (vintages.Count == 0)
            {
                throw new ArgumentException("At least one vintage is required.", nameof(vintages));
            }

            OregonRuleParameters selected = vintages[0];
            DateOnly previous = vintages[0].EffectiveDate;

            foreach (OregonRuleParameters vintage in vintages)
            {
                if (vintage.EffectiveDate < previous)
                {
                    throw new ArgumentException(
                        "Vintages must be sorted oldest to newest by effective date.", nameof(vintages));
                }

                previous = vintage.EffectiveDate;

                if (vintage.EffectiveDate <= date)
                {
                    selected = vintage;
                }
            }

            return selected;
        }

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
        /// Eugene, Corvallis, Springfield, Monmouth, Ashland). DELIBERATELY NOT applied by the
        /// calculator: the official DOJ calculator (v3.6.13) has no age/location inputs and
        /// takes the entered child-care figure as-is - Table 1 is guidance the FILER applies -
        /// and an over-cap case run through it on 2026-08-28 matched FairShare to the penny
        /// only because neither tool caps (see OregonWorksheetCalculatorTests). These values
        /// power the same guidance surface FairShare shows at the child-care input.
        /// </summary>
        public required ChildCareCaps MetroChildCareCaps { get; init; }

        /// <summary>OAR 137-050-0735 Table 1 (eff. 7/7/2023) for all other locations; see
        /// <see cref="MetroChildCareCaps"/> for why these are guidance, never auto-applied.</summary>
        public required ChildCareCaps NonMetroChildCareCaps { get; init; }
    }

    /// <summary>
    /// Monthly per-child caps on allowed child care costs by the child's age band
    /// (OAR 137-050-0735 Table 1).
    /// </summary>
    public sealed record ChildCareCaps(int UnderOne, int OneToThree, int ThreeToSix, int SixAndOlder);
}
