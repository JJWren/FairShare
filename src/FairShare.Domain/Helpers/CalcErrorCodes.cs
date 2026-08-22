namespace FairShare.Domain.Helpers
{
    /// <summary>
    /// The <see cref="CalcError.Code"/> values a calculator can report. Clients switch on these strings, so treat them as wire contract.
    /// </summary>
    public static class CalcErrorCodes
    {
        /// <summary>The number of children is outside the range the schedule covers (1-6).</summary>
        public const string InvalidChildCount = "INVALID_CHILD_COUNT";

        /// <summary>
        /// The combined adjusted gross income rounds above the top of the Basic Child-Support Obligation schedule ($30,000);
        /// the guidelines leave support above the schedule to the court's discretion, so no amount is computed.
        /// </summary>
        public const string IncomeAboveSchedule = "INCOME_ABOVE_SCHEDULE";

        /// <summary>Something unexpected went wrong; details are in the API log, not the response.</summary>
        public const string UnexpectedError = "UNEXPECTED_ERROR";

        /// <summary>A money, count or overnight input is negative.</summary>
        public const string NegativeInput = "NEGATIVE_INPUT";

        /// <summary>
        /// The parents' average overnights must total 365 when there are joint minor children
        /// (Oregon worksheet line 6a; OAR 137-050-0730).
        /// </summary>
        public const string OvernightsMustTotal365 = "OVERNIGHTS_MUST_TOTAL_365";

        /// <summary>
        /// The selected health-care-coverage provider is not among the parents whose coverage is
        /// available at a reasonable cost (Oregon worksheet lines 4d/4f; OAR 137-050-0750).
        /// </summary>
        public const string CoverageSelectionUnavailable = "COVERAGE_SELECTION_UNAVAILABLE";
    }
}
