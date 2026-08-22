namespace FairShare.Domain.Models
{
    /// <summary>
    /// One parent's column of the Oregon Child Support Worksheet (CSF 02 0910). Money values are
    /// monthly dollars and may carry cents - the Oregon workbook computes to the penny, unlike
    /// Alabama's whole-dollar forms.
    /// </summary>
    public sealed record OregonParentInput
    {
        /// <summary>Line 1a: the parent's monthly income (actual, potential, or presumed).</summary>
        public decimal MonthlyIncome { get; init; }

        /// <summary>Line 1b addition: spousal support owed to this parent by anyone.</summary>
        public decimal SpousalSupportReceived { get; init; }

        /// <summary>Line 1b subtraction: spousal support this parent owes to anyone.</summary>
        public decimal SpousalSupportPaid { get; init; }

        /// <summary>Line 1b subtraction: mandatory union dues.</summary>
        public decimal UnionDues { get; init; }

        /// <summary>Line 1b subtraction: cost of the parent's own health insurance.</summary>
        public decimal OwnHealthInsuranceCost { get; init; }

        /// <summary>Line 1c: the parent's non-joint children (minor in household or under an ongoing support order).</summary>
        public int NonJointChildren { get; init; }

        /// <summary>Line 3a: the parent's child care costs for joint children under 13 or disabled.</summary>
        public decimal ChildCareCosts { get; init; }

        /// <summary>
        /// Line 4a: what this parent pays to enroll the joint children in health care coverage.
        /// <see langword="null"/> means appropriate coverage is not available to this parent
        /// (the worksheet's "none"); zero means coverage is available at no cost.
        /// </summary>
        public decimal? ChildrensHealthCoverageCost { get; init; }

        /// <summary>
        /// Line 6a: the parent's average annual overnights with the joint minor children
        /// (a two-year average, so it may be fractional). Both parents' overnights must total 365
        /// when there are joint minor children.
        /// </summary>
        public decimal AverageOvernights { get; init; }

        /// <summary>
        /// Line 8e: Social Security or apportioned veterans benefits paid to the joint children
        /// because of this parent's disability or retirement (OAR 137-050-0740).
        /// </summary>
        public decimal SocialSecurityVeteransBenefits { get; init; }

        /// <summary>
        /// Line 8b: this parent has an exception to the $100 minimum order presumption
        /// (OAR 137-050-0755: incarceration, disability benefits as sole income, public benefits...).
        /// </summary>
        public bool MinimumOrderException { get; init; }
    }

    /// <summary>Line 5a: whether cash medical support is included (OAR 137-050-0750).</summary>
    public enum CashMedicalElection
    {
        /// <summary>"n": excluded - appropriate coverage is available, or the order explains why not.</summary>
        No,

        /// <summary>"y": included - no appropriate health care coverage is available.</summary>
        Yes,

        /// <summary>"c": included contingently - payable whenever the obligated parent does not provide coverage.</summary>
        Contingent
    }

    /// <summary>Line 4f: who will provide the joint children's health care coverage.</summary>
    public enum CoverageProvider
    {
        Plaintiff,
        Defendant,

        /// <summary>Both parents provide coverage simultaneously.</summary>
        Both,

        /// <summary>Neither parent can provide now; either provides when coverage becomes available ($0).</summary>
        EitherWhenAvailable
    }

    /// <summary>
    /// The complete input to the Oregon Child Support Worksheet: both parents' columns plus the
    /// case-level answers the form asks for. The caretaker/state-care variant is not modeled.
    /// </summary>
    public sealed record OregonWorksheetInput
    {
        public required OregonParentInput Plaintiff { get; init; }

        public required OregonParentInput Defendant { get; init; }

        /// <summary>Line 1d: joint minor children (including 18-year-olds in high school living with a parent).</summary>
        public int JointMinorChildren { get; init; }

        /// <summary>Line 1e: joint Children Attending School age 18 to 20 (ORS 107.108).</summary>
        public int JointChildrenAttendingSchool { get; init; }

        /// <summary>Line 5a: the cash medical support election.</summary>
        public CashMedicalElection CashMedical { get; init; } = CashMedicalElection.No;

        /// <summary>
        /// Line 4f: who will provide coverage. Leave <see langword="null"/> to let the calculator
        /// choose per OAR 137-050-0750: the only parent whose coverage is available at a reasonable
        /// cost, or - when both qualify - the parent with more parenting time (ties go to the
        /// cheaper coverage). An explicit choice is validated against line 4d's available options.
        /// </summary>
        public CoverageProvider? CoverageSelection { get; init; }

        /// <summary>
        /// Line 4e: order health care coverage even though it exceeds the reasonable-cost amount
        /// (the rule's "compelling factors" override, OAR 137-050-0750(7)).
        /// </summary>
        public bool OrderCoverageAtHigherAmount { get; init; }
    }
}
